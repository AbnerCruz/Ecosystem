using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;
using Urbe.Core;
using Urbe.Client;

namespace Urbe.Client.Tests;

/// <summary>CST-142: a tall fraction must not be forced into a fixed-height text row.</summary>
public sealed partial class HeadlessUiTests
{
    [Fact]
    public async Task EditorMath_InlineFractionUsesNaturalHeightInsteadOfClippingAdjacentText() =>
        Assert.True(await Session.Dispatch<bool>(() =>
    {
        var reader = new MarkdownReader();
        var content = reader.BuildMarkdown("Antes da fórmula $\\frac{\\sqrt{a+b}}{\\sum_{n=1}^{20} n}$ e depois.\n\nParágrafo abaixo.");
        var window = new Window { Width = 360, Height = 640, Content = content };
        window.Show();
        try
        {
            window.UpdateLayout();
            var text = content.GetLogicalDescendants().OfType<TextBlock>().ToList();
            var withFormula = text.Single(t => t.Inlines?.OfType<InlineUIContainer>().Any() == true);
            Assert.True(double.IsNaN(withFormula.LineHeight),
                "Uma linha com matemática inline não pode forçar altura fixa menor que o SVG.");
            Assert.False(withFormula.ClipToBounds);
            // MarkdownReader builds paragraphs from Runs, not TextBlock.Text.
            var after = text.Single(t => t.Inlines?.OfType<Run>().Any(r => r.Text == "Parágrafo abaixo.") == true);
            Assert.True(after.Bounds.Height > 0);
            var formula = Assert.Single(withFormula.Inlines!.OfType<InlineUIContainer>()).Child!;
            Assert.True(formula.Bounds.Height > reader.Size * reader.LineFactor,
                "A reprodução precisa conter uma fórmula mais alta que uma linha normal.");
            var top = formula.TranslatePoint(default, window)!.Value.Y;
            var next = after.TranslatePoint(default, window)!.Value.Y;
            Assert.True(next >= top + formula.Bounds.Height - 1,
                "O parágrafo seguinte não pode desenhar por cima da fórmula.");
            Snapshot(window, "native-math-natural-height.png");
            return Task.FromResult(true);
        }
        finally { window.Close(); }
    }, CancellationToken.None));

    [Fact]
    public async Task EditorMath_NormalParagraphPreservesConfiguredLineSpacing() =>
        Assert.True(await Session.Dispatch<bool>(() =>
    {
        var reader = new MarkdownReader { Size = 16, LineFactor = 1.7 };
        var content = reader.BuildMarkdown("Texto sem matemática.");
        var window = new Window { Width = 360, Height = 220, Content = content };
        window.Show();
        try
        {
            window.UpdateLayout();
            var paragraph = content.GetLogicalDescendants().OfType<TextBlock>().First(t => t.FontSize == 16);
            Assert.Equal(16 * 1.7, paragraph.LineHeight, 5);
            return Task.FromResult(true);
        }
        finally { window.Close(); }
    }, CancellationToken.None));

    [Theory]
    [InlineData(320)]
    [InlineData(412)]
    public async Task EditorMath_WideDisplayFormulaCanScrollWithoutShrinkingOrCuttingTheSource(double width) =>
        Assert.True(await Session.Dispatch<bool>(() =>
    {
        const string tex = "a+b+c+d+e+f+g+h+i+j+k+l+m+n+o+p+q+r+s+t+u+v+w+x+y+z";
        var reader = new MarkdownReader { Size = 24 };
        var content = reader.BuildMarkdown("$$\n" + tex + "\n$$\n\nDepois da fórmula.");
        var window = new Window { Width = width, Height = 640, Content = content };
        window.Show();
        try
        {
            window.UpdateLayout();
            var scroller = content.GetLogicalDescendants().OfType<ScrollViewer>().Single();
            Assert.Equal(ScrollBarVisibility.Auto, scroller.HorizontalScrollBarVisibility);
            Assert.True(scroller.Viewport.Width <= width);
            Assert.True(scroller.Extent.Width > scroller.Viewport.Width,
                "A fórmula precisa ser mais larga que a tela para testar a rolagem.");
            Assert.True(scroller.Viewport.Height >= scroller.Extent.Height - 1,
                "A fórmula mantém a altura natural, sem corte vertical.");
            scroller.Offset = new Vector(scroller.Extent.Width, 0);
            window.UpdateLayout();
            Assert.True(scroller.Offset.X > 0);
            Snapshot(window, "native-math-wide-" + width + ".png");
            return Task.FromResult(true);
        }
        finally { window.Close(); }
    }, CancellationToken.None));

