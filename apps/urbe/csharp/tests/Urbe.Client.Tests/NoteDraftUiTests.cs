using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Urbe.Client;

namespace Urbe.Client.Tests;

public sealed partial class HeadlessUiTests
{
    /// <summary>
    /// CST-144: text typed and not saved when the app is killed comes back: opening the note again offers it,
    /// "Recuperar" puts it in the editor and it saves; the draft then leaves.
    /// </summary>
    [Fact]
    public async Task Drafts_TextTypedBeforeACrashIsOfferedBack() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var dir = Path.Combine(Path.GetTempPath(), "urbe-draft-" + Guid.NewGuid().ToString("N"));
        var vault = Path.Combine(dir, "vault");
        Directory.CreateDirectory(vault);
        const string saved = "# Plano\n\nPrimeira parte.\n";
        var file = Path.Combine(vault, "Plano.md");
        File.WriteAllText(file, saved);
        var state = UrbeApp.LocalStatePath;
        UrbeApp.LocalStatePath = Path.Combine(dir, "app", "notes-state.json");
        var drafts = Path.Combine(dir, "app", "rascunhos");
        async Task Pump(int ms) { for (int i = 0; i < ms / 10; i++) { await Task.Delay(10); Dispatcher.UIThread.RunJobs(); } }
        async Task<(MainView, Window)> Open()
        {
            var v = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "app", "sel.json")));
            var w = new Window { Width = 412, Height = 860, Content = v };
            w.Show();
            await v.OpenVaultAsync();
            await Settle(w, v);
            return (v, w);
        }
        try
        {
            var (view, window) = await Open();
            view.OpenNoteByPath("Plano.md");
            await Settle(window, view);
            view.Editor.SetMode(EditorMode.Fonte);
            const string typed = "# Plano\n\nPrimeira parte.\n\nSegunda parte, digitada e não salva.\n";
            view.Editor.Editor.Text = typed;
            await Pump(400);                       // the draft is written after a short pause…
            await view.DraftWrites;
            // …in the background: a busy machine may take longer than the pause
            for (int i = 0; i < 500 && (!Directory.Exists(drafts) || Directory.GetFiles(drafts, "*.json").Length == 0); i++) { await Pump(10); await view.DraftWrites; }
            Assert.NotEmpty(Directory.GetFiles(drafts, "*.json"));
            // …and the app is killed before its save: the vault keeps the old text, the draft stays
            var kept = Directory.GetFiles(drafts, "*.json").ToDictionary(f => f, File.ReadAllText);
            window.Close();
            await Pump(100);
            // closing saves the note and, once saved, clears its draft in the background; both must end before the
            // "crash" state is put back, or a late clear deletes it (seen when the whole suite runs and the machine is busy)
            for (int i = 0; i < 500 && (File.ReadAllText(file) != typed || Directory.GetFiles(drafts, "*.json").Length > 0); i++)
            {
                await Pump(10);
                await view.DraftWrites;
            }
            File.WriteAllText(file, saved);
            foreach (var (path, text) in kept) File.WriteAllText(path, text);

            (view, window) = await Open();
            view.OpenNoteByPath("Plano.md");
            // the recovery dialog comes after the vault opens; under a loaded test run that can take a few seconds
            for (int i = 0; i < 500 && !view.Dialogs.IsOpen; i++) await Pump(10);
            Assert.Contains(view.Dialogs.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text?.Contains("não chegou a ser salvo") == true);
            ClickButtonWithText(view.Dialogs, "Recuperar");
            await Pump(50);
            Assert.Equal(typed, view.Editor.Editor.Text);
            await view.Editor.FlushAsync();
            for (int i = 0; i < 100 && File.ReadAllText(file) != typed; i++) await Pump(20);
            Assert.Equal(typed, File.ReadAllText(file));
            await Pump(100);
            await view.DraftWrites;
            Assert.Empty(Directory.GetFiles(drafts, "*.json"));   // saved: the draft left
            window.Close();
            return true;
        }
        finally
        {
            UrbeApp.LocalStatePath = state;
            try { Directory.Delete(dir, true); } catch (IOException) { }
        }
    }, CancellationToken.None));
}

public sealed class NoteDraftRestoreTests
{
    /// <summary>CST-144: a recovered text is an ordinary edit: Desfazer brings the note back as it was, in both modes.</summary>
    [Theory]
    [InlineData(EditorMode.Visual)]
    [InlineData(EditorMode.Fonte)]
    public async Task ARestoredTextCanBeUndone(EditorMode mode) =>
        Assert.True(await HeadlessUiTests.Session.Dispatch<bool>(() =>
        {
            var editor = new EditorOverlay();
            var window = new Window { Width = 412, Height = 860, Content = editor };
            window.Show();
            editor.Open(new CityNote("Plano.md", "# Plano\n\nAtual.\n"));
            editor.SetMode(mode);
            Dispatcher.UIThread.RunJobs();
            editor.RestoreText("# Plano\n\nRecuperado.\n");
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("# Plano\n\nRecuperado.\n", editor.Editor.Text);
            Assert.True(editor.HasChanges);
            editor.Undo();
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("# Plano\n\nAtual.\n", editor.Editor.Text!.Replace("\r\n", "\n"));
            window.Close();
            return true;
        }, TestContext.Current.CancellationToken));
}
