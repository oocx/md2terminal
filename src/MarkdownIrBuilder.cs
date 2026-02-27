using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using Markdig;
using Markdig.Extensions.Tables;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace Md2Terminal;

/// <summary>
/// Walks the Markdig AST and produces the IR node tree (DocumentNode).
/// HTML blocks are delegated to HtmlBlockProcessor.
/// </summary>
public sealed class MarkdownIrBuilder
{
    private readonly MarkdownPipeline _pipeline;

    public MarkdownIrBuilder(MarkdownPipeline pipeline)
    {
        _pipeline = pipeline;
    }

    public DocumentNode Build(MarkdownDocument mdDoc)
    {
        var doc = new DocumentNode();
        var blocks = mdDoc.ToList();
        BuildBlocks(blocks, doc);
        return doc;
    }

    public DocumentNode Build(string markdown)
    {
        var mdDoc = Markdown.Parse(markdown, _pipeline);
        return Build(mdDoc);
    }

    /// <summary>
    /// Process a list of blocks, using index-based iteration so we can
    /// consume multiple sibling blocks when aggregating &lt;details&gt; regions.
    /// </summary>
    private void BuildBlocks(List<Markdig.Syntax.Block> blocks, DocumentNode parent)
    {
        int i = 0;
        while (i < blocks.Count)
        {
            var block = blocks[i];

            // Check for <details> opening HTML block — aggregate siblings until </details>
            if (block is HtmlBlock htmlBlock && IsDetailsOpen(htmlBlock))
            {
                i = BuildDetailsFromSiblings(blocks, i, parent);
                continue;
            }

            BuildBlock(block, parent);
            i++;
        }
    }

