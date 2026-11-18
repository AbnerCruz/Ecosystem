using System.Text.Json.Nodes;
using Urbe.Core;
using Urbe.Core.Assistant;

namespace Urbe.Core.Tests;

public sealed class AssistantPublicationTests
{
    private sealed class Host : IAssistantHost
    {
        public DocumentStore Documents { get; } = new();
        public KnowledgeIndex Knowledge { get; }
        public Host() => Knowledge = new(Documents);
        public int ReadChars => 2000;
        public (string? Path, string? Selection) EditorContext() => (null, null);
        public Task<UrbeDocument> CreateNoteAsync(string path, string content) => Task.FromResult(Documents.Upsert(new() { Path = path, Content = content }));
        public Task<UrbeDocument> SaveNoteAsync(UrbeDocument doc, string content) => Task.FromResult(Documents.Upsert(new() { Id = doc.Id, Path = doc.Path, Content = content }));
        public Task<UrbeDocument> RenameNoteAsync(UrbeDocument doc, string newPath) => throw new NotSupportedException();
        public Task TrashNoteAsync(UrbeDocument doc) => throw new NotSupportedException();
        public Task CreateFolderAsync(string path) => throw new NotSupportedException();
        public void OpenNote(string path) { }
        public void Remember(string fact) { }
        public void SetPlan(IReadOnlyList<AiPlanStep> steps) { }
    }

    [Fact]
    public async Task PublicationToolUsesRealWriterAndRejectsMissingSourcesCopiesAndStaleRevisions()
    {
        var host = new Host();
        var note = host.Documents.Upsert(new() { Path = "A.md", Content = "Texto autoral" });
        var project = PublicationProject.Create("Livro", note.Id);
        var input = new JsonObject { ["path"] = "Publicações/Livro.publication.json", ["spec"] = JsonNode.Parse(PublicationProjectCodec.Serialize(project)) };
        var tool = AssistantPublicationTools.All.Single(t => t.Name == "write_publication");
        var preview = tool.Preview!(input, host);
        Assert.DoesNotContain("Texto autoral", preview.After!);
        Assert.Equal("write", tool.Access);
        var result = await tool.Run(input, host);
        Assert.Single(result.Changes!);
        Assert.Equal("Texto autoral", host.Documents.Get(note.Id)!.Content);
        await Assert.ThrowsAsync<AiToolException>(() => tool.Run(input, host));
        var doc = host.Documents.Get((string)input["path"]!)!;
        input["expected_revision"] = doc.Revision;
        await tool.Run(input, host);
        input["expected_revision"] = -1;
        Assert.Throws<AiToolException>(() => tool.Preview(input, host));
        input["spec"]!["content"] = "CÓPIA PROIBIDA";
        Assert.Throws<AiToolException>(() => tool.Preview(input, host));
        input["spec"] = JsonNode.Parse(PublicationProjectCodec.Serialize(project.AddSource("doc_missing")));
        Assert.Throws<AiToolException>(() => tool.Preview(input, host));
        input["path"] = "../Livro.publication.json";
        Assert.Throws<AiToolException>(() => tool.Preview(input, host));
    }

    /// <summary>CST-171/176: an agent writes a video publication through the same contract: short on-screen labels, the script stays in the note.</summary>
    [Fact]
    public async Task VideoPublication_AllowsShortLabelsKeepsTheScriptInTheNoteAndValidates()
    {
        var host = new Host();
        var note = host.Documents.Upsert(new() { Path = "Cena.md", Content = "# Ciclo da água\n\nA água evapora e condensa." });
        var project = VideoComposition.Synced(PublicationProject.Create("Explicação", note.Id) with { Design = new PublicationDesign { Layout = "video" } });
        var spec = JsonNode.Parse(PublicationProjectCodec.Serialize(project))!.AsObject();
        var elements = spec["video"]!["scenes"]![0]!["elements"]!.AsArray();
        elements.Add(new JsonObject { ["kind"] = "text", ["text"] = "Passo 1", ["x"] = 0.5, ["y"] = 0.8 });
        var tool = AssistantPublicationTools.All.Single(t => t.Name == "write_publication");
        var input = new JsonObject { ["path"] = "Publicações/Água.publication.json", ["spec"] = spec };
        var preview = tool.Preview!(input, host);
        Assert.Contains("Passo 1", preview.After!);
        Assert.DoesNotContain("evapora", preview.After!);
        await tool.Run(input, host);
        var saved = PublicationProjectCodec.Parse(host.Documents.Get("Publicações/Água.publication.json")!.Content);
        Assert.Equal("video", saved.Design.Layout);
        Assert.Contains(saved.Video!.Scenes[0].Elements, e => e.Text == "Passo 1");
        // a whole script pasted as a label is refused: it belongs in the note
        var copied = JsonNode.Parse(PublicationProjectCodec.Serialize(project))!.AsObject();
        copied["video"]!["scenes"]![0]!["elements"]!.AsArray().Add(new JsonObject { ["kind"] = "text", ["text"] = new string('x', AssistantPublicationTools.MaxOverlay + 1) });
        var error = Assert.Throws<AiToolException>(() => tool.Preview(new JsonObject { ["path"] = "Publicações/B.publication.json", ["spec"] = copied }, host));
        Assert.Contains("roteiro pertence à nota", error.Message);
        // "text" outside a video element is still authored text
        var outside = JsonNode.Parse(PublicationProjectCodec.Serialize(project))!.AsObject();
        outside["text"] = "curto";
        Assert.Throws<AiToolException>(() => tool.Preview(new JsonObject { ["path"] = "Publicações/C.publication.json", ["spec"] = outside }, host));
        // an invalid video (fps 0) is refused by the same rules as the studio
        var bad = JsonNode.Parse(PublicationProjectCodec.Serialize(project))!.AsObject();
        bad["video"]!["fps"] = 0;
        Assert.Throws<AiToolException>(() => tool.Preview(new JsonObject { ["path"] = "Publicações/D.publication.json", ["spec"] = bad }, host));
        var schema = (await AssistantPublicationTools.All.Single(t => t.Name == "publication_schema").Run(new JsonObject(), host)).Content;
        Assert.Contains("layout:article|book|slides|course|video", schema);
        Assert.Contains("placementId", schema);
    }

