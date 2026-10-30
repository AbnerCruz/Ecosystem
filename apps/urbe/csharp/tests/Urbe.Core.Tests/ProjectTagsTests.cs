using System.Text.Json.Nodes;
using Urbe.Core;
using Urbe.Core.Assistant;

namespace Urbe.Core.Tests;

/// <summary>CST-042 — tags herdadas por projeto (DEC-0058 / ADR-0046, alternativa A: a nota da pasta, herança calculada).</summary>
public sealed class ProjectTagsTests
{
    private static DocumentStore Vault()
    {
        var store = new DocumentStore();
        store.Upsert(new() { Id = "droerni", Path = "Droerni/Droerni.md", Content = "---\ntags: [worldbuilding, #projeto]\n---\n# Droerni" });
        store.Upsert(new() { Id = "povos", Path = "Droerni/Povos/Povos.md", Content = "---\ntags:\n  - povos\n---\n" });
        store.Upsert(new() { Id = "elfos", Path = "Droerni/Povos/Elfos.md", Content = "Os elfos #lore" });
        store.Upsert(new() { Id = "mapa", Path = "Droerni/Mapa.md", Content = "O mapa" });
        store.Upsert(new() { Id = "solta", Path = "Solta.md", Content = "Fora de projeto" });
        store.Upsert(new() { Id = "outro", Path = "Outro/Nota.md", Content = "Outro projeto" });
        return store;
    }

    [Fact]
    public void TheFolderNotesTagsReachEverythingInsideTheFolderAndSubfoldersAddTheirOwn()
    {
        var store = Vault();
        var projects = ProjectTags.Of(store.List());

        var mapa = projects.For("Droerni/Mapa.md", "O mapa");
        Assert.Equal(["worldbuilding", "projeto"], mapa.Select(t => t.Tag));
        Assert.All(mapa, t => Assert.Equal("Droerni/Droerni.md", t.FolderNote));

        var elfos = projects.For("Droerni/Povos/Elfos.md", "Os elfos #lore");
        Assert.Equal(["worldbuilding", "projeto", "povos"], elfos.Select(t => t.Tag));
        Assert.Equal("Droerni/Povos", elfos[2].Folder);
        Assert.Equal("#worldbuilding · de Droerni", ProjectTags.Describe(elfos[0]));
        Assert.Equal("#povos · de Povos", ProjectTags.Describe(elfos[2]));

        // the folder note keeps its tags as its own, and inherits only from the folders above it
        Assert.Empty(projects.For("Droerni/Droerni.md", store.Get("droerni")!.Content));
        Assert.Equal(["worldbuilding", "projeto"], projects.For("Droerni/Povos/Povos.md", store.Get("povos")!.Content).Select(t => t.Tag));

        Assert.Empty(projects.For("Solta.md", "Fora de projeto"));
        Assert.Empty(projects.For("Outro/Nota.md", "Outro projeto"));
    }

    [Fact]
    public void ANoteRefusesTheInheritanceOrSomeTagsAndATagItHasIsItsOwn()
    {
        var projects = ProjectTags.Of(Vault().List());

        Assert.Empty(projects.For("Droerni/Mapa.md", "---\nherdar: false\n---\nO mapa"));
        Assert.Equal(["projeto"], projects.For("Droerni/Mapa.md", "---\nsem-tags: [worldbuilding]\n---\n").Select(t => t.Tag));
        Assert.Equal(["projeto"], projects.For("Droerni/Mapa.md", "---\nsem-tags: \"#worldbuilding\"\n---\n").Select(t => t.Tag));
        Assert.Equal(["projeto"], projects.For("Droerni/Mapa.md", "---\nsem-tags:\n  - worldbuilding\n---\n").Select(t => t.Tag));

        // written in the note, the tag is the note's: not listed again as inherited
        Assert.Equal(["projeto"], projects.For("Droerni/Mapa.md", "O mapa #Worldbuilding").Select(t => t.Tag));
    }

