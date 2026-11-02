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
}
