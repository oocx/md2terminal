namespace Md2Terminal;

/// <summary>Box-drawing character set for borders.</summary>
public readonly record struct BorderChars(
    char TL, char TM, char TR,
    char ML, char MM, char MR,
    char BL, char BM, char BR,
    char H, char V)
{
    public static readonly BorderChars Single  = new('┌', '┬', '┐', '├', '┼', '┤', '└', '┴', '┘', '─', '│');
    public static readonly BorderChars Double  = new('╔', '╦', '╗', '╠', '╬', '╣', '╚', '╩', '╝', '═', '║');
    public static readonly BorderChars Rounded = new('╭', '┬', '╮', '├', '┼', '┤', '╰', '┴', '╯', '─', '│');
    public static readonly BorderChars Heavy   = new('┏', '┳', '┓', '┣', '╋', '┫', '┗', '┻', '┛', '━', '┃');
    public static readonly BorderChars Ascii   = new('+', '+', '+', '+', '+', '+', '+', '+', '+', '-', '|');
}

/// <summary>Complete visual theme controlling all colors, border characters, and decorations.</summary>
public sealed record Theme
{
    public required string Name { get; init; }

    // ── Document ──
    public required RgbColor DocBg { get; init; }
    public required RgbColor DocFg { get; init; }

    // ── Headings (6 levels) ──
    public required RgbColor[] HeadingColors { get; init; }
    public required string[] HeadingPrefixes { get; init; }
    public char H1Rule { get; init; } = '═';
    public char H2Rule { get; init; } = '─';

    // ── Code blocks ──
    public required RgbColor CodeBorderFg { get; init; }
    public required RgbColor CodeBg { get; init; }
    public required RgbColor CodeFg { get; init; }

    // ── Tables ──
    public required RgbColor TableBorderFg { get; init; }
    public required RgbColor TableHeaderBg { get; init; }
    public required RgbColor TableHeaderFg { get; init; }
    public required RgbColor TableCellBg { get; init; }
    public required RgbColor TableCellFg { get; init; }
    public bool TableRowDividers { get; init; } = true;

    // ── Details / collapsible ──
    public required RgbColor DetailsSummBg { get; init; }
    public required RgbColor DetailsSummHoverBg { get; init; }
    public required RgbColor DetailsSummFocusBg { get; init; }
    public required RgbColor DetailsSummFg { get; init; }
    public required RgbColor DetailsArrowFg { get; init; }
    public required RgbColor DetailsBorderFg { get; init; }
    public string OpenArrow { get; init; } = "▼ ";
    public string ClosedArrow { get; init; } = "▶ ";

    // ── Blockquote ──
    public required RgbColor BlockquoteGutterFg { get; init; }
    public string BlockquoteGutter { get; init; } = "▌ ";

    // ── Lists ──
    public required RgbColor BulletFg { get; init; }
    public string[] Bullets { get; init; } = ["● ", "○ ", "▪ "];

    // ── Thematic break ──
    public required RgbColor HrFg { get; init; }
    public char HrChar { get; init; } = '─';

    // ── Details content background (distinct from DocBg, rendered with half-block edges) ──
    public required RgbColor DetailsContentBg { get; init; }

    // ── Misc ──
    public required RgbColor HintFg { get; init; }
    public required BorderChars Borders { get; init; }

    /// <summary>
    /// When true, border characters share the content background color instead of DocBg,
    /// making the entire box (frame + content) a solid coloured block.
    /// </summary>
    public bool FilledBorders { get; init; } = false;

    // ═════════════════════════════════════════════════════════════════════════
    // Presets (All list at end of class)
    // ═════════════════════════════════════════════════════════════════════════

