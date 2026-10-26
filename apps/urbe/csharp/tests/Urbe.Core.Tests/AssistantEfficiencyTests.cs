using System.Text.Json.Nodes;
using Urbe.Core;
using Urbe.Core.Assistant;
namespace Urbe.Core.Tests;

public sealed class AssistantEfficiencyTests
{
    private sealed class Host : IAssistantHost
    {
        public DocumentStore Documents { get; } = new();
        public KnowledgeIndex Knowledge { get; }
        public Host() { Documents.Upsert(new() { Id = "a", Path = "A.md", Content = "Original" }); Knowledge = new(Documents); }
        public int Writes;
        public int ReadChars => 16000;
        public (string? Path, string? Selection) EditorContext() => ("A.md", null);
        public Task<UrbeDocument> SaveNoteAsync(UrbeDocument doc, string text)
        { Writes++; return Task.FromResult(Documents.Upsert(new() { Id = doc.Id, Path = doc.Path, Content = text })); }
        public Task<UrbeDocument> CreateNoteAsync(string path, string content) => throw new NotSupportedException();
        public Task<UrbeDocument> RenameNoteAsync(UrbeDocument doc, string path) => throw new NotSupportedException();
        public Task TrashNoteAsync(UrbeDocument doc) => throw new NotSupportedException();
        public Task CreateFolderAsync(string path) => throw new NotSupportedException();
        public void OpenNote(string path) { }
        public void Remember(string fact) { }
        public void SetPlan(IReadOnlyList<AiPlanStep> steps) { }
    }
    private static AiResult Answer(params AiBlock[] blocks) => new(blocks, "", "end_turn", new(1, 1, null), "test");
    private static List<AiChatMessage> Messages() => [new() { Id = "user", Role = "user", Content = [AiChatBlock.TextBlock("Faça o trabalho")] }];

    [Fact]
    public async Task IdenticalWriteDoesNotApproveWriteWatermarkOrStartPaidQualityReview()
    {
        var host = new Host(); int streams = 0, approvals = 0;
        var run = await AssistantAgent.RunAsync(new()
        {
            Host = host, Agent = AssistantAgent.AgentById("geral"), Policy = "ask", Model = "test", Provider = new("p", "openai"),
            Messages = Messages(), Tools = [AssistantTools.Get("write_note")!], OptimizeToolUse = true, MarkAiText = true, ReviewWrittenArtifacts = true,
            Approve = (_, _) => { approvals++; return Task.FromResult(AiApproval.Approve); },
            Stream = (_, _, _, _) => Task.FromResult(++streams == 1 ? Answer(AiBlock.ToolUse("w", "write_note", new JsonObject { ["path"] = "A.md", ["content"] = "Original" })) : Answer(AiBlock.TextBlock("O conteúdo já estava correto.")))
        });
        Assert.Equal(0, host.Writes); Assert.Equal(0, approvals); Assert.Equal(2, streams);
        Assert.Equal("Original", host.Documents.Get("A.md")!.Content);
        Assert.Empty(run.Changes); Assert.Equal(1, run.Efficiency.NoOpWrites);
    }

    [Fact]
    public async Task ParallelIdenticalReadsExecuteOnceButEachCallKeepsItsResultAndOnlyOneCopyIsSent()
    {
        var host = new Host(); int reads = 0, streams = 0; AiRequest? finalRequest = null;
        var text = new string('x', 5000);
        var read = new AiTool { Name = "read_note", Access = "read", Description = "read", Parameters = new() { ["type"] = "object" }, Label = _ => "Read",
            Run = async (_, _) => { reads++; await Task.Delay(5); return new(text); } };
        var messages = Messages();
        var run = await AssistantAgent.RunAsync(new()
        {
            Host = host, Model = "test", Provider = new("p", "openai"), Policy = "readonly", Messages = messages, Tools = [read], OptimizeToolUse = true,
            Stream = (_, request, _, _) =>
            {
                streams++;
                if (streams == 1) return Task.FromResult(Answer(AiBlock.ToolUse("one", "read_note", new JsonObject { ["path"] = "A.md", ["start_line"] = 1 }),
                    AiBlock.ToolUse("two", "read_note", new JsonObject { ["start_line"] = 1, ["path"] = "A.md" })));
                finalRequest = request; return Task.FromResult(Answer(AiBlock.TextBlock("Pronto")));
            }
        });
        Assert.Equal(1, reads); Assert.Equal(1, run.Efficiency.ReusedReads);
        var stored = messages.SelectMany(m => m.Content).Where(b => b.Type == "tool_result").ToList();
        Assert.Equal(2, stored.Count); Assert.All(stored, b => Assert.Equal(text, b.Content));
        var sent = finalRequest!.Messages.SelectMany(m => m.Content).Where(b => b.Type == "tool_result").ToList();
        Assert.Single(sent, b => b.Content == text);
        Assert.Contains(sent, b => b.ToolUseId == "two" && b.Content.Contains("one"));
        Assert.True(run.Efficiency.ContextCharsSaved > 4500);
    }