    [Fact]
    public async Task EditorMath_FormulaEditorPreviewAlsoReachesBothEndsOfWideEquations() =>
        Assert.True(await Session.Dispatch<bool>(() =>
    {
        var pane = new FormulaPane();
        var window = new Window { Width = 320, Height = 720, Content = pane };
        window.Show();
        try
        {
            _ = pane.ShowAsync("a+b+c+d+e+f+g+h+i+j+k+l+m+n+o+p+q+r+s+t+u+v+w+x+y+z", true);
            window.UpdateLayout();
            var scroller = pane.GetLogicalDescendants().OfType<ScrollViewer>()
                .Single(s => s.Content is Canvas);
            Assert.True(scroller.Viewport.Width > 0);
            Assert.True(scroller.Extent.Width > scroller.Viewport.Width);
            scroller.Offset = new Vector(scroller.Extent.Width, 0);
            window.UpdateLayout();
            Assert.True(scroller.Offset.X > 0);
            Snapshot(window, "native-formula-pane-wide.png");
            return Task.FromResult(true);
        }
        finally { pane.Cancel(); window.Close(); }
    }, CancellationToken.None));

    // formulas whose ink goes past the box CSharpMath measures (italic overhang, accents, braces, radical index)
    private static readonly string[] OverhangingTex =
        ["f", "\\hat{y} = \\vec{v}", "\\underbrace{a+b}_{n}", "\\sqrt[3]{x^3+y}", "\\int f", "\\tilde{A}\\dot{B}", "\\frac{\\sqrt{a+b}}{\\sum_{n=1}^{20} n}"];

    /// <summary>Every painted shape of each formula canvas, in <paramref name="relativeTo"/>'s coordinates.</summary>
    private static List<(Canvas Canvas, Rect Ink)> FormulaInk(Control root, Visual relativeTo)
    {
        var result = new List<(Canvas, Rect)>();
        foreach (var canvas in root.GetVisualDescendants().OfType<Canvas>().Where(c => c.Children.OfType<Avalonia.Controls.Shapes.Shape>().Any()))
            foreach (var shape in canvas.Children.OfType<Avalonia.Controls.Shapes.Path>())
            {
                if (shape.Data is null || shape.Fill is null && shape.Stroke is null) continue;
                var b = shape.Data.Bounds;
                if (shape.TranslatePoint(b.TopLeft, relativeTo) is { } a && shape.TranslatePoint(b.BottomRight, relativeTo) is { } z)
                    result.Add((canvas, new Rect(a, z)));
            }
        return result;
    }

    [Theory]
    [InlineData("y")]
    [InlineData("\\frac{a}{b}")]
    [InlineData("\\frac{\\sqrt{a+b}}{\\sum_{n=1}^{20} n}")]
    public async Task EditorMath_AnInlineFormulaSitsOnTheTextBaseline(string tex) =>
        Assert.True(await Session.Dispatch<bool>(() =>
    {
        var reader = new MarkdownReader { Size = 18 };
        var content = reader.BuildMarkdown("Antes $" + tex + "$ depois.");
        var window = new Window { Width = 412, Height = 400, Content = content };
        window.Show();
        try
        {
            window.UpdateLayout();
            var line = content.GetLogicalDescendants().OfType<TextBlock>().Single(t => t.Inlines?.OfType<InlineUIContainer>().Any() == true);
            var formula = Assert.Single(line.Inlines!.OfType<InlineUIContainer>()).Child!;
            var result = Urbe.Core.MathRenderer.RenderSvg(tex, false, 20);
            var textBaseline = line.TextLayout.TextLines[0].Baseline;
            var formulaTop = formula.TranslatePoint(default, line)!.Value.Y;
            var formulaBaseline = formulaTop + TextBlock.GetBaselineOffset(formula);
            Assert.Equal(textBaseline, formulaBaseline, 1.5);
            // the part below the baseline (descender, denominator) is really below it, inside the line
            Assert.True(formulaTop + formula.Bounds.Height >= textBaseline + result.Depth - 1.5);
            Assert.True(formulaTop >= -.5 && formulaTop + formula.Bounds.Height <= line.Bounds.Height + .5,
                $"fórmula {formulaTop}+{formula.Bounds.Height} fora da linha {line.Bounds.Height}");
            return Task.FromResult(true);
        }
        finally { window.Close(); }
    }, CancellationToken.None));

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task EditorMath_TheInkOfEveryFormulaStaysInsideItsOwnBox(bool display) =>
        Assert.True(await Session.Dispatch<bool>(() =>
    {
        var reader = new MarkdownReader { Size = 18 };
        var md = string.Join("\n\n", OverhangingTex.Select(t => display ? "$$\n" + t + "\n$$" : "Antes $" + t + "$ depois."));
        var content = reader.BuildMarkdown(md);
        var window = new Window { Width = 412, Height = 1400, Content = new ScrollViewer { Content = content } };
        window.Show();
        try
        {
            window.UpdateLayout();
            var ink = FormulaInk(content, content);
            Assert.True(ink.Count >= OverhangingTex.Length);
            foreach (var (canvas, rect) in ink)
            {
                var origin = canvas.TranslatePoint(default, content)!.Value;
                var box = new Rect(origin, canvas.Bounds.Size).Inflate(.5);
                Assert.True(box.Contains(rect), $"tinta {rect} fora da caixa {box}");
                // nor clipped by a scroll viewer around it (the display formula's horizontal scroll)
                if (canvas.GetVisualAncestors().OfType<ScrollViewer>().FirstOrDefault() is { } scroll && !ReferenceEquals(scroll, window.Content))
                    Assert.True(scroll.Extent.Height <= scroll.Viewport.Height + .5, $"{scroll.Extent} / {scroll.Viewport}");
            }
            Snapshot(window, "native-math-ink-" + (display ? "display" : "inline") + ".png");
            return Task.FromResult(true);
        }
        finally { window.Close(); }
    }, CancellationToken.None));