    // ── 1. Dark Modern ──────────────────────────────────────────────────────
    public static readonly Theme DarkModern = new()
    {
        Name = "Dark Modern",
        DocBg = new(15, 15, 20), DocFg = new(230, 230, 230),
        HeadingColors = [new(255, 215, 0), new(100, 180, 255), new(150, 255, 150),
                         new(200, 200, 100), new(180, 180, 180), new(140, 140, 140)],
        HeadingPrefixes = ["█ ", "▌ ", "▎ ", "  ", "  ", "  "],
        CodeBorderFg = new(100, 100, 140), CodeBg = new(30, 30, 40), CodeFg = new(220, 220, 220),
        TableBorderFg = new(100, 100, 140), TableHeaderBg = new(40, 40, 55),
        TableHeaderFg = new(255, 255, 255), TableCellBg = new(25, 25, 38),
        TableCellFg = new(220, 220, 220),
        DetailsSummBg = new(25, 25, 35), DetailsSummHoverBg = new(40, 50, 80),
        DetailsSummFocusBg = new(50, 70, 120), DetailsSummFg = new(255, 255, 255),
        DetailsArrowFg = new(180, 200, 255), DetailsBorderFg = new(60, 60, 100),
        DetailsContentBg = new(30, 30, 48),
        BlockquoteGutterFg = new(120, 120, 180),
        BulletFg = new(180, 180, 220),
        HrFg = new(100, 100, 120),
        HintFg = new(120, 120, 160),
        Borders = BorderChars.Single,
    };

    // ── 2. Monokai Pro ──────────────────────────────────────────────────────
    public static readonly Theme MonokaiPro = new()
    {
        Name = "Monokai Pro",
        DocBg = new(46, 44, 28), DocFg = new(248, 248, 242),
        HeadingColors = [new(253, 151, 31), new(249, 38, 114), new(166, 226, 46),
                         new(102, 217, 239), new(174, 129, 255), new(117, 113, 94)],
        HeadingPrefixes = ["█ ", "▌ ", "▎ ", "  ", "  ", "  "],
        CodeBorderFg = new(117, 113, 94), CodeBg = new(32, 32, 27), CodeFg = new(248, 248, 242),
        TableBorderFg = new(117, 113, 94), TableHeaderBg = new(55, 55, 48),
        TableHeaderFg = new(248, 248, 242), TableCellBg = new(50, 48, 36),
        TableCellFg = new(230, 230, 220),
        DetailsSummBg = new(50, 50, 44), DetailsSummHoverBg = new(65, 60, 50),
        DetailsSummFocusBg = new(80, 55, 30), DetailsSummFg = new(248, 248, 242),
        DetailsArrowFg = new(253, 151, 31), DetailsBorderFg = new(90, 88, 75),
        DetailsContentBg = new(62, 58, 40),
        BlockquoteGutterFg = new(249, 38, 114), BlockquoteGutter = "▌ ",
        BulletFg = new(166, 226, 46), Bullets = ["▸ ", "▹ ", "‣ "],
        HrFg = new(117, 113, 94),
        HintFg = new(117, 113, 94),
        Borders = BorderChars.Rounded,
    };

    // ── 3. Dracula ──────────────────────────────────────────────────────────
    public static readonly Theme Dracula = new()
    {
        Name = "Dracula",
        DocBg = new(34, 36, 58), DocFg = new(248, 248, 242),
        HeadingColors = [new(139, 233, 253), new(80, 250, 123), new(255, 184, 108),
                         new(255, 121, 198), new(189, 147, 249), new(98, 114, 164)],
        HeadingPrefixes = ["◆ ", "◇ ", "▸ ", "▸ ", "  ", "  "],
        CodeBorderFg = new(98, 114, 164), CodeBg = new(33, 34, 44), CodeFg = new(248, 248, 242),
        TableBorderFg = new(98, 114, 164), TableHeaderBg = new(55, 57, 72),
        TableHeaderFg = new(255, 255, 255), TableCellBg = new(42, 44, 66),
        TableCellFg = new(248, 248, 242),
        DetailsSummBg = new(50, 52, 66), DetailsSummHoverBg = new(60, 62, 80),
        DetailsSummFocusBg = new(70, 72, 100), DetailsSummFg = new(248, 248, 242),
        DetailsArrowFg = new(189, 147, 249), DetailsBorderFg = new(98, 114, 164),
        DetailsContentBg = new(50, 54, 78),
        BlockquoteGutterFg = new(139, 233, 253),
        BulletFg = new(255, 121, 198),
        HrFg = new(98, 114, 164),
        HintFg = new(98, 114, 164),
        Borders = BorderChars.Rounded,
    };

