using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Urbe.Client;
using Urbe.Core;

namespace Urbe.Client.Tests;

/// <summary>UC-44 (DEC-0045): a note made from a template of Modelos/, and `:::tipo` containers in the Visual mode.</summary>
public sealed partial class HeadlessUiTests
{
    [Fact]
    public async Task NoteTemplates_ANewNoteStartsFromATemplateOfModelos() =>
        Assert.True(await Session.Dispatch(NoteTemplatesScenario, CancellationToken.None));

    private static async Task<bool> NoteTemplatesScenario()
    {
        var dir = Path.Combine(Path.GetTempPath(), "urbe-templates-" + Guid.NewGuid().ToString("N"));
        var vault = Path.Combine(dir, "vault");
        Directory.CreateDirectory(Path.Combine(vault, "Modelos"));
        File.WriteAllText(Path.Combine(vault, "Comece.md"), "# Comece\n");
        File.WriteAllText(Path.Combine(vault, "Modelos", "Ficha de personagem.md"), "# {{titulo}}\n\n:::ficha {{titulo}}\nCriada em {{data}}\n{{classe}}\n:::\n");
        var previous = UrbeApp.LocalStatePath;
        UrbeApp.LocalStatePath = Path.Combine(dir, "app", "notes-state.json");
        async Task Settled() { for (int i = 0; i < 40; i++) { await Task.Delay(10); Dispatcher.UIThread.RunJobs(); } }
        try
        {
            var view = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "app", "sel.json")));
            var window = new Window { Width = 412, Height = 860, Content = view };
            window.Show();
            await view.OpenVaultAsync();
            await Settle(window, view);
            await UntilNotesSaved(view);
            view.ShowNotes();
            Dispatcher.UIThread.RunJobs();

            // Criar offers the template; picking it and naming the note writes the filled-in content to the vault
            _ = view.ShowNotesCreateMenuAsync();
            await Settled();
            ClickButtonWithText(view.Dialogs, "A partir de um modelo");
            await Settled();
            ClickButtonWithText(view.Dialogs, "Ficha de personagem");
            await Settled();
            view.Dialogs.GetVisualDescendants().OfType<TextBox>().Single().Text = "Gandalf";
            ClickButtonWithText(view.Dialogs, "Criar nota");
            await Settled();
            // the template's other field is asked for after the name
            Assert.Contains(view.Dialogs.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text == "classe");
            view.Dialogs.GetVisualDescendants().OfType<TextBox>().Single().Text = "Mago {{data}}";
            ClickButtonWithText(view.Dialogs, "Criar nota");
            await Settled();
            var made = Path.Combine(vault, "Gandalf.md");
            Assert.True(File.Exists(made));
            var today = DateTime.Now.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
            Assert.Equal("# Gandalf\n\n:::ficha Gandalf\nCriada em " + today + "\nMago {{data}}\n:::\n", File.ReadAllText(made));
            Assert.True(view.Editor.IsVisible);
            // the template itself is untouched
            Assert.Equal("# {{titulo}}\n\n:::ficha {{titulo}}\nCriada em {{data}}\n{{classe}}\n:::\n", File.ReadAllText(Path.Combine(vault, "Modelos", "Ficha de personagem.md")));

            // Cancelar on a field creates nothing
            _ = view.CreateNoteFromTemplateAsync("Modelos/Ficha de personagem.md", "Saruman");
            await Settled();
            ClickButtonWithText(view.Dialogs, "Cancelar");
            await Settled();
            Assert.False(File.Exists(Path.Combine(vault, "Saruman.md")));
            window.Close();
            return true;
        }
        finally
        {
            UrbeApp.LocalStatePath = previous;
            try { Directory.Delete(dir, true); } catch (IOException) { }
        }
    }

    [Fact]
    public async Task NoteContainers_AreBoxesInVisualAndATapEditsTheWholeBlock() =>
        Assert.True(await Session.Dispatch<bool>(() =>
        {
            var surface = new VisualSurface();
            var window = new Window { Width = 600, Height = 700, Content = surface };
            window.Show();
            try
            {
                const string box = ":::ficha Gandalf\nRaça: **Maia**\n\n:::atributos\nSabedoria: 20\n:::\n:::";
                surface.SetText("Antes\n\n" + box + "\n\nDepois\n");
                Dispatcher.UIThread.RunJobs();
                var boxes = surface.GetLogicalDescendants().OfType<Border>().Where(b => b.Classes.Contains("urbe-box")).ToList();
                Assert.Equal(2, boxes.Count);   // the ficha and the one nested in it
                var texts = surface.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
                Assert.Contains("FICHA", texts);
                Assert.Contains("Gandalf", texts);
                Assert.Contains("ATRIBUTOS", texts);
                Assert.DoesNotContain(texts, t => (t ?? "").Contains(":::", StringComparison.Ordinal));
                Snapshot(window, "native-note-containers.png");

                // one tap on the box: its whole source in the edit box
                var start = surface.Text.IndexOf(":::ficha", StringComparison.Ordinal);
                typeof(VisualSurface).GetMethod("Edit", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(surface, [start]);
                Dispatcher.UIThread.RunJobs();
                var edit = surface.GetLogicalDescendants().OfType<TextBox>().Single();
                Assert.Equal(box, edit.Text);
                edit.Text = box.Replace("Maia", "Istar", StringComparison.Ordinal);
                Dispatcher.UIThread.RunJobs();
                surface.FinishEdit();
                Dispatcher.UIThread.RunJobs();
                Assert.Equal("Antes\n\n" + box.Replace("Maia", "Istar", StringComparison.Ordinal) + "\n\nDepois\n", surface.Text);
                Assert.Equal(2, surface.GetLogicalDescendants().OfType<Border>().Count(b => b.Classes.Contains("urbe-box")));
                return true;
            }
            finally { window.Close(); }
        }, CancellationToken.None));

    [Fact]
    public async Task NoteContainers_CaixaFromTheSlashMenuWrapsTheBlockAndFonteWrapsTheSelection() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var surface = new VisualSurface();
            var window = new Window { Width = 600, Height = 600, Content = surface };
            window.Show();
            try
            {
                surface.SetText("Antes\n\nfim\n");
                surface.AskBox = () => Task.FromResult<(string, string)?>(("ficha", "Gandalf"));
                Dispatcher.UIThread.RunJobs();
                var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                typeof(VisualSurface).GetMethod("Edit", flags)!.Invoke(surface, [surface.Text.IndexOf("fim", StringComparison.Ordinal)]);
                Dispatcher.UIThread.RunJobs();
                var edit = surface.GetLogicalDescendants().OfType<TextBox>().Single();
                edit.Text = "/cai";
                edit.CaretIndex = 4;
                Dispatcher.UIThread.RunJobs();
                // "/cai" finds Caixa in the menu
                Assert.Contains(LegacyEditorTools.FilterSlashWithAddons("cai"), x => x.Act == "box");
                typeof(VisualSurface).GetMethod("ChooseSlash", flags)!.Invoke(surface, ["box"]);
                await Pump(60);
                Assert.Equal(":::ficha Gandalf\n\n:::", edit.Text);
                Assert.Equal(17, edit.CaretIndex);
                surface.FinishEdit();
                Dispatcher.UIThread.RunJobs();
                Assert.Equal("Antes\n\n:::ficha Gandalf\n\n:::\n", surface.Text);
                Assert.Single(surface.GetLogicalDescendants().OfType<Border>(), b => b.Classes.Contains("urbe-box"));
            }
            finally { window.Close(); }

            // Fonte: the Caixa button asks for the type and the title and wraps the selection
            var editor = new EditorOverlay();
            var dialogs = new UrbeDialogs();
            editor.Dialogs = dialogs;
            var host = new Window { Width = 800, Height = 700, Content = new Grid { Children = { editor, dialogs } } };
            host.Show();
            try
            {
                editor.Open(new CityNote("Notas/x.md", "um dois três"));
                editor.SetMode(EditorMode.Fonte);
                editor.Editor.SelectionStart = 3;
                editor.Editor.SelectionEnd = 7;
                Dispatcher.UIThread.RunJobs();
                host.UpdateLayout();
                var button = editor.GetVisualDescendants().OfType<Button>().First(b => ToolTip.GetTip(b) as string == "Caixa");
                button.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                await Pump(60);
                dialogs.GetVisualDescendants().OfType<TextBox>().Single().Text = "dois tipos";
                ClickButtonWithText(dialogs, "Continuar");
                await Pump(40);
                Assert.Contains(dialogs.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text?.Contains("palavra só", StringComparison.Ordinal) == true);
                dialogs.GetVisualDescendants().OfType<TextBox>().Single().Text = "aviso";
                ClickButtonWithText(dialogs, "Continuar");
                await Pump(60);
                ClickButtonWithText(dialogs, "Inserir");
                await Pump(60);
                Assert.Equal("um \n:::aviso\ndois\n:::\n três", editor.Editor.Text);
                return true;
            }
            finally { host.Close(); }
        }, CancellationToken.None));
}