    /// <summary>
    /// Check if an HtmlBlock starts with &lt;details.
    /// </summary>
    private static bool IsDetailsOpen(HtmlBlock htmlBlock)
    {
        var text = htmlBlock.Lines.ToString().TrimStart();
        return text.StartsWith("<details", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Check if an HtmlBlock is a closing &lt;/details&gt; tag.
    /// </summary>
    private static bool IsDetailsClose(HtmlBlock htmlBlock)
    {
        var text = htmlBlock.Lines.ToString().Trim();
        return text.Equals("</details>", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Starting from blocks[startIndex] which is a &lt;details&gt; HtmlBlock,
    /// collect sibling blocks until the matching &lt;/details&gt; HtmlBlock.
    /// Builds a DetailsNode with the collected content and adds it to parent.
    /// Returns the index of the next block to process after the &lt;/details&gt;.
    /// </summary>
    private int BuildDetailsFromSiblings(List<Markdig.Syntax.Block> blocks, int startIndex, DocumentNode parent)
    {
        var openingBlock = (HtmlBlock)blocks[startIndex];
        var openingHtml = openingBlock.Lines.ToString().Trim();

        // Parse the opening HTML for <summary> and the "open" attribute
        var summaryLine = new StyledLine();
        bool isOpen = false;

        try
        {
            var parser = new AngleSharp.Html.Parser.HtmlParser();
            var doc = parser.ParseDocument($"<body>{openingHtml}</body>");
            var detailsEl = doc.QuerySelector("details");
            if (detailsEl is not null)
            {
                isOpen = detailsEl.HasAttribute("open");
                var summaryEl = detailsEl.QuerySelector("summary");
                if (summaryEl is not null)
                {
                    var summaryLines = InlineContentBuilder.BuildFromHtmlNode(summaryEl, StyleContext.Default);
                    if (summaryLines.Count > 0)
                        summaryLine = summaryLines[0];
                }
            }
        }
        catch
        {
            // If parsing fails, treat as plain text
        }

        // Collect content blocks between <details> and </details>
        var contentDoc = new DocumentNode();
        int i = startIndex + 1;

        // If summary wasn't in the opening block, look for it in subsequent HTML blocks
        // (Markdig splits them when separated by a blank line)
        if (summaryLine.Spans.Count == 0 && i < blocks.Count)
        {
            // Skip blank paragraph blocks that Markdig may insert
            while (i < blocks.Count && blocks[i] is HtmlBlock candidateHb
                   && !IsDetailsClose(candidateHb) && !IsDetailsOpen(candidateHb))
            {
                var candidateHtml = candidateHb.Lines.ToString().Trim();
                if (candidateHtml.Contains("<summary", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        var p2 = new AngleSharp.Html.Parser.HtmlParser();
                        var d2 = p2.ParseDocument($"<body>{candidateHtml}</body>");
                        var summEl = d2.QuerySelector("summary");
                        if (summEl is not null)
                        {
                            var sLines = InlineContentBuilder.BuildFromHtmlNode(summEl, StyleContext.Default);
                            if (sLines.Count > 0)
                                summaryLine = sLines[0];
                        }
                    }
                    catch { /* ignore parse errors */ }

                    i++; // consume this block so it's not treated as content
                    break;
                }

                break; // stop if the next HTML block isn't a summary
            }
        }

        while (i < blocks.Count)
        {
            var block = blocks[i];

            if (block is HtmlBlock hb)
            {
                if (IsDetailsOpen(hb))
                {
                    // Nested <details>: recursively aggregate (consumes through its </details>)
                    i = BuildDetailsFromSiblings(blocks, i, contentDoc);
                    continue;
                }

                if (IsDetailsClose(hb))
                {
                    i++; // skip past </details>
                    break;
                }
            }

            BuildBlock(block, contentDoc);
            i++;
        }

        parent.Children.Add(new DetailsNode
        {
            SummaryLine = summaryLine,
            IsOpen = isOpen,
            Content = contentDoc
        });

        return i;
    }

    private void BuildBlock(Markdig.Syntax.Block block, DocumentNode parent)
    {
        switch (block)
        {
            case HeadingBlock heading:
                parent.Children.Add(new HeadingNode
                {
                    Level = heading.Level,
                    Content = InlineContentBuilder.BuildFromInlines(heading.Inline)
                });
                break;

            case ParagraphBlock para:
                parent.Children.Add(new ParagraphNode
                {
                    OriginalLines = InlineContentBuilder.BuildFromInlines(para.Inline)
                });
                break;

            case ThematicBreakBlock:
                parent.Children.Add(new ThematicBreakNode());
                break;

            case FencedCodeBlock fenced:
                var lang = fenced.Info ?? "";
                var lines = GetCodeBlockLines(fenced);
                var highlighted = SyntaxHighlighter.Highlight(lines, lang);
                parent.Children.Add(new CodeBlockNode
                {
                    Language = lang,
                    RawLines = lines,
                    Highlighted = highlighted
                });
                break;

            case CodeBlock code: // indented code block
                var codeLines = GetCodeBlockLines(code);
                parent.Children.Add(new CodeBlockNode
                {
                    Language = "",
                    RawLines = codeLines,
                    Highlighted = SyntaxHighlighter.Highlight(codeLines, "")
                });
                break;

            case QuoteBlock quote:
                var quoteContent = new DocumentNode();
                foreach (var child in quote)
                    BuildBlock(child, quoteContent);
                parent.Children.Add(new BlockquoteNode { Content = quoteContent });
                break;

            case ListBlock list:
                var listNode = new ListNode
                {
                    Ordered = list.IsOrdered,
                };
                int idx = 1;
                foreach (var item in list)
                {
                    if (item is ListItemBlock listItem)
                    {
                        var itemContent = new DocumentNode();
                        foreach (var child in listItem)
                            BuildBlock(child, itemContent);
                        listNode.Items.Add(new ListItemNode
                        {
                            Index = idx++,
                            Content = itemContent
                        });
                    }
                }
                parent.Children.Add(listNode);
                break;

            case Table table:
                parent.Children.Add(BuildTable(table));
                break;

            case HtmlBlock htmlBlock:
                // If we get here, it's an HTML block not handled by details aggregation
                // (e.g., standalone <pre><code> or misc HTML)
                var raw = htmlBlock.Lines.ToString();
                var nodes = HtmlBlockProcessor.Process(raw, _pipeline, this);
                foreach (var n in nodes)
                    parent.Children.Add(n);
                break;

            case Markdig.Syntax.ContainerBlock container:
                var childBlocks = container.ToList();
                BuildBlocks(childBlocks, parent);
                break;

            case Markdig.Syntax.LeafBlock leaf:
                // Fallback for any leaf block we haven't handled
                if (leaf.Inline is not null)
                {
                    parent.Children.Add(new ParagraphNode
                    {
                        OriginalLines = InlineContentBuilder.BuildFromInlines(leaf.Inline)
                    });
                }
                break;
        }
    }

    private TableNode BuildTable(Table mdTable)
    {
        var alignments = new List<ColumnAlign>();
        var headerCells = new List<TableCellNode>();
        var headers = new List<string>();
        var rows = new List<TableRowNode>();

        // Get column definitions for alignments
        foreach (var col in mdTable.ColumnDefinitions)
        {
            alignments.Add(col.Alignment switch
            {
                TableColumnAlign.Left => ColumnAlign.Left,
                TableColumnAlign.Center => ColumnAlign.Center,
                TableColumnAlign.Right => ColumnAlign.Right,
                _ => ColumnAlign.Left,
            });
        }

        bool isFirst = true;
        foreach (var rowBlock in mdTable)
        {
            if (rowBlock is not TableRow tableRow) continue;

            if (isFirst && tableRow.IsHeader)
            {
                isFirst = false;
                foreach (TableCell cell in tableRow)
                {
                    var cellLines = BuildTableCellContent(cell);
                    headers.Add(GetPlainText(cell));
                    headerCells.Add(new TableCellNode { Lines = cellLines });
                }
                continue;
            }

            isFirst = false;
            var rowNode = new TableRowNode();
            foreach (TableCell cell in tableRow)
            {
                var cellLines = BuildTableCellContent(cell);
                rowNode.Cells.Add(new TableCellNode { Lines = cellLines });
            }
            rows.Add(rowNode);
        }

        // Ensure alignments count matches columns
        while (alignments.Count < headers.Count)
            alignments.Add(ColumnAlign.Left);

        return new TableNode
        {
            Headers = headers,
            HeaderCells = headerCells,
            Alignments = alignments.ToArray(),
            Rows = rows
        };
    }

    /// <summary>
    /// Get the ContainerInline from a TableCell.
    /// TableCell is a ContainerBlock — its first child is typically a ParagraphBlock.
    /// </summary>
    private static ContainerInline? GetCellInline(TableCell cell)
    {
        foreach (var child in cell)
        {
            if (child is ParagraphBlock para)
                return para.Inline;
        }
        return null;
    }

    private List<StyledLine> BuildTableCellContent(TableCell cell)
    {
        var inline = GetCellInline(cell);
        if (inline is null)
            return [new StyledLine([new StyledSpan("")])];

        // Check if this cell contains HTML content that needs AngleSharp parsing
        bool hasHtml = false;
        foreach (var inl in inline)
        {
            if (inl is HtmlInline hi && !hi.Tag.TrimStart().StartsWith("<br", StringComparison.OrdinalIgnoreCase))
            {
                hasHtml = true;
                break;
            }
        }

        if (hasHtml)
        {
            // Render the cell to HTML string, then parse with AngleSharp
            var html = RenderInlinesToHtml(inline);
            return InlineContentBuilder.BuildFromHtmlString(html);
        }

        return InlineContentBuilder.BuildFromInlines(inline);
    }

    /// <summary>Render Markdig inlines to an HTML string for AngleSharp re-parsing.</summary>
    private string RenderInlinesToHtml(ContainerInline container)
    {
        using var writer = new StringWriter();
        var htmlRenderer = new Markdig.Renderers.HtmlRenderer(writer);
        _pipeline.Setup(htmlRenderer);
        htmlRenderer.WriteChildren(container);
        writer.Flush();
        return writer.ToString();
    }

    private static string GetPlainText(TableCell cell)
    {
        var inline = GetCellInline(cell);
        if (inline is null) return "";
        using var writer = new StringWriter();
        foreach (var inl in inline)
            WritePlainText(inl, writer);
        return writer.ToString().Trim();
    }

    private static void WritePlainText(Inline inline, TextWriter writer)
    {
        switch (inline)
        {
            case LiteralInline literal:
                writer.Write(literal.Content);
                break;
            case CodeInline code:
                writer.Write(code.Content);
                break;
            case HtmlInline html when html.Tag.TrimStart().StartsWith("<br", StringComparison.OrdinalIgnoreCase):
                writer.WriteLine();
                break;
            case HtmlInline:
                break; // skip other HTML tags for plain text
            case ContainerInline container:
                foreach (var child in container)
                    WritePlainText(child, writer);
                break;
        }
    }

    private static string[] GetCodeBlockLines(Markdig.Syntax.LeafBlock block)
    {
        var lines = new List<string>();
        if (block.Lines.Lines is not null)
        {
            foreach (var sl in block.Lines.Lines)
            {
                if (sl.Slice.Text is null) continue;
                lines.Add(sl.Slice.ToString());
            }
        }
        // Remove trailing empty lines
        while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[^1]))
            lines.RemoveAt(lines.Count - 1);
        return lines.ToArray();
    }
}