    // ── 4. Nord Frost ───────────────────────────────────────────────────────
    public static readonly Theme NordFrost = new()
    {
        Name = "Nord Frost",
        DocBg = new(36, 44, 66), DocFg = new(216, 222, 233),
        HeadingColors = [new(136, 192, 208), new(129, 161, 193), new(163, 190, 140),
                         new(235, 203, 139), new(208, 135, 112), new(180, 142, 173)],
        HeadingPrefixes = ["█ ", "▌ ", "▎ ", "  ", "  ", "  "],
        H1Rule = '━', H2Rule = '─',
        CodeBorderFg = new(76, 86, 106), CodeBg = new(59, 66, 82), CodeFg = new(216, 222, 233),
        TableBorderFg = new(76, 86, 106), TableHeaderBg = new(59, 66, 82),
        TableHeaderFg = new(229, 233, 240), TableCellBg = new(46, 54, 74),
        TableCellFg = new(216, 222, 233),
        DetailsSummBg = new(59, 66, 82), DetailsSummHoverBg = new(67, 76, 94),
        DetailsSummFocusBg = new(76, 86, 106), DetailsSummFg = new(229, 233, 240),
        DetailsArrowFg = new(136, 192, 208), DetailsBorderFg = new(76, 86, 106),
        DetailsContentBg = new(50, 60, 90),
        BlockquoteGutterFg = new(136, 192, 208),
        BulletFg = new(163, 190, 140),
        HrFg = new(76, 86, 106),
        HintFg = new(76, 86, 106),
        Borders = BorderChars.Heavy,
    };

    // ── 5. Gruvbox Dark ─────────────────────────────────────────────────────
    public static readonly Theme GruvboxDark = new()
    {
        Name = "Gruvbox Dark",
        DocBg = new(50, 42, 30), DocFg = new(235, 219, 178),
        HeadingColors = [new(254, 128, 25), new(250, 189, 47), new(184, 187, 38),
                         new(142, 192, 124), new(131, 165, 152), new(211, 134, 155)],
        HeadingPrefixes = ["██ ", "▓▓ ", "▒▒ ", "░░ ", "  ", "  "],
        H1Rule = '═', H2Rule = '━',
        CodeBorderFg = new(146, 131, 116), CodeBg = new(50, 48, 47), CodeFg = new(235, 219, 178),
        TableBorderFg = new(146, 131, 116), TableHeaderBg = new(60, 56, 54),
        TableHeaderFg = new(235, 219, 178), TableCellBg = new(56, 50, 38),
        TableCellFg = new(213, 196, 161),
        DetailsSummBg = new(50, 48, 47), DetailsSummHoverBg = new(60, 56, 54),
        DetailsSummFocusBg = new(80, 73, 69), DetailsSummFg = new(235, 219, 178),
        DetailsArrowFg = new(254, 128, 25), DetailsBorderFg = new(146, 131, 116),
        DetailsContentBg = new(68, 58, 42),
        BlockquoteGutterFg = new(250, 189, 47), BlockquoteGutter = "║ ",
        BulletFg = new(184, 187, 38),
        HrFg = new(146, 131, 116), HrChar = '━',
        HintFg = new(146, 131, 116),
        Borders = BorderChars.Double,
    };

