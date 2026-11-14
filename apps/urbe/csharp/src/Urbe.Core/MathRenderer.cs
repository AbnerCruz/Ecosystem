using System.Drawing;
using CSharpMath.Atom;
using CSharpMath.Rendering.FrontEnd;

namespace Urbe.Core;

public sealed record MathRenderResult(
    string Tex,
    bool Display,
    bool Success,
    string Svg,
    string? Diagnostic,
    float Width,
    float Height)
{
    public IReadOnlyList<MathCompatibilityDiagnostic> CompatibilityDiagnostics { get; init; } =
        Array.Empty<MathCompatibilityDiagnostic>();

    /// <summary>
    /// CST-142: how far the formula goes below its baseline (the bottom of the
    /// image is <see cref="Height"/>; the baseline is <c>Height - Depth</c> from
    /// the top), so inline math can sit on the text's baseline.
    /// </summary>
    public float Depth { get; init; }
}

/// <summary>
/// Pure C# TeX renderer for Urbe. Parsing/typesetting come from CSharpMath;
/// drawing is performed by Urbe's SVG canvas, with no JavaScript or native
/// graphics backend.
/// </summary>
public static class MathRenderer
{
    // CSharpMath 1.0.0-pre.2 uses process-wide mutable typesetting caches.
    // Serialize access to its painter to prevent concurrent dictionary corruption.
    private static readonly object TypesettingGate = new();

    public const string CSharpMathVersion = "1.0.0-pre.2";
    public const int MaxTexLength = 32_768;

    public static MathRenderResult RenderSvg(
        string? tex,
        bool display = true,
        float fontSize = 20,
        IReadOnlyDictionary<string, string>? macros = null)
    {
        var source = tex ?? string.Empty;
        if (source.Length > MaxTexLength)
        {
            return Failure(
                source,
                display,
                "Fórmula excede o limite de " + MaxTexLength + " caracteres.");
        }

        if (!float.IsFinite(fontSize) || fontSize is < 6 or > 256)
            return Failure(source, display, "Tamanho de fonte inválido.");

        var compatibility = MathCompatibility.Analyze(source);
        var blocking = compatibility.FirstOrDefault(item => item.BlocksRendering);
        if (blocking is not null)
        {
            return Failure(
                source,
                display,
                FormatCompatibilityDiagnostic(blocking),
                compatibility);
        }

        var macroExpansion = MathMacros.Expand(source, macros);
        if (!macroExpansion.Success)
        {
            return Failure(
                source,
                display,
                "Falha ao expandir macros: " + macroExpansion.Diagnostic,
                compatibility);
        }

        var renderSource = macroExpansion.Expanded;
        if (!string.Equals(renderSource, source, StringComparison.Ordinal))
        {
            var expandedCompatibility = MathCompatibility.Analyze(renderSource);
            var expandedBlocking =
                expandedCompatibility.FirstOrDefault(item => item.BlocksRendering);
            if (expandedBlocking is not null)
            {
                return Failure(
                    source,
                    display,
                    "Após expandir macros: " +
                    FormatCompatibilityDiagnostic(expandedBlocking),
                    compatibility);
            }
        }

        try
        {
            lock (TypesettingGate)
            {
                var painter = new UrbeSvgMathPainter
                {
                    DisplayErrorInline = false,
                    FontSize = fontSize,
                    LineStyle = display ? LineStyle.Display : LineStyle.Text,
                    LaTeX = renderSource
                };

                if (!string.IsNullOrWhiteSpace(painter.ErrorMessage))
                    return Failure(source, display, painter.ErrorMessage!, compatibility);

                var bounds = painter.Measure(float.NaN);
                if (painter.Display is { } typeset && TableWidths.Fix(typeset) != 0)
                    bounds = painter.Measure(float.NaN);
                var width = Math.Max(1, bounds.Width);
                var height = Math.Max(1, bounds.Height);
                if (!float.IsFinite(width) || !float.IsFinite(height) ||
                    width > 100_000 || height > 100_000)
                {
                    return Failure(
                        source,
                        display,
                        "Dimensões de renderização inválidas.",
                        compatibility);
                }

                var canvas = new SvgMathCanvas(width, height);
                painter.Draw(canvas, TextAlignment.TopLeft);

                return new MathRenderResult(
                    source,
                    display,
                    true,
                    canvas.ToSvg(source),
                    null,
                    width,
                    height)
                {
                    CompatibilityDiagnostics = compatibility,
                    Depth = Math.Clamp(height + bounds.Y, 0, height)
                };
            }
        }
        catch (Exception error)
        {
            // Renderer failures are diagnostics at the domain boundary: user
            // TeX must never crash a host or silently mutate the source.
            return Failure(
                source,
                display,
                "Falha ao renderizar fórmula: " + error.Message,
                compatibility);
        }
    }

    private static string FormatCompatibilityDiagnostic(
        MathCompatibilityDiagnostic diagnostic) =>
        string.IsNullOrWhiteSpace(diagnostic.Suggestion)
            ? diagnostic.Message
            : diagnostic.Message + " " + diagnostic.Suggestion;

