using System.Text.Json.Nodes;
using Urbe.Core;
using Urbe.Core.Assistant;

namespace Urbe.Core.Tests;

/// <summary>
/// Owner's report (FeaturesCheck, edições longas): the agent repeated edits after exact-match failures, rewrote whole
/// notes from stale content and created inconsistencies. A whole rewrite now needs the current note, read in full;
/// a failed edit shows where the text really is; repeated failures stop the blind retries.
/// </summary>
public sealed class AssistantStaleRewriteTests
{
    private sealed class Host : IAssistantHost
    {
        public DocumentStore Documents { get; } = new();
        public KnowledgeIndex Knowledge { get; }
        public int ReadChars => 16000;
        public Host(string content) { Documents.Upsert(new() { Id = "n", Path = "Dicionário.md", Content = content }); Knowledge = new(Documents); }
        public (string? Path, string? Selection) EditorContext() => (null, null);
        public Task<UrbeDocument> SaveNoteAsync(UrbeDocument doc, string text) => Task.FromResult(Documents.Upsert(new() { Id = doc.Id, Path = doc.Path, Content = text }));
        public Task<UrbeDocument> CreateNoteAsync(string path, string content) => Task.FromResult(Documents.Upsert(new() { Path = path, Content = content }));
        public Task<UrbeDocument> RenameNoteAsync(UrbeDocument doc, string path) => throw new NotSupportedException();
        public Task TrashNoteAsync(UrbeDocument doc) => throw new NotSupportedException();
        public Task CreateFolderAsync(string path) => throw new NotSupportedException();
        public void OpenNote(string path) { }
        public void Remember(string fact) { }
        public void SetPlan(IReadOnlyList<AiPlanStep> steps) { }
        public string Text => Documents.Get("Dicionário.md")!.Content;
    }

    private static AiResult Answer(params AiBlock[] blocks) => new(blocks, "", "end_turn", new(1, 1, null), "test");
    private static AiBlock Read(string id, int? start = null, int? max = null)
    {
        var input = new JsonObject { ["path"] = "Dicionário.md" };
        if (start is not null) input["start_line"] = start;
        if (max is not null) input["max_lines"] = max;
        return AiBlock.ToolUse(id, "read_note", input);
    }
    private static AiBlock Write(string id, string content) => AiBlock.ToolUse(id, "write_note", new JsonObject { ["path"] = "Dicionário.md", ["content"] = content });
    private static AiBlock Edit(string id, string old, string @new) => AiBlock.ToolUse(id, "edit_note", new JsonObject { ["path"] = "Dicionário.md", ["old_text"] = old, ["new_text"] = @new });

    /// <summary>Runs the scripted steps; <paramref name="between"/> runs before each model step (index, host).</summary>
    private static async Task<(List<AiChatMessage> Messages, AiAgentResult Result)> Run(Host host, Action<int, Host>? between, params AiResult[] steps)
    {
        var messages = new List<AiChatMessage> { new() { Id = "u", Role = "user", Content = [AiChatBlock.TextBlock("Revise o dicionário")] } };
        int step = 0;
        var result = await AssistantAgent.RunAsync(new()
        {
            Host = host, Agent = AssistantAgent.AgentById("geral"), Policy = "auto", Model = "m", Provider = new("p", "openai"),
            Messages = messages, OptimizeToolUse = true, MarkAiText = true,
            Tools = [.. AssistantTools.All.Select(AssistantTools.EfficientRead)],
            Stream = (_, _, _, _) =>
            {
                between?.Invoke(step, host);
                return Task.FromResult(step < steps.Length ? steps[step++] : Answer(AiBlock.TextBlock("fim")));
            }
        });
        return (messages, result);
    }

    private static string ResultOf(List<AiChatMessage> messages, string id) =>
        messages.SelectMany(m => m.Content).Single(b => b.Type == "tool_result" && b.ToolUseId == id).Content ?? "";

    private const string Entries = "# Dicionário\n\n- água: líquido\n- casa: moradia\n- livro: obra\n";

