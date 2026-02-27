using System.Text.RegularExpressions;

namespace Md2Terminal;

/// <summary>
/// Per-language regex-based syntax highlighter.
/// Produces pre-computed colored spans for code blocks at load time.
/// </summary>
public static class SyntaxHighlighter
{
    public static List<List<StyledSpan>> Highlight(string[] lines, string lang) =>
        lang.ToLowerInvariant() switch
        {
            "csharp" or "cs" or "c#" => Tokenize(lines, CSharpRules),
            "json" or "jsonc"        => Tokenize(lines, JsonRules),
            "xml" or "html" or "htm" => Tokenize(lines, XmlRules),
            "bash" or "sh" or "shell" or "zsh" => Tokenize(lines, BashRules),
            "sql"                    => Tokenize(lines, SqlRules),
            "yaml" or "yml"          => Tokenize(lines, YamlRules),
            "hcl" or "terraform" or "tf" => Tokenize(lines, HclRules),
            _ => lines.Select(l =>
                new List<StyledSpan> { new(l, Fg: new RgbColor(220, 220, 220)) }).ToList()
        };

    // ── Rule definitions ────────────────────────────────────────────────────

    private static readonly TokenRule[] CSharpRules =
    [
        new(@"//[^\n]*",                               new(100, 160, 100)),  // comment
        new(@"""(?:\\.|[^""])*""",                      new(206, 145, 120)),  // string
        new(@"@""(?:""""|[^""])*""",                    new(206, 145, 120)),  // verbatim string
        new(@"\b(var|if|else|for|foreach|while|return|new|class|public|private|protected|static|void|async|await|using|namespace|record|sealed|override|abstract|interface|enum|where|true|false|null|this|base|in|out|ref|readonly|const|get|set|init|required|yield|switch|case|break|continue|try|catch|finally|throw|is|as|typeof)\b",
                                                       new(86, 156, 214)),   // keywords
        new(@"\b[A-Z][A-Za-z0-9]+\b",                  new(78, 201, 176)),   // types
        new(@"\b\d+[fdmulL]?\b",                       new(181, 206, 168)),  // numbers
        new(@"[(){}[\];,.]",                           new(150, 150, 150)),  // punctuation
    ];

