using System.Text.Json.Nodes;
using Urbe.Core;
using Urbe.Core.Assistant;

namespace Urbe.Core.Tests;

/// <summary>Regression scenarios from the beta.32 conversation about removing the authorship seal.</summary>
public sealed class AssistantAuthorshipRecoveryTests
{
    private sealed class Host : IAssistantHost
    {
        public DocumentStore Documents { get; } = new();
        public KnowledgeIndex Knowledge { get; }
        public int Writes { get; private set; }
        public int ReadChars => 16000;
        public Host(string content = "Texto {ia:\"modelo\"}")
        {
            Documents.Upsert(new() { Id = "note", Path = "Nota.md", Content = content });
            Knowledge = new(Documents);
        }

        public (string? Path, string? Selection) EditorContext() => ("Nota.md", null);
        public Task<UrbeDocument> SaveNoteAsync(UrbeDocument doc, string text)
        {
            Writes++;
            return Task.FromResult(Documents.Upsert(new() { Id = doc.Id, Path = doc.Path, Content = text }));
        }
        public Task<UrbeDocument> CreateNoteAsync(string path, string content) => throw new NotSupportedException();
        public Task<UrbeDocument> RenameNoteAsync(UrbeDocument doc, string path) => throw new NotSupportedException();
        public Task TrashNoteAsync(UrbeDocument doc) => throw new NotSupportedException();
        public Task CreateFolderAsync(string path) => throw new NotSupportedException();
        public void OpenNote(string path) { }
        public void Remember(string fact) { }
        public void SetPlan(IReadOnlyList<AiPlanStep> steps) { }
    }

    private static List<AiChatMessage> Messages() =>
        [new() { Id = "request", Role = "user", Content = [AiChatBlock.TextBlock("Remova o selo de IA")] }];

    private static AiResult Answer(params AiBlock[] blocks) =>
        new(blocks, "", "end_turn", new(1, 1, null), "test");

    [Fact]
    public async Task NativeRuntimeExplainsProtectedAuthorshipWithoutChangingLegacyPrompt()
    {
        var host = new Host();
        string? prompt = null;
        await AssistantAgent.RunAsync(new()
        {
            Host = host, Agent = AssistantAgent.AgentById("geral"), Policy = "auto",
            Model = "test", Provider = new("p", "openai"), Messages = Messages(),
            Tools = [], OptimizeToolUse = true, MarkAiText = true,
            Stream = (_, request, _, _) =>
            {
                prompt = request.System;
                return Task.FromResult(Answer(AiBlock.TextBlock("Não posso remover a marca.")));
            }
        });
        Assert.NotNull(prompt);
        Assert.Contains("{ia:", prompt);
        Assert.Contains("autoria", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("não", prompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EmptyReplacementIsNotMissingRequiredArgument()
    {
        var edit = AssistantTools.Get("edit_note")!;
        Assert.Empty(AssistantTools.Validate(edit, new JsonObject
        {
            ["path"] = "Nota.md", ["old_text"] = "palavra", ["new_text"] = ""
        }));
        Assert.Contains(AssistantTools.Validate(edit, new JsonObject
        {
            ["path"] = "Nota.md", ["old_text"] = "palavra"
        }), e => e.Contains("new_text", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("Texto {ia:\"modelo\"}", "Texto")]
    [InlineData("Texto {ia:\"modelo\"}", "Texto {ia:\"outro\"}")]
    [InlineData("A {ia:\"modelo\"}\nB {ia:\"outro\"}", "A {ia:\"modelo\"}\nB")]
    public void AuthorshipMarkCannotBeRemovedOrRewritten(string before, string after)
    {
        Assert.False(NoteMarks.PreservesAiAuthorship(before, after));
    }

    [Theory]
    [InlineData("Texto {ia:\"modelo\"}", "Texto revisado {ia:\"modelo\"}")]
    [InlineData("Sem autoria", "Sem autoria revisado")]
    [InlineData("Texto {ia:\"modelo\"}", "Texto {ia:\"modelo\"}\nNovo {ia:\"outro\"}")]
    public void LegitimateChangesAndNewSystemMarksAreAllowed(string before, string after)
    {
        Assert.True(NoteMarks.PreservesAiAuthorship(before, after));
    }

    [Theory]
    [InlineData("Texto {ia:\"modelo\"}", "Texto")]
    [InlineData("Texto {ia:\"modelo\"}", "Texto {ia:\"novo\"}")]
    [InlineData("Texto {ia:\"modelo\"}", "Texto {ia:\"modelo\"}\n{ia:\"falso\"}")]
    public void ManualEditorCannotRemoveAlterOrForgeAiMark(string before, string after)
    {
        Assert.False(NoteMarks.AllowsUserAuthorshipEdit(before, after));
    }

    [Fact]
    public void ManualEditorCanEditContentAroundProtectedMark()
    {
        Assert.True(NoteMarks.AllowsUserAuthorshipEdit("Texto {ia:\"modelo\"}", "Texto revisado {ia:\"modelo\"}"));
    }

    [Fact]
    public async Task RejectedRemovalDoesNotWriteAndAgentCanFinishNormally()
    {
        var host = new Host();
        int steps = 0;
        var result = await AssistantAgent.RunAsync(new()
        {
            Host = host, Agent = AssistantAgent.AgentById("geral"), Policy = "auto",
            Model = "test", Provider = new("p", "openai"), Messages = Messages(),
            Tools = [AssistantTools.Get("edit_note")!, AssistantTools.Get("read_note")!],
            OptimizeToolUse = true, MarkAiText = true,
            Stream = (_, _, _, _) => Task.FromResult(++steps switch
            {
                1 => Answer(AiBlock.ToolUse("remove", "edit_note", new JsonObject
                {
                    ["path"] = "Nota.md", ["old_text"] = " {ia:\"modelo\"}", ["new_text"] = ""
                })),
                2 => Answer(AiBlock.ToolUse("read", "read_note", new JsonObject { ["path"] = "Nota.md" })),
                _ => Answer(AiBlock.TextBlock("A marca de autoria é protegida; a nota não foi alterada."))
            })
        });
        Assert.Equal("end", result.Stopped);
        Assert.Equal(0, host.Writes);
        Assert.Equal(3, steps);
        Assert.Equal("Texto {ia:\"modelo\"}", host.Documents.Get("Nota.md")!.Content);
    }
}