    [Fact]
    public async Task ANoteChangedAfterTheReadIsNotRewrittenFromTheOldCopy()
    {
        var host = new Host(Entries);
        var (messages, _) = await Run(host,
            (step, h) => { if (step == 1) h.Documents.Upsert(new() { Id = "n", Path = "Dicionário.md", Content = Entries + "- zebra: animal\n" }); },
            Answer(Read("r1")),
            Answer(Write("w1", "# Dicionário\n\n- água: líquido\n- casa: moradia\n- livro: obra escrita\n")),
            Answer(Read("r2")),
            Answer(Write("w2", "# Dicionário\n\n- água: líquido\n- casa: moradia\n- livro: obra escrita\n- zebra: animal\n")),
            Answer(AiBlock.TextBlock("Pronto.")));
        Assert.Contains("mudou desde a sua última leitura", ResultOf(messages, "w1"));
        Assert.Contains("Nada foi gravado", ResultOf(messages, "w1"));
        // after reading the current version, the same kind of rewrite goes through and keeps the newer entry
        Assert.StartsWith("Reescrita", ResultOf(messages, "w2"));
        Assert.Contains("zebra: animal", AiAuthorship.Strip(host.Text));
        Assert.Contains("obra escrita", AiAuthorship.Strip(host.Text));
    }

    [Fact]
    public async Task AWholeRewriteNeedsTheWholeNote()
    {
        var host = new Host(Entries);
        var (messages, _) = await Run(host, null,
            Answer(Read("r1", 1, 2)),
            Answer(Write("w1", "# Dicionário\n\nnovo\n")),
            Answer(Read("r2", 3)),
            Answer(Write("w2", "# Dicionário\n\n- água: líquido\n- casa: moradia\n- livro: obra\n- zebra: animal\n")),
            Answer(AiBlock.TextBlock("Pronto.")));
        Assert.Contains("só parte", ResultOf(messages, "w1"));
        // the two reads together cover the note: the rewrite is allowed
        Assert.StartsWith("Reescrita", ResultOf(messages, "w2"));
    }

    [Fact]
    public async Task TheAgentsOwnEditsKeepWhatItKnowsCurrent()
    {
        var host = new Host(Entries);
        var (messages, _) = await Run(host, null,
            Answer(Read("r1")),
            Answer(Edit("e1", "- livro: obra", "- livro: obra escrita")),
            Answer(Write("w1", "# Dicionário\n\n- água: líquido\n- casa: moradia\n- livro: obra escrita\n- zebra: animal\n")),
            Answer(AiBlock.TextBlock("Pronto.")));
        Assert.StartsWith("Reescrita", ResultOf(messages, "w1"));
    }

    [Fact]
    public async Task ARewriteWithoutAnyReadStaysAllowed()
    {
        // replacing a note with given text is a deliberate act; the preview, the approval and the history cover it
        var host = new Host(Entries);
        var (messages, _) = await Run(host, null, Answer(Write("w1", "# Dicionário\n\nNovo começo.\n")), Answer(AiBlock.TextBlock("Pronto.")));
        Assert.StartsWith("Reescrita", ResultOf(messages, "w1"));
    }

    [Fact]
    public async Task TheAgentReadsTheNoteWithoutTheSeals()
    {
        var host = new Host("# Dicionário\n\n- água: líquido {ia:\"gpt\"}\n");
        var (messages, _) = await Run(host, null, Answer(Read("r1")), Answer(AiBlock.TextBlock("Pronto.")));
        Assert.DoesNotContain("{ia:", ResultOf(messages, "r1"));
        Assert.Contains("água: líquido", ResultOf(messages, "r1"));
    }

    [Fact]
    public void AFailedEditShowsWhereTheTextReallyIs()
    {
        var content = "# Dicionário\n\n- água: líquido incolor\n- casa: lugar de moradia\n- livro: obra\n";
        var error = Assert.Throws<AiToolException>(() => AssistantTools.EditOrExplain(content, "- casa: local de moradia", "- casa: lar", false));
        Assert.Contains("linha 4", error.Message);
        Assert.Contains("- casa: lugar de moradia", error.Message);
    }

    [Fact]
    public async Task RepeatedFailedEditsStopTheBlindRetries()
    {
        var host = new Host(Entries);
        var (messages, _) = await Run(host, null,
            Answer(Edit("e1", "- casa: lar", "x")),
            Answer(Edit("e2", "- casa:  lar", "x")),
            Answer(Edit("e3", "- casa: o lar", "x")),
            Answer(AiBlock.TextBlock("Não consegui.")));
        var nudge = messages.SelectMany(m => m.Content).FirstOrDefault(b => b.Type == "text" && (b.Text ?? "").Contains("tentativas de alterar"));
        Assert.NotNull(nudge);
        Assert.Contains("Dicionário.md", nudge!.Text);
        Assert.Contains("read_note", nudge.Text);
    }
}
