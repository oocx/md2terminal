using Terminal.Gui;
using Md2Terminal;

// ── Entry point ─────────────────────────────────────────────────────────────

if (args.Length == 0)
{
    Console.Error.WriteLine("Usage: md2terminal <file.md>");
    Console.Error.WriteLine("       md2terminal -        (read from stdin)");
    return 1;
}

string markdown;
if (args[0] == "-")
{
    markdown = Console.In.ReadToEnd();
}
else
{
    var filePath = Path.GetFullPath(args[0]);
    if (!File.Exists(filePath))
    {
        Console.Error.WriteLine($"File not found: {filePath}");
        return 1;
    }
    markdown = File.ReadAllText(filePath);
}

// ── Initialize Terminal.Gui ─────────────────────────────────────────────────

Application.Init();

try
{
    var top = new Toplevel();

    var mdView = new MarkdownView
    {
        X = 0,
        Y = 0,
        Width = Dim.Fill(),
        Height = Dim.Fill(1), // leave room for status bar
    };

    // Set initial Toplevel color to match theme
    var initTheme = Theme.All[0];
    var initAttr = new Terminal.Gui.Attribute(
        RenderContext.RgbToColor(initTheme.DocFg),
        RenderContext.RgbToColor(initTheme.DocBg));
    top.ColorScheme = new ColorScheme(initAttr);

    mdView.LoadMarkdown(markdown);

    // Update Toplevel ColorScheme when theme changes
    mdView.ThemeChanged += name =>
    {
        var t = Theme.All.First(th => th.Name == name);
        var attr = new Terminal.Gui.Attribute(
            RenderContext.RgbToColor(t.DocFg),
            RenderContext.RgbToColor(t.DocBg));
        top.ColorScheme = new ColorScheme(attr);
    };

    var statusBar = new StatusBar(
    [
        new Shortcut(Key.Q, "Quit", () => Application.RequestStop()),
        new Shortcut(Key.Tab, "Next Section", null),
        new Shortcut(Key.Enter, "Toggle", null),
        new Shortcut(new Key((KeyCode)'-'), "Collapse All", null),
        new Shortcut(new Key((KeyCode)'='), "Expand All", null),
        new Shortcut(Key.CursorRight, "\u25c4\u25ba Theme", null),
        new Shortcut(Key.C.WithCtrl, "Copy", null),
    ]);

    top.Add(mdView);
    top.Add(statusBar);

    mdView.SetFocus();

    Application.Run(top);
}
finally
{
    Application.Shutdown();
}

return 0;
