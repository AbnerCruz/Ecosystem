using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Urbe.Client;

namespace Urbe.Client.Tests;

/// <summary>"Versões anteriores": an edit keeps the previous text in .urbe/history.v2.json; the editor restores it.</summary>
public sealed partial class HeadlessUiTests
{
    [Fact]
    public async Task Editor_PreviousVersionsAreKeptAndRestored() =>
        Assert.True(await Session.Dispatch(HistoryScenario, CancellationToken.None));

    private static async Task<bool> HistoryScenario()
    {
        var dir = Path.Combine(Path.GetTempPath(), "urbe-history-" + Guid.NewGuid().ToString("N"));
        var vault = Path.Combine(dir, "vault");
        Directory.CreateDirectory(vault);
        File.WriteAllText(Path.Combine(vault, "Casa.md"), "# Casa\n\nPrimeiro texto.\n");
        File.WriteAllText(Path.Combine(vault, "Outra.md"), "# Outra\n");
        try
        {
            var view = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "app", "sel.json")));
            var window = new Window { Width = 412, Height = 860, Content = view };
            window.Show();
            await view.OpenVaultAsync();
            await Settle(window, view);
            view.OpenNoteByPath("Casa.md");
            Dispatcher.UIThread.RunJobs();
            view.Editor.SetMode(EditorMode.Fonte);
            Dispatcher.UIThread.RunJobs();

            // no version yet
            _ = view.ShowVersionsAsync();
            Dispatcher.UIThread.RunJobs();
            Assert.Contains(view.Dialogs.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text?.StartsWith("Esta nota ainda não tem versões", StringComparison.Ordinal) == true);
            window.KeyPress(Avalonia.Input.Key.Escape, Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PhysicalKey.Escape, null);
            for (int i = 0; i < 20 && view.Dialogs.IsOpen; i++) { await Task.Delay(10); Dispatcher.UIThread.RunJobs(); }

            // an edit: the previous text is kept, and written with the note by the autosave
            view.Editor.Editor.Text = "# Casa\n\nSegundo texto.\n";
            Dispatcher.UIThread.RunJobs();
            var history = Path.Combine(vault, ".urbe", "history.v2.json");
            for (int i = 0; i < 300 && !File.Exists(history); i++) { await Task.Delay(20); Dispatcher.UIThread.RunJobs(); }
            Assert.Equal("# Casa\n\nSegundo texto.\n", File.ReadAllText(Path.Combine(vault, "Casa.md")));
            var saved = File.ReadAllText(history);
            Assert.StartsWith("{\"version\":2,\"documents\":{\"", saved);
            Assert.Contains("\"path\":\"Casa.md\",\"content\":\"# Casa\\n\\nPrimeiro texto.\\n\"}", saved);

            // Versões anteriores → the version → Restaurar
            _ = view.ShowVersionsAsync();
            Dispatcher.UIThread.RunJobs();
            UrbeDialogs dialogs = view.Dialogs;
            Button Option(string start) => dialogs.GetLogicalDescendants().OfType<Button>()
                .First(b => b.GetLogicalDescendants().OfType<TextBlock>().Any(t => t.Text?.StartsWith(start, StringComparison.Ordinal) == true));
            Assert.Contains(dialogs.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text == "# Casa Primeiro texto.");
            Option("Hoje às ").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            for (int i = 0; i < 20; i++) { await Task.Delay(10); Dispatcher.UIThread.RunJobs(); }
            Assert.Contains(view.Dialogs.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text == "Restaurar esta versão?");
            Option("Restaurar").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            for (int i = 0; i < 20; i++) { await Task.Delay(10); Dispatcher.UIThread.RunJobs(); }
            Assert.Equal("# Casa\n\nPrimeiro texto.\n", view.Editor.Editor.Text);
            for (int i = 0; i < 300 && view.Editor.HasChanges; i++) { await Task.Delay(20); Dispatcher.UIThread.RunJobs(); }
            Assert.Equal("# Casa\n\nPrimeiro texto.\n", File.ReadAllText(Path.Combine(vault, "Casa.md")));
            window.Close();
            return true;
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch (IOException) { }
        }
    }
}
