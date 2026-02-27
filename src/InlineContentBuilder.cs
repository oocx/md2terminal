using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using Markdig.Syntax.Inlines;

namespace Md2Terminal;

/// <summary>
/// Converts Markdig inline nodes and AngleSharp DOM nodes into List&lt;StyledLine&gt;.
/// Handles &lt;br&gt; line splitting, style inheritance, and mixed Markdown/HTML content.
/// </summary>
public static class InlineContentBuilder
{
    // ── Markdig inline nodes ────────────────────────────────────────────────

    /// <summary>Build styled lines from a Markdig ContainerInline (e.g. ParagraphBlock.Inline).</summary>
    public static List<StyledLine> BuildFromInlines(ContainerInline? container, StyleContext? parentStyle = null)
    {
        var style = parentStyle ?? StyleContext.Default;
        var lines = new List<StyledLine>();
        var currentSpans = new List<StyledSpan>();

        if (container is null)
        {
            lines.Add(new StyledLine());
            return lines;
        }

        ProcessInlineNode(container, style, currentSpans, lines);

        // Flush remaining spans
        if (currentSpans.Count > 0)
            lines.Add(new StyledLine(currentSpans));
        else if (lines.Count == 0)
            lines.Add(new StyledLine());

        return lines;
    }

    private static void ProcessInlineNode(
        Inline node, StyleContext style,
        List<StyledSpan> currentSpans, List<StyledLine> lines)
    {
        switch (node)
        {
            case LiteralInline literal:
                if (literal.Content.Length > 0)
                    currentSpans.Add(style.ToSpan(literal.Content.ToString()));
                break;

            case EmphasisInline emphasis:
            {
                var childStyle = emphasis.DelimiterChar switch
                {
                    '*' or '_' when emphasis.DelimiterCount == 2
                        => style with { Bold = true },
                    '*' or '_' when emphasis.DelimiterCount == 1
                        => style with { Italic = true },
                    '~' when emphasis.DelimiterCount == 2
                        => style with { Strikethrough = true },
                    _ => style
                };
                foreach (var child in emphasis)
                    ProcessInlineNode(child, childStyle, currentSpans, lines);
                break;
            }

            case CodeInline code:
                currentSpans.Add(style.ToSpan(code.Content) with
                {
                    Code = true,
                    Bg = style.Bg ?? new RgbColor(50, 50, 60),
                    Fg = style.Fg ?? new RgbColor(220, 220, 220)
                });
                break;

            case LinkInline link:
            {
                var linkStyle = style with { Fg = style.Fg ?? new RgbColor(100, 180, 255) };
                if (link.FirstChild is null)
                {
                    // No children — use URL as text
                    currentSpans.Add(linkStyle.ToSpan(link.Url ?? ""));
                }
                else
                {
                    foreach (var child in link)
                        ProcessInlineNode(child, linkStyle, currentSpans, lines);
                }
                break;
            }

            case AutolinkInline autolink:
                currentSpans.Add((style with { Fg = new RgbColor(100, 180, 255) }).ToSpan(autolink.Url));
                break;

            case LineBreakInline:
                // Hard/soft line break → flush to a new StyledLine
                lines.Add(new StyledLine(currentSpans));
                currentSpans.Clear();
                break;

            case HtmlInline htmlInline:
                HandleHtmlInline(htmlInline, style, currentSpans, lines);
                break;

            case HtmlEntityInline entity:
                currentSpans.Add(style.ToSpan(entity.Transcoded.ToString()));
                break;

            case ContainerInline container:
                foreach (var child in container)
                    ProcessInlineNode(child, style, currentSpans, lines);
                break;
        }
    }

    private static void HandleHtmlInline(
        HtmlInline html, StyleContext style,
        List<StyledSpan> currentSpans, List<StyledLine> lines)
    {
        var tag = html.Tag.Trim();

        // <br> / <br/> / <br /> → line break
        if (tag.StartsWith("<br", StringComparison.OrdinalIgnoreCase))
        {
            lines.Add(new StyledLine(currentSpans));
            currentSpans.Clear();
            return;
        }

        // Self-closing or end tags we can mostly ignore for inline rendering
        // but we still pass through to not lose content
        // For <b>, </b>, <code>, </code>, <em>, </em>, <strong>, </strong> etc.
        // we ignore them here — Markdig already handles ** and * emphasis.
        // HTML inline tags within table cells are handled via AngleSharp path.
    }

    // ── AngleSharp DOM nodes ────────────────────────────────────────────────

    /// <summary>Build styled lines from an AngleSharp DOM node tree.</summary>
    public static List<StyledLine> BuildFromHtmlNode(INode node, StyleContext? parentStyle = null)
    {
        var style = parentStyle ?? StyleContext.Default;
        var lines = new List<StyledLine>();
        var currentSpans = new List<StyledSpan>();

        ProcessDomNode(node, style, currentSpans, lines);

        if (currentSpans.Count > 0)
            lines.Add(new StyledLine(currentSpans));
        else if (lines.Count == 0)
            lines.Add(new StyledLine());

        return lines;
    }

    /// <summary>Build styled lines from an HTML string fragment.</summary>
    public static List<StyledLine> BuildFromHtmlString(string html, StyleContext? parentStyle = null)
    {
        var parser = new HtmlParser();
        var doc = parser.ParseDocument($"<body>{html}</body>");
        return BuildFromHtmlNode(doc.Body!, parentStyle);
    }

    private static void ProcessDomNode(
        INode node, StyleContext style,
        List<StyledSpan> currentSpans, List<StyledLine> lines)
    {
        switch (node)
        {
            case IText textNode:
            {
                var text = textNode.Data;
                if (!string.IsNullOrEmpty(text))
                    currentSpans.Add(style.ToSpan(text));
                break;
            }

            case IElement element:
            {
                var tagName = element.LocalName.ToLowerInvariant();

                // Parse style attribute and merge
                var elementStyle = CssParser.ParseInlineStyle(
                    element.GetAttribute("style"), style);

                // Apply tag-level semantic styles
                elementStyle = tagName switch
                {
                    "b" or "strong" => elementStyle with { Bold = true },
                    "i" or "em"     => elementStyle with { Italic = true },
                    "s" or "del"    => elementStyle with { Strikethrough = true },
                    "code"          => elementStyle with
                    {
                        Code = true,
                        Bg = elementStyle.Bg ?? new RgbColor(50, 50, 60),
                        Fg = elementStyle.Fg ?? new RgbColor(220, 220, 220)
                    },
                    _ => elementStyle
                };

                // <br> → line break
                if (tagName == "br")
                {
                    lines.Add(new StyledLine(currentSpans));
                    currentSpans.Clear();
                    break;
                }

                // Recurse into children
                foreach (var child in element.ChildNodes)
                    ProcessDomNode(child, elementStyle, currentSpans, lines);

                // Block-level elements followed by a line break
                if (tagName is "div" or "p" && currentSpans.Count > 0)
                {
                    lines.Add(new StyledLine(currentSpans));
                    currentSpans.Clear();
                }

                break;
            }

            default:
                // Other node types (comments etc.) — skip
                break;
        }
    }
}
