using Avalonia.Controls;
using Avalonia.VisualTree;
using Urbe.Client;
using Urbe.Core;

namespace Urbe.Client.Tests;

public sealed partial class HeadlessUiTests
{
    /// <summary>CST-206: a ```calculo block reads as lines with their results; the note keeps only the lines.</summary>
    [Fact]
    public async Task CalcBlock_ShowsEachLineWithItsResult() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var view = new MainView();
        var window = new Window { Width = 412, Height = 900, Content = view };
        window.Show();
        await view.LoadAsync(TutorialNotes.Load());
        const string note = "# Orçamento\n\n```calculo\n// mercado\npreco = 12,5\nqtd = 4\npreco * qtd\n100 km/h em m/s\nresolver x^2 - 5x + 6 = 0\nln(-1)\n```\n";
        view.Editor.Open(new CityNote("Tutorial/Orcamento.md", note));
        view.Editor.SetMode(EditorMode.Visual);
        await BenchPump(window);
        var block = view.Editor.GetVisualDescendants().OfType<Urbe.Client.Ui.CalcBlockView>().Single();
        var texts = block.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
        Assert.Contains("50", texts);
        Assert.Contains("27,7777777778 m/s", texts);
        Assert.Contains("x = 2; x = 3", texts);
        Assert.Contains("ln recebe números maiores que zero.", texts);
        Assert.Equal(note, view.Editor.Editor.Text);
        Snapshot(window, "calc-block.png");
        window.Close();
        return true;
    }, CancellationToken.None));
}