    private static MathRenderResult Failure(
        string tex,
        bool display,
        string diagnostic,
        IReadOnlyList<MathCompatibilityDiagnostic>? compatibility = null) =>
        new(tex, display, false, string.Empty, diagnostic, 0, 0)
        {
            CompatibilityDiagnostics =
                compatibility ?? Array.Empty<MathCompatibilityDiagnostic>()
        };

    private sealed class UrbeSvgMathPainter :
        CSharpMath.Rendering.FrontEnd.MathPainter<SvgMathCanvas, Color>
    {
        public override Color WrapColor(Color color) => color;
        public override Color UnwrapColor(Color color) => color;
        public override ICanvas WrapCanvas(SvgMathCanvas canvas) => canvas;
    }

    /// <summary>
    /// CST-142: CSharpMath (1.0.0-pre.2 and its main branch) measures a row of a matrix from its first drawn cell, not
    /// from the row's start (CollectionWidth: rightmost minus leftmost child). A row whose first cell is narrower than its
    /// column (centred) comes out short, so the closing delimiter, or whatever follows the matrix, was drawn over the last
    /// column (<c>\begin{pmatrix}10 &amp; 2\\3 &amp; 40\end{pmatrix}</c>). The rows are moved to where their first cell is
    /// and their cells back by as much (nothing moves on the page), which makes every width honest; the delimiters and
    /// what follows on the line are then placed again from those widths.
    /// </summary>
    internal static class TableWidths
    {
        /// <summary>Fixes the tree; returns how much wider the whole formula became (0: nothing to fix).</summary>
        public static float Fix(CSharpMath.Display.IDisplay<CSharpMath.Rendering.BackEnd.Fonts, CSharpMath.Rendering.BackEnd.Glyph> root)
        {
            var before = root.Width;
            Walk(root);
            return root.Width - before;
        }

        private static float Walk(CSharpMath.Display.IDisplay<CSharpMath.Rendering.BackEnd.Fonts, CSharpMath.Rendering.BackEnd.Glyph> display)
        {
            switch (display)
            {
                case CSharpMath.Display.Displays.InnerDisplay<CSharpMath.Rendering.BackEnd.Fonts, CSharpMath.Rendering.BackEnd.Glyph> inner:
                {
                    var was = inner.Width;
                    Walk(inner.Inner);
                    // the delimiters are placed again from the inner list's width
                    inner.Position = inner.Position;
                    return inner.Width - was;
                }
                case CSharpMath.Display.Displays.ListDisplay<CSharpMath.Rendering.BackEnd.Fonts, CSharpMath.Rendering.BackEnd.Glyph> list:
                {
                    var was = list.Width;
                    if (IsTable(list)) { FixRows(list); return list.Width - was; }
                    // a line: what comes after a child that grew moves right by as much
                    var children = list.Displays.OrderBy(d => d.Position.X).ToList();
                    float shift = 0;
                    foreach (var child in children)
                    {
                        if (shift != 0) child.Position = new PointF(child.Position.X + shift, child.Position.Y);
                        shift += Walk(child);
                    }
                    return list.Width - was;
                }
                default:
                    return 0;
            }
        }

        // a table: rows (lists of cells) one under the other, all starting at the table's left
        private static bool IsTable(CSharpMath.Display.Displays.ListDisplay<CSharpMath.Rendering.BackEnd.Fonts, CSharpMath.Rendering.BackEnd.Glyph> list) =>
            list.Displays.Count > 0 &&
            list.Displays.All(d => d is CSharpMath.Display.Displays.ListDisplay<CSharpMath.Rendering.BackEnd.Fonts, CSharpMath.Rendering.BackEnd.Glyph> row && d.Position.X == 0 &&
                row.Displays.All(c => c is CSharpMath.Display.Displays.ListDisplay<CSharpMath.Rendering.BackEnd.Fonts, CSharpMath.Rendering.BackEnd.Glyph>) &&
                // a list holding a table is not one: its only "row" would be the table, whose rows all start at 0
                !(row.Displays.Count > 1 && row.Displays.All(c => c.Position.X == 0))) &&
            (list.Displays.Count > 1 || ((CSharpMath.Display.Displays.ListDisplay<CSharpMath.Rendering.BackEnd.Fonts, CSharpMath.Rendering.BackEnd.Glyph>)list.Displays[0]).Displays.Count > 1);

        private static void FixRows(CSharpMath.Display.Displays.ListDisplay<CSharpMath.Rendering.BackEnd.Fonts, CSharpMath.Rendering.BackEnd.Glyph> table)
        {
            foreach (var row in table.Displays.Cast<CSharpMath.Display.Displays.ListDisplay<CSharpMath.Rendering.BackEnd.Fonts, CSharpMath.Rendering.BackEnd.Glyph>>())
            {
                foreach (var cell in row.Displays) Walk(cell);
                if (row.Displays.Count == 0) continue;
                var first = row.Displays.Min(c => c.Position.X);
                if (first <= 0) continue;
                row.Position = new PointF(row.Position.X + first, row.Position.Y);
                foreach (var cell in row.Displays) cell.Position = new PointF(cell.Position.X - first, cell.Position.Y);
            }
        }
    }
}
