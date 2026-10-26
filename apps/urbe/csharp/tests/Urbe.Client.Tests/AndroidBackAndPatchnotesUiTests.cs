using Avalonia.Controls;
using Urbe.Client;
using Urbe.Core;

namespace Urbe.Client.Tests;

public sealed partial class HeadlessUiTests
{
    [Fact]
    public async Task AndroidBackClosesTopLayerBeforeEditorAndNeverKillsAppInsideNavigation() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var view = new MainView();
            var window = new Window { Width = 412, Height = 860, Content = view };
            window.Show();
            await view.LoadAsync(TutorialNotes.Load());
            await Settle(window, view);
            try
            {
                view.Editor.Open(new CityNote("Teste.md", "# Teste"));
                Assert.True(view.Editor.IsVisible);
                Assert.True(view.HandleSystemBack());
                Assert.False(view.Editor.IsVisible);

                view.Assistant.Open();
                Assert.True(view.Assistant.IsVisible);
                Assert.NotNull(view.Assistant.ViewTitle);
                Assert.True(view.HandleSystemBack()); // closes nested Agentes screen first
                Assert.True(view.Assistant.IsVisible);
                Assert.Null(view.Assistant.ViewTitle);
                Assert.True(view.HandleSystemBack()); // closes assistant overlay
                Assert.False(view.Assistant.IsVisible);

                var confirmed = view.Dialogs.ConfirmAsync("Confirma?", null);
                Assert.True(view.Dialogs.IsOpen);
                Assert.True(view.HandleSystemBack());
                Assert.False(await confirmed);
                Assert.False(view.Dialogs.IsOpen);
                return true;
            }
            finally { window.Close(); }
        }, CancellationToken.None));

    [Fact]
    public void EmbeddedPatchnotesComeFromCanonicalPreparedVersion()
    {
        var notes = MainView.CurrentPatchnotes();
        Assert.Contains("2.0.0-beta.33", notes);
        Assert.Contains("Selecionar nota inteira", notes);
        Assert.Contains("publicação", notes, StringComparison.OrdinalIgnoreCase);
    }
}
