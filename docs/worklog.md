# Work Log

## 2026-02-28 — Collapse All / Expand All shortcuts

**Feature:** `-` collapses all `<details>` sections; `=` expands all `<details>` sections.

**Design:**
- `LayoutEngine.CollectAllDetails()` traverses the entire tree regardless of open/closed state, so nested sections are found even when their parent is collapsed.
- `MarkdownView.CollapseAll()` / `ExpandAll()` iterate over every `DetailsNode`, set `IsOpen`, then re-layout and redraw.
- Keys use `new Key((KeyCode)'-')` / `new Key((KeyCode)'=')` because `KeyCode` encodes printable characters as their Unicode value with no named enum member for these keys.
- Status bar updated with `-` / `=` hints; README keyboard table updated; spec FR-17/FR-18 added.

**Files changed:** `src/LayoutEngine.cs`, `src/MarkdownView.cs`, `src/Program.cs`, `README.md`, `docs/specification.md`

---

## 2026-02-27 — Mouse text selection and clipboard copy

**Feature:** Users can now select text with the mouse and copy it via right-click or `Ctrl+C`.

**Design:**
- `TextSelection` class stores a selection as an anchor `DocPoint` and an active `DocPoint`, both in document coordinates (row = `docY`, column = screen-column).
- `RenderContext` gains a `ScreenBuf char[,]?` property. When non-null, `DrawStrAt` records every drawn character into the buffer (indexed `[screenRow, screenCol]`), giving a character-accurate snapshot of the last render pass.
- `MarkdownView` creates a fresh `ScreenBuf` each draw when a non-empty selection exists, passes it to `RenderContext`, and after the main render calls `ApplySelectionOverlay` which redraws the selected cells from the buffer with inverted colors.
- On `Button1Pressed`, a drag anchor is set and `_isDragging = true`. `ReportMousePosition` events while dragging update `_selection.Active`. `Button1Clicked` after a zero-movement click clears the selection and restores normal details-toggle behavior.
- `CopyAndClearSelection()` uses `TextSelection.ExtractText()` to reconstruct the text from the `ScreenBuf`, calls `Clipboard.TrySetClipboardData()`, then clears the selection and requests a redraw.
- The status bar now shows a `^C Copy` hint.

**Files changed:** `src/TextSelection.cs` (new), `src/RenderContext.cs`, `src/MarkdownView.cs`, `src/Program.cs`

---

## 2026-02-27 — VS16 emoji cursor drift fix (second pass)

**Problem:** Spaces between VS16 emoji and following text still missing after the first fix. Root cause was deeper than width measurement: Terminal.Gui's `AddStr()` internally calls `GetColumns()` per rune to advance its own cursor. For `⚠️`, it sees `⚠` (1 col) + `️` (0 col), keeps its cursor at +1, then draws the space at +1. In the terminal, however, `⚠️` occupies 2 columns, so the terminal cursor is at +2. After Terminal.Gui draws the space at +1 it regresses the terminal cursor to 1, overwriting the right half of the emoji with the space character. Net effect: the space cell is lost.

**Fix:** Added `DrawStrAt(string text, ref int x, int screenY)` to `RenderContext`. This method scans the text for VS16 emoji sequences (base rune + U+FE0F where base rune width < 2), flushes any leading plain text, calls `View.AddStr` for each segment, and then explicitly calls `View.Move(x + 2, y)` to correct the cursor after each VS16 emoji before drawing the following text. All span-rendering loops in `RenderContext.cs` and `Renderers.cs` (heading, code blocks ×4, table cells, details summary) now use `DrawStrAt` instead of direct `View.AddStr` calls.

**Files changed:** `src/RenderContext.cs`, `src/Renderers.cs`

---

## 2026-02-27 — VS16 emoji width measurement fix (first pass)

**Problem:** Identified that `UnicodeWidth.GetWidth()` and `Truncate()` used Terminal.Gui's `GetColumns()` which under-reports VS16 emoji as 1 column instead of 2.

**Fix:** Added VS16 detection in `UnicodeWidth.GetWidth()` and `Truncate()`. `StyledLine.VisibleLength` updated to use `UnicodeWidth.GetWidth()`. All `string.GetColumns()` calls in rendering code replaced with `UnicodeWidth.GetWidth()`.

