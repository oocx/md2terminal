using Terminal.Gui;
using Attribute = Terminal.Gui.Attribute;

namespace Md2Terminal;

// ── Heading ─────────────────────────────────────────────────────────────────

public static class HeadingRenderer
{
    public static void Render(HeadingNode node, RenderContext ctx)
    {
        var t = ctx.Theme;
        int y = node.OffsetY;
        int lvl = Math.Clamp(node.Level, 1, 6) - 1;
        var color = t.HeadingColors[lvl];
        var prefix = t.HeadingPrefixes[lvl];
        var attr = ctx.Attr(color);

        // Blank line before heading
        ctx.DrawText(y, 0, "", attr);
        y++;

        // Draw prefix + heading content
        foreach (var line in node.Content)
        {
            if (!ctx.TryDocToScreen(y, out int screenY)) { y++; continue; }

            int x = ctx.IndentX;
            ctx.View.SetAttribute(attr);
            ctx.DrawStrAt(prefix, ref x, screenY);

            // Draw each span
            foreach (var span in line.Spans)
            {
                if (x >= ctx.ViewportWidth) break;
                var spanFg = span.Fg ?? color;
                var spanBg = span.Bg ?? t.DocBg;
                ctx.View.SetAttribute(ctx.Attr(spanFg, spanBg));
                int maxLen = ctx.ViewportWidth - x;
                var text = UnicodeWidth.GetWidth(span.Text) > maxLen ? UnicodeWidth.Truncate(span.Text, maxLen) : span.Text;
                ctx.DrawStrAt(text, ref x, screenY);
            }
            y++;
        }

        // Rule line for H1/H2
        if (node.Level <= 2)
        {
            char ruleChar = node.Level == 1 ? t.H1Rule : t.H2Rule;
            int ruleWidth = Math.Max(0, ctx.ViewportWidth - ctx.IndentX - 1);
            ctx.DrawText(y, 0, new string(ruleChar, ruleWidth), attr);
        }
    }
}

// ── Paragraph ───────────────────────────────────────────────────────────────

public static class ParagraphRenderer
{
    public static void Render(ParagraphNode node, RenderContext ctx)
    {
        int y = node.OffsetY;
        foreach (var line in node.WrappedLines)
        {
            ctx.DrawStyledLine(y, 0, line);
            y++;
        }
        // Spacing line after paragraph (already counted in Height)
    }
}

// ── Thematic Break ──────────────────────────────────────────────────────────

public static class ThematicBreakRenderer
{
    public static void Render(ThematicBreakNode node, RenderContext ctx)
    {
        var t = ctx.Theme;
        int width = Math.Max(0, ctx.ViewportWidth - ctx.IndentX - 1);
        ctx.DrawText(node.OffsetY, 0, new string(t.HrChar, width), ctx.Attr(t.HrFg));
    }
}

// ── Blockquote ──────────────────────────────────────────────────────────────

public static class BlockquoteRenderer
{
    public static void Render(BlockquoteNode node, RenderContext ctx)
    {
        var t = ctx.Theme;
        int y = node.OffsetY;
        int height = node.Content.Height;
        var gutterAttr = ctx.Attr(t.BlockquoteGutterFg);

        for (int i = 0; i < height; i++)
        {
            ctx.DrawText(y + i, 0, t.BlockquoteGutter, gutterAttr);
        }

        using (ctx.PushIndent(3))
        {
            node.Content.Render(ctx);
        }
    }
}

// ── Lists ───────────────────────────────────────────────────────────────────

public static class ListRenderer
{
    public static void Render(ListNode node, RenderContext ctx)
    {
        foreach (var item in node.Items)
        {
            item.Render(ctx);
        }
    }
}

public static class ListItemRenderer
{
    public static void Render(ListItemNode node, RenderContext ctx)
    {
        var t = ctx.Theme;
        int y = node.OffsetY;
        string bullet = t.Bullets.Length > 0 ? t.Bullets[0] : "● ";
        ctx.DrawText(y, 0, bullet, ctx.Attr(t.BulletFg));

        using (ctx.PushIndent(3))
        {
            node.Content.Render(ctx);
        }
    }
}

