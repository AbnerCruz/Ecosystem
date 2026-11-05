using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Urbe.Client;
using Urbe.Core;

namespace Urbe.Client.Tests;

public sealed partial class HeadlessUiTests
{
    private static async Task NameAndCreate(MainView view, Task creating, string title)
    {
        Dispatcher.UIThread.RunJobs();
        view.Dialogs.GetLogicalDescendants().OfType<TextBox>().First().Text = title;
        ClickButtonWithText(view.Dialogs, "Criar");
        await creating;
    }

    /// <summary>CST-135: publications from a selection, search results, a tag and a section — references to real notes.</summary>
    [Fact]
    public async Task Publications_FromSelectionSearchTagAndSection() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var (view, window, vault, dir) = await OpenCreateVault();
        try
        {
            var app = (IAssistantApp)view;
            await app.CreateNoteAsync("Aulas/Três.md", "# Três\n\nTexto #curso\n");
            await app.CreateNoteAsync("Aulas/Um.md", "# Um\n\n#curso\n\n## Exercícios\n\nResolva.\n\n## Extra\n\nFora.\n");
            await app.CreateNoteAsync("Outros/Dois.md", "# Dois\n\nSem tag.\n");
            string Path(SourceRef s) => view.Documents!.Get(s.Id)!.Path;

            // a selection keeps the order it was chosen in
            await NameAndCreate(view, view.SelectionActionAsync("publication", ["Outros/Dois.md", NotesExplorer.FolderPrefix + "Aulas", "Aulas/Três.md"]), "Escolhidas");
            Assert.Equal(["Outros/Dois.md", "Aulas/Três.md"], view.PublicationsStudio.Project!.Sources.Select(s => Path(new(s.DocumentId))));
            view.PublicationsStudio.Close();

            // a tag gathers its notes
            await NameAndCreate(view, view.CreatePublicationAsync(tag: "curso"), "Curso");
            Assert.Equal(["Aulas/Três.md", "Aulas/Um.md"], view.PublicationsStudio.Project!.Sources.Select(s => Path(new(s.DocumentId))));
            view.PublicationsStudio.Close();

            // a section of a note: the reference names the heading, the text stays the note's
            await NameAndCreate(view, view.CreatePublicationAsync(sourcePath: "Aulas/Um.md", fragment: "Exercícios"), "Só exercícios");
            var section = Assert.Single(view.PublicationsStudio.Project!.Sources);
            Assert.Equal("Exercícios", section.Fragment);
            var html = PublicationHtml.Export(view.PublicationsStudio.Project!, new PublicationSourceCatalog(view.Documents!)).Html;
            Assert.Contains("Resolva.", html);
            Assert.DoesNotContain("Fora.", html);
            Assert.DoesNotContain("Resolva", File.ReadAllText(System.IO.Path.Combine(vault, "Publicações", "Só exercícios.publication.json")));
            view.PublicationsStudio.Close();

            // a tag no note has is told, and nothing is created
            var none = view.CreatePublicationAsync(tag: "inexistente");
            Dispatcher.UIThread.RunJobs();
            view.Dialogs.GetLogicalDescendants().OfType<TextBox>().First().Text = "Vazia";
            ClickButtonWithText(view.Dialogs, "Criar");
            Dispatcher.UIThread.RunJobs();
            Assert.Contains(view.Dialogs.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text?.Contains("Nenhuma nota tem a tag") == true);
            view.Dialogs.Cancel();
            await none;
            Assert.False(File.Exists(System.IO.Path.Combine(vault, "Publicações", "Vazia.publication.json")));
            return true;
        }
        finally { window.Close(); try { Directory.Delete(dir, true); } catch (IOException) { } }
    }, CancellationToken.None));

    private readonly record struct SourceRef(string Id);
}
