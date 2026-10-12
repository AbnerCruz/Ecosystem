using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Urbe.Client;

namespace Urbe.Client.Tests;

/// <summary>"Localizar e substituir" (find-ui.js) in the native editor, against a real folder vault.</summary>
public sealed partial class HeadlessUiTests
{
    /// <summary>
    /// Ctrl+F in the editor opens the bar over the plain text (Visual → Fonte) and counts; Próximo selects the hits in
    /// turn; Substituir replaces the first, Todos the rest; the note saved holds the new text; Escape closes the bar.
    /// </summary>
    [Fact]
    public async Task Editor_FindAndReplaceAsTheOriginal() =>
        Assert.True(await Session.Dispatch(EditorFindScenario, CancellationToken.None));

    private static async Task<bool> EditorFindScenario()
    {
        var dir = Path.Combine(Path.GetTempPath(), "urbe-find-" + Guid.NewGuid().ToString("N"));
        var vault = Path.Combine(dir, "vault");
        Directory.CreateDirectory(vault);
        File.WriteAllText(Path.Combine(vault, "Casa.md"), "# Casa\n\nA casa e a CASA.\n\n[[Outra]]\n");
        File.WriteAllText(Path.Combine(vault, "Outra.md"), "# Outra\n");
        try
        {
            var view = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "app", "sel.json")));
            var window = new Window { Width = 1280, Height = 760, Content = view };
            window.Show();
            await view.OpenVaultAsync();
            await Settle(window, view);
            view.OpenNoteByPath("Casa.md");
            Dispatcher.UIThread.RunJobs();
            var editor = view.Editor;
            Assert.Equal(EditorMode.Visual, editor.Mode);

            window.KeyPress(Key.F, RawInputModifiers.Control, PhysicalKey.F, "f");
            Dispatcher.UIThread.RunJobs();
            var bar = editor.FindBar;
            Assert.True(bar.IsVisible);
            Assert.Equal(EditorMode.Fonte, editor.Mode);                     // localizar works on the plain text
            Assert.Equal("Nenhum resultado", bar.Status);
            bar.Query.Text = "casa";
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("3 resultados", bar.Status);
            Snapshot(window, "native-editor-find.png");
            bar.Next();
            Assert.Equal((2, 6), (editor.Editor.SelectionStart, editor.Editor.SelectionEnd));
            bar.Next();
            Assert.Equal((10, 14), (editor.Editor.SelectionStart, editor.Editor.SelectionEnd));

            bar.Replacement.Text = "Lar";
            bar.Replace(all: false);
            Assert.Equal("# Lar\n\nA casa e a CASA.\n\n[[Outra]]\n", editor.Editor.Text);
            Assert.Equal("2 resultados", bar.Status);
            bar.Replace(all: true);
            Assert.Equal("# Lar\n\nA Lar e a Lar.\n\n[[Outra]]\n", editor.Editor.Text);
            Assert.Equal("Nenhum resultado", bar.Status);

            Assert.True(await editor.SaveAsync());
            Assert.Equal("# Lar\n\nA Lar e a Lar.\n\n[[Outra]]\n", File.ReadAllText(Path.Combine(vault, "Casa.md")));

            bar.Query.Focus();
            window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
            Dispatcher.UIThread.RunJobs();
            Assert.False(bar.IsVisible);
            Assert.True(editor.IsVisible);
            window.Close();
            return true;
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch (IOException) { }
        }
    }
}
