using Terminal.Gui;
using Attribute = Terminal.Gui.Attribute;

namespace Md2Terminal;

/// <summary>
/// Passed to every RenderNode.Render() call. Carries viewport state, scroll offset,
/// indent tracking, and drawing helpers that map document coordinates to screen.
/// </summary>
public sealed class RenderContext
{
    public required View View { get; init; }
    public required Theme Theme { get; init; }
    public int ScrollY { get; init; }
    public int ViewportWidth { get; init; }
    public int ViewportHeight { get; init; }
    public DetailsNode? HoveredDetails { get; init; }
    public DetailsNode? FocusedDetails { get; init; }

    /// <summary>Active text selection, or null when nothing is selected.</summary>
    public TextSelection? Selection { get; init; }

    /// <summary>
    /// Screen-coordinate character buffer: [screenRow, screenCol]. Populated by
    /// <see cref="DrawStrAt"/> whenever this property is non-null. Used by
    /// <see cref="MarkdownView"/> to apply the selection highlight overlay and to
    /// extract the selected text for copying.
    /// </summary>
    public char[,]? ScreenBuf { get; init; }

    /// <summary>Attribute used to highlight the selected text (inverted foreground/background).</summary>
    public Attribute SelectionAttr => new(RgbToColor(Theme.DocBg), RgbToColor(Theme.DocFg));

    private readonly Stack<int> _indentStack = new();
    public int IndentX => _indentStack.Sum();

    private readonly Stack<RgbColor> _bgOverrideStack = new();
    /// <summary>The effective default background: topmost override, or DocBg.</summary>
    public RgbColor EffectiveBg => _bgOverrideStack.Count > 0 ? _bgOverrideStack.Peek() : Theme.DocBg;

    /// <summary>Convert document Y to screen Y. Returns false if outside viewport.</summary>
    public bool TryDocToScreen(int docY, out int screenY)
    {
        screenY = docY - ScrollY;
        return screenY >= 0 && screenY < ViewportHeight;
    }

    /// <summary>
    /// Draw text starting at absolute screen column x, segmenting at VS16 emoji boundaries.
    /// Terminal.Gui's AddStr internally tracks cursor width using GetColumns(), which
    /// under-counts VS16 emoji. Segmenting and calling Move() after each emoji corrects drift.
    /// Advances x by the actual display-column width of the text drawn.
    /// </summary>
    public void DrawStrAt(string text, ref int x, int screenY)
    {
        int i = 0;
        int segStart = 0;

        while (i < text.Length)
        {
            int cp = char.IsHighSurrogate(text[i]) && i + 1 < text.Length
                ? char.ConvertToUtf32(text[i], text[i + 1])
                : text[i];
            var rune = new System.Text.Rune(cp);
            int runeLen = rune.Utf16SequenceLength;

            // VS16 after a text-default char → 2-column emoji presentation
            if (i + runeLen < text.Length && text[i + runeLen] == '\uFE0F' && rune.GetColumns() < 2)
            {
                // Flush preceding plain text first
                if (i > segStart)
                {
                    var seg = text[segStart..i];
                    View.Move(x, screenY);
                    View.AddStr(seg);
                    RecordSegment(seg, screenY, x);
                    x += UnicodeWidth.GetWidth(seg);
                }

                // Draw the emoji (base + VS16) then explicitly correct cursor to x+2
                var emoji = text[i..(i + runeLen + 1)];
                View.Move(x, screenY);
                View.AddStr(emoji);
                RecordEmoji(emoji[..runeLen], screenY, x);
                x += 2;
                i += runeLen + 1;
                segStart = i;
            }
            else
            {
                i += runeLen;
            }
        }

        // Flush remaining plain text
        if (segStart < text.Length)
        {
            var remaining = text[segStart..];
            View.Move(x, screenY);
            View.AddStr(remaining);
            RecordSegment(remaining, screenY, x);
            x += UnicodeWidth.GetWidth(remaining);
        }
    }

    /// <summary>
    /// Records a plain-text segment into <see cref="ScreenBuf"/> at the given screen position.
    /// Wide characters occupy two cells; the second cell is stored as '\0' (wide-char marker).
    /// </summary>
    private void RecordSegment(string text, int screenY, int startCol)
    {
        if (ScreenBuf is null || screenY < 0 || screenY >= ScreenBuf.GetLength(0)) return;
        int col = startCol;
        for (int i = 0; i < text.Length && col < ViewportWidth; )
        {
            int cp = char.IsHighSurrogate(text[i]) && i + 1 < text.Length
                ? char.ConvertToUtf32(text[i], text[i + 1]) : text[i];
            var rune = new System.Text.Rune(cp);
            int runeLen = rune.Utf16SequenceLength;
            int cols   = rune.GetColumns();

            ScreenBuf[screenY, col] = text[i];
            if (cols >= 2 && col + 1 < ViewportWidth)
                ScreenBuf[screenY, col + 1] = '\0'; // wide-char trailing marker

            col += Math.Max(1, cols);
            i   += runeLen;
        }
    }

