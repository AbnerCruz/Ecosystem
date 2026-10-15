using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Urbe.Client;

namespace Urbe.Client.Tests;

/// <summary>Owner (UC-54): "ter barrinha de pesquisa na área de selecionar para onde mover".</summary>
public sealed partial class HeadlessUiTests
{
    [Fact]
    public async Task Choose_ALongListHasASearchThatFiltersWithoutAccentsAndEnterTakesTheFirst() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var dialogs = new UrbeDialogs();
            var window = new Window { Width = 400, Height = 800, Content = dialogs };
            window.Show();
            try
            {
                var names = new[] { "Raiz", "Academia", "Estudos de idiomas", "Páginas", "Personalização", "Projetos", "Tutorial", "Ações" };
                var flow = dialogs.ChooseAsync("Mover para", names.Select(n => new DialogItem(n, Detail: n)).ToList());
                await Pump(60);
                var search = dialogs.GetLogicalDescendants().OfType<TextBox>().Single(t => AutomationProperties.GetName(t) == "Buscar");
                search.Text = "acao";
                await Pump(40);
                var shown = dialogs.GetLogicalDescendants().OfType<Button>().Where(b => b.Tag is string && b.IsVisible).ToList();
                Assert.Single(shown);
                search.Text = "p";
                await Pump(40);
                Assert.Equal(3, dialogs.GetLogicalDescendants().OfType<Button>().Count(b => b.Tag is string && b.IsVisible));
                search.Text = "perso";
                await Pump(40);
                search.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });
                await Pump(40);
                Assert.True(flow.IsCompleted);
                Assert.Equal(4, await flow);
                return true;
            }
            finally { window.Close(); }
        }, CancellationToken.None));
}
