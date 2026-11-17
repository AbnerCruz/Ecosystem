using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Urbe.Client;
using Urbe.Core;

namespace Urbe.Client.Tests;

public sealed partial class HeadlessUiTests
{
    private const string OddFrontmatter =
        "---\n" +
        "title: \"Plano: fase 2\"   # o título\n" +
        "aliases:\n  - P1\n" +
        "nota: 7.50\n" +
        "feito: false\n" +
        "prazo: 2026-11-30\n" +
        "autor:\n  nome: Ana\n  orcid: 0000-0002\n" +
        "texto: |\n  linha um\n  linha dois\n" +
        "vazio:\n" +
        "lista mista: [1, \"dois\", {três: 3}]\n" +
        "---\n\n# Corpo\n\nTexto com **negrito**.\n";

    /// <summary>
    /// CST-119: switching Visual and Fonte without editing never changes a byte; the form changes the one property
    /// it edits and keeps every other value and type it does not know; a draft typed in Fonte survives the switch.
    /// </summary>
    [Fact]
    public async Task Properties_UnknownValuesAndModeSwitchesNeverChangeTheNote() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var editor = new EditorOverlay();
            var window = new Window { Width = 412, Height = 900, Content = editor };
            window.Show();
            try
            {
                editor.Open(new CityNote("Notas/Plano.md", OddFrontmatter));
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                for (int i = 0; i < 3; i++)
                {
                    editor.SetMode(EditorMode.Fonte); Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
                    Assert.Equal(OddFrontmatter, editor.Editor.Text);
                    editor.SetMode(EditorMode.Visual); Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
                    Assert.Equal(OddFrontmatter, editor.Current!.Content);
                }
                Assert.False(editor.HasChanges);

                // one alias added by the form: only the aliases change
                var panel = editor.Properties;
                panel.Expand(true);
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                var input = panel.GetVisualDescendants().OfType<TextBox>().First(t => t.PlaceholderText == "adicionar");
                input.Focus();
                input.Text = "P2";
                window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
                Dispatcher.UIThread.RunJobs();
                var after = editor.Current!.Content;
                Assert.Equal(["P1", "P2"], NoteProperties.Parse(after).Aliases);
                foreach (var line in new[] { "title: \"Plano: fase 2\"   # o título", "nota: 7.50", "feito: false", "prazo: 2026-11-30",
                             "autor:\n  nome: Ana\n  orcid: 0000-0002", "texto: |\n  linha um\n  linha dois", "vazio:", "lista mista: [1, \"dois\", {três: 3}]" })
                    Assert.Contains(line, after);
                Assert.EndsWith("---\n\n# Corpo\n\nTexto com **negrito**.\n", after);

                // a draft typed in Fonte is there after going to Visual and back
                editor.SetMode(EditorMode.Fonte); Dispatcher.UIThread.RunJobs();
                editor.Editor.Text = editor.Editor.Text + "\nRascunho ainda não salvo.\n";
                Dispatcher.UIThread.RunJobs();
                editor.SetMode(EditorMode.Visual); Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
                editor.SetMode(EditorMode.Fonte); Dispatcher.UIThread.RunJobs();
                Assert.Contains("Rascunho ainda não salvo.", editor.Editor.Text);
                return true;
            }
            finally { window.Close(); }
        }, CancellationToken.None));

    /// <summary>CST-026: nested boxes survive mode switches and entering and leaving their block without a byte changed.</summary>
    [Fact]
    public async Task Containers_NestedBoxesSurviveModesAndEditingUntouched() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            const string note = "# Personagens\n\n:::ficha Frodo\nUm hobbit.\n\n:::atributos Valores\nForça: 3 %%anotação%%\n:::\n\nDepois.\n:::\n\nFim.\n";
            var editor = new EditorOverlay();
            var window = new Window { Width = 412, Height = 900, Content = editor };
            window.Show();
            try
            {
                editor.Open(new CityNote("Notas/Fichas.md", note));
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                foreach (var start in editor.Surface.BlockStarts.ToList())
                {
                    if (editor.Surface.BlockView(start) is not { } view) continue;
                    editor.Surface.TapBlock(start, view, null, new Avalonia.Point(5, 5), select: false);
                    Dispatcher.UIThread.RunJobs();
                    editor.FinishWriting();
                    Dispatcher.UIThread.RunJobs();
                }
                Assert.Equal(note, editor.Current!.Content);
                editor.SetMode(EditorMode.Fonte); Dispatcher.UIThread.RunJobs();
                Assert.Equal(note, editor.Editor.Text);
                editor.SetMode(EditorMode.Visual); Dispatcher.UIThread.RunJobs();
                Assert.Equal(note, editor.Current!.Content);
                Assert.False(editor.HasChanges);
                await Task.CompletedTask;
                return true;
            }
            finally { window.Close(); }
        }, CancellationToken.None));
}