// ── Code Block (fenced) ─────────────────────────────────────────────────────

public static class CodeBlockRenderer
{
    public static void Render(CodeBlockNode node, RenderContext ctx)
    {
        var t = ctx.Theme;
        var bc = t.Borders;
        int y = node.OffsetY;
        var bgAttr = ctx.Attr(t.CodeFg, t.CodeBg);
        int contentWidth = Math.Max(10, ctx.ViewportWidth - ctx.IndentX - 2);

        if (t.FilledBorders)
        {
            RenderFilled(node, ctx, t, y, bgAttr, contentWidth);
        }
        else
        {
            RenderBoxDrawing(node, ctx, t, bc, y, bgAttr, contentWidth);
        }
    }

    /// <summary>Half-block rendering: background only inside the border.</summary>
    private static void RenderFilled(CodeBlockNode node, RenderContext ctx, Theme t,
        int y, Attribute bgAttr, int contentWidth)
    {
        // fg = CodeBg (the visible half), bg = DocBg (the outer half)
        var edgeAttr = ctx.Attr(t.CodeBg, t.DocBg);
        int totalWidth = contentWidth + 2; // +2 for left/right edges

        // Top edge: ▄▄▄▄▄▄▄▄▄▄▄▄
        if (ctx.TryDocToScreen(y, out int topSy))
        {
            int x = ctx.IndentX;
            ctx.View.Move(x, topSy);
            ctx.View.SetAttribute(edgeAttr);
            ctx.View.AddStr(new string('▄', Math.Min(totalWidth, ctx.ViewportWidth - x)));

            // Overlay language label on the top edge
            if (!string.IsNullOrEmpty(node.Language))
            {
                string label = $" {node.Language} ";
                int labelX = x + 2;
                if (labelX + label.Length < ctx.ViewportWidth)
                {
                    ctx.View.Move(labelX, topSy);
                    ctx.View.SetAttribute(ctx.Attr(t.CodeBorderFg, t.DocBg));
                    ctx.View.AddStr(label);
                }
            }
        }
        y++;

        // Content lines
        var linesToRender = node.Highlighted.Count > 0
            ? node.Highlighted
            : node.RawLines.Select(l => new List<StyledSpan> { new(l) }).ToList();

        foreach (var spanLine in linesToRender)
        {
            if (!ctx.TryDocToScreen(y, out int screenY)) { y++; continue; }
            int x = ctx.IndentX;

            // Left edge: ▐ (right half = CodeBg)
            ctx.View.Move(x, screenY);
            ctx.View.SetAttribute(edgeAttr);
            ctx.View.AddStr("▐");
            x++;

            // Content
            int drawn = 0;
            foreach (var span in spanLine)
            {
                if (x >= ctx.ViewportWidth - 1) break;
                var fg = span.Fg ?? t.CodeFg;
                ctx.View.SetAttribute(ctx.Attr(fg, t.CodeBg));
                int maxLen = Math.Max(0, ctx.ViewportWidth - x - 1);
                var text = UnicodeWidth.GetWidth(span.Text) > maxLen ? UnicodeWidth.Truncate(span.Text, maxLen) : span.Text;
                int xBefore = x;
                ctx.DrawStrAt(text, ref x, screenY);
                drawn += x - xBefore;
            }

            // Pad to fill
            int pad = Math.Max(0, contentWidth - drawn);
            if (pad > 0 && x < ctx.ViewportWidth - 1)
            {
                ctx.View.Move(x, screenY);
                ctx.View.SetAttribute(bgAttr);
                int padLen = Math.Min(pad, ctx.ViewportWidth - x - 1);
                ctx.View.AddStr(new string(' ', padLen));
                x += padLen;
            }

            // Right edge: ▌ (left half = CodeBg)
            if (x < ctx.ViewportWidth)
            {
                ctx.View.Move(x, screenY);
                ctx.View.SetAttribute(edgeAttr);
                ctx.View.AddStr("▌");
            }
            y++;
        }

        // Bottom edge: ▀▀▀▀▀▀▀▀▀▀▀▀
        if (ctx.TryDocToScreen(y, out int botSy))
        {
            int x = ctx.IndentX;
            ctx.View.Move(x, botSy);
            ctx.View.SetAttribute(edgeAttr);
            ctx.View.AddStr(new string('▀', Math.Min(totalWidth, ctx.ViewportWidth - x)));
        }
    }

