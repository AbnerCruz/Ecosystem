using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Urbe.Client;

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
            await UntilMapSaved(view);
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
            var made = Path.Combine(vault, "Gandalf.md");
            Assert.True(File.Exists(made));
            var today = DateTime.Now.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
            Assert.Equal("# Gandalf\n\n:::ficha Gandalf\nCriada em " + today + "\n{{classe}}\n:::\n", File.ReadAllText(made));
            Assert.True(view.Editor.IsVisible);
            // the template itself is untouched
            Assert.Equal("# {{titulo}}\n\n:::ficha {{titulo}}\nCriada em {{data}}\n{{classe}}\n:::\n", File.ReadAllText(Path.Combine(vault, "Modelos", "Ficha de personagem.md")));
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
}