    [Fact]
    public async Task OutOfOrderParallelReadCompletionsPreserveCanonicalEvidenceReferences()
    {
        var ready = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var content = new string('e', 5000);
        var finished = new List<string>();
        var read = new AiTool
        {
            Name = "read_note", Access = "read", Description = "Read", Label = _ => "Read",
            Parameters = new() { ["type"] = "object" },
            Run = async (input, _) =>
            {
                var path = (string?)input["path"];
                if (path == "A.md")
                {
                    await ready.Task.WaitAsync(TestContext.Current.CancellationToken);
                    await Task.Delay(25, TestContext.Current.CancellationToken); // completes after B.md
                }
                else ready.TrySetResult(true);
                lock (finished) finished.Add(path ?? "");
                return new AiToolOutput(content);
            }
        };
        var history = Messages();
        AiRequest? captured = null;
        int steps = 0;
        await AssistantAgent.RunAsync(new()
        {
            Host = new Host(), Policy = "readonly", Provider = new("p", "openai"),
            Model = "test", Messages = history, Tools = [read], OptimizeToolUse = true,
            Stream = (_, req, _, _) =>
            {
                if (++steps == 1)
                    return Task.FromResult(Answer(
                        AiBlock.ToolUse("one", "read_note", new JsonObject { ["path"] = "A.md" }),
                        AiBlock.ToolUse("two", "read_note", new JsonObject { ["path"] = "B.md" })));
                captured = req;
                return Task.FromResult(Answer(AiBlock.TextBlock("Concluído")));
            }
        });
        Assert.Equal(new[] { "B.md", "A.md" }, finished); // assert genuinely reversed completion
        var original = history.SelectMany(m => m.Content).Where(b => b.Type == "tool_result").ToList();
        Assert.Equal(new[] { "one", "two" }, original.Select(b => b.ToolUseId));
        Assert.All(original, b => Assert.Equal(content, b.Content));
        var sent = captured!.Messages.SelectMany(m => m.Content).Where(b => b.Type == "tool_result").ToList();
        Assert.Equal(new[] { "one", "two" }, sent.Select(b => b.ToolUseId));
        Assert.Equal(content, sent[0].Content);
        Assert.Contains("chamada one", sent[1].Content);
    }

    [Fact]
    public async Task ChangingTheVaultInvalidatesReadReuse()
    {
        var host = new Host(); int reads = 0, streams = 0;
        var read = new AiTool { Name = "read_note", Access = "read", Description = "read", Parameters = new() { ["type"] = "object" }, Label = _ => "Read",
            Run = (_, _) => { reads++; return Task.FromResult(new AiToolOutput(host.Documents.Get("A.md")!.Content)); } };
        var write = new AiTool { Name = "mutate_document", Access = "write", Description = "write", Parameters = new() { ["type"] = "object" }, Label = _ => "Write",
            Run = async (_, _) => { await host.SaveNoteAsync(host.Documents.Get("A.md")!, "Novo"); return new("Alterado"); } };
        var messages = Messages();
        var result = await AssistantAgent.RunAsync(new()
        {
            Host = host, Model = "test", Provider = new("p", "openai"), Policy = "auto", Agent = AssistantAgent.AgentById("geral") with { Tools = "all" },
            Messages = messages, Tools = [read, write], OptimizeToolUse = true,
            Stream = (_, _, _, _) => Task.FromResult(++streams switch
            {
                1 => Answer(AiBlock.ToolUse("r1", "read_note", new JsonObject())),
                2 => Answer(AiBlock.ToolUse("w", "mutate_document", new JsonObject())),
                3 => Answer(AiBlock.ToolUse("r2", "read_note", new JsonObject())),
                _ => Answer(AiBlock.TextBlock("Concluído"))
            })
        });
        Assert.Equal(2, reads); Assert.Equal(0, result.Efficiency.ReusedReads);
        Assert.Equal("Novo", messages.SelectMany(m => m.Content).Single(b => b.ToolUseId == "r2").Content);
    }

