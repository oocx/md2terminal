using Markdig;
using Attribute = Terminal.Gui.Attribute;
using Terminal.Gui;

namespace Md2Terminal;

/// <summary>
/// Terminal.Gui View that owns the IR node tree, handles scrolling,
/// mouse interaction (click/hover on details), and keyboard navigation.
/// </summary>
public sealed class MarkdownView : View
{
    private DocumentNode _document = new();
    private int _scrollY;
    private DetailsNode? _hoveredDetails;
    private List<DetailsNode> _allDetails = [];
    private int _focusedDetailsIndex = -1;
    private int _documentHeight;
    private MarkdownPipeline? _pipeline;
    private int _themeIndex;

    // ── Selection state ─────────────────────────────────────────────────────
    /// <summary>Currently active text selection, or null when nothing is selected.</summary>
    private TextSelection? _selection;
    /// <summary>True while the user is holding mouse button 1 to drag a selection.</summary>
    private bool _isDragging;
    /// <summary>The document-coordinate anchor captured on mouse-button-down.</summary>
    private DocPoint _selectionAnchor;    /// <summary>True when the drag that created the current selection was started with Alt held.</summary>
    private bool _isRectangularDrag;    /// <summary>Character buffer from the last render pass; used for copy extraction.</summary>
    private char[,]? _lastScreenBuf;

    public string CurrentThemeName => Theme.All[_themeIndex].Name;
    public event Action<string>? ThemeChanged;

    public MarkdownView()
    {
        CanFocus = true;
        WantMousePositionReports = true;
        SyncColorScheme();
    }

    private void SyncColorScheme()
    {
        var t = Theme.All[_themeIndex];
        var normal = new Attribute(
            RenderContext.RgbToColor(t.DocFg),
            RenderContext.RgbToColor(t.DocBg));
        ColorScheme = new ColorScheme(normal);
        // Also update parent/toplevel so no default gray leaks through
        if (SuperView is not null)
            SuperView.ColorScheme = new ColorScheme(normal);
    }

    public void LoadMarkdown(string markdown)
    {
        _pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .Build();

        var irBuilder = new MarkdownIrBuilder(_pipeline);
        _document = irBuilder.Build(markdown);

        RunLayout();
        SetNeedsDraw();
    }

    private void RunLayout()
    {
        var vp = GetContentSize();
        int width = vp.Width > 0 ? vp.Width : 120;
        LayoutEngine.Layout(_document, width, Theme.All[_themeIndex]);
        _documentHeight = _document.Height;
        _allDetails = LayoutEngine.CollectDetails(_document);

        // Clamp scroll
        int maxScroll = Math.Max(0, _documentHeight - (GetContentSize().Height > 0 ? GetContentSize().Height : 40));
        _scrollY = Math.Clamp(_scrollY, 0, maxScroll);
    }

    // ── Drawing ─────────────────────────────────────────────────────────────

    protected override bool OnDrawingContent()
    {
        var vp = GetContentSize();
        if (vp.Width <= 0 || vp.Height <= 0) return true;

        var theme = Theme.All[_themeIndex];

        // Allocate a fresh screen buffer when a selection is active so DrawStrAt can record
        // every drawn character for the overlay pass and text extraction.
        var screenBuf = (_selection is not null && !_selection.IsEmpty)
            ? new char[vp.Height, vp.Width]
            : null;

        var ctx = new RenderContext
        {
            View = this,
            Theme = theme,
            ScrollY = _scrollY,
            ViewportWidth = vp.Width,
            ViewportHeight = vp.Height,
            HoveredDetails = _hoveredDetails,
            FocusedDetails = _focusedDetailsIndex >= 0 && _focusedDetailsIndex < _allDetails.Count
                ? _allDetails[_focusedDetailsIndex] : null,
            Selection = _selection,
            ScreenBuf = screenBuf,
        };

        // Ensure ColorScheme is in sync (covers first draw before parent is set)
        if (ColorScheme?.Normal.Background != RenderContext.RgbToColor(theme.DocBg))
            SyncColorScheme();

        // Clear with theme background
        SetAttribute(new Attribute(
            RenderContext.RgbToColor(theme.DocFg),
            RenderContext.RgbToColor(theme.DocBg)));
        ClearViewport();

        _document.Render(ctx);

        // Apply selection highlight overlay on top of normal rendering
        if (_selection is not null && !_selection.IsEmpty && screenBuf is not null)
        {
            _lastScreenBuf = screenBuf;
            ApplySelectionOverlay(_selection, screenBuf, vp.Width, vp.Height, theme);
        }

        // Theme indicator at top-right
        string indicator = $" \u25c4 {theme.Name} \u25ba ";
        int ix = Math.Max(0, vp.Width - indicator.Length);
        if (ix > 0)
        {
            Move(ix, 0);
            SetAttribute(new Attribute(
                RenderContext.RgbToColor(theme.DetailsSummFg),
                RenderContext.RgbToColor(theme.DetailsSummFocusBg)));
            AddStr(indicator);
        }

        return true;
    }