    // ── 6. Solarized Dark ───────────────────────────────────────────────────
    public static readonly Theme SolarizedDark = new()
    {
        Name = "Solarized Dark",
        DocBg = new(0, 43, 54), DocFg = new(131, 148, 150),
        HeadingColors = [new(203, 75, 22), new(38, 139, 210), new(42, 161, 152),
                         new(133, 153, 0), new(108, 113, 196), new(211, 54, 130)],
        HeadingPrefixes = ["█ ", "▌ ", "▎ ", "  ", "  ", "  "],
        CodeBorderFg = new(88, 110, 117), CodeBg = new(7, 54, 66), CodeFg = new(131, 148, 150),
        TableBorderFg = new(88, 110, 117), TableHeaderBg = new(7, 54, 66),
        TableHeaderFg = new(147, 161, 161), TableCellBg = new(0, 52, 64),
        TableCellFg = new(131, 148, 150),
        TableRowDividers = false,
        DetailsSummBg = new(7, 54, 66), DetailsSummHoverBg = new(20, 65, 75),
        DetailsSummFocusBg = new(35, 80, 90), DetailsSummFg = new(147, 161, 161),
        DetailsArrowFg = new(203, 75, 22), DetailsBorderFg = new(88, 110, 117),
        DetailsContentBg = new(0, 60, 74),
        BlockquoteGutterFg = new(38, 139, 210),
        BulletFg = new(42, 161, 152),
        HrFg = new(88, 110, 117),
        HintFg = new(88, 110, 117),
        Borders = BorderChars.Single,
    };

    // ── 7. Catppuccin Mocha ─────────────────────────────────────────────────
    public static readonly Theme CatppuccinMocha = new()
    {
        Name = "Catppuccin Mocha",
        DocBg = new(30, 30, 46), DocFg = new(205, 214, 244),
        HeadingColors = [new(245, 224, 220), new(203, 166, 247), new(250, 179, 135),
                         new(166, 227, 161), new(137, 220, 235), new(180, 190, 254)],
        HeadingPrefixes = ["█ ", "▌ ", "▎ ", "  ", "  ", "  "],
        CodeBorderFg = new(88, 91, 112), CodeBg = new(24, 24, 37), CodeFg = new(205, 214, 244),
        TableBorderFg = new(88, 91, 112), TableHeaderBg = new(49, 50, 68),
        TableHeaderFg = new(205, 214, 244), TableCellBg = new(36, 36, 56),
        TableCellFg = new(186, 194, 222),
        DetailsSummBg = new(36, 36, 54), DetailsSummHoverBg = new(49, 50, 68),
        DetailsSummFocusBg = new(69, 71, 90), DetailsSummFg = new(205, 214, 244),
        DetailsArrowFg = new(203, 166, 247), DetailsBorderFg = new(88, 91, 112),
        DetailsContentBg = new(42, 42, 66),
        BlockquoteGutterFg = new(245, 224, 220),
        BulletFg = new(250, 179, 135),
        HrFg = new(88, 91, 112),
        HintFg = new(88, 91, 112),
        Borders = BorderChars.Rounded,
    };

    // ── 8. Tokyo Night ──────────────────────────────────────────────────────
    public static readonly Theme TokyoNight = new()
    {
        Name = "Tokyo Night",
        DocBg = new(26, 27, 38), DocFg = new(192, 202, 245),
        HeadingColors = [new(255, 158, 100), new(122, 162, 247), new(158, 206, 106),
                         new(187, 154, 247), new(125, 207, 255), new(247, 118, 142)],
        HeadingPrefixes = ["█ ", "▌ ", "▎ ", "  ", "  ", "  "],
        CodeBorderFg = new(65, 72, 104), CodeBg = new(30, 31, 46), CodeFg = new(192, 202, 245),
        TableBorderFg = new(65, 72, 104), TableHeaderBg = new(41, 46, 66),
        TableHeaderFg = new(192, 202, 245), TableCellBg = new(32, 36, 52),
        TableCellFg = new(169, 177, 214),
        DetailsSummBg = new(33, 35, 50), DetailsSummHoverBg = new(41, 46, 66),
        DetailsSummFocusBg = new(55, 60, 85), DetailsSummFg = new(192, 202, 245),
        DetailsArrowFg = new(255, 158, 100), DetailsBorderFg = new(65, 72, 104),
        DetailsContentBg = new(38, 42, 62),
        BlockquoteGutterFg = new(122, 162, 247),
        BulletFg = new(158, 206, 106),
        HrFg = new(65, 72, 104),
        HintFg = new(65, 72, 104),
        Borders = BorderChars.Single,
    };

