# Architecture

## Overview

md2terminal is a .NET 10 full-screen TUI markdown viewer built on Terminal.Gui v2. It renders Markdown files in the terminal with syntax highlighting, themeable colors, collapsible `<details>` sections, and proper Unicode/emoji display.

## Pipeline

```
Markdown text
    │
    ▼
Markdig AST          (via Markdig 0.38)
    │
    ▼
Intermediate Representation (IR)   [MarkdownIrBuilder]
    │
    ▼
Layout pass          [LayoutEngine]
    │  computes OffsetY, word-wrap, table column widths
    ▼
Render pass          [Renderers + RenderContext]
    │  draws blocks to the Terminal.Gui View on demand
    ▼
Terminal.Gui View    [MarkdownView]
```

## Key Modules

| File | Responsibility |
|---|---|
| `Core.cs` | Shared value types: `RgbColor`, `StyledSpan`, `StyledLine`, `StyleContext`, `UnicodeWidth` |
| `RenderNodes.cs` | IR node hierarchy (`ParagraphNode`, `HeadingNode`, `CodeBlockNode`, `TableNode`, `DetailsNode`, …) |
| `MarkdownIrBuilder.cs` | Walks Markdig AST + AngleSharp DOM → IR node tree |
| `InlineContentBuilder.cs` | Markdig inline nodes + AngleSharp spans → `StyledLine` lists |
| `HtmlBlockProcessor.cs` | Parses raw HTML blocks (`<details>`, `<pre><code>`, `<span style="">`) |
| `CssParser.cs` | Extracts `color:` and `background-color:` from inline CSS |
| `SyntaxHighlighter.cs` | Converts plain code lines to syntax-highlighted `StyledLine` lists |
| `LayoutEngine.cs` | Single-pass layout: assigns `OffsetY` to each node, word-wraps paragraphs, computes table column widths |
| `RenderContext.cs` | Drawing helpers: `DrawText`, `DrawStyledLine`, `DrawStyledSpans`; owns background-override stack (`PushBgOverride`/`PopBgOverride`); populates `ScreenBuf` for selection capture |
| `Renderers.cs` | One static renderer per IR node type; selected by `MarkdownView` during `OnDrawContent` |
| `Theme.cs` | 17 named color themes as immutable `record` values |
| `TextSelection.cs` | `DocPoint` value type and `TextSelection` class: selection state, column-range query, and text extraction from `ScreenBuf` |
| `MarkdownView.cs` | Terminal.Gui `View` subclass; handles scrolling, keyboard, mouse (hover, click, drag-select), theme cycling, focus/Tab navigation, selection copy |
| `Program.cs` | Entry point; argument parsing, `Application.Init()`, status bar setup |

## Architecture Decisions

### ADR-001 — Intermediate Representation layer
**Date:** early development  
**Decision:** Parse Markdown into an IR node tree rather than rendering directly from the Markdig AST.  
**Rationale:** Decouples layout (which needs to know full document structure for table widths, word-wrap, offset computation) from rendering (which fires per visible region). Also makes it straightforward to carry theme-independent style information through the pipeline.

### ADR-002 — Full re-render on scroll (no retained draw buffer)
**Date:** early development  
**Decision:** `OnDrawContent` re-renders only the currently visible nodes by mapping document Y to screen Y via `TryDocToScreen`.  
**Rationale:** Terminal.Gui v2 invalidates the whole view on scroll anyway; a retained buffer would require synchronization and was deemed YAGNI. The viewport render is fast because only visible nodes are touched.

### ADR-003 — Background override stack
**Date:** table rendering iteration  
**Decision:** `RenderContext` has a `PushBgOverride`/`PopBgOverride` stack instead of passing background color through every method.  
**Rationale:** Table cells and details blocks need a different background from the document. A stack lets renderers locally override the background without changing all call sites.

### ADR-004 — Border attribute computed before PushBgOverride
**Date:** table border background bleed fix  
**Decision:** Table border `Attribute` is computed **before** calling `PushBgOverride(t.TableCellBg)`.  
**Rationale:** Border characters sit outside the cell content area and must use the outer (document) background. Computing the attribute after the push caused VS16 cell background to show on border characters.

### ADR-005 — VS16 emoji width: two-layer fix
**Date:** February 2026  
**Decision:** Two separate fixes are required for VS16 emoji rendering.  
1. **Measurement layer** (`UnicodeWidth`): `GetWidth()` and `Truncate()` scan for U+FE0F and treat the preceding character as 2 columns wide. Prevents truncation and layout errors.  
2. **Render layer** (`RenderContext.DrawStrAt`): Splits text at VS16 emoji boundaries, issuing explicit `View.Move()` calls after each emoji to correct Terminal.Gui's internal cursor drift. Without this, `AddStr("⚠️ High")` causes TG to move its cursor to position 1 (thinking `⚠` = 1 col) and overwrite position 1 with the VS16 character, consuming the following space.  
**Rationale:** Terminal.Gui's `AddStr` uses `GetColumns()` internally and cannot be overridden from outside; the only reliable fix is to prevent VS16 sequences from appearing mid-string in an `AddStr` call.

### ADR-006 — All string width measurement goes through UnicodeWidth
**Date:** February 2026  
**Decision:** Every width measurement on a `string` (not `Rune`) uses `UnicodeWidth.GetWidth()` or `UnicodeWidth.Truncate()`. Direct calls to `string.GetColumns()` are prohibited outside `UnicodeWidth`.  
**Rationale:** Single choke-point for width logic; VS16 correction applies everywhere automatically.

### ADR-007 — Text selection via screen-buffer overlay
**Date:** February 2026  
**Decision:** Text selection highlight and text extraction are implemented via a `char[,] ScreenBuf` populated during the normal render pass, rather than by modifying individual draw methods to split and re-color spans.  
**Rationale:** All span-drawing calls are deep inside `DrawStrAt`/`DrawStyledLine`/`DrawStyledSpans` with complex VS16 handling. Splitting each drawn segment at selection column boundaries inside those methods would dramatically increase their cyclomatic complexity and risk introducing emoji/wide-char rendering regressions. The overlay approach keeps draw methods unchanged: `DrawStrAt` simply also writes each character to `ScreenBuf[screenRow, col]`, and after the entire document is drawn, `ApplySelectionOverlay` re-draws only the selected cells with inverted colors from the buffer. This is correct because the buffer captures the exact characters as they were positioned by the VS16-aware drawing logic. The buffer is only allocated when a non-empty selection is active, so there is no overhead during normal rendering.