    /// <summary>Traditional box-drawing rendering.</summary>
    private static void RenderBoxDrawing(CodeBlockNode node, RenderContext ctx, Theme t,
        BorderChars bc, int y, Attribute bgAttr, int contentWidth)
    {
        var borderAttr = ctx.Attr(t.CodeBorderFg);

        // Top border: ┌─ lang ────────────────────────────────┐
        string langLabel = string.IsNullOrEmpty(node.Language) ? "" : $" {node.Language} ";
        int dashCount = Math.Max(0, contentWidth - UnicodeWidth.GetWidth(langLabel) - 2);
        string topBorder = $"{bc.TL}{bc.H}{langLabel}{new string(bc.H, dashCount)}{bc.TR}";
        ctx.DrawText(y, 0, topBorder, borderAttr);
        y++;

        // Content lines
        var linesToRender = node.Highlighted.Count > 0
            ? node.Highlighted
            : node.RawLines.Select(l => new List<StyledSpan> { new(l) }).ToList();

        foreach (var spanLine in linesToRender)
        {
            if (!ctx.TryDocToScreen(y, out int screenY)) { y++; continue; }
            int x = ctx.IndentX;

            // Left border
            ctx.View.Move(x, screenY);
            ctx.View.SetAttribute(borderAttr);
            ctx.View.AddStr(bc.V.ToString());
            x++;

            // Content
            int drawn = 0;
            foreach (var span in spanLine)
            {
                if (x >= ctx.ViewportWidth) break;
                var fg = span.Fg ?? t.CodeFg;
                ctx.View.SetAttribute(ctx.Attr(fg, t.CodeBg));
                int maxLen = Math.Max(0, ctx.ViewportWidth - x - 1);
                var text = UnicodeWidth.GetWidth(span.Text) > maxLen ? UnicodeWidth.Truncate(span.Text, maxLen) : span.Text;
                int xBefore = x;
                ctx.DrawStrAt(text, ref x, screenY);
                drawn += x - xBefore;
            }

            // Pad + right border
            int pad = Math.Max(0, contentWidth - drawn - 1);
            if (x < ctx.ViewportWidth)
            {
                ctx.View.Move(x, screenY);
                ctx.View.SetAttribute(bgAttr);
                ctx.View.AddStr(new string(' ', Math.Min(pad, ctx.ViewportWidth - x - 1)));
                x += Math.Min(pad, ctx.ViewportWidth - x - 1);
            }
            if (x < ctx.ViewportWidth)
            {
                ctx.View.Move(x, screenY);
                ctx.View.SetAttribute(borderAttr);
                ctx.View.AddStr(bc.V.ToString());
            }
            y++;
        }

        // Bottom border
        string bottomBorder = $"{bc.BL}{new string(bc.H, Math.Max(0, contentWidth))}{bc.BR}";
        ctx.DrawText(y, 0, bottomBorder, borderAttr);
    }
}

// ── Pre/Code Block (HTML with embedded spans) ───────────────────────────────

public static class PreCodeRenderer
{
    public static void Render(PreCodeNode node, RenderContext ctx)
    {
        var t = ctx.Theme;
        var bc = t.Borders;
        int y = node.OffsetY;
        var bgAttr = ctx.Attr(t.CodeFg, t.CodeBg);
        int contentWidth = Math.Max(10, ctx.ViewportWidth - ctx.IndentX - 2);

        if (t.FilledBorders)
        {
            RenderFilled(node, ctx, t, y, bgAttr, contentWidth);
        }
        else
        {
            RenderBoxDrawing(node, ctx, t, bc, y, bgAttr, contentWidth);
        }
    }

