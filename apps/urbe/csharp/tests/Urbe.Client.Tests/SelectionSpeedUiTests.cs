using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Urbe.Client;

namespace Urbe.Client.Tests;

/// <summary>Organizing a growing vault (observation of 15/11/2026: "multi seleção ... não consigo organizar meu vault"):
/// a tap while selecting changes only its row, a refresh that changes nothing keeps the selection, and "Selecionar tudo"
/// takes every note shown, so many notes move in one go.</summary>
public sealed partial class HeadlessUiTests
{
    [Fact]
    public async Task Notes_SelectionAnswersAtOnceSurvivesRefreshesAndSelectsAll() =>
        Assert.True(await Session.Dispatch(SelectionSpeedScenario, CancellationToken.None));

    private static async Task<bool> SelectionSpeedScenario()
    {
        var dir = Path.Combine(Path.GetTempPath(), "urbe-selspeed-" + Guid.NewGuid().ToString("N"));
        var vault = Path.Combine(dir, "vault");
        Directory.CreateDirectory(Path.Combine(vault, "Destino"));
        for (int i = 0; i < 12; i++) File.WriteAllText(Path.Combine(vault, $"Nota {i:00}.md"), $"# Nota {i}\n");
        var previous = UrbeApp.LocalStatePath;
        UrbeApp.LocalStatePath = Path.Combine(dir, "app", "notes-state.json");
        async Task Settled() { for (int i = 0; i < 60; i++) { await Task.Delay(10); Dispatcher.UIThread.RunJobs(); } }
        try
        {
            var view = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "app", "sel.json")));
            var window = new Window { Width = 1280, Height = 760, Content = view };
            window.Show();
            await view.OpenVaultAsync();
            await Settle(window, view);
            await UntilNotesSaved(view);
            view.ShowNotes();
            await Settled();

            // a hold selects; a tap toggles: the rows stay the same controls (the tree is not rebuilt)
            Assert.True(view.Explorer.HoldNote("Nota 00.md"));
            var before = view.Explorer.GetLogicalDescendants().OfType<Button>().ToList();
            Assert.True(view.Explorer.TryOpenNote("Nota 01.md"));
            Assert.True(view.Explorer.TryOpenNote("Nota 02.md"));
            Assert.True(view.Explorer.TryOpenNote("Nota 02.md"));   // tapped again: out
            var after = view.Explorer.GetLogicalDescendants().OfType<Button>().ToList();
            Assert.Equal(before, after);
            Assert.Equal(["Nota 00.md", "Nota 01.md"], view.Explorer.SelectedPaths);
            Assert.True(view.Explorer.IsRowSelected("Nota 01.md"));
            Assert.False(view.Explorer.IsRowSelected("Nota 02.md"));
            Assert.False(view.Editor.IsVisible);

            // the folder read again with nothing changed keeps what is being selected
            Assert.False(await view.RefreshFromDiskAsync());
            view.RefreshNotesForTests();
            Assert.Equal(["Nota 00.md", "Nota 01.md"], view.Explorer.SelectedPaths);

            // Selecionar tudo, then Mover para pasta → Destino: all twelve move in one go and the selection ends
            view.Explorer.SelectAllShown();
            Assert.Equal(12, view.Explorer.SelectedPaths.Count);
            Assert.Contains(view.Explorer.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text == "12 notas");
            var move = view.Explorer.GetLogicalDescendants().OfType<Button>().Single(b => ToolTip.GetTip(b) as string == "Mover para pasta");
            move.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            await Settled();
            ClickButtonWithText(view.Dialogs, "Destino");
            await Settled();
            for (int i = 0; i < 12; i++) Assert.True(File.Exists(Path.Combine(vault, "Destino", $"Nota {i:00}.md")));
            Assert.Empty(Directory.GetFiles(vault, "*.md"));
            Assert.Empty(view.Explorer.SelectedPaths);
            window.Close();
            return true;
        }
        finally
        {
            UrbeApp.LocalStatePath = previous;
            try { Directory.Delete(dir, true); } catch { }
        }
    }
}