    [Theory]
    [InlineData(320)]
    [InlineData(412)]
    [InlineData(900)]
    public async Task EditorMath_VisualBlocksWithFormulasNeverOverlapAndHoldTheirWholeFormula(double width) =>
        Assert.True(await Session.Dispatch<bool>(() =>
    {
        var editor = new EditorOverlay();
        var window = new Window { Width = width, Height = 2400, Content = editor };
        window.Show();
        try
        {
            var md = "# Fórmulas\n\nTexto com $\\frac{\\sqrt{a+b}}{\\sum_{n=1}^{20} n}$ no meio de uma linha que continua e quebra.\n\n" +
                     "$$\n\\int_{-\\infty}^{\\infty} \\frac{\\partial^2 f}{\\partial x^2} dx\n$$\n\n" +
                     "$$\n\\underbrace{a+b+c+d+e+f+g+h+i+j+k+l+m+n+o+p+q+r+s+t+u+v+w+x+y+z}_{26}\n$$\n\n" +
                     "Linha com $\\hat{y}$ e $f$ e $\\sqrt[3]{x}$.\n\n$$\n\\left( \\frac{1}{2} \\right)^{\\frac{a}{b}}\n$$\n\nFim.";
            editor.Open(new CityNote("Fórmulas.md", md));
            editor.SetMode(EditorMode.Visual);
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            var surface = editor.Surface;
            var blocks = surface.Children.Where(c => c.IsVisible && c.Bounds.Height > 0).ToList();
            Assert.True(blocks.Count >= 7, "blocos: " + blocks.Count);
            for (int i = 1; i < blocks.Count; i++)
                Assert.True(blocks[i].Bounds.Top >= blocks[i - 1].Bounds.Bottom - .5, $"bloco {i} sobre o anterior: {blocks[i - 1].Bounds} / {blocks[i].Bounds}");
            var ink = FormulaInk(surface, surface);
            Assert.True(ink.Count > 0);
            foreach (var (canvas, rect) in ink)
            {
                var block = blocks.Single(b => b.IsVisualAncestorOf(canvas));
                // vertically the whole formula is inside its block (the next block starts below it)
                Assert.True(rect.Top >= block.Bounds.Top - .5 && rect.Bottom <= block.Bounds.Bottom + .5, $"fórmula {rect} cortada no bloco {block.Bounds}");
                var scroll = canvas.GetVisualAncestors().OfType<ScrollViewer>().FirstOrDefault(s => block.IsVisualAncestorOf(s));
                if (scroll is not null) Assert.True(scroll.Extent.Height <= scroll.Viewport.Height + .5, $"{scroll.Extent} / {scroll.Viewport}");
                else Assert.True(rect.Right <= surface.Bounds.Width + .5, $"fórmula inline {rect} além da coluna {surface.Bounds.Width}");
            }
            Snapshot(window, "native-math-visual-" + width + ".png");
            return Task.FromResult(true);
        }
        finally { window.Close(); }
    }, CancellationToken.None));
}
