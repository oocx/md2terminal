using Terminal.Gui;

namespace Md2Terminal;

/// <summary>
/// Assigns OffsetY to every node, word-wraps paragraphs, computes table column widths,
/// and collects all DetailsNodes for keyboard navigation.
/// Run after IR construction and after every details toggle.
/// </summary>
public static class LayoutEngine
{
    public static void Layout(DocumentNode root, int viewportWidth, Theme theme)
    {
        int availableWidth = Math.Max(20, viewportWidth - 2); // leave margin
        int y = 0;
        LayoutChildren(root, ref y, availableWidth, indentLevel: 0, theme);
    }

    public static List<DetailsNode> CollectDetails(DocumentNode root)
    {
        var list = new List<DetailsNode>();
        CollectDetailsRecursive(root, list);
        return list;
    }

    /// <summary>Collects every DetailsNode in the tree regardless of open/closed state.</summary>
    public static List<DetailsNode> CollectAllDetails(DocumentNode root)
    {
        var list = new List<DetailsNode>();
        CollectAllDetailsRecursive(root, list);
        return list;
    }

    private static void CollectDetailsRecursive(RenderNode node, List<DetailsNode> list)
    {
        if (node is DetailsNode d)
        {
            list.Add(d);
            if (d.IsOpen)
                CollectDetailsRecursive(d.Content, list);
        }
        else if (node is DocumentNode doc)
        {
            foreach (var child in doc.Children)
                CollectDetailsRecursive(child, list);
        }
        else if (node is BlockquoteNode bq)
        {
            CollectDetailsRecursive(bq.Content, list);
        }
        else if (node is ListNode ln)
        {
            foreach (var item in ln.Items)
                CollectDetailsRecursive(item.Content, list);
        }
    }

    // Always recurses into details children so nested sections are found even when collapsed.
    private static void CollectAllDetailsRecursive(RenderNode node, List<DetailsNode> list)
    {
        if (node is DetailsNode d)
        {
            list.Add(d);
            CollectAllDetailsRecursive(d.Content, list);
        }
        else if (node is DocumentNode doc)
        {
            foreach (var child in doc.Children)
                CollectAllDetailsRecursive(child, list);
        }
        else if (node is BlockquoteNode bq)
        {
            CollectAllDetailsRecursive(bq.Content, list);
        }
        else if (node is ListNode ln)
        {
            foreach (var item in ln.Items)
                CollectAllDetailsRecursive(item.Content, list);
        }
    }

    private static void LayoutChildren(DocumentNode doc, ref int y, int width, int indentLevel, Theme theme)
    {
        foreach (var node in doc.Children)
        {
            LayoutNode(node, ref y, width, indentLevel, theme);
        }
    }

    private static void LayoutNode(RenderNode node, ref int y, int width, int indentLevel, Theme theme)
    {
        int effectiveWidth = Math.Max(10, width - indentLevel * 2);

        node.OffsetY = y;

        switch (node)
        {
            case ParagraphNode para:
                para.WrappedLines = WordWrap(para.OriginalLines, effectiveWidth);
                y += para.Height;
                break;

            case HeadingNode heading:
                // Headings don't word-wrap (typically short)
                y += heading.Height;
                break;

            case ThematicBreakNode:
                y += node.Height;
                break;

            case CodeBlockNode:
            case PreCodeNode:
                y += node.Height;
                break;

            case BlockquoteNode bq:
                bq.OffsetY = y;
                int bqY = y;
                LayoutChildren(bq.Content, ref bqY, effectiveWidth - 3, indentLevel + 1, theme);
                y = bqY;
                break;

            case ListNode list:
                list.OffsetY = y;
                foreach (var item in list.Items)
                {
                    item.OffsetY = y;
                    int itemY = y;
                    LayoutChildren(item.Content, ref itemY, effectiveWidth - 3, indentLevel + 1, theme);
                    y = itemY;
                }
                break;

            case TableNode table:
                ComputeTableLayout(table, effectiveWidth);
                // Compute height accounting for theme's row divider setting
                int th = 3; // top border + header + divider
                for (int i = 0; i < table.Rows.Count; i++)
                {
                    th += table.Rows[i].Height;
                    if (i < table.Rows.Count - 1 && theme.TableRowDividers) th++;
                }
                th++; // bottom border
                table.ComputedHeight = th;
                y += table.Height;
                break;

            case DetailsNode details:
                details.OffsetY = y;
                int detailsStart = y;
                y += 1; // summary row
                if (details.IsOpen)
                {
                    y += 1; // top border
                    LayoutChildren(details.Content, ref y, effectiveWidth - 4, indentLevel + 1, theme);
                    y += 1; // bottom border
                }
                y += 1; // blank line after details block
                details.ComputedHeight = y - detailsStart;
                break;

            case DocumentNode doc:
                LayoutChildren(doc, ref y, effectiveWidth, indentLevel, theme);
                break;

            default:
                y += node.Height;
                break;
        }
    }