    private static void RenderFilled(PreCodeNode node, RenderContext ctx, Theme t,
        int y, Attribute bgAttr, int contentWidth)
    {
        var edgeAttr = ctx.Attr(t.CodeBg, t.DocBg);
        int totalWidth = contentWidth + 2;

        // Top edge
        if (ctx.TryDocToScreen(y, out int topSy))
        {
            int x = ctx.IndentX;
            ctx.View.Move(x, topSy);
            ctx.View.SetAttribute(edgeAttr);
            ctx.View.AddStr(new string('▄', Math.Min(totalWidth, ctx.ViewportWidth - x)));
        }
        y++;

        foreach (var line in node.Lines)
        {
            if (!ctx.TryDocToScreen(y, out int screenY)) { y++; continue; }
            int x = ctx.IndentX;

            // Left edge
            ctx.View.Move(x, screenY);
            ctx.View.SetAttribute(edgeAttr);
            ctx.View.AddStr("▐");
            x++;

            // Gutter
            if (line.GutterColor is { } gc)
            {
                ctx.View.Move(x, screenY);
                ctx.View.SetAttribute(RenderContext.MakeAttr(gc, t.CodeBg));
                ctx.View.AddStr("\u258c");
                x++;
            }

            int drawn = line.GutterColor is not null ? 1 : 0;
            foreach (var span in line.Spans)
            {
                if (x >= ctx.ViewportWidth - 1) break;
                var fg = span.Fg ?? t.CodeFg;
                var bg = span.Bg ?? t.CodeBg;
                ctx.View.SetAttribute(ctx.Attr(fg, bg));
                int maxLen = Math.Max(0, ctx.ViewportWidth - x - 1);
                var text = UnicodeWidth.GetWidth(span.Text) > maxLen ? UnicodeWidth.Truncate(span.Text, maxLen) : span.Text;
                int xBefore = x;
                ctx.DrawStrAt(text, ref x, screenY);
                drawn += x - xBefore;
            }

            int pad = Math.Max(0, contentWidth - drawn);
            if (pad > 0 && x < ctx.ViewportWidth - 1)
            {
                ctx.View.Move(x, screenY);
                ctx.View.SetAttribute(bgAttr);
                ctx.View.AddStr(new string(' ', Math.Min(pad, ctx.ViewportWidth - x - 1)));
                x += Math.Min(pad, ctx.ViewportWidth - x - 1);
            }

            // Right edge
            if (x < ctx.ViewportWidth)
            {
                ctx.View.Move(x, screenY);
                ctx.View.SetAttribute(edgeAttr);
                ctx.View.AddStr("▌");
            }
            y++;
        }

        // Bottom edge
        if (ctx.TryDocToScreen(y, out int botSy))
        {
            int x = ctx.IndentX;
            ctx.View.Move(x, botSy);
            ctx.View.SetAttribute(edgeAttr);
            ctx.View.AddStr(new string('▀', Math.Min(totalWidth, ctx.ViewportWidth - x)));
        }
    }

