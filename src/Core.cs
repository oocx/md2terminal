using Terminal.Gui;

namespace Md2Terminal;

/// <summary>24-bit RGB color.</summary>
public readonly record struct RgbColor(byte R, byte G, byte B)
{
    public static readonly RgbColor White = new(255, 255, 255);
    public static readonly RgbColor Black = new(0, 0, 0);

    public override string ToString() => $"#{R:X2}{G:X2}{B:X2}";
}

/// <summary>A run of styled text within a line.</summary>
public sealed record StyledSpan(
    string Text,
    RgbColor? Fg = null,
    RgbColor? Bg = null,
    bool Bold = false,
    bool Italic = false,
    bool Code = false,
    bool Strikethrough = false,
    RgbColor? GutterColor = null);

/// <summary>One visual line of styled text made up of contiguous spans.</summary>
public sealed class StyledLine
{
    public List<StyledSpan> Spans { get; } = [];

    public StyledLine() { }
    public StyledLine(IEnumerable<StyledSpan> spans) => Spans.AddRange(spans);

    /// <summary>Visible display width accounting for wide Unicode characters.</summary>
    public int VisibleLength => Spans.Sum(s => UnicodeWidth.GetWidth(s.Text));

    /// <summary>Gets the gutter color from the first span that has one, if any.</summary>
    public RgbColor? GutterColor => Spans.FirstOrDefault(s => s.GutterColor is not null)?.GutterColor;
}

/// <summary>Helpers for Unicode-aware display width.</summary>
public static class UnicodeWidth
{
    /// <summary>
    /// Display width of a string, accounting for wide/emoji chars.
    /// Corrects Terminal.Gui's GetColumns() which under-counts emoji
    /// with Variation Selector 16 (U+FE0F) that request emoji presentation.
    /// </summary>
    public static int GetWidth(string text)
    {
        int cols = 0;
        int i = 0;
        while (i < text.Length)
        {
            int cp = char.IsHighSurrogate(text[i]) && i + 1 < text.Length
                ? char.ConvertToUtf32(text[i], text[i + 1])
                : text[i];
            var rune = new System.Text.Rune(cp);
            int runeLen = rune.Utf16SequenceLength;
            int w = rune.GetColumns();

            // VS16 (U+FE0F) following a text-default char promotes it to emoji (2 columns)
            if (i + runeLen < text.Length && text[i + runeLen] == '\uFE0F' && w < 2)
            {
                w = 2;
                runeLen++; // consume the VS16
            }

            cols += w;
            i += runeLen;
        }
        return cols;
    }

    /// <summary>Truncate a string to fit within maxColumns display columns.</summary>
    public static string Truncate(string text, int maxColumns)
    {
        if (GetWidth(text) <= maxColumns) return text;

        int cols = 0;
        int i = 0;
        while (i < text.Length)
        {
            int cp = char.IsHighSurrogate(text[i]) && i + 1 < text.Length
                ? char.ConvertToUtf32(text[i], text[i + 1])
                : text[i];
            var rune = new System.Text.Rune(cp);
            int runeLen = rune.Utf16SequenceLength;
            int w = rune.GetColumns();

            if (i + runeLen < text.Length && text[i + runeLen] == '\uFE0F' && w < 2)
            {
                w = 2;
                runeLen++;
            }

            if (cols + w > maxColumns) break;
            cols += w;
            i += runeLen;
        }
        return text[..i];
    }
}

/// <summary>
/// Immutable style context threaded through DOM/inline walks.
/// Inner values win (simplified CSS cascade via Merge).
/// </summary>
public sealed record StyleContext(
    RgbColor? Fg = null,
    RgbColor? Bg = null,
    bool Bold = false,
    bool Italic = false,
    bool Code = false,
    bool Strikethrough = false,
    RgbColor? BorderLeftColor = null)
{
    public static readonly StyleContext Default = new();

    /// <summary>Merge child styles on top of this parent. Child wins when set.</summary>
    public StyleContext Merge(StyleContext child) => new(
        Fg: child.Fg ?? Fg,
        Bg: child.Bg ?? Bg,
        Bold: child.Bold || Bold,
        Italic: child.Italic || Italic,
        Code: child.Code || Code,
        Strikethrough: child.Strikethrough || Strikethrough,
        BorderLeftColor: child.BorderLeftColor ?? BorderLeftColor);

    public StyledSpan ToSpan(string text) => new(
        text.Replace('\u00A0', ' '),
        Fg: Fg,
        Bg: Bg,
        Bold: Bold,
        Italic: Italic,
        Code: Code,
        Strikethrough: Strikethrough,
        GutterColor: BorderLeftColor);
}
