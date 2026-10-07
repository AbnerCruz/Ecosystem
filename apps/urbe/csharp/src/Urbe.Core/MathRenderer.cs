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
}

/// <summary>
/// Pure C# TeX renderer for Urbe. Parsing/typesetting come from CSharpMath;
/// drawing is performed by Urbe's SVG canvas, with no JavaScript or native
/// graphics backend.
/// </summary>
public static class MathRenderer
{
    public const string CSharpMathVersion = "1.0.0-pre.2";
    public const int MaxTexLength = 32_768;

    public static MathRenderResult RenderSvg(
        string? tex,
        bool display = true,
        float fontSize = 20)
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

        try
        {
            var painter = new UrbeSvgMathPainter
            {
                DisplayErrorInline = false,
                FontSize = fontSize,
                LineStyle = display ? LineStyle.Display : LineStyle.Text,
                LaTeX = source
            };

            if (!string.IsNullOrWhiteSpace(painter.ErrorMessage))
                return Failure(source, display, painter.ErrorMessage!, compatibility);

            var bounds = painter.Measure(float.NaN);
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
                CompatibilityDiagnostics = compatibility
            };
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
}
