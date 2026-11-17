using System.Text.Json.Nodes;
using Urbe.Core.Assistant;

namespace Urbe.Core.Tests;

/// <summary>#963 case 01: a folder with no notes is not a folder with no files; the book is listed apart from the notes.</summary>
public sealed class ListNotesFilesTests
{
    private sealed class Host : IAssistantHost
    {
        public DocumentStore Documents { get; } = new();
        public KnowledgeIndex Knowledge => new(Documents);
        public int ReadChars => 16000;
        public IReadOnlyCollection<string> OtherFiles => ["Livros/Livro (1).pdf", "Livros/capa.png", "Outro/x.pdf"];
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

    [Fact]
    public async Task AFolderWithoutNotesStillShowsItsPdf()
    {
        var tool = AssistantTools.All.Single(t => t.Name == "list_notes");
        var text = (await tool.Run(new JsonObject { ["folder"] = "Livros" }, new Host())).Content;
        Assert.StartsWith("Nenhuma nota em \"Livros\".", text);
        Assert.Contains("Outros arquivos", text);
        Assert.Contains("- Livros/Livro (1).pdf", text);
        Assert.Contains("- Livros/capa.png", text);
        Assert.DoesNotContain("Outro/x.pdf", text);
    }
}
