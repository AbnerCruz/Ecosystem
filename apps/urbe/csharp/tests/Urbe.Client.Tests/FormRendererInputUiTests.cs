using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Urbe.Client.Ui;
using Urbe.Core;

namespace Urbe.Client.Tests;

public sealed partial class HeadlessUiTests
{
    [Fact]
    public async Task Forms_NumbersTakeOnlyNumbersAndMultipleChoiceKeepsEveryTick() => Assert.True(await Session.Dispatch<bool>(() =>
    {
        // CST-047 (owner's test of beta.31): letters got into a number field; only one option of a multiple choice stuck
        var definition = new FormDefinition
        {
            Title = "Ficha", Fields =
            [
                new() { Id = "idade", Label = "Idade", Type = "integer", Min = 0, Max = 120 },
                new() { Id = "temas", Label = "Temas", Type = "multi_select", Options = ["Mapa", "Clima", "Povos"] }
            ]
        };
        var session = FormSession.Create(definition);
        var form = new FormRenderer(session, () => { }, () => { }, () => { });
        var window = new Window { Width = 412, Height = 800, Content = new ScrollViewer { Content = form } };
        window.Show();
        var age = form.GetLogicalDescendants().OfType<TextBox>().Single();
        age.Text = "12a";
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("12", age.Text);
        Assert.Equal(12, (double)session.Values["idade"]!);
        age.Text = "130";
        Dispatcher.UIThread.RunJobs();
        Assert.Contains(form.GetLogicalDescendants().OfType<TextBlock>(), t => t.IsVisible && t.Text == "Use um valor entre 0 e 120.");
        age.Text = "1,5";
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("15", age.Text);
        var boxes = form.GetLogicalDescendants().OfType<CheckBox>().ToList();
        Assert.Equal(["Mapa", "Clima", "Povos"], boxes.Select(b => b.Content as string));
        boxes[0].IsChecked = true;
        boxes[2].IsChecked = true;
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(["Mapa", "Povos"], ((JsonArray)session.Values["temas"]!).Select(x => (string)x!));
        Assert.True(form.Validate());
        window.Close();
        return true;
    }, CancellationToken.None));
}