    // ── 9. Retro Terminal ───────────────────────────────────────────────────
    public static readonly Theme RetroTerminal = new()
    {
        Name = "Retro Terminal",
        DocBg = new(0, 0, 0), DocFg = new(0, 255, 65),
        HeadingColors = [new(0, 255, 0), new(0, 220, 0), new(0, 190, 0),
                         new(0, 160, 0), new(0, 130, 0), new(0, 100, 0)],
        HeadingPrefixes = ["# ", "## ", "### ", "#### ", "##### ", "###### "],
        H1Rule = '=', H2Rule = '-',
        CodeBorderFg = new(0, 180, 0), CodeBg = new(0, 15, 0), CodeFg = new(0, 255, 65),
        TableBorderFg = new(0, 180, 0), TableHeaderBg = new(0, 30, 0),
        TableHeaderFg = new(0, 255, 0), TableCellBg = new(0, 16, 0),
        TableCellFg = new(0, 220, 0),
        DetailsSummBg = new(0, 20, 0), DetailsSummHoverBg = new(0, 40, 0),
        DetailsSummFocusBg = new(0, 60, 0), DetailsSummFg = new(0, 255, 0),
        DetailsArrowFg = new(0, 255, 0), DetailsBorderFg = new(0, 140, 0),
        DetailsContentBg = new(0, 30, 0),
        OpenArrow = "v ", ClosedArrow = "> ",
        BlockquoteGutterFg = new(0, 180, 0), BlockquoteGutter = "| ",
        BulletFg = new(0, 200, 0), Bullets = ["> ", "- ", ". "],
        HrFg = new(0, 160, 0), HrChar = '=',
        HintFg = new(0, 120, 0),
        Borders = BorderChars.Ascii,
    };

    // ── 10. Cyberpunk Neon ──────────────────────────────────────────────────
    public static readonly Theme CyberpunkNeon = new()
    {
        Name = "Cyberpunk Neon",
        DocBg = new(18, 2, 40), DocFg = new(230, 230, 255),
        HeadingColors = [new(255, 0, 110), new(0, 255, 255), new(255, 255, 0),
                         new(0, 255, 128), new(255, 0, 255), new(255, 165, 0)],
        HeadingPrefixes = ["█ ", "▌ ", "▎ ", "  ", "  ", "  "],
        H1Rule = '━', H2Rule = '─',
        CodeBorderFg = new(128, 0, 255), CodeBg = new(15, 0, 35), CodeFg = new(230, 230, 255),
        TableBorderFg = new(128, 0, 255), TableHeaderBg = new(30, 0, 60),
        TableHeaderFg = new(0, 255, 255), TableCellBg = new(24, 4, 52),
        TableCellFg = new(230, 230, 255),
        DetailsSummBg = new(20, 0, 40), DetailsSummHoverBg = new(40, 0, 70),
        DetailsSummFocusBg = new(60, 0, 100), DetailsSummFg = new(0, 255, 255),
        DetailsArrowFg = new(255, 0, 110), DetailsBorderFg = new(128, 0, 255),
        DetailsContentBg = new(35, 8, 70),
        BlockquoteGutterFg = new(255, 0, 110),
        BulletFg = new(0, 255, 255), Bullets = ["◈ ", "◇ ", "◦ "],
        HrFg = new(128, 0, 255), HrChar = '━',
        HintFg = new(100, 0, 200),
        Borders = BorderChars.Heavy,
    };

