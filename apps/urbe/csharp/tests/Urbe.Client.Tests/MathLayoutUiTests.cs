using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless;
using Avalonia.LogicalTree;
using Avalonia.Threading;
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
}
