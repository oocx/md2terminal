namespace Md2Terminal;

/// <summary>A position in document coordinate space (row, column).</summary>
public readonly record struct DocPoint(int Y, int X) : IComparable<DocPoint>
{
    /// <inheritdoc/>
    public int CompareTo(DocPoint other)
    {
        int yc = Y.CompareTo(other.Y);
        return yc != 0 ? yc : X.CompareTo(other.X);
    }

    /// <summary>Less-than comparison by document order (top-to-bottom, left-to-right).</summary>
    public static bool operator <(DocPoint a, DocPoint b)  => a.CompareTo(b) < 0;
    /// <summary>Greater-than comparison.</summary>
    public static bool operator >(DocPoint a, DocPoint b)  => a.CompareTo(b) > 0;
    /// <summary>Less-than-or-equal comparison.</summary>
    public static bool operator <=(DocPoint a, DocPoint b) => a.CompareTo(b) <= 0;
    /// <summary>Greater-than-or-equal comparison.</summary>
    public static bool operator >=(DocPoint a, DocPoint b) => a.CompareTo(b) >= 0;
}

/// <summary>
/// Tracks an active text selection defined by an anchor point (where the mouse was pressed)
/// and an active point (current mouse position). Both are in document coordinate space.
/// Supports two modes: linear (normal) and rectangular (block), toggled by Alt while dragging.
/// </summary>
public sealed class TextSelection
{
    /// <summary>The point where the selection started (mouse-button-down position).</summary>
    public DocPoint Anchor { get; set; }

    /// <summary>The current end of the selection (mouse cursor position while dragging).</summary>
    public DocPoint Active { get; set; }

    /// <summary>
    /// When true the selection is a rectangle: every covered row uses the same fixed column
    /// range defined by the X coordinates of <see cref="Anchor"/> and <see cref="Active"/>.
    /// </summary>
    public bool IsRectangular { get; set; }

    /// <summary>True when anchor equals active (no text spanned).</summary>
    public bool IsEmpty => Anchor == Active;

    /// <summary>Normalized start: always &lt;= <see cref="End"/>.</summary>
    public DocPoint Start => Anchor <= Active ? Anchor : Active;

    /// <summary>Normalized end: always &gt;= <see cref="Start"/>.</summary>
    public DocPoint End =>   Anchor <= Active ? Active  : Anchor;

    // ── Rectangular helpers ───────────────────────────────────────────────

    /// <summary>Left column of the rectangle (minimum X of anchor/active).</summary>
    private int RectStartX => Math.Min(Anchor.X, Active.X);

    /// <summary>Right column of the rectangle (maximum X of anchor/active).</summary>
    private int RectEndX   => Math.Max(Anchor.X, Active.X);

    /// <summary>Returns true if the selection spans at least part of the given document row.</summary>
    public bool CoversRow(int docY) => !IsEmpty && docY >= Start.Y && docY <= End.Y;

    /// <summary>
    /// Returns the column range [startCol, endCol) selected on the given document row.
    /// In rectangular mode every row uses the same fixed column range.
    /// In linear mode <paramref name="lineWidth"/> bounds interior rows.
    /// </summary>
    public (int StartCol, int EndCol) GetColRange(int docY, int lineWidth)
    {
        if (IsRectangular)
            return (RectStartX, RectEndX);

        int startCol = docY == Start.Y ? Start.X : 0;
        int endCol   = docY == End.Y   ? End.X   : lineWidth;
        return (startCol, endCol);
    }

    /// <summary>
    /// Extracts the selected text from a screen-coordinate character buffer.
    /// The buffer is indexed [screenRow, screenCol] and covers rows 0..viewportHeight-1.
    /// </summary>
    /// <param name="buf">Character buffer populated during the last render pass.</param>
    /// <param name="scrollY">Current scroll offset (converts docY → screen row).</param>
    /// <param name="viewportWidth">Width of the viewport (columns in buffer).</param>
    /// <returns>The selected text with newlines between rows.</returns>
    public string ExtractText(char[,] buf, int scrollY, int viewportWidth)
    {
        if (IsEmpty) return string.Empty;

        var sb = new System.Text.StringBuilder();
        for (int docY = Start.Y; docY <= End.Y; docY++)
        {
            int screenRow = docY - scrollY;
            // Row might have scrolled out of the buffer during the render that built it — skip gracefully
            if (screenRow < 0 || screenRow >= buf.GetLength(0))
            {
                if (docY < End.Y) sb.AppendLine();
                continue;
            }

            var (startCol, endCol) = GetColRange(docY, viewportWidth);
            startCol = Math.Clamp(startCol, 0, viewportWidth);
            endCol   = Math.Clamp(endCol,   0, viewportWidth);

            if (startCol < endCol)
            {
                // Collect non-null chars in range, trim trailing whitespace
                var chars = new char[endCol - startCol];
                for (int col = startCol; col < endCol; col++)
                {
                    char c = buf[screenRow, col];
                    // '\0' marks the trailing cell of a wide character — treat as space
                    chars[col - startCol] = c == '\0' ? ' ' : c;
                }
                // In rectangular mode keep per-row trailing spaces so columns align;
                // in linear mode trim them for cleaner prose copying.
                sb.Append(IsRectangular ? new string(chars) : new string(chars).TrimEnd());
            }

            if (docY < End.Y) sb.AppendLine();
        }
        return sb.ToString();
    }
}