    private static void RenderBoxDrawing(PreCodeNode node, RenderContext ctx, Theme t,
        BorderChars bc, int y, Attribute bgAttr, int contentWidth)
    {
        var borderAttr = ctx.Attr(t.CodeBorderFg);

        // Top border
        string topBorder = $"{bc.TL}{new string(bc.H, contentWidth)}{bc.TR}";
        ctx.DrawText(y, 0, topBorder, borderAttr);
        y++;

        foreach (var line in node.Lines)
        {
            if (!ctx.TryDocToScreen(y, out int screenY)) { y++; continue; }
            int x = ctx.IndentX;

            ctx.View.Move(x, screenY);
            ctx.View.SetAttribute(borderAttr);
            ctx.View.AddStr(bc.V.ToString());
            x++;

            if (line.GutterColor is { } gc)
            {
                ctx.View.Move(x, screenY);
                ctx.View.SetAttribute(RenderContext.MakeAttr(gc, null));
                ctx.View.AddStr("\u258c");
                x++;
            }

            int drawn = line.GutterColor is not null ? 1 : 0;
            foreach (var span in line.Spans)
            {
                if (x >= ctx.ViewportWidth - 1) break;
                var fg = span.Fg ?? t.CodeFg;
                var bg = span.Bg ?? t.CodeBg;
                ctx.View.SetAttribute(ctx.Attr(fg, bg));
                int maxLen = Math.Max(0, ctx.ViewportWidth - x - 1);
                var text = UnicodeWidth.GetWidth(span.Text) > maxLen ? UnicodeWidth.Truncate(span.Text, maxLen) : span.Text;
                int xBefore = x;
                ctx.DrawStrAt(text, ref x, screenY);
                drawn += x - xBefore;
            }

            int pad = Math.Max(0, contentWidth - drawn - 1);
            if (x < ctx.ViewportWidth)
            {
                ctx.View.Move(x, screenY);
                ctx.View.SetAttribute(bgAttr);
                ctx.View.AddStr(new string(' ', Math.Min(pad, ctx.ViewportWidth - x - 1)));
                x += Math.Min(pad, ctx.ViewportWidth - x - 1);
            }
            if (x < ctx.ViewportWidth)
            {
                ctx.View.Move(x, screenY);
                ctx.View.SetAttribute(borderAttr);
                ctx.View.AddStr(bc.V.ToString());
            }
            y++;
        }

        string bottomBorder = $"{bc.BL}{new string(bc.H, contentWidth)}{bc.BR}";
        ctx.DrawText(y, 0, bottomBorder, borderAttr);
    }
}

// ── Table ───────────────────────────────────────────────────────────────────

public static class TableRenderer
{
    public static void Render(TableNode node, RenderContext ctx)
    {
        if (node.ColumnWidths.Length == 0) return;

        var t = ctx.Theme;
        var bc = t.Borders;
        int y = node.OffsetY;

        // Compute border attr with the outer (document) background so cell bg
        // doesn't bleed through border characters to the outside of the table.
        var borderAttr = ctx.Attr(t.TableBorderFg);

        // Push table cell background so cell text and padding use it
        using (ctx.PushBgOverride(t.TableCellBg))
        {
            var headerAttr = ctx.Attr(t.TableHeaderFg, t.TableHeaderBg);
            var cellAttr = ctx.Attr(t.TableCellFg);

            // Top border
            DrawBorder(ctx, y, node.ColumnWidths, bc.TL, bc.TM, bc.TR, bc.H, borderAttr);
            y++;

            // Header row
            DrawRow(ctx, y, node.HeaderCells, node.ColumnWidths, node.Alignments, headerAttr, borderAttr, bc);
            int headerHeight = node.HeaderCells.Count > 0
                ? node.HeaderCells.Max(c => Math.Max(1, c.Lines.Count))
                : 1;
            y += headerHeight;

            // Header-body divider
            DrawBorder(ctx, y, node.ColumnWidths, bc.ML, bc.MM, bc.MR, bc.H, borderAttr);
            y++;

            // Body rows
            for (int i = 0; i < node.Rows.Count; i++)
            {
                var row = node.Rows[i];
                DrawRow(ctx, y, row.Cells, node.ColumnWidths, node.Alignments, cellAttr, borderAttr, bc);
                y += row.Height;

                if (i < node.Rows.Count - 1 && t.TableRowDividers)
                {
                    DrawBorder(ctx, y, node.ColumnWidths, bc.ML, bc.MM, bc.MR, bc.H, borderAttr);
                    y++;
                }
            }

            // Bottom border
            DrawBorder(ctx, y, node.ColumnWidths, bc.BL, bc.BM, bc.BR, bc.H, borderAttr);
        }
    }