    // ── 11. Amber CRT ───────────────────────────────────────────────────────
    public static readonly Theme AmberCrt = new()
    {
        Name = "Amber CRT",
        DocBg = new(15, 10, 0), DocFg = new(255, 176, 0),
        HeadingColors = [new(255, 200, 50), new(255, 176, 0), new(220, 150, 0),
                         new(180, 120, 0), new(140, 95, 0), new(100, 70, 0)],
        HeadingPrefixes = ["# ", "## ", "### ", "#### ", "##### ", "###### "],
        H1Rule = '=', H2Rule = '-',
        CodeBorderFg = new(180, 120, 0), CodeBg = new(20, 14, 0), CodeFg = new(255, 176, 0),
        TableBorderFg = new(180, 120, 0), TableHeaderBg = new(35, 25, 0),
        TableHeaderFg = new(255, 200, 50), TableCellBg = new(24, 16, 0),
        TableCellFg = new(255, 176, 0),
        DetailsSummBg = new(25, 18, 0), DetailsSummHoverBg = new(40, 28, 0),
        DetailsSummFocusBg = new(60, 42, 0), DetailsSummFg = new(255, 200, 50),
        DetailsArrowFg = new(255, 200, 50), DetailsBorderFg = new(140, 95, 0),
        DetailsContentBg = new(40, 28, 0),
        OpenArrow = "v ", ClosedArrow = "> ",
        BlockquoteGutterFg = new(200, 140, 0), BlockquoteGutter = "| ",
        BulletFg = new(220, 150, 0), Bullets = ["> ", "- ", ". "],
        HrFg = new(140, 95, 0), HrChar = '=',
        HintFg = new(120, 80, 0),
        Borders = BorderChars.Ascii,
    };

    // ── 12. Paper Light ─────────────────────────────────────────────────────
    public static readonly Theme PaperLight = new()
    {
        Name = "Paper Light",
        DocBg = new(250, 248, 240), DocFg = new(50, 50, 50),
        HeadingColors = [new(0, 90, 156), new(0, 128, 128), new(46, 125, 50),
                         new(230, 81, 0), new(106, 27, 154), new(96, 96, 96)],
        HeadingPrefixes = ["█ ", "▌ ", "▎ ", "  ", "  ", "  "],
        CodeBorderFg = new(180, 175, 165), CodeBg = new(240, 237, 228), CodeFg = new(50, 50, 50),
        TableBorderFg = new(180, 175, 165), TableHeaderBg = new(230, 226, 216),
        TableHeaderFg = new(30, 30, 30), TableCellBg = new(238, 234, 222),
        TableCellFg = new(50, 50, 50),
        DetailsSummBg = new(235, 232, 222), DetailsSummHoverBg = new(220, 216, 206),
        DetailsSummFocusBg = new(200, 196, 186), DetailsSummFg = new(30, 30, 30),
        DetailsArrowFg = new(0, 90, 156), DetailsBorderFg = new(180, 175, 165),
        DetailsContentBg = new(228, 224, 210),
        BlockquoteGutterFg = new(0, 128, 128),
        BulletFg = new(46, 125, 50), Bullets = ["● ", "○ ", "▪ "],
        HrFg = new(180, 175, 165),
        HintFg = new(160, 155, 145),
        Borders = BorderChars.Rounded,
    };

    // ── 13. Ocean Deep (filled) ───────────────────────────────────────────
    public static readonly Theme OceanDeep = new()
    {
        Name = "Ocean Deep",
        DocBg = new(8, 20, 38), DocFg = new(180, 210, 230),
        HeadingColors = [new(0, 200, 220), new(80, 160, 255), new(100, 230, 180),
                         new(200, 180, 255), new(140, 190, 210), new(100, 140, 160)],
        HeadingPrefixes = ["≋ ", "≈ ", "~ ", "  ", "  ", "  "],
        H1Rule = '~', H2Rule = '·',
        CodeBorderFg = new(50, 130, 160), CodeBg = new(5, 30, 50), CodeFg = new(180, 220, 240),
        TableBorderFg = new(40, 110, 140), TableHeaderBg = new(10, 45, 70),
        TableHeaderFg = new(200, 235, 255), TableCellBg = new(10, 32, 54),
        TableCellFg = new(170, 200, 220),
        DetailsSummBg = new(10, 35, 55), DetailsSummHoverBg = new(15, 50, 75),
        DetailsSummFocusBg = new(20, 65, 100), DetailsSummFg = new(200, 230, 250),
        DetailsArrowFg = new(0, 200, 220), DetailsBorderFg = new(40, 100, 130),
        DetailsContentBg = new(12, 36, 58),
        BlockquoteGutterFg = new(0, 180, 200), BlockquoteGutter = "▌ ",
        BulletFg = new(80, 200, 180), Bullets = ["◆ ", "◇ ", "· "],
        HrFg = new(40, 100, 130), HrChar = '~',
        HintFg = new(60, 120, 150),
        Borders = BorderChars.Rounded,
        FilledBorders = true,
    };