    /// <summary>Records a VS16 emoji base character (2-column wide) into <see cref="ScreenBuf"/>.</summary>
    private void RecordEmoji(string baseChar, int screenY, int col)
    {
        if (ScreenBuf is null || screenY < 0 || screenY >= ScreenBuf.GetLength(0)) return;
        if (col < ViewportWidth) ScreenBuf[screenY, col] = baseChar[0];
        if (col + 1 < ViewportWidth) ScreenBuf[screenY, col + 1] = '\0';
    }

    /// <summary>Draw a plain string at document-Y, screen-X.</summary>
    public void DrawText(int docY, int x, string text, Attribute attr)
    {
        if (!TryDocToScreen(docY, out int screenY)) return;
        int screenX = x + IndentX;
        if (screenX >= ViewportWidth) return;

        // Truncate text to fit viewport
        int maxLen = ViewportWidth - screenX;
        if (UnicodeWidth.GetWidth(text) > maxLen) text = UnicodeWidth.Truncate(text, maxLen);

        View.SetAttribute(attr);
        DrawStrAt(text, ref screenX, screenY);
    }

    /// <summary>Draw a styled line at document Y, starting at screen X.</summary>
    public void DrawStyledLine(int docY, int startX, StyledLine line, Attribute? defaultAttr = null)
    {
        if (!TryDocToScreen(docY, out int screenY)) return;
        int x = startX + IndentX;

        // Draw gutter character if the line has a border-left color
        if (line.GutterColor is { } gc)
        {
            if (x < ViewportWidth)
            {
                View.Move(x, screenY);
                View.SetAttribute(MakeAttr(gc, null));
                View.AddStr("▌");
                x++;
            }
        }

        foreach (var span in line.Spans)
        {
            if (x >= ViewportWidth) break;

            var attr = SpanToAttribute(span, defaultAttr);
            View.SetAttribute(attr);

            int maxLen = ViewportWidth - x;
            var text = UnicodeWidth.GetWidth(span.Text) > maxLen ? UnicodeWidth.Truncate(span.Text, maxLen) : span.Text;
            DrawStrAt(text, ref x, screenY);
        }
    }

    /// <summary>Draw a row of styled spans with a background fill.</summary>
    public void DrawStyledSpans(int docY, int startX, IReadOnlyList<StyledSpan> spans, Attribute? bgFillAttr = null)
    {
        if (!TryDocToScreen(docY, out int screenY)) return;
        int x = startX + IndentX;

        foreach (var span in spans)
        {
            if (x >= ViewportWidth) break;

            var attr = SpanToAttribute(span, bgFillAttr);
            View.SetAttribute(attr);

            int maxLen = ViewportWidth - x;
            var text = UnicodeWidth.GetWidth(span.Text) > maxLen ? UnicodeWidth.Truncate(span.Text, maxLen) : span.Text;
            DrawStrAt(text, ref x, screenY);
        }

        // Fill remaining width with background if specified
        if (bgFillAttr.HasValue && x < ViewportWidth)
        {
            View.Move(x, screenY);
            View.SetAttribute(bgFillAttr.Value);
            View.AddStr(new string(' ', ViewportWidth - x));
        }
    }

    /// <summary>Push an indent amount. Returns a disposable that pops on dispose.</summary>
    public IDisposable PushIndent(int dx)
    {
        _indentStack.Push(dx);
        return new StackPopper<int>(_indentStack);
    }

    /// <summary>Push a background override. Content rendered while this is active will
    /// use the given color instead of DocBg as the default background.</summary>
    public IDisposable PushBgOverride(RgbColor bg)
    {
        _bgOverrideStack.Push(bg);
        return new StackPopper<RgbColor>(_bgOverrideStack);
    }

    private sealed class StackPopper<T>(Stack<T> stack) : IDisposable
    {
        public void Dispose() { if (stack.Count > 0) stack.Pop(); }
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    public Attribute SpanToAttribute(StyledSpan span, Attribute? defaults = null)
    {
        var fg = ToColor(span.Fg) ?? (defaults?.Foreground ?? RgbToColor(Theme.DocFg));
        var bg = ToColor(span.Bg) ?? (defaults?.Background ?? RgbToColor(EffectiveBg));
        return new Attribute(fg, bg);
    }

    /// <summary>Create an attribute with effective background default (respects overrides).</summary>
    public Attribute Attr(RgbColor fg) => new(RgbToColor(fg), RgbToColor(EffectiveBg));
    public Attribute Attr(RgbColor fg, RgbColor bg) => new(RgbToColor(fg), RgbToColor(bg));

    public static Attribute MakeAttr(RgbColor? fg, RgbColor? bg)
    {
        var fgColor = fg is not null ? new Color(fg.Value.R, fg.Value.G, fg.Value.B) : new Color(255, 255, 255);
        var bgColor = bg is not null ? new Color(bg.Value.R, bg.Value.G, bg.Value.B) : new Color(0, 0, 0);
        return new Attribute(fgColor, bgColor);
    }

    public static Attribute MakeAttr(int fr, int fg, int fb, int br = 0, int bg = 0, int bb = 0)
        => new(new Color(fr, fg, fb), new Color(br, bg, bb));

    public static Color RgbToColor(RgbColor c) => new(c.R, c.G, c.B);

    private static Color? ToColor(RgbColor? c)
        => c is not null ? new Color(c.Value.R, c.Value.G, c.Value.B) : null;
}