    [Fact]
    public void OnlyTheFrontmatterOfTheFolderNoteGivesTags()
    {
        var store = new DocumentStore();
        store.Upsert(new() { Id = "p", Path = "Projeto/Projeto.md", Content = "Sem frontmatter, mas com #tag no texto" });
        store.Upsert(new() { Id = "n", Path = "Projeto/Nota.md", Content = "Nota" });
        Assert.Empty(ProjectTags.Of(store.List()).For("Projeto/Nota.md", "Nota"));
    }

    [Fact]
    public void FindingByTagIncludesInheritedOnesAndMovingANoteChangesThemWithoutWritingIt()
    {
        var store = Vault();
        using var index = new KnowledgeIndex(store);

        Assert.Equal(["droerni", "elfos", "mapa", "povos"], index.Tagged("worldbuilding").Select(d => d.Id).Order());
        Assert.Equal(["worldbuilding", "projeto"], index.Inherited("mapa").Select(t => t.Tag));
        Assert.Equal(["lore", "worldbuilding", "projeto", "povos"], index.EffectiveTags("elfos"));

        // moving the note out of the project: the inherited tags go at once, and the text is untouched
        store.Upsert(new() { Id = "mapa", Path = "Outro/Mapa.md", Content = "O mapa" });
        Assert.Empty(index.Inherited("mapa"));
        Assert.DoesNotContain(index.Tagged("worldbuilding"), d => d.Id == "mapa");
        Assert.Equal("O mapa", store.Get("mapa")!.Content);

        // a change in the folder note reaches the whole project
        store.Upsert(new() { Id = "droerni", Path = "Droerni/Droerni.md", Content = "---\ntags: [mundo]\n---\n" });
        Assert.Equal(["lore", "mundo", "povos"], index.EffectiveTags("elfos"));
        Assert.Empty(index.Tagged("worldbuilding"));
    }

    [Fact]
    public async Task TheAssistantFindsAndReadsInheritedTagsWithTheirOrigin()
    {
        var host = new Host(Vault());

        var found = await AssistantTools.Get("find_by_tag")!.Run(new JsonObject { ["tag"] = "#worldbuilding" }, host);
        Assert.Contains("Droerni/Mapa.md", found.Content);
        Assert.Contains("Droerni/Povos/Elfos.md", found.Content);

        var read = await AssistantTools.Get("read_note")!.Run(new JsonObject { ["path"] = "Droerni/Povos/Elfos.md" }, host);
        Assert.Contains("tags: #lore", read.Content);
        Assert.Contains("herdadas: #worldbuilding · de Droerni, #projeto · de Droerni, #povos · de Povos", read.Content);

        var links = await AssistantTools.Get("get_links")!.Run(new JsonObject { ["path"] = "Droerni/Mapa.md" }, host);
        Assert.Contains("Herdadas do projeto: #worldbuilding · de Droerni", links.Content);
    }

    private sealed class Host : IAssistantHost
    {
        public Host(DocumentStore documents) { Documents = documents; Knowledge = new(documents); }
        public DocumentStore Documents { get; }
        public KnowledgeIndex Knowledge { get; }
        public int ReadChars => 16000;
        public (string? Path, string? Selection) EditorContext() => (null, null);
        public Task<UrbeDocument> SaveNoteAsync(UrbeDocument doc, string text) => throw new NotSupportedException();
        public Task<UrbeDocument> CreateNoteAsync(string path, string content) => throw new NotSupportedException();
        public Task<UrbeDocument> RenameNoteAsync(UrbeDocument doc, string path) => throw new NotSupportedException();
        public Task TrashNoteAsync(UrbeDocument doc) => throw new NotSupportedException();
        public Task CreateFolderAsync(string path) => throw new NotSupportedException();
        public void OpenNote(string path) { }
        public void Remember(string fact) { }
        public void SetPlan(IReadOnlyList<AiPlanStep> steps) { }
    }
}