    [Fact]
    public async Task ExactRepeatedStateStopsAfterOneBoundedRecoveryAttempt()
    {
        var host = new Host(); int streams = 0, reads = 0;
        var read = new AiTool { Name = "read_note", Access = "read", Description = "read", Parameters = new() { ["type"] = "object" }, Label = _ => "Read",
            Run = (_, _) => { reads++; return Task.FromResult(new AiToolOutput("Mesmo conteúdo")); } };
        var result = await AssistantAgent.RunAsync(new()
        {
            Host = host, Model = "test", Provider = new("p", "openai"), Policy = "readonly", Messages = Messages(), Tools = [read], OptimizeToolUse = true,
            Stream = (_, _, _, _) => Task.FromResult(Answer(AiBlock.ToolUse("r" + ++streams, "read_note", new JsonObject())))
        });
        Assert.Equal("no_progress", result.Stopped); Assert.Equal(1, reads); Assert.Equal(3, streams);
    }

    [Fact]
    public async Task RepeatedNoOpGetsOneChanceToRecoverAndExplainInsteadOfAborting()
    {
        var host = new Host(); int streams = 0;
        var write = AssistantTools.Get("write_note")!;
        var result = await AssistantAgent.RunAsync(new()
        {
            Host = host, Model = "test", Provider = new("p", "openai"), Policy = "auto",
            Messages = Messages(), Tools = [write], OptimizeToolUse = true,
            Stream = (_, _, _, _) => Task.FromResult(++streams switch
            {
                1 or 2 => Answer(AiBlock.ToolUse("w" + streams, "write_note",
                    new JsonObject { ["path"] = "A.md", ["content"] = "Original" })),
                _ => Answer(AiBlock.TextBlock("Não precisei regravar a nota; tarefa concluída."))
            })
        });
        Assert.Equal("end", result.Stopped);
        Assert.Equal(3, streams);
        Assert.Equal(0, host.Writes);
    }

    [Fact]
    public async Task NativeRequestedRangeIsExactAndDoesNotForceReadingTheRestOfTheNote()
    {
        var host = new Host();
        host.Documents.Upsert(new() { Id = "a", Path = "A.md", Content = "um\ndois\ntres\nquatro\n" });
        var read = AssistantTools.EfficientRead(AssistantTools.Get("read_note")!);
        var output = await read.Run(new JsonObject { ["path"] = "A.md", ["max_lines"] = 2 }, host);
        Assert.Contains("um", output.Content); Assert.Contains("dois", output.Content); Assert.DoesNotContain("tres", output.Content);
        Assert.Null(Assert.Single(output.Reads!).Partial); Assert.DoesNotContain("LEITURA PARCIAL", output.Content);
    }

    [Fact]
    public void EvidenceReferencesAlwaysPointToContentRetainedInTheSameRequest()
    {
        var text = new string('a', 5000);
        var messages = Messages();
        for (int i = 0; i < 4; i++)
        {
            messages.Add(new() { Role = "assistant", Content = [new() { Type = "tool_use", Id = "read" + i, Name = "read_note", Input = new JsonObject() }] });
            messages.Add(new() { Role = "user", Meta = new() { ToolResults = true }, Content = [new() { Type = "tool_result", ToolUseId = "read" + i, Content = text }] });
        }
        var prepared = AssistantAgent.Prepare(messages, 7000, reuseEvidence: true);
        var results = prepared.SelectMany(m => m.Content).Where(b => b.Type == "tool_result").ToList();
        Assert.Equal(4, results.Count); Assert.Single(results, b => b.Content == text);
        Assert.All(results.Where(b => b.Content != text), b => Assert.Contains("read0", b.Content));
        Assert.All(messages.SelectMany(m => m.Content).Where(b => b.Type == "tool_result"), b => Assert.Equal(text, b.Content));
    }
}