**Note:** This fixed measurement/truncation but NOT the rendering cursor drift — that required the second pass above.

**Files changed:** `src/Core.cs`, `src/RenderContext.cs`, `src/Renderers.cs`

---

## 2026-02-27 — Non-breaking space fix

**Problem:** Spaces between emoji icons and following text were missing in the rendered output. For example, `⚠️ High` rendered with the space consumed.

**Root cause:** Terminal.Gui v2's `GetColumns()` extension method under-reports display width for emoji that use VS16 (U+FE0F, Variation Selector-16) to request emoji presentation. A character like `⚠` (U+26A0) is in the BMP text range and `GetColumns()` reports 1 column; but when followed by U+FE0F, modern terminals render it in a 2-column emoji cell. The cursor tracking drift caused the following space to be drawn one column to the left, overwriting it.

**Affected characters (confirmed by test):** `⚠️` `♻️` `ℹ️` `🛡️` `🏷️` `⬇️` `⬆️` `✳️`

**Fix:**
- Rewrote `UnicodeWidth.GetWidth()` to iterate rune-by-rune, detecting U+FE0F after a rune reported as <2 columns wide, and bumping the width to 2 while consuming the VS16.
- Same logic applied in `UnicodeWidth.Truncate()`.
- Fixed `StyledLine.VisibleLength` to call `UnicodeWidth.GetWidth()` instead of `GetColumns()` directly.
- Replaced all `someString.GetColumns()` calls throughout `RenderContext.cs`, `Renderers.cs` (heading, code block, pre/code, table, details renderers) with `UnicodeWidth.GetWidth(someString)`.

**Files changed:** `src/Core.cs`, `src/RenderContext.cs`, `src/Renderers.cs`

---

## 2026-02-27 — Non-breaking space fix

**Problem:** Emoji followed by U+00A0 (NBSP) and text rendered without a visible space because Terminal.Gui's `AddStr` doesn't render NBSP as a space cell.

**Fix:** Added `.Replace('\u00A0', ' ')` in `StyleContext.ToSpan()` in `Core.cs`.

---

## 2026-02-27 — Empty `<details>` summary fix

**Problem:** When Markdig parsed a `<details>` block with a blank line between `<details>` and `<summary>`, it split them into separate HTML block nodes. The summary text was not picked up.

**Fix:** `BuildDetailsFromSiblings` in `MarkdownIrBuilder.cs` now looks ahead through subsequent HTML blocks to find a `<summary>` element when it is not present in the opening `<details>` block.

---

## 2026-02-27 — Tab scroll positioning

**Change:** `CycleFocus()` in `MarkdownView.cs` now scrolls the focused `<details>` block to the upper third of the viewport (1/6 from the top) instead of just ensuring it is minimally visible.

---

## 2026-02-27 — Table border background bleed fix

**Problem:** Table border box-drawing characters showed the `TableCellBg` background color on the outside, making borders appear to bleed into the document background area.

**Fix:** Border `Attribute` is computed before calling `PushBgOverride(t.TableCellBg)` so borders use the outer document background.

---

## 2026-02-27 — TableCellBg theme property

**Change:** Added `TableCellBg` as a required property to the `Theme` record. Set a distinct value for all 17 themes. Table cell content is wrapped in `PushBgOverride(t.TableCellBg)` to give cells a visually distinct background.

---

## Prior work (pre-worklog)

The following features were implemented before this worklog was started:

- .NET 10 project scaffolding with Terminal.Gui v2, Markdig, AngleSharp
- IR pipeline: Markdig AST → IR nodes → layout pass → render pass
- 17 named color themes with `FilledBorders` and `BorderChars` support
- Heading renderer with H1/H2 rule lines
- Paragraph renderer with word-wrap
- Code block renderer (filled half-block and box-drawing variants) with syntax highlighting
- `<pre><code>` HTML block renderer
- `<span style="">` inline color support via CSS parser
- Table renderer with column-aligned borders
- `<details>`/`<summary>` collapsible sections
- Background override stack in `RenderContext`
- Mouse click toggle for `<details>`
- Theme cycling with `t`/`T`
- Status bar with keybinding hints
- Unicode display width using `GetColumns()` (later corrected for VS16)