    // ── Scrolling ───────────────────────────────────────────────────────────

    private void Scroll(int delta)
    {
        var vp = GetContentSize();
        int maxScroll = Math.Max(0, _documentHeight - vp.Height);
        int newScroll = Math.Clamp(_scrollY + delta, 0, maxScroll);
        if (newScroll != _scrollY)
        {
            _scrollY = newScroll;
            SetNeedsDraw();
        }
    }

    // ── Mouse ───────────────────────────────────────────────────────────────

    protected override bool OnMouseEvent(MouseEventArgs e)
    {
        if (e.Flags.HasFlag(MouseFlags.WheeledDown))  { Scroll(+3);  return true; }
        if (e.Flags.HasFlag(MouseFlags.WheeledUp))    { Scroll(-3);  return true; }

        int docY = e.Position.Y + _scrollY;
        int docX = e.Position.X;

        // Begin drag-selection when button goes down (fires before Clicked/Released)
        if (e.Flags.HasFlag(MouseFlags.Button1Pressed) && !_isDragging)
        {
            _isDragging = true;
            _selectionAnchor = new DocPoint(docY, docX);
            // Alt held at drag-start activates rectangular (block) selection mode
            _isRectangularDrag = e.Flags.HasFlag(MouseFlags.ButtonAlt);
            // Clear any previous selection immediately so the screen refreshes cleanly
            _selection = null;
            SetNeedsDraw();
        }

        // Button released → end drag (Button1Clicked may follow for simple clicks)
        if (e.Flags.HasFlag(MouseFlags.Button1Released))
        {
            _isDragging = false;
            // A trivial zero-length selection is treated as a plain click
            if (_selection is not null && _selection.IsEmpty)
                _selection = null;
        }

        // Right-click: copy and clear immediately on button-down, before the
        // ReportMousePosition check which would swallow the event otherwise.
        // Button3Pressed fires on the first event carrying button-3 info, so it
        // is both more reliable and more responsive than Button3Clicked.
        if (e.Flags.HasFlag(MouseFlags.Button3Pressed))
        {
            CopyAndClearSelection();
            return true;
        }

        // Hit-test details headers
        var hit = HitTestDetails(docY);

        if (e.Flags.HasFlag(MouseFlags.ReportMousePosition))
        {
            if (_isDragging)
            {
                // Extend the selection as the user drags
                _selection ??= new TextSelection { Anchor = _selectionAnchor, Active = _selectionAnchor, IsRectangular = _isRectangularDrag };
                _selection.Active = new DocPoint(docY, docX);
                SetNeedsDraw();
                return true;
            }

            // Normal hover highlight for details blocks
            if (_hoveredDetails != hit)
            {
                _hoveredDetails = hit;
                SetNeedsDraw();
            }
            return true;
        }

        if (e.Flags.HasFlag(MouseFlags.Button1Clicked))
        {
            // If a real drag produced a selection, don't toggle details
            if (_selection is not null && !_selection.IsEmpty) return true;
            _selection = null;
            if (hit is not null) { ToggleDetails(hit); return true; }
            return true;
        }

        return base.OnMouseEvent(e);
    }

    private DetailsNode? HitTestDetails(int docY)
    {
        return _allDetails.FirstOrDefault(d => d.OffsetY == docY);
    }

    private void ToggleDetails(DetailsNode node)
    {
        node.IsOpen = !node.IsOpen;
        RunLayout();
        SetNeedsDraw();
    }

    /// <summary>Collapses every details section in the document.</summary>
    private void CollapseAll()
    {
        foreach (var d in LayoutEngine.CollectAllDetails(_document))
            d.IsOpen = false;
        RunLayout();
        SetNeedsDraw();
    }

    /// <summary>Expands every details section in the document.</summary>
    private void ExpandAll()
    {
        foreach (var d in LayoutEngine.CollectAllDetails(_document))
            d.IsOpen = true;
        RunLayout();
        SetNeedsDraw();
    }

    // ── Keyboard ────────────────────────────────────────────────────────────

