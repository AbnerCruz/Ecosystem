using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Urbe.Client;
using Urbe.Core;

namespace Urbe.Client.Tests;

public sealed partial class HeadlessUiTests
{
    [Fact]
    public async Task NoteTemplates_OwnExtensionBracketsAndFillingInTheEditor() =>
        Assert.True(await Session.Dispatch(NoteTemplateFlowScenario, CancellationToken.None));

    private static async Task<bool> NoteTemplateFlowScenario()
    {
        // CST-044/046/050: a *.modelo.md template anywhere; [brackets] offered as fields; "Preencher no editor"
        var dir = Path.Combine(Path.GetTempPath(), "urbe-template-flow-" + Guid.NewGuid().ToString("N"));
        var vault = Path.Combine(dir, "vault");
        Directory.CreateDirectory(Path.Combine(vault, "Projetos"));
        File.WriteAllText(Path.Combine(vault, "Projetos", "Personagem.modelo.md"), "# {{titulo}}\n\nRaça: [Raça]\nClasse: [Classe]\n- [ ] revisar\n");
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

            _ = view.ShowNotesCreateMenuAsync();
            await Settled();
            ClickButtonWithText(view.Dialogs, "A partir de um modelo");
            await Settled();
            ClickButtonWithText(view.Dialogs, "Personagem");   // the .modelo.md, by its name
            await Settled();
            // the brackets are not fields: the Urbe says so and offers to make them fields
            Assert.Contains(view.Dialogs.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text?.Contains("[Raça], [Classe]") == true);
            ClickButtonWithText(view.Dialogs, "Converter em campos");
            await Settled();
            Assert.Equal("# {{titulo}}\n\nRaça: {{Raça}}\nClasse: {{Classe}}\n- [ ] revisar\n", File.ReadAllText(Path.Combine(vault, "Projetos", "Personagem.modelo.md")));
            view.Dialogs.GetVisualDescendants().OfType<TextBox>().Single().Text = "Gandalf";
            ClickButtonWithText(view.Dialogs, "Criar nota");
            await Settled();
            // one answer, then the rest in the editor
            var boxes = view.Dialogs.GetVisualDescendants().OfType<TextBox>().ToList();
            Assert.Equal(2, boxes.Count);
            boxes[0].Text = "Maia";
            ClickButtonWithText(view.Dialogs, "Preencher no editor");
            await Settled();
            var made = Path.Combine(vault, "Gandalf.md");
            Assert.True(File.Exists(made));
            Assert.Equal("# Gandalf\n\nRaça: Maia\nClasse: \n- [ ] revisar\n", File.ReadAllText(made));
            Assert.True(view.Editor.IsVisible);
            window.Close();
            return true;
        }
        finally
        {
            UrbeApp.LocalStatePath = previous;
            try { Directory.Delete(dir, true); } catch (IOException) { }
        }
    }
}