    private static void ComputeTableLayout(TableNode table, int availableWidth)
    {
        int colCount = Math.Max(table.Headers.Count,
            table.Rows.Count > 0 ? table.Rows.Max(r => r.Cells.Count) : 0);

        if (colCount == 0) return;

        var widths = new int[colCount];

        // Initialize with header widths
        for (int i = 0; i < table.Headers.Count; i++)
        {
            widths[i] = Math.Max(widths[i], MeasureStyledLines(table.HeaderCells.Count > i
                ? table.HeaderCells[i].Lines : null, table.Headers[i]));
        }

        // Measure body cell widths
        foreach (var row in table.Rows)
        {
            for (int i = 0; i < row.Cells.Count && i < colCount; i++)
            {
                int cellWidth = row.Cells[i].Lines.Max(l => l.VisibleLength);
                widths[i] = Math.Max(widths[i], cellWidth);
            }
        }

        // Add padding (1 space each side)
        for (int i = 0; i < widths.Length; i++)
            widths[i] = Math.Max(3, widths[i] + 2);

        // Constrain to available width: if total > available, shrink proportionally
        int totalWidth = widths.Sum() + colCount + 1; // +1 per col borders + left border
        if (totalWidth > availableWidth && availableWidth > colCount * 4)
        {
            double scale = (double)(availableWidth - colCount - 1) / widths.Sum();
            for (int i = 0; i < widths.Length; i++)
                widths[i] = Math.Max(3, (int)(widths[i] * scale));
        }

        table.ColumnWidths = widths;
    }

    private static int MeasureStyledLines(List<StyledLine>? lines, string fallback)
    {
        if (lines is null || lines.Count == 0)
            return UnicodeWidth.GetWidth(fallback);
        return lines.Max(l => l.VisibleLength);
    }

    private static List<StyledLine> WordWrap(List<StyledLine> lines, int maxWidth)
    {
        if (maxWidth <= 0) return lines;

        var result = new List<StyledLine>();
        foreach (var line in lines)
        {
            WrapLine(line, maxWidth, result);
        }
        return result;
    }

    private static void WrapLine(StyledLine line, int maxWidth, List<StyledLine> output)
    {
        if (line.VisibleLength <= maxWidth)
        {
            output.Add(line);
            return;
        }

        // Flatten all spans into one string with style markers to split at word boundaries
        var currentLine = new StyledLine();
        int currentWidth = 0;

        foreach (var span in line.Spans)
        {
            var remaining = span.Text;
            while (remaining.Length > 0)
            {
                int spaceLeft = maxWidth - currentWidth;
                if (spaceLeft <= 0)
                {
                    output.Add(currentLine);
                    currentLine = new StyledLine();
                    currentWidth = 0;
                    spaceLeft = maxWidth;
                }

                int remainingWidth = UnicodeWidth.GetWidth(remaining);
                if (remainingWidth <= spaceLeft)
                {
                    currentLine.Spans.Add(span with { Text = remaining });
                    currentWidth += remainingWidth;
                    remaining = "";
                }
                else
                {
                    // Try to break at a word boundary
                    int breakAt = remaining.LastIndexOf(' ', Math.Min(spaceLeft, remaining.Length) - 1);
                    if (breakAt <= 0)
                    {
                        // Force break by display width
                        string truncated = UnicodeWidth.Truncate(remaining, spaceLeft);
                        breakAt = truncated.Length;
                    }

                    currentLine.Spans.Add(span with { Text = remaining[..breakAt] });
                    output.Add(currentLine);
                    currentLine = new StyledLine();
                    currentWidth = 0;

                    remaining = remaining[breakAt..].TrimStart();
                }
            }
        }

        if (currentLine.Spans.Count > 0)
            output.Add(currentLine);
    }
}