    // ── 14. Rose Pine (filled) ──────────────────────────────────────────────
    public static readonly Theme RosePine = new()
    {
        Name = "Rosé Pine",
        DocBg = new(25, 23, 36), DocFg = new(224, 222, 244),
        HeadingColors = [new(235, 188, 186), new(196, 167, 231), new(246, 193, 119),
                         new(156, 207, 216), new(49, 116, 143), new(144, 140, 170)],
        HeadingPrefixes = ["✿ ", "❀ ", "▸ ", "  ", "  ", "  "],
        CodeBorderFg = new(110, 106, 134), CodeBg = new(32, 29, 46), CodeFg = new(224, 222, 244),
        TableBorderFg = new(110, 106, 134), TableHeaderBg = new(38, 35, 53),
        TableHeaderFg = new(235, 188, 186), TableCellBg = new(32, 30, 46),
        TableCellFg = new(224, 222, 244),
        DetailsSummBg = new(30, 27, 42), DetailsSummHoverBg = new(42, 38, 58),
        DetailsSummFocusBg = new(55, 50, 75), DetailsSummFg = new(224, 222, 244),
        DetailsArrowFg = new(235, 188, 186), DetailsBorderFg = new(110, 106, 134),
        DetailsContentBg = new(38, 34, 56),
        BlockquoteGutterFg = new(196, 167, 231),
        BulletFg = new(235, 188, 186), Bullets = ["◈ ", "◇ ", "· "],
        HrFg = new(110, 106, 134),
        HintFg = new(110, 106, 134),
        Borders = BorderChars.Rounded,
        FilledBorders = true,
    };

    // ── 15. Everforest (filled) ─────────────────────────────────────────────
    public static readonly Theme Everforest = new()
    {
        Name = "Everforest",
        DocBg = new(30, 38, 30), DocFg = new(211, 198, 170),
        HeadingColors = [new(167, 192, 128), new(230, 196, 117), new(219, 188, 127),
                         new(131, 192, 179), new(214, 153, 134), new(160, 160, 140)],
        HeadingPrefixes = ["🌿", "▌ ", "▎ ", "  ", "  ", "  "],
        H1Rule = '━', H2Rule = '─',
        CodeBorderFg = new(90, 110, 80), CodeBg = new(35, 46, 35), CodeFg = new(211, 198, 170),
        TableBorderFg = new(85, 105, 75), TableHeaderBg = new(40, 52, 40),
        TableHeaderFg = new(230, 220, 190), TableCellBg = new(36, 46, 36),
        TableCellFg = new(200, 190, 165),
        DetailsSummBg = new(36, 46, 36), DetailsSummHoverBg = new(45, 58, 45),
        DetailsSummFocusBg = new(55, 72, 55), DetailsSummFg = new(220, 210, 185),
        DetailsArrowFg = new(167, 192, 128), DetailsBorderFg = new(85, 105, 75),
        DetailsContentBg = new(40, 55, 40),
        BlockquoteGutterFg = new(131, 192, 179), BlockquoteGutter = "▌ ",
        BulletFg = new(167, 192, 128), Bullets = ["● ", "○ ", "▪ "],
        HrFg = new(85, 105, 75), HrChar = '─',
        HintFg = new(85, 105, 75),
        Borders = BorderChars.Single,
        FilledBorders = true,
    };

