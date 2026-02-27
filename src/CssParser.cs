using System.Globalization;
using System.Text.RegularExpressions;

namespace Md2Terminal;

/// <summary>
/// Parses a small whitelist of CSS properties from inline style="" attributes.
/// Supports: color, background-color, border-left (color extraction),
/// font-weight (bold), font-style (italic).
/// All other properties are silently ignored.
/// </summary>
public static partial class CssParser
{
    // ── Regex patterns ──────────────────────────────────────────────────────

    [GeneratedRegex(@"#([0-9a-fA-F]{6})\b")]
    private static partial Regex HexColor6Regex();

    [GeneratedRegex(@"#([0-9a-fA-F]{3})\b")]
    private static partial Regex HexColor3Regex();

    [GeneratedRegex(@"rgb\(\s*(\d{1,3})\s*,\s*(\d{1,3})\s*,\s*(\d{1,3})\s*\)")]
    private static partial Regex RgbFuncRegex();

    // ── Named colors (only the ones actually appearing in the demo file) ────

    private static readonly Dictionary<string, RgbColor> NamedColors = new(StringComparer.OrdinalIgnoreCase)
    {
        ["black"]       = new(0, 0, 0),
        ["white"]       = new(255, 255, 255),
        ["red"]         = new(255, 0, 0),
        ["green"]       = new(0, 128, 0),
        ["blue"]        = new(0, 0, 255),
        ["yellow"]      = new(255, 255, 0),
        ["gray"]        = new(128, 128, 128),
        ["grey"]        = new(128, 128, 128),
        ["silver"]      = new(192, 192, 192),
        ["orange"]      = new(255, 165, 0),
        ["purple"]      = new(128, 0, 128),
        ["cyan"]        = new(0, 255, 255),
        ["magenta"]     = new(255, 0, 255),
        ["darkgray"]    = new(169, 169, 169),
        ["darkgrey"]    = new(169, 169, 169),
        ["lightgray"]   = new(211, 211, 211),
        ["lightgrey"]   = new(211, 211, 211),
        ["cornflowerblue"] = new(100, 149, 237),
        ["transparent"] = new(0, 0, 0), // best-effort; terminals have no transparency
    };

    // ── Public API ──────────────────────────────────────────────────────────

    /// <summary>
    /// Parse an inline style attribute value and merge onto a parent StyleContext.
    /// Returns a new StyleContext with the parsed values merged on top.
    /// </summary>
    public static StyleContext ParseInlineStyle(string? styleAttr, StyleContext parent)
    {
        if (string.IsNullOrWhiteSpace(styleAttr))
            return parent;

        RgbColor? fg = null;
        RgbColor? bg = null;
        RgbColor? borderLeft = null;
        bool bold = false;
        bool italic = false;

        // Split on ';' and process each property: value pair
        foreach (var decl in styleAttr.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var colonIdx = decl.IndexOf(':');
            if (colonIdx < 0) continue;

            var prop = decl[..colonIdx].Trim().ToLowerInvariant();
            var val = decl[(colonIdx + 1)..].Trim();

            switch (prop)
            {
                case "color":
                    fg = ParseColor(val);
                    break;

                case "background-color" or "background":
                    bg = ParseColor(val);
                    break;

                case "border-left":
                    // e.g. "3px solid #d73a49" — extract the color part
                    borderLeft = ExtractBorderColor(val);
                    break;

                case "font-weight":
                    bold = val.Equals("bold", StringComparison.OrdinalIgnoreCase)
                        || val == "700" || val == "800" || val == "900";
                    break;

                case "font-style":
                    italic = val.Equals("italic", StringComparison.OrdinalIgnoreCase);
                    break;

                // Everything else (display, padding, margin, white-space, etc.) is ignored
            }
        }

        var child = new StyleContext(
            Fg: fg,
            Bg: bg,
            Bold: bold,
            Italic: italic,
            BorderLeftColor: borderLeft);

        return parent.Merge(child);
    }

    /// <summary>Parse a CSS color value. Supports #hex, rgb(), and named colors.</summary>
    public static RgbColor? ParseColor(string value)
    {
        var v = value.Trim();

        // #rrggbb
        var m6 = HexColor6Regex().Match(v);
        if (m6.Success)
        {
            var hex = m6.Groups[1].Value;
            return new RgbColor(
                byte.Parse(hex[0..2], NumberStyles.HexNumber),
                byte.Parse(hex[2..4], NumberStyles.HexNumber),
                byte.Parse(hex[4..6], NumberStyles.HexNumber));
        }

        // #rgb
        var m3 = HexColor3Regex().Match(v);
        if (m3.Success)
        {
            var hex = m3.Groups[1].Value;
            byte r = byte.Parse($"{hex[0]}{hex[0]}", NumberStyles.HexNumber);
            byte g = byte.Parse($"{hex[1]}{hex[1]}", NumberStyles.HexNumber);
            byte b = byte.Parse($"{hex[2]}{hex[2]}", NumberStyles.HexNumber);
            return new RgbColor(r, g, b);
        }

        // rgb(r, g, b)
        var mRgb = RgbFuncRegex().Match(v);
        if (mRgb.Success)
        {
            return new RgbColor(
                byte.Parse(mRgb.Groups[1].Value),
                byte.Parse(mRgb.Groups[2].Value),
                byte.Parse(mRgb.Groups[3].Value));
        }

        // Named color
        if (NamedColors.TryGetValue(v, out var named))
            return named;

        return null;
    }

    private static RgbColor? ExtractBorderColor(string borderValue)
    {
        // border-left: 3px solid #d73a49
        // Try to find a color anywhere in the value
        var m6 = HexColor6Regex().Match(borderValue);
        if (m6.Success)
            return ParseColor(m6.Value);

        var m3 = HexColor3Regex().Match(borderValue);
        if (m3.Success)
            return ParseColor(m3.Value);

        var mRgb = RgbFuncRegex().Match(borderValue);
        if (mRgb.Success)
            return ParseColor(mRgb.Value);

        // Try named colors — split by spaces and check each word
        foreach (var word in borderValue.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (NamedColors.ContainsKey(word))
                return ParseColor(word);
        }

        return null;
    }
}