    private static void DrawBorder(
        RenderContext ctx, int docY, int[] colWidths,
        char left, char mid, char right, char fill, Attribute attr)
    {
        if (!ctx.TryDocToScreen(docY, out _)) return;

        var sb = new System.Text.StringBuilder();
        sb.Append(left);
        for (int i = 0; i < colWidths.Length; i++)
        {
            sb.Append(new string(fill, colWidths[i]));
            sb.Append(i < colWidths.Length - 1 ? mid : right);
        }
        ctx.DrawText(docY, 0, sb.ToString(), attr);
    }

    private static void DrawRow(
        RenderContext ctx, int docY,
        List<TableCellNode> cells, int[] colWidths,
        ColumnAlign[] alignments, Attribute cellAttr, Attribute borderAttr,
        BorderChars bc)
    {
        int rowHeight = cells.Count > 0 ? cells.Max(c => Math.Max(1, c.Lines.Count)) : 1;

        for (int lineIdx = 0; lineIdx < rowHeight; lineIdx++)
        {
            int y = docY + lineIdx;
            if (!ctx.TryDocToScreen(y, out int screenY)) continue;

            int x = ctx.IndentX;
            ctx.View.Move(x, screenY);
            ctx.View.SetAttribute(borderAttr);
            ctx.View.AddStr(bc.V.ToString());
            x++;

            for (int col = 0; col < colWidths.Length; col++)
            {
                int colW = colWidths[col];
                var cell = col < cells.Count ? cells[col] : null;
                var cellLine = cell is not null && lineIdx < cell.Lines.Count
                    ? cell.Lines[lineIdx] : null;

                if (cellLine is not null && cellLine.Spans.Count > 0)
                {
                    // Draw gutter if present
                    int gutterWidth = 0;
                    if (cellLine.GutterColor is { } gc)
                    {
                        ctx.View.Move(x, screenY);
                        ctx.View.SetAttribute(RenderContext.MakeAttr(gc, null));
                        ctx.View.AddStr("\u258c");
                        x++;
                        gutterWidth = 1;
                    }

                    // Check if spans have their own backgrounds
                    bool hasCustomBg = cellLine.Spans.Any(s => s.Bg is not null);

                    // Draw span content
                    int drawn = gutterWidth;
                    foreach (var span in cellLine.Spans)
                    {
                        if (drawn >= colW - 1) break;
                        int maxLen = colW - drawn - 1;
                        var text = UnicodeWidth.GetWidth(span.Text) > maxLen ? UnicodeWidth.Truncate(span.Text, maxLen) : span.Text;
                        ctx.View.SetAttribute(ctx.SpanToAttribute(span));
                        int xBefore = x;
                        ctx.DrawStrAt(text, ref x, screenY);
                        int tw = x - xBefore;
                        drawn += tw;
                    }

                    // Pad remaining
                    int pad = Math.Max(0, colW - drawn);
                    if (pad > 0 && x < ctx.ViewportWidth)
                    {
                        ctx.View.Move(x, screenY);
                        ctx.View.SetAttribute(hasCustomBg ? ctx.SpanToAttribute(cellLine.Spans[^1]) : cellAttr);
                        ctx.View.AddStr(new string(' ', Math.Min(pad, ctx.ViewportWidth - x)));
                        x += pad;
                    }
                }
                else
                {
                    // Empty cell
                    if (x < ctx.ViewportWidth)
                    {
                        ctx.View.Move(x, screenY);
                        ctx.View.SetAttribute(cellAttr);
                        int fillLen = Math.Min(colW, ctx.ViewportWidth - x);
                        ctx.View.AddStr(new string(' ', fillLen));
                        x += colW;
                    }
                }

                // Column separator
                if (x < ctx.ViewportWidth)
                {
                    ctx.View.Move(x, screenY);
                    ctx.View.SetAttribute(borderAttr);
                    ctx.View.AddStr(bc.V.ToString());
                    x++;
                }
            }
        }
    }
}

// ── Details ─────────────────────────────────────────────────────────────────