    private static readonly TokenRule[] JsonRules =
    [
        new(@"""(?:\\.|[^""])*""\s*:",                  new(156, 220, 254)),  // key
        new(@"""(?:\\.|[^""])*""",                      new(206, 145, 120)),  // string value
        new(@"\b(true|false|null)\b",                  new(86, 156, 214)),   // keywords
        new(@"\b-?\d+(?:\.\d+)?(?:[eE][+-]?\d+)?\b",  new(181, 206, 168)),  // numbers
        new(@"[{}\[\]:,]",                             new(150, 150, 150)),  // punctuation
    ];

    private static readonly TokenRule[] XmlRules =
    [
        new(@"<!--[\s\S]*?-->",                        new(100, 160, 100)),  // comment
        new(@"</?[a-zA-Z][a-zA-Z0-9_:-]*",            new(86, 156, 214)),   // tag name
        new(@"/?>",                                    new(86, 156, 214)),   // tag close
        new(@"\b[a-zA-Z_][a-zA-Z0-9_-]*(?=\s*=)",     new(156, 220, 254)),  // attribute name
        new(@"""[^""]*""",                             new(206, 145, 120)),  // attribute value
        new(@"'[^']*'",                                new(206, 145, 120)),  // attribute value
    ];

    private static readonly TokenRule[] BashRules =
    [
        new(@"#[^\n]*",                                new(100, 160, 100)),  // comment
        new(@"""(?:\\.|[^""])*""",                      new(206, 145, 120)),  // double-quoted string
        new(@"'[^']*'",                                new(206, 145, 120)),  // single-quoted string
        new(@"\$\{?[A-Za-z_][A-Za-z0-9_]*\}?",        new(156, 220, 254)),  // variables
        new(@"\b(if|then|else|elif|fi|for|do|done|while|until|case|esac|function|in|return|exit|local|export|source)\b",
                                                       new(86, 156, 214)),   // keywords
        new(@"\b\d+\b",                                new(181, 206, 168)),  // numbers
    ];

    private static readonly TokenRule[] SqlRules =
    [
        new(@"--[^\n]*",                               new(100, 160, 100)),  // comment
        new(@"'(?:''|[^'])*'",                         new(206, 145, 120)),  // string
        new(@"\b(SELECT|FROM|WHERE|INSERT|UPDATE|DELETE|CREATE|ALTER|DROP|TABLE|INDEX|VIEW|JOIN|LEFT|RIGHT|INNER|OUTER|ON|AND|OR|NOT|IN|IS|NULL|AS|SET|INTO|VALUES|ORDER|BY|GROUP|HAVING|LIMIT|OFFSET|UNION|EXISTS|BETWEEN|LIKE|DISTINCT|COUNT|SUM|AVG|MIN|MAX|CASE|WHEN|THEN|ELSE|END)\b",
                                                       new(86, 156, 214)),   // keywords (case-insensitive in regex)
        new(@"\b\d+(?:\.\d+)?\b",                     new(181, 206, 168)),  // numbers
    ];

    private static readonly TokenRule[] YamlRules =
    [
        new(@"#[^\n]*",                                new(100, 160, 100)),  // comment
        new(@"^[\s]*[a-zA-Z_][a-zA-Z0-9_.-]*(?=\s*:)", new(156, 220, 254)), // key
        new(@"""(?:\\.|[^""])*""",                      new(206, 145, 120)),  // double-quoted string
        new(@"'[^']*'",                                new(206, 145, 120)),  // single-quoted string
        new(@"\b(true|false|null|yes|no)\b",           new(86, 156, 214)),   // keywords
        new(@"\b\d+(?:\.\d+)?\b",                     new(181, 206, 168)),  // numbers
    ];

    private static readonly TokenRule[] HclRules =
    [
        new(@"#[^\n]*",                                new(100, 160, 100)),  // comment
        new(@"//[^\n]*",                               new(100, 160, 100)),  // comment
        new(@"/\*[\s\S]*?\*/",                         new(100, 160, 100)),  // block comment
        new(@"""(?:\\.|[^""])*""",                      new(206, 145, 120)),  // string
        new(@"\b(resource|data|variable|output|locals|module|provider|terraform|for_each|count|depends_on|lifecycle|provisioner|connection|dynamic|for|in|if)\b",
                                                       new(86, 156, 214)),   // keywords
        new(@"\b(true|false|null)\b",                  new(86, 156, 214)),   // booleans
        new(@"\b\d+(?:\.\d+)?\b",                     new(181, 206, 168)),  // numbers
        new(@"\b[a-zA-Z_][a-zA-Z0-9_]*(?=\s*\{)",     new(78, 201, 176)),   // block type
        new(@"[{}[\]()=,.]",                           new(150, 150, 150)),  // punctuation
    ];

    // ── Tokenizer core ──────────────────────────────────────────────────────

    private static List<List<StyledSpan>> Tokenize(string[] lines, TokenRule[] rules)
    {
        return lines.Select(line => TokenizeLine(line, rules)).ToList();
    }

    private static List<StyledSpan> TokenizeLine(string line, TokenRule[] rules)
    {
        if (string.IsNullOrEmpty(line))
            return [new StyledSpan("", Fg: new RgbColor(220, 220, 220))];

        // Build a color map: index → color
        var colors = new RgbColor?[line.Length];

        foreach (var rule in rules)
        {
            foreach (Match m in rule.Pattern.Matches(line))
            {
                for (int i = m.Index; i < m.Index + m.Length && i < colors.Length; i++)
                    colors[i] = rule.Color;
            }
        }

        // RLE compress into spans
        var defaultColor = new RgbColor(220, 220, 220);
        var spans = new List<StyledSpan>();
        int start = 0;
        var startColor = colors[0] ?? defaultColor;

        for (int i = 1; i <= line.Length; i++)
        {
            var currentColor = i < line.Length ? (colors[i] ?? defaultColor) : defaultColor;
            if (i == line.Length || currentColor != startColor)
            {
                spans.Add(new StyledSpan(line[start..i], Fg: startColor));
                start = i;
                if (i < line.Length)
                    startColor = currentColor;
            }
        }

        return spans;
    }
}

internal sealed record TokenRule
{
    public Regex Pattern { get; }
    public RgbColor Color { get; }

    public TokenRule(string pattern, RgbColor color)
    {
        Pattern = new Regex(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
        Color = color;
    }
}
