using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Urbe.Client;

namespace Urbe.Client.Tests;

/// <summary>The editor's header, formatting bar and footer as the 1.8.4 draws them (#editorTop, #mdToolbar, #editorBottom).</summary>
public sealed partial class HeadlessUiTests
{
    [Fact]
    public async Task Editor_ChromeIsTheOriginals() =>
        Assert.True(await Session.Dispatch(EditorChromeScenario, CancellationToken.None));

    private static async Task<bool> EditorChromeScenario()
    {
        var dir = Path.Combine(Path.GetTempPath(), "urbe-chrome-" + Guid.NewGuid().ToString("N"));
        var vault = Path.Combine(dir, "vault");
        Directory.CreateDirectory(vault);
        File.WriteAllText(Path.Combine(vault, "Casa.md"), "# Casa\n\nA casa e a CASA.\n\n[[Outra]]\n");
        File.WriteAllText(Path.Combine(vault, "Outra.md"), "# Outra\n");
        try
        {
            var view = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "app", "sel.json")));
            var window = new Window { Width = 412, Height = 860, Content = view };
            window.Show();
            await view.OpenVaultAsync();
            await Settle(window, view);
            Snapshot(window, "native-city-phone.png");
            // #v23Top: the brand and three buttons (Buscar notas, Mapa da cidade, Configurações); settings.js menu
            var tops = view.GetLogicalDescendants().OfType<Button>().Where(b => b.Width == 44 && b.CornerRadius.TopLeft == 22).Select(b => ToolTip.GetTip(b) as string).ToList();
            Assert.Equal(new[] { "Buscar notas", "Mapa da cidade", "Configurações" }, tops);
            Assert.Equal("ok", view.SaveDotState);
            _ = view.ShowSettingsAsync();
            Dispatcher.UIThread.RunJobs();
            var menu = view.Dialogs.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
            Assert.Contains("Configurações", menu);
            Assert.Contains("Personalização", menu);
            Assert.Contains("Importar arquivos", menu);
            Assert.Contains("Pasta no dispositivo", menu);
            Snapshot(window, "native-settings-menu.png");
            window.KeyPress(Avalonia.Input.Key.Escape, Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PhysicalKey.Escape, null);
            for (int i = 0; i < 20 && view.Dialogs.IsOpen; i++) { await Task.Delay(10); Dispatcher.UIThread.RunJobs(); }
            Assert.False(view.Dialogs.IsOpen);
            view.OpenNoteByPath("Casa.md");
            Dispatcher.UIThread.RunJobs();
            var editor = view.Editor;
            static bool Shown(Control c) => c.GetSelfAndLogicalAncestors().OfType<Control>().All(x => x.IsVisible);
            string[] Texts() => editor.GetLogicalDescendants().OfType<TextBlock>().Where(Shown).Select(t => t.Text ?? "").ToArray();
            Button? Tip(string tip) => editor.GetLogicalDescendants().OfType<Button>().FirstOrDefault(b => ToolTip.GetTip(b) as string == tip);

            // #editorTop: Voltar, the file name, Visual/Fonte, Mais ações; no Salvar (the 1.8.4 saves by itself)
            Assert.DoesNotContain(editor.GetLogicalDescendants().OfType<Button>(), b => b.Content as string == "Salvar");
            Assert.NotNull(Tip("Mais ações"));
            // #editorBottom: "MD · 6 linhas" until the first edit, "Salvo"
            Assert.True(Texts().Contains("MD · 6 linhas"), string.Join(" / ", Texts()));
            Assert.Contains("Salvo", Texts());
            // the dock is hidden under the editor on a phone (v23Dock.hidden = editor)
            Assert.DoesNotContain(view.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text == "Notas" && Shown(t));
            // #propertiesBox only in Fonte
            Assert.DoesNotContain(Texts(), t => t.StartsWith("--- título:", StringComparison.Ordinal));
            Snapshot(window, "native-editor-preview.png");

            // #mdToolbar in Visual too (v23): with no block being edited, Título 2 acts on the first block (the caret lands
            // at the start of the text), turning the H1 into an H2; again, it goes back to a paragraph
            var h2 = Tip("Título 2")!;
            Assert.True(Shown(h2));
            h2.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            editor.Surface.FinishEdit();
            Assert.StartsWith("## Casa\n", editor.Editor.Text);
            h2.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            editor.Surface.FinishEdit();
            Assert.StartsWith("Casa\n", editor.Editor.Text);
            editor.Editor.Text = "# Casa\n\nA casa e a CASA.\n\n[[Outra]]\n";
            for (int i = 0; i < 200 && editor.HasChanges; i++) { await Task.Delay(20); Dispatcher.UIThread.RunJobs(); }
            editor.Open(new CityNote("Casa.md", editor.Editor.Text));
            Dispatcher.UIThread.RunJobs();

            editor.SetMode(EditorMode.Fonte);
            Dispatcher.UIThread.RunJobs();
            var props = Texts().Single(t => t.StartsWith("--- título:", StringComparison.Ordinal));
            Assert.Matches(@"^--- título: Casa categoria: raiz tags: \[\] criado: \d{4}-\d{2}-\d{2} modificado: \d{4}-\d{2}-\d{2} ---$", props);
            // the text column fills the width (textarea width:100%): 412 - 2·20 of padding
            Assert.True(editor.Editor.Bounds.Width >= 360, "coluna Fonte " + editor.Editor.Bounds.Width);
            Snapshot(window, "native-editor-source.png");

            // an edit: "Alterado", then the autosave writes it and says "Salvo"; the words join the count
            editor.Editor.Text += "mais\n";
            Dispatcher.UIThread.RunJobs();
            Assert.Contains("Alterado", Texts());
            Assert.Contains(Texts(), t => t.StartsWith("MD · 7 linhas · ", StringComparison.Ordinal) && t.EndsWith(" palavras", StringComparison.Ordinal));
            for (int i = 0; i < 200 && (editor.HasChanges || !Texts().Contains("Salvo")); i++) { await Task.Delay(20); Dispatcher.UIThread.RunJobs(); }
            Assert.Contains("Salvo", Texts());
            Assert.EndsWith("mais\n", File.ReadAllText(Path.Combine(vault, "Casa.md")));
            editor.IsVisible = false;
            Dispatcher.UIThread.RunJobs();
            Assert.Contains(view.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text == "Notas" && Shown(t));
            window.Close();
            return true;
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch (IOException) { }
        }
    }
}