public static class DetailsRenderer
{
    public static void Render(DetailsNode node, RenderContext ctx)
    {
        var t = ctx.Theme;
        var bc = t.Borders;
        int y = node.OffsetY;
        bool isFocused = ctx.FocusedDetails == node;
        bool isHovered = ctx.HoveredDetails == node;

        // Summary row background
        var summaryBgColor = isFocused ? t.DetailsSummFocusBg
            : isHovered ? t.DetailsSummHoverBg
            : t.DetailsSummBg;
        var summaryAttr = ctx.Attr(t.DetailsSummFg, summaryBgColor);

        string arrow = node.IsOpen ? t.OpenArrow : t.ClosedArrow;

        if (ctx.TryDocToScreen(y, out int screenY))
        {
            int x = ctx.IndentX;

            // Fill full line with background
            ctx.View.Move(x, screenY);
            ctx.View.SetAttribute(summaryAttr);
            ctx.View.AddStr(new string(' ', Math.Max(0, ctx.ViewportWidth - x)));

            // Draw arrow
            ctx.View.Move(x, screenY);
            ctx.View.SetAttribute(ctx.Attr(t.DetailsArrowFg, summaryBgColor));
            ctx.View.AddStr(arrow);
            x += arrow.Length;

            // Draw summary content spans
            foreach (var span in node.SummaryLine.Spans)
            {
                if (x >= ctx.ViewportWidth) break;
                var fg = span.Fg ?? t.DetailsSummFg;
                ctx.View.SetAttribute(ctx.Attr(fg, summaryBgColor));
                int maxLen = ctx.ViewportWidth - x;
                var text = UnicodeWidth.GetWidth(span.Text) > maxLen ? UnicodeWidth.Truncate(span.Text, maxLen) : span.Text;
                ctx.DrawStrAt(text, ref x, screenY);
            }

            // Focus indicator
            if (isFocused && x + 16 < ctx.ViewportWidth)
            {
                ctx.View.Move(x + 1, screenY);
                ctx.View.SetAttribute(ctx.Attr(t.HintFg, summaryBgColor));
                ctx.View.AddStr("[Enter to toggle]");
            }
        }

        if (!node.IsOpen) return;

        y++;

        int boxLeft = ctx.IndentX;
        int boxWidth = Math.Max(4, ctx.ViewportWidth - boxLeft);
        int contentHeight = node.Height - 4;

        if (t.FilledBorders)
            RenderFilled(node, ctx, t, y, boxLeft, boxWidth, contentHeight);
        else
            RenderBoxDrawing(node, ctx, t, y, boxLeft, boxWidth, contentHeight);
    }

