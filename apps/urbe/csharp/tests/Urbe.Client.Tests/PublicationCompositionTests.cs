using Avalonia.Controls;
using Avalonia.Threading;
using Urbe.Client;
using Urbe.Core;

namespace Urbe.Client.Tests;

public sealed class PublicationCompositionTests
{
    [Fact]
    public void LiveRegistryExposesPublicationsAndRetiresLegacyPageWriting()
    {
        Assert.DoesNotContain(AssistantPanel.Tools, t => t.Name is "write_page" or "page_schema");
        Assert.Contains(AssistantPanel.Tools, t => t.Name == "write_publication" && t.Access == "write");
        Assert.Contains(AssistantPanel.Tools, t => t.Name == "publication_schema" && t.Access == "read");
        Assert.DoesNotContain("chame page_schema", Urbe.Core.Assistant.AssistantAgent.PublicationGuidance);
    }

    [Fact]
    public async Task CompositionUndoRedoNeverChangesSourceNotes() =>
        Assert.True(await HeadlessUiTests.Session.Dispatch<bool>(() =>
        {
            var store = new DocumentStore();
            var source = store.Upsert(new DocumentInput { Path = "Fonte.md", Content = "Texto da pessoa" });
            using var catalog = new PublicationSourceCatalog(store);
            var studio = new PublicationStudioView();
            var window = new Window { Width = 320, Height = 780, Content = studio };
            window.Show();
            var project = PublicationProject.Create("Livro", source.Id);
            studio.Open("Livro.publication.json", project, catalog);
            studio.UpdateProject(project with { Design = new PublicationDesign { Theme = "ocean", Layout = "slides" } });
            Assert.True(studio.CanUndo);
            studio.UndoComposition();
            Assert.Equal("article", studio.Project!.Design.Layout);
            Assert.True(studio.CanRedo);
            studio.RedoComposition();
            Assert.Equal("slides", studio.Project.Design.Layout);
            studio.UndoComposition();
            studio.UpdateProject(studio.Project with { Title = "Outro título" });
            Assert.False(studio.CanRedo);
            Assert.Equal("Texto da pessoa", store.Get(source.Id)!.Content);
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            window.Close();
            return true;
        }, TestContext.Current.CancellationToken));
}
