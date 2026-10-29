using System.Text.Json.Nodes;
using Urbe.Core;
using Urbe.Core.Assistant;

namespace Urbe.Core.Tests;

/// <summary>
/// CST-094/095/096 (owner: measure useful execution as time + work/rework + verified quality, not "answered fast"):
/// a run records times split by where they were spent and counts of calls, failures, edits and checks — never the
/// content of notes or prompts.
/// </summary>
public sealed class AssistantRunMetricsTests
{
    private sealed class Host : IAssistantHost
    {
        public DocumentStore Documents { get; } = new();
        public KnowledgeIndex Knowledge { get; }
        public int ReadChars => 16000;
        public Host() { Documents.Upsert(new() { Id = "n", Path = "Plano.md", Content = "Plano.\n" }); Knowledge = new(Documents); }
        public (string? Path, string? Selection) EditorContext() => (null, null);
        public Task<UrbeDocument> SaveNoteAsync(UrbeDocument doc, string text) => Task.FromResult(Documents.Upsert(new() { Id = doc.Id, Path = doc.Path, Content = text }));
        public Task<UrbeDocument> CreateNoteAsync(string path, string content) => Task.FromResult(Documents.Upsert(new() { Path = path, Content = content }));
        public Task<UrbeDocument> RenameNoteAsync(UrbeDocument doc, string path) => throw new NotSupportedException();
        public Task TrashNoteAsync(UrbeDocument doc) => throw new NotSupportedException();
        public Task CreateFolderAsync(string path) => throw new NotSupportedException();
        public void OpenNote(string path) { }
        public void Remember(string fact) { }
        public void SetPlan(IReadOnlyList<AiPlanStep> steps) { }
    }

    private static AiResult Answer(params AiBlock[] blocks) => new(blocks, "", "end_turn", new(1, 1, null), "test");

    private static async Task<AiAgentResult> Run(Func<AiApprovalRequest, Task<AiApproval>>? approve, params AiResult[] steps)
    {
        var messages = new List<AiChatMessage> { new() { Id = "u", Role = "user", Content = [AiChatBlock.TextBlock("segredo do usuário")] } };
        int step = 0;
        return await AssistantAgent.RunAsync(new()
        {
            Host = new Host(), Agent = AssistantAgent.AgentById("geral"), Policy = approve is null ? "auto" : "ask", Model = "m", Provider = new("p", "openai"),
            Messages = messages, OptimizeToolUse = true, MarkAiText = true,
            Approve = approve is null ? null : (r, _) => approve(r),
            Stream = async (_, _, _, _) => { await Task.Delay(30); return step < steps.Length ? steps[step++] : Answer(AiBlock.TextBlock("fim")); }
        });
    }

    [Fact]
    public async Task ARunRecordsWhereTheTimeWentAndWhatWasDoneAndChecked()
    {
        var r = await Run(null,
            Answer(AiBlock.ToolUse("x", "edit_note", new JsonObject { ["path"] = "Plano.md", ["old_text"] = "nada", ["new_text"] = "y" })),
            Answer(AiBlock.ToolUse("w", "write_note", new JsonObject { ["path"] = "Plano.md", ["content"] = "Plano novo.\n" })),
            Answer(AiBlock.ToolUse("r", "read_note", new JsonObject { ["path"] = "Plano.md" })),
            Answer(AiBlock.TextBlock("Pronto.")));
        var m = r.Efficiency;
        Assert.Equal(4, m.ModelCalls);
        Assert.Equal(3, m.ToolCalls);
        Assert.Equal(1, m.ToolFailures);
        Assert.Equal(1, m.Edits);
        Assert.Equal(1, m.Verified);   // the written note was read again in the run
        Assert.True(m.ProviderMs >= 100, "provider " + m.ProviderMs);
        Assert.True(m.TotalMs >= m.ProviderMs + m.ToolMs);
        Assert.NotNull(m.FirstUsefulMs);
        Assert.True(m.FirstUsefulMs <= m.TotalMs);
        Assert.True(m.Completed);
        // nothing of the conversation's content is in the record
        Assert.DoesNotContain("segredo", System.Text.Json.JsonSerializer.Serialize(m));
    }

    [Fact]
    public async Task WaitingForThePersonIsNotToolTimeAndARefusalIsCounted()
    {
        var r = await Run(async _ => { await Task.Delay(120); return new AiApproval("reject"); },
            Answer(AiBlock.ToolUse("w", "write_note", new JsonObject { ["path"] = "Plano.md", ["content"] = "x\n" })),
            Answer(AiBlock.TextBlock("Ok.")));
        Assert.Equal(1, r.Efficiency.Rejected);
        Assert.True(r.Efficiency.ApprovalMs >= 100, "approval " + r.Efficiency.ApprovalMs);
        Assert.True(r.Efficiency.ToolMs < 100, "tool " + r.Efficiency.ToolMs);
        Assert.Equal(0, r.Efficiency.Edits);
    }

    [Fact]
    public void ManyRunsSummarizeAsMedianP90AndHowMuchWasCompletedReworkedAndChecked()
    {
        AiRunRecord Run(long ms, bool done, int failures, int edits, int verified) => new()
        { Efficiency = new() { TotalMs = ms, Completed = done, ToolFailures = failures, Edits = edits, Verified = verified, ToolCalls = 5 } };
        var stats = AiRunStats.Of([Run(1000, true, 0, 2, 2), Run(2000, true, 1, 1, 0), Run(3000, false, 2, 0, 0), Run(10000, true, 0, 1, 1), new AiRunRecord()]);
        Assert.Equal(4, stats.Runs);   // a run recorded before the measures came has no time and is left out
        Assert.Equal(2500, stats.MedianMs);
        Assert.Equal(10000, stats.P90Ms);
        Assert.Equal(0.75, stats.Completion);
        Assert.Equal(0.75, stats.FailuresPerRun);
        Assert.Equal(0.75, stats.VerifiedShare);
        Assert.Contains("mediana 2,5 s", stats.Describe());
    }

    [Fact]
    public void TheLineUnderARunSaysWhereTheTimeWent()
    {
        var m = new AiExecutionMetrics { TotalMs = 72_000, ProviderMs = 48_000, ToolMs = 9_000, ApprovalMs = 5_000, ToolCalls = 14, ToolFailures = 1, Edits = 3, Verified = 2 };
        Assert.Equal("Levou 1 min 12 s · modelo 48 s · ferramentas 9 s · aguardando você 5 s · 14 chamadas, 1 falhou · 3 edições, 2 arquivos conferidos", m.Describe());
        Assert.Null(new AiExecutionMetrics { ReusedReads = 3 }.Describe());
        var off = m.CountersOnly();
        Assert.Equal(0, off.TotalMs);
        Assert.Null(off.Describe());
    }
}