    /// <summary>Half-block style: solid colour fill with blended top/bottom/side edges.</summary>
    private static void RenderFilled(DetailsNode node, RenderContext ctx, Theme t,
        int y, int boxLeft, int boxWidth, int contentHeight)
    {
        var contentBg = t.DetailsContentBg;
        // Edge attr: fg = contentBg (visible inner half), bg = surrounding background (visible outer half).
        // Use EffectiveBg so nested details pick up the parent content background instead of DocBg.
        var edgeAttr = ctx.Attr(contentBg, ctx.EffectiveBg);
        var fillAttr = ctx.Attr(t.DocFg, contentBg);

        // Top edge: ▄▄▄▄▄▄  (bottom half = contentBg, top half = outer bg)
        if (ctx.TryDocToScreen(y, out int topSy))
        {
            ctx.View.Move(boxLeft, topSy);
            ctx.View.SetAttribute(edgeAttr);
            ctx.View.AddStr(new string('▄', Math.Min(boxWidth, ctx.ViewportWidth - boxLeft)));
        }
        y++;

        // Content rows: fill background + half-block left/right edges
        for (int i = 0; i < contentHeight; i++)
        {
            if (ctx.TryDocToScreen(y + i, out int rowSy))
            {
                // Left edge: ▐ (right half = contentBg shown inside)
                ctx.View.Move(boxLeft, rowSy);
                ctx.View.SetAttribute(edgeAttr);
                ctx.View.AddStr("▐");

                // Fill interior with content background
                int innerLeft = boxLeft + 1;
                int innerWidth = Math.Max(0, boxWidth - 2);
                if (innerLeft < ctx.ViewportWidth && innerWidth > 0)
                {
                    ctx.View.Move(innerLeft, rowSy);
                    ctx.View.SetAttribute(fillAttr);
                    ctx.View.AddStr(new string(' ', Math.Min(innerWidth, ctx.ViewportWidth - innerLeft)));
                }

                // Right edge: ▌ (left half = contentBg shown inside)
                int rightX = boxLeft + boxWidth - 1;
                if (rightX < ctx.ViewportWidth)
                {
                    ctx.View.Move(rightX, rowSy);
                    ctx.View.SetAttribute(edgeAttr);
                    ctx.View.AddStr("▌");
                }
            }
        }

        // Render child content with bg override so Attr(fg) uses contentBg
        using (ctx.PushBgOverride(contentBg))
        using (ctx.PushIndent(3))
        {
            node.Content.Render(ctx);
        }

        // Bottom edge: ▀▀▀▀▀▀  (top half = contentBg, bottom half = outer bg)
        int bottomY = y + contentHeight;
        if (ctx.TryDocToScreen(bottomY, out int botSy))
        {
            ctx.View.Move(boxLeft, botSy);
            ctx.View.SetAttribute(edgeAttr);
            ctx.View.AddStr(new string('▀', Math.Min(boxWidth, ctx.ViewportWidth - boxLeft)));
        }
    }

    /// <summary>Box-drawing style: visible border lines using DetailsBorderFg and theme box chars.</summary>
    private static void RenderBoxDrawing(DetailsNode node, RenderContext ctx, Theme t,
        int y, int boxLeft, int boxWidth, int contentHeight)
    {
        var bc = t.Borders;
        var contentBg = t.DetailsContentBg;
        var borderAttr = ctx.Attr(t.DetailsBorderFg, ctx.EffectiveBg);
        var fillAttr = ctx.Attr(t.DocFg, contentBg);

        // Top border: ╭──────────────────────╮  (DrawText adds IndentX to x, so pass 0)
        int dashCount = Math.Max(0, boxWidth - 2);
        string topLine = $"{bc.TL}{new string(bc.H, dashCount)}{bc.TR}";
        ctx.DrawText(y, 0, topLine, borderAttr);
        y++;

        // Content rows: border │ on each side, content background fill
        for (int i = 0; i < contentHeight; i++)
        {
            if (ctx.TryDocToScreen(y + i, out int rowSy))
            {
                // Left border
                ctx.View.Move(boxLeft, rowSy);
                ctx.View.SetAttribute(borderAttr);
                ctx.View.AddStr(bc.V.ToString());

                // Interior fill
                int innerLeft = boxLeft + 1;
                int innerWidth = Math.Max(0, boxWidth - 2);
                if (innerLeft < ctx.ViewportWidth && innerWidth > 0)
                {
                    ctx.View.Move(innerLeft, rowSy);
                    ctx.View.SetAttribute(fillAttr);
                    ctx.View.AddStr(new string(' ', Math.Min(innerWidth, ctx.ViewportWidth - innerLeft)));
                }

                // Right border
                int rightX = boxLeft + boxWidth - 1;
                if (rightX < ctx.ViewportWidth)
                {
                    ctx.View.Move(rightX, rowSy);
                    ctx.View.SetAttribute(borderAttr);
                    ctx.View.AddStr(bc.V.ToString());
                }
            }
        }

        // Render child content with bg override and indent to sit inside the box
        using (ctx.PushBgOverride(contentBg))
        using (ctx.PushIndent(3))
        {
            node.Content.Render(ctx);
        }

        // Bottom border: ╰──────────────────────╯
        int bottomY = y + contentHeight;
        string botLine = $"{bc.BL}{new string(bc.H, dashCount)}{bc.BR}";
        ctx.DrawText(bottomY, 0, botLine, borderAttr);
    }
}
