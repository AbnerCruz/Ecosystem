using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.Controls.Primitives;
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
}