    protected override bool OnKeyDown(Key key)
    {
        if (key == Key.CursorDown || key == Key.J)   { Scroll(+1);  return true; }
        if (key == Key.CursorUp || key == Key.K)     { Scroll(-1);  return true; }
        if (key == Key.PageDown || key == Key.Space)  { Scroll(+GetContentSize().Height); return true; }
        if (key == Key.PageUp)                        { Scroll(-GetContentSize().Height); return true; }
        if (key == Key.Home)                          { _scrollY = 0; SetNeedsDraw(); return true; }
        if (key == Key.End)
        {
            _scrollY = Math.Max(0, _documentHeight - GetContentSize().Height);
            SetNeedsDraw();
            return true;
        }

        if (key == Key.Tab)       { CycleFocus(+1); return true; }
        if (key == Key.Tab.WithShift) { CycleFocus(-1); return true; }

        if (key == Key.Enter)
        {
            if (_focusedDetailsIndex >= 0 && _focusedDetailsIndex < _allDetails.Count)
            {
                ToggleDetails(_allDetails[_focusedDetailsIndex]);
            }
            return true;
        }

        if (key == Key.CursorRight) { CycleTheme(+1); return true; }
        if (key == Key.CursorLeft)  { CycleTheme(-1); return true; }

        // Minus collapses all sections; equals (unshifted +) expands all sections.
        if (key == new Key((KeyCode)'-')) { CollapseAll(); return true; }
        if (key == new Key((KeyCode)'=')) { ExpandAll();   return true; }

        if (key == Key.Q || key == Key.Esc)
        {
            Application.RequestStop();
            return true;
        }

        // Ctrl+C copies the current selection and clears it
        if (key == Key.C.WithCtrl)
        {
            CopyAndClearSelection();
            return true;
        }

        return base.OnKeyDown(key);
    }

    private void CycleTheme(int dir)
    {
        int count = Theme.All.Count;
        _themeIndex = ((_themeIndex + dir) % count + count) % count;
        SyncColorScheme();
        RunLayout(); // re-layout since table heights may differ
        ThemeChanged?.Invoke(CurrentThemeName);
        SetNeedsDraw();
    }

    private void CycleFocus(int dir)
    {
        if (_allDetails.Count == 0) return;

        if (_focusedDetailsIndex < 0)
            _focusedDetailsIndex = dir > 0 ? 0 : _allDetails.Count - 1;
        else
            _focusedDetailsIndex = ((_focusedDetailsIndex + dir) % _allDetails.Count + _allDetails.Count) % _allDetails.Count;

        // Scroll so the focused item sits in the upper third of the viewport
        var node = _allDetails[_focusedDetailsIndex];
        int vpHeight = GetContentSize().Height;
        int target = node.OffsetY - vpHeight / 6; // place ~1/6 down from top → upper third
        int maxScroll = Math.Max(0, _documentHeight - vpHeight);
        _scrollY = Math.Clamp(target, 0, maxScroll);

        SetNeedsDraw();
    }

    // ── Selection ───────────────────────────────────────────────────────────

    /// <summary>
    /// Redraws cells that fall within the selection using inverted colors, so the
    /// selection is visually distinct. Characters come from <paramref name="buf"/> which
    /// was populated during the main render pass by <see cref="RenderContext.DrawStrAt"/>.
    /// </summary>
    private void ApplySelectionOverlay(
        TextSelection sel, char[,] buf,
        int viewportWidth, int viewportHeight,
        Theme theme)
    {
        // Inverted colors give a classic "selected text" look
        var selAttr = new Terminal.Gui.Attribute(
            RenderContext.RgbToColor(theme.DocBg),
            RenderContext.RgbToColor(theme.DocFg));

        for (int docY = sel.Start.Y; docY <= sel.End.Y; docY++)
        {
            int screenY = docY - _scrollY;
            if (screenY < 0 || screenY >= viewportHeight) continue;

            var (startCol, endCol) = sel.GetColRange(docY, viewportWidth);
            startCol = Math.Clamp(startCol, 0, viewportWidth);
            endCol   = Math.Clamp(endCol,   0, viewportWidth);
            if (startCol >= endCol) continue;

            SetAttribute(selAttr);
            for (int col = startCol; col < endCol; col++)
            {
                char c = buf[screenY, col];
                // '\0' is a wide-char trailing marker — render as space to avoid artifacts
                Move(col, screenY);
                AddStr(c == '\0' ? " " : c.ToString());
            }
        }
    }

    /// <summary>
    /// Copies the selected text to the system clipboard, then clears the selection.
    /// Does nothing when there is no active selection.
    /// </summary>
    private void CopyAndClearSelection()
    {
        if (_selection is null || _selection.IsEmpty || _lastScreenBuf is null) return;

        var vp = GetContentSize();
        string text = _selection.ExtractText(_lastScreenBuf, _scrollY, vp.Width);

        // Clear the selection and redraw first so the highlight disappears immediately,
        // before the clipboard write (which on Linux shells out to xclip/xsel/wl-copy
        // and can block for tens of milliseconds). Running it on a background thread
        // keeps the UI fully responsive.
        _selection = null;
        _lastScreenBuf = null;
        SetNeedsDraw();

        if (!string.IsNullOrEmpty(text))
            Task.Run(() => Clipboard.TrySetClipboardData(text));
    }

    // ── Resize ──────────────────────────────────────────────────────────────

    protected override void OnViewportChanged(DrawEventArgs e)
    {
        base.OnViewportChanged(e);
        RunLayout();
        SetNeedsDraw();
    }
}