    /// <summary>CST-178: the agent reviews its publication as the Estúdio does before handing it over, and is told what the review does not check.</summary>
    [Fact]
    public async Task TheAgentReviewsThePublicationBeforeDelivering()
    {
        var host = new Host();
        var note = host.Documents.Upsert(new() { Path = "A.md", Content = "# Capítulo\n\n#### Pulei níveis\n\n![](foto.png)\n" });
        var project = PublicationProject.Create("Livro", note.Id);
        var path = "Publicações/Livro.publication.json";
        host.Documents.Upsert(new() { Path = path, Content = PublicationProjectCodec.Serialize(project) });
        var tool = AssistantPublicationTools.All.Single(t => t.Name == "review_publication");
        Assert.Equal("read", tool.Access);
        var text = (await tool.Run(new JsonObject { ["path"] = path }, host)).Content;
        Assert.Contains("a corrigir", text);
        Assert.Contains("A.md", text);
        Assert.Contains("pula do nível", text);
        Assert.Contains("Não conferido", text);
        // fixed in the note, the review is clean and still says what it did not look at
        host.Documents.Upsert(new() { Id = note.Id, Path = note.Path, Content = "# Capítulo\n\nTexto.\n" });
        text = (await tool.Run(new JsonObject { ["path"] = path }, host)).Content;
        Assert.StartsWith("Revisão automática sem problemas em 1 seção.", text);
        Assert.Contains("se os fatos estão certos", text);
        await Assert.ThrowsAsync<AiToolException>(() => tool.Run(new JsonObject { ["path"] = "Nada.publication.json" }, host));
        // the method is in the contract the agent reads
        Assert.Contains("review_publication", AssistantPublicationTools.Schema);
        Assert.Contains("briefing", AssistantPublicationTools.Schema);
    }

    /// <summary>#986: the agent writes a video publication from the note it just created, naming it by path or title (it never sees the id); no loop of id errors.</summary>
    [Fact]
    public async Task AVideoPublicationAcceptsTheNoteByPathOrTitleAndSaysWhatIsWrong()
    {
        var host = new Host();
        var note = host.Documents.Upsert(new() { Path = "Roteiros/Demo.md", Content = "# Demo\n\nCena um.\n" });
        var tool = AssistantPublicationTools.All.Single(t => t.Name == "write_publication");
        var spec = JsonNode.Parse("""{"schemaVersion":1,"title":"Demo","sources":[{"documentId":"Roteiros/Demo.md"}],"design":{"layout":"video"},"formats":["mp4"]}""")!.AsObject();
        var result = await tool.Run(new JsonObject { ["path"] = "Publicações/Demo.publication.json", ["spec"] = spec }, host);
        var saved = PublicationProjectCodec.Parse(host.Documents.Get("Publicações/Demo.publication.json")!.Content);
        Assert.Equal(note.Id, Assert.Single(saved.Sources).DocumentId);
        Assert.Equal(note.Id, Assert.Single(saved.Sections).Source.DocumentId);   // the section is made from the source
        Assert.Equal("video", saved.Design.Layout);
        Assert.Contains(note.Id, result.Content);   // the ids are told back for the next edit
        using (var catalog = new PublicationSourceCatalog(host.Documents))
        {
            var timeline = VideoTimeline.Build(saved, catalog);
            Assert.True(timeline.IsComplete);
            Assert.Equal("Demo", Assert.Single(timeline.Scenes).Title);
            Assert.True(timeline.FrameCount > 0);
        }
        // by title too; a name that matches nothing says which one and how to find it
        spec["sources"] = JsonNode.Parse("""[{"documentId":"Demo"},{"documentId":"Inexistente"}]""");
        var e = Assert.Throws<AiToolException>(() => tool.Preview!(new JsonObject { ["path"] = "Publicações/Outra.publication.json", ["spec"] = spec.DeepClone() }, host));
        Assert.Contains("\"Inexistente\"", e.Message);
        Assert.Contains("list_notes", e.Message);
    }
}
