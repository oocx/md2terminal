using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using Markdig;

namespace Md2Terminal;

/// <summary>
/// Processes Markdig HtmlBlock nodes into IR nodes.
/// Handles &lt;details&gt;/&lt;summary&gt;, &lt;pre&gt;&lt;code&gt;, and generic HTML.
/// </summary>
public static class HtmlBlockProcessor
{
    public static List<RenderNode> Process(string rawHtml, MarkdownPipeline pipeline, MarkdownIrBuilder irBuilder)
    {
        var results = new List<RenderNode>();

        if (string.IsNullOrWhiteSpace(rawHtml))
            return results;

        var trimmed = rawHtml.Trim();

        // Quick check: is this a <details> block?
        if (trimmed.StartsWith("<details", StringComparison.OrdinalIgnoreCase))
        {
            var detailsNode = BuildDetailsNode(trimmed, pipeline, irBuilder);
            if (detailsNode is not null)
            {
                results.Add(detailsNode);
                return results;
            }
        }

        // Check for <pre><code> blocks
        if (trimmed.StartsWith("<pre", StringComparison.OrdinalIgnoreCase))
        {
            var preNode = BuildPreCodeNode(trimmed);
            if (preNode is not null)
            {
                results.Add(preNode);
                return results;
            }
        }

        // Fallback: strip tags and render as paragraph
        var fallback = BuildFallbackParagraph(trimmed);
        if (fallback is not null)
            results.Add(fallback);

        return results;
    }

    private static DetailsNode? BuildDetailsNode(string html, MarkdownPipeline pipeline, MarkdownIrBuilder irBuilder)
    {
        try
        {
            var parser = new HtmlParser();
            var doc = parser.ParseDocument($"<body>{html}</body>");
            var detailsEl = doc.QuerySelector("details");
            if (detailsEl is null) return null;

            // Extract <summary> content
            var summaryEl = detailsEl.QuerySelector("summary");
            var summaryLine = new StyledLine();
            if (summaryEl is not null)
            {
                var summaryLines = InlineContentBuilder.BuildFromHtmlNode(summaryEl, StyleContext.Default);
                if (summaryLines.Count > 0)
                    summaryLine = summaryLines[0];
                summaryEl.Remove();
            }

            // Check <details open> attribute
            bool isOpen = detailsEl.HasAttribute("open");

            // Get inner content — this is usually Markdown, so re-parse it
            var innerHtml = detailsEl.InnerHtml.Trim();

            // Clean up stray <br> tags at the start
            while (innerHtml.StartsWith("<br", StringComparison.OrdinalIgnoreCase))
            {
                var idx = innerHtml.IndexOf('>');
                if (idx >= 0)
                    innerHtml = innerHtml[(idx + 1)..].TrimStart();
                else
                    break;
            }

            // Re-parse inner content as Markdown
            DocumentNode content;
            if (!string.IsNullOrWhiteSpace(innerHtml))
            {
                // The inner HTML may contain more <details> blocks, tables, etc.
                // We need to re-parse it through Markdig
                content = irBuilder.Build(innerHtml);
            }
            else
            {
                content = new DocumentNode();
            }

            return new DetailsNode
            {
                SummaryLine = summaryLine,
                IsOpen = isOpen,
                Content = content
            };
        }
        catch
        {
            return null;
        }
    }

    private static PreCodeNode? BuildPreCodeNode(string html)
    {
        try
        {
            var parser = new HtmlParser();
            var doc = parser.ParseDocument($"<body>{html}</body>");
            var codeEl = doc.QuerySelector("pre code") ?? doc.QuerySelector("pre");
            if (codeEl is null) return null;

            // Parse the <code> children with styles intact
            var lines = InlineContentBuilder.BuildFromHtmlNode(codeEl, StyleContext.Default);

            // Also split on actual newlines within text nodes
            var splitLines = new List<StyledLine>();
            foreach (var line in lines)
            {
                SplitLineOnNewlines(line, splitLines);
            }

            return new PreCodeNode { Lines = splitLines };
        }
        catch
        {
            return null;
        }
    }

    private static void SplitLineOnNewlines(StyledLine line, List<StyledLine> output)
    {
        var current = new StyledLine();
        foreach (var span in line.Spans)
        {
            if (span.Text.Contains('\n'))
            {
                var parts = span.Text.Split('\n');
                for (int i = 0; i < parts.Length; i++)
                {
                    if (i > 0)
                    {
                        output.Add(current);
                        current = new StyledLine();
                    }
                    if (parts[i].Length > 0)
                    {
                        current.Spans.Add(span with { Text = parts[i] });
                    }
                }
            }
            else
            {
                current.Spans.Add(span);
            }
        }
        output.Add(current);
    }

    private static ParagraphNode? BuildFallbackParagraph(string html)
    {
        try
        {
            var lines = InlineContentBuilder.BuildFromHtmlString(html);
            // Filter out empty lines
            lines = lines.Where(l => l.Spans.Any(s => !string.IsNullOrWhiteSpace(s.Text))).ToList();
            if (lines.Count == 0) return null;

            return new ParagraphNode { OriginalLines = lines };
        }
        catch
        {
            return null;
        }
    }
}
