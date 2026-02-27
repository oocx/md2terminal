namespace Md2Terminal;

/// <summary>Table column alignment.</summary>
public enum ColumnAlign { Left, Center, Right }

// ── Base ────────────────────────────────────────────────────────────────────

/// <summary>Abstract base for all IR nodes in the render tree.</summary>
public abstract class RenderNode
{
    /// <summary>Absolute row offset from the top of the document (set by layout pass).</summary>
    public int OffsetY { get; set; }

    /// <summary>Total rendered height in terminal rows.</summary>
    public abstract int Height { get; }

    /// <summary>Render this node using the given context.</summary>
    public abstract void Render(RenderContext ctx);
}

// ── Container ───────────────────────────────────────────────────────────────

public sealed class DocumentNode : RenderNode
{
    public List<RenderNode> Children { get; } = [];
    public override int Height => Children.Sum(c => c.Height);
    public override void Render(RenderContext ctx)
    {
        foreach (var child in Children)
        {
            if (child.OffsetY + child.Height < ctx.ScrollY) continue;
            if (child.OffsetY > ctx.ScrollY + ctx.ViewportHeight) break;
            child.Render(ctx);
        }
    }
}

// ── Heading ─────────────────────────────────────────────────────────────────

public sealed class HeadingNode : RenderNode
{
    public int Level { get; init; }
    public List<StyledLine> Content { get; init; } = [];

    // heading line + rule line for H1/H2
    public override int Height => 1 + Content.Count + (Level <= 2 ? 1 : 0);

    public override void Render(RenderContext ctx) =>
        HeadingRenderer.Render(this, ctx);
}

// ── Paragraph ───────────────────────────────────────────────────────────────

public sealed class ParagraphNode : RenderNode
{
    /// <summary>Original inline content (before wrapping).</summary>
    public List<StyledLine> OriginalLines { get; init; } = [];

    /// <summary>Word-wrapped lines (computed during layout pass).</summary>
    public List<StyledLine> WrappedLines { get; set; } = [];

    public override int Height => Math.Max(1, WrappedLines.Count) + 1; // +1 for spacing after

    public override void Render(RenderContext ctx) =>
        ParagraphRenderer.Render(this, ctx);
}

// ── Thematic Break ──────────────────────────────────────────────────────────

public sealed class ThematicBreakNode : RenderNode
{
    public override int Height => 1;
    public override void Render(RenderContext ctx) =>
        ThematicBreakRenderer.Render(this, ctx);
}

// ── Blockquote ──────────────────────────────────────────────────────────────

public sealed class BlockquoteNode : RenderNode
{
    public DocumentNode Content { get; init; } = new();
    public override int Height => Content.Height;
    public override void Render(RenderContext ctx) =>
        BlockquoteRenderer.Render(this, ctx);
}

// ── Lists ───────────────────────────────────────────────────────────────────

public sealed class ListNode : RenderNode
{
    public bool Ordered { get; init; }
    public List<ListItemNode> Items { get; init; } = [];
    public int NestingLevel { get; set; }
    public override int Height => Items.Sum(i => i.Height);
    public override void Render(RenderContext ctx) =>
        ListRenderer.Render(this, ctx);
}

public sealed class ListItemNode : RenderNode
{
    public int Index { get; init; } // 1-based for ordered lists
    public DocumentNode Content { get; init; } = new();
    public override int Height => Content.Height;
    public override void Render(RenderContext ctx) =>
        ListItemRenderer.Render(this, ctx);
}

// ── Code Blocks ─────────────────────────────────────────────────────────────

/// <summary>Fenced code block — syntax highlighted via tokenizer.</summary>
public sealed class CodeBlockNode : RenderNode
{
    public string Language { get; init; } = "";
    public string[] RawLines { get; init; } = [];
    public List<List<StyledSpan>> Highlighted { get; set; } = [];
    public override int Height => RawLines.Length + 2; // top + bottom border
    public override void Render(RenderContext ctx) =>
        CodeBlockRenderer.Render(this, ctx);
}

/// <summary>&lt;pre&gt;&lt;code&gt; with embedded spans — styles from HTML, no tokenizer.</summary>
public sealed class PreCodeNode : RenderNode
{
    public List<StyledLine> Lines { get; init; } = [];
    public override int Height => Lines.Count + 2; // top + bottom border
    public override void Render(RenderContext ctx) =>
        PreCodeRenderer.Render(this, ctx);
}

// ── Tables ──────────────────────────────────────────────────────────────────

public sealed class TableNode : RenderNode
{
    public List<string> Headers { get; init; } = [];
    public List<TableCellNode> HeaderCells { get; init; } = [];
    public ColumnAlign[] Alignments { get; init; } = [];
    public int[] ColumnWidths { get; set; } = [];
    public List<TableRowNode> Rows { get; init; } = [];

    /// <summary>Computed by the layout engine (accounts for theme's TableRowDividers).</summary>
    public int ComputedHeight { get; set; } = 1;

    public override int Height => ComputedHeight;

    public override void Render(RenderContext ctx) =>
        TableRenderer.Render(this, ctx);
}

public sealed class TableRowNode
{
    public List<TableCellNode> Cells { get; init; } = [];
    public int Height => Cells.Count > 0 ? Cells.Max(c => c.Lines.Count) : 1;
}

public sealed class TableCellNode
{
    public List<StyledLine> Lines { get; init; } = [];
}

// ── Details ─────────────────────────────────────────────────────────────────

public sealed class DetailsNode : RenderNode
{
    public StyledLine SummaryLine { get; init; } = new();
    public bool IsOpen { get; set; }
    public DocumentNode Content { get; init; } = new();

    /// <summary>Computed by the layout engine. Includes summary + content + border + trailing blank line.</summary>
    public int ComputedHeight { get; set; } = 1;

    public override int Height => ComputedHeight;

    public override void Render(RenderContext ctx) =>
        DetailsRenderer.Render(this, ctx);
}
