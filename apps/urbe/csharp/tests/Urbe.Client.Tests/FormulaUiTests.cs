using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Urbe.Client;

namespace Urbe.Client.Tests;

/// <summary>∑ / Ctrl+M (math/editor.js startFormula, #mathPane) in the native editor, against a real folder vault.</summary>
public sealed partial class HeadlessUiTests
{
    [Fact]
    public async Task Editor_FormulaPaneInsertsLikeTheOriginal() =>
        Assert.True(await Session.Dispatch(FormulaScenario, CancellationToken.None));

    /// <summary>A tap on a formula of the Visual text opens "Editar fórmula": Salvar keeps its delimiters with the new LaTeX,
    /// Remover takes it out (a display formula's block goes with it); "/" lists Fórmula and Fórmula em destaque.</summary>
    [Fact]
    public async Task Editor_TappingAFormulaEditsOrRemovesIt() =>
        Assert.True(await Session.Dispatch(FormulaEditScenario, CancellationToken.None));

    private static async Task<bool> FormulaEditScenario()
    {
        var view = new MainView();
        var window = new Window { Width = 412, Height = 860, Content = view };
        window.Show();
        async Task Settled() { for (int i = 0; i < 30; i++) { await Task.Delay(10); Dispatcher.UIThread.RunJobs(); } }
        view.Editor.Open(new CityNote("Fórmulas.md", "# Fórmulas\n\nÁrea $x^2$ aqui.\n\n$$\n\\int f\n$$\n\nFim.\n"));
        await Settled();
        var editor = view.Editor;
        Assert.Equal(EditorMode.Visual, editor.Mode);
        Control[] Atoms() => editor.Surface.GetVisualDescendants().OfType<Border>()
            .Where(b => b.Cursor is not null && b.Background == Avalonia.Media.Brushes.Transparent && b.Child is not TextBlock).ToArray();
        async Task Tap(Control c)
        {
            var p = c.TranslatePoint(new Avalonia.Point(c.Bounds.Width / 2, c.Bounds.Height / 2), window)!.Value;
            window.MouseDown(p, MouseButton.Left);
            window.MouseUp(p, MouseButton.Left);
            await Settled();
        }
        Assert.Equal(2, Atoms().Length);

        // the inline one: "Editar fórmula", its LaTeX, Salvar
        await Tap(Atoms()[0]);
        var pane = view.Formula;
        Assert.True(pane.IsOpen);
        Assert.Contains(pane.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text == "Editar fórmula");
        Assert.Contains(pane.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text == "Salvar");
        Assert.Equal("x^2", pane.Input.Text);
        pane.Input.Text = "y^3";
        pane.Commit();
        await Settled();
        Assert.Contains("Área $y^3$ aqui.", editor.Editor.Text);

        // the display one: Remover takes its block out
        await Tap(Atoms()[1]);
        Assert.True(pane.IsOpen);
        Assert.Equal("\\int f", pane.Input.Text);
        pane.GetLogicalDescendants().OfType<Button>().Single(b => b.Content is TextBlock { Text: "Remover" })
            .RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        await Settled();
        Assert.Equal("# Fórmulas\n\nÁrea $y^3$ aqui.\n\nFim.\n", editor.Editor.Text);
        window.Close();
        return true;
    }

    /// <summary>
    /// The formula bar of Fonte (#mathDock): it shows while the caret is inside a formula, with its preview and the
    /// \command suggestions; Tab goes to the next empty {} and then out of the formula; outside one it hides, and ∑ shows
    /// the symbols, a symbol going in as $symbol$ with the caret inside.
    /// </summary>
    [Fact]
    public async Task Editor_FormulaBarFollowsTheCaretInFonte() =>
        Assert.True(await Session.Dispatch(FormulaBarScenario, CancellationToken.None));

    private static async Task<bool> FormulaBarScenario()
    {
        var view = new MainView();
        var window = new Window { Width = 1280, Height = 760, Content = view };
        window.Show();
        view.Editor.Open(new CityNote("Fórmulas.md", "Área $x^2$ e $\\fr$ fim."));
        view.Editor.SetMode(EditorMode.Fonte);
        Dispatcher.UIThread.RunJobs();
        var ed = view.Editor.Editor;
        var dock = view.Editor.MathBar;
        ed.Focus();
        Dispatcher.UIThread.RunJobs();

        ed.CaretIndex = 0;                                         // outside: no bar
        Dispatcher.UIThread.RunJobs();
        Assert.False(dock.IsVisible);
        ed.CaretIndex = "Área $x".Length;                          // inside $x^2$: the bar and its preview
        Dispatcher.UIThread.RunJobs();
        Assert.True(dock.IsVisible);
        Assert.Equal("x^2", dock.Current!.Tex);
        Assert.False(dock.SymbolsShown);                           // a mouse device: the symbols wait for ∑
        ed.CaretIndex = "Área $x^2$ e $\\fr".Length;               // after \fr: the suggestions
        Dispatcher.UIThread.RunJobs();
        Assert.Contains("\\frac", dock.Suggestions);
        window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);   // accepts the first
        Dispatcher.UIThread.RunJobs();
        Assert.StartsWith("Área $x^2$ e $\\frac{", ed.Text);
        var inner = ed.CaretIndex;
        Assert.Equal('}', ed.Text![inner]);                        // the caret in the first {}
        window.KeyPress(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, null);
        Dispatcher.UIThread.RunJobs();
        Assert.True(ed.CaretIndex > inner && ed.Text[ed.CaretIndex] == '}');  // the next {}
        window.KeyPress(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, null);   // no {} left: out of the formula
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(' ', ed.Text[ed.CaretIndex]);                 // before " fim."
        Assert.True(ed.IsFocused);
        Assert.Null(dock.Current);

        // ∑ outside a formula: the bar with its hint and the symbols; x² goes in as $^{}$ with the caret inside
        dock.ToggleSymbols();
        Dispatcher.UIThread.RunJobs();
        Assert.True(dock.IsVisible);
        Assert.True(dock.SymbolsShown);
        var at = ed.CaretIndex;
        var sym = dock.GetLogicalDescendants().OfType<Button>().First(b => b.Content is TextBlock { Text: "x²" });
        sym.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("$^{}$", ed.Text!.Substring(at, 5));
        Assert.Equal(at + 3, ed.CaretIndex);
        Snapshot(window, "native-math-dock.png");

        view.Editor.SetMode(EditorMode.Visual);                    // never in Visual
        Dispatcher.UIThread.RunJobs();
        Assert.False(dock.IsVisible);
        window.Close();
        return true;
    }

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
