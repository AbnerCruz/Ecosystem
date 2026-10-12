using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Urbe.Client;

namespace Urbe.Client.Tests;

/// <summary>∑ / Ctrl+M (math/editor.js startFormula, #mathPane) in the native editor, against a real folder vault.</summary>
public sealed partial class HeadlessUiTests
{
    [Fact]
    public async Task Editor_FormulaPaneInsertsLikeTheOriginal() =>
        Assert.True(await Session.Dispatch(FormulaScenario, CancellationToken.None));

    private static async Task<bool> FormulaScenario()
    {
        var dir = Path.Combine(Path.GetTempPath(), "urbe-formula-" + Guid.NewGuid().ToString("N"));
        var vault = Path.Combine(dir, "vault");
        Directory.CreateDirectory(vault);
        File.WriteAllText(Path.Combine(vault, "Casa.md"), "# Casa\n\nA casa e a CASA.\n\n[[Outra]]\n");
        File.WriteAllText(Path.Combine(vault, "Outra.md"), "# Outra\n");
        async Task Settled() { for (int i = 0; i < 30; i++) { await Task.Delay(10); Dispatcher.UIThread.RunJobs(); } }
        try
        {
            var view = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "app", "sel.json")));
            var window = new Window { Width = 412, Height = 860, Content = view };
            window.Show();
            await view.OpenVaultAsync();
            await Settle(window, view);
            view.OpenNoteByPath("Casa.md");
            Dispatcher.UIThread.RunJobs();
            var editor = view.Editor;
            var sigma = editor.GetLogicalDescendants().OfType<Button>().Single(b => ToolTip.GetTip(b) as string == "Fórmula (LaTeX) — Ctrl+M");

            // Visual: ∑ with no block being edited opens "Nova fórmula" on the first block (caret after "# ")
            sigma.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            await Settled();
            var pane = view.Formula;
            Assert.True(pane.IsOpen);
            Assert.Contains(pane.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text == "Nova fórmula");
            Assert.Contains(pane.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text == "A prévia aparece aqui");
            pane.Input.Text = "\\fr";
            pane.Input.CaretIndex = 3;
            Dispatcher.UIThread.RunJobs();
            Assert.Contains("\\frac", pane.Suggestions);                       // the \command suggestions
            pane.Input.Text = "\\frac{a}{b}";
            Dispatcher.UIThread.RunJobs();
            Assert.DoesNotContain(pane.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text == "A prévia aparece aqui");
            Snapshot(window, "native-math-typed.png");
            pane.Commit();
            await Settled();
            Assert.False(pane.IsOpen);
            editor.Surface.FinishEdit();
            Assert.StartsWith("# $\\frac{a}{b}$Casa\n", editor.Editor.Text);

            // Fonte: Ctrl+M wraps the selection in $…$; Ctrl+Shift+M at the end opens $$ … $$ on its own lines, caret inside
            editor.SetMode(EditorMode.Fonte);
            Dispatcher.UIThread.RunJobs();
            var text = editor.Editor.Text!;
            var at = text.IndexOf("CASA", StringComparison.Ordinal);
            editor.Editor.Focus();
            editor.Editor.SelectionStart = at;
            editor.Editor.SelectionEnd = at + 4;
            window.KeyPress(Key.M, RawInputModifiers.Control, PhysicalKey.M, "m");
            Dispatcher.UIThread.RunJobs();
            Assert.Contains("A casa e a $CASA$.", editor.Editor.Text);
            editor.Editor.SelectionStart = editor.Editor.SelectionEnd = editor.Editor.CaretIndex = editor.Editor.Text!.Length;
            await editor.StartFormulaAsync(block: true);
            Assert.EndsWith("[[Outra]]\n$$\n\n$$\n", editor.Editor.Text);
            Assert.Equal(editor.Editor.Text!.Length - "\n$$\n".Length, editor.Editor.CaretIndex);

            // the autosave writes it
            for (int i = 0; i < 200 && editor.HasChanges; i++) { await Task.Delay(20); Dispatcher.UIThread.RunJobs(); }
            Assert.Contains("$\\frac{a}{b}$Casa", File.ReadAllText(Path.Combine(vault, "Casa.md")));
            window.Close();
            return true;
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch (IOException) { }
        }
    }
}