    // ── 16. Kanagawa Wave (filled) ──────────────────────────────────────────
    public static readonly Theme KanagawaWave = new()
    {
        Name = "Kanagawa Wave",
        DocBg = new(22, 22, 40), DocFg = new(220, 215, 186),
        HeadingColors = [new(255, 160, 102), new(122, 169, 175), new(152, 187, 108),
                         new(210, 126, 153), new(180, 150, 210), new(130, 130, 150)],
        HeadingPrefixes = ["⛩ ", "▌ ", "▎ ", "  ", "  ", "  "],
        H1Rule = '═', H2Rule = '─',
        CodeBorderFg = new(84, 84, 109), CodeBg = new(26, 26, 50), CodeFg = new(220, 215, 186),
        TableBorderFg = new(84, 84, 109), TableHeaderBg = new(35, 35, 60),
        TableHeaderFg = new(240, 235, 210), TableCellBg = new(28, 28, 50),
        TableCellFg = new(210, 205, 175),
        DetailsSummBg = new(28, 28, 48), DetailsSummHoverBg = new(40, 40, 62),
        DetailsSummFocusBg = new(55, 55, 80), DetailsSummFg = new(230, 225, 200),
        DetailsArrowFg = new(255, 160, 102), DetailsBorderFg = new(84, 84, 109),
        DetailsContentBg = new(32, 32, 60),
        BlockquoteGutterFg = new(122, 169, 175),
        BulletFg = new(152, 187, 108), Bullets = ["◆ ", "◇ ", "· "],
        HrFg = new(84, 84, 109),
        HintFg = new(84, 84, 109),
        Borders = BorderChars.Heavy,
        FilledBorders = true,
    };

    // ── 17. Midnight Blue (filled) ──────────────────────────────────────────
    public static readonly Theme MidnightBlue = new()
    {
        Name = "Midnight Blue",
        DocBg = new(12, 14, 36), DocFg = new(200, 205, 230),
        HeadingColors = [new(100, 160, 255), new(200, 140, 255), new(110, 220, 200),
                         new(255, 200, 100), new(255, 130, 160), new(140, 150, 180)],
        HeadingPrefixes = ["★ ", "☆ ", "▸ ", "  ", "  ", "  "],
        H1Rule = '━', H2Rule = '─',
        CodeBorderFg = new(60, 70, 140), CodeBg = new(16, 18, 48), CodeFg = new(200, 210, 240),
        TableBorderFg = new(55, 65, 130), TableHeaderBg = new(22, 25, 60),
        TableHeaderFg = new(220, 225, 255), TableCellBg = new(18, 20, 48),
        TableCellFg = new(190, 195, 220),
        DetailsSummBg = new(18, 20, 50), DetailsSummHoverBg = new(28, 32, 72),
        DetailsSummFocusBg = new(38, 45, 95), DetailsSummFg = new(210, 215, 240),
        DetailsArrowFg = new(100, 160, 255), DetailsBorderFg = new(55, 65, 130),
        DetailsContentBg = new(22, 24, 58),
        BlockquoteGutterFg = new(100, 160, 255),
        BulletFg = new(200, 140, 255), Bullets = ["◈ ", "◇ ", "· "],
        HrFg = new(55, 65, 130), HrChar = '━',
        HintFg = new(55, 65, 130),
        Borders = BorderChars.Double,
        FilledBorders = true,
    };

    // ── All presets ─────────────────────────────────────────────────────────
    public static IReadOnlyList<Theme> All { get; } =
    [
        DarkModern, MonokaiPro, Dracula, NordFrost, GruvboxDark,
        SolarizedDark, CatppuccinMocha, TokyoNight, RetroTerminal,
        CyberpunkNeon, AmberCrt, PaperLight, OceanDeep, RosePine,
        Everforest, KanagawaWave, MidnightBlue
    ];
}
