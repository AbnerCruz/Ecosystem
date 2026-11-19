using System.Text.Json.Nodes;
using Urbe.Core;
using Urbe.Core.Assistant;

namespace Urbe.Core.Tests;

/// <summary>
/// Conversa real da beta.58: o modelo escrevia o vídeo inteiro numa só chamada, a resposta era cortada pelo limite de
/// saída e chegava sem os argumentos ("falta spec"); a execução também parava a cada texto cortado, e a pessoa tinha de
/// escrever "Continue de onde parou". Uma chamada cortada não roda e o modelo sabe por quê; um texto cortado continua.
/// </summary>
public sealed class AssistantOutputLimitTests
{
    private sealed class Host : IAssistantHost
    {
        public DocumentStore Documents { get; } = new();
        public KnowledgeIndex Knowledge { get; }
        public int ReadChars => 16000;
        public Host() { Documents.Upsert(new() { Path = "A.md", Content = "# A\n\nTexto.\n" }); Knowledge = new(Documents); }
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

    private static async Task<(AiAgentResult Result, List<AiRequest> Seen)> Run(Host host, params AiResult[] replies)
    {
        var queue = new Queue<AiResult>(replies);
        var seen = new List<AiRequest>();
        var result = await AssistantAgent.RunAsync(new AiAgentRun
        {
            Agent = AssistantAgent.AgentById("geral"), Policy = "auto", Host = host, Cancel = CancellationToken.None,
            Messages = [new() { Id = "u", Role = "user", Content = [AiChatBlock.TextBlock("Crie a nota B")] }],
            Provider = new AiProviderConfig("p", ""), Model = "m",
            Stream = (_, request, _, _) => { seen.Add(request); return Task.FromResult(queue.Dequeue()); },
        });
        return (result, seen);
    }

    private static string LastToolResult(AiRequest request) =>
        string.Join("\n", request.Messages[^1].Content.Where(b => b.Type is "tool_result" or "text").Select(b => b.Content ?? b.Text));

    [Fact]
    public async Task ACallCutByTheOutputLimitIsNotRunAndTheModelIsToldToSplitIt()
    {
        var host = new Host();
        var (result, seen) = await Run(host,
            // the arguments arrived incomplete: only the path
            new AiResult([AiBlock.ToolUse("t1", "create_note", new JsonObject { ["path"] = "B.md" })], "", "max_tokens", new AiUsage(1, 1, null), "m"),
            new AiResult([AiBlock.ToolUse("t2", "create_note", new JsonObject { ["path"] = "B.md", ["content"] = "# B\n" })], "", "tool_use", new AiUsage(1, 1, null), "m"),
            new AiResult([AiBlock.TextBlock("Pronto.")], "", "end", new AiUsage(1, 1, null), "m"));
        Assert.Contains("cortada pelo limite de saída", LastToolResult(seen[1]));
        Assert.Contains("divida o trabalho em chamadas menores", LastToolResult(seen[1]));
        Assert.Equal("# B\n", host.Documents.Get("B.md")!.Content);
        Assert.NotEqual("max_tokens", result.Stopped);
    }

    [Fact]
    public async Task AnAnswerCutByTheOutputLimitGoesOnWithoutThePersonAsking()
    {
        var (result, seen) = await Run(new Host(),
            new AiResult([AiBlock.TextBlock("Primeira parte")], "", "max_tokens", new AiUsage(1, 1, null), "m"),
            new AiResult([AiBlock.TextBlock(" e o fim.")], "", "end", new AiUsage(1, 1, null), "m"));
        Assert.Equal(2, seen.Count);
        Assert.Contains("Continue exatamente de onde parou", string.Join("\n", seen[1].Messages.SelectMany(m => m.Content).Select(b => b.Text ?? b.Content)));
        Assert.NotEqual("max_tokens", result.Stopped);
    }

    [Fact]
    public async Task TwoEmptyCutsInARowStop()
    {
        // a model that spends its whole output thinking and never writes: the run ends instead of looping forever
        var (result, seen) = await Run(new Host(),
            new AiResult([], "pensando…", "max_tokens", new AiUsage(1, 1, null), "m"),
            new AiResult([], "pensando…", "max_tokens", new AiUsage(1, 1, null), "m"));
        Assert.Equal(2, seen.Count);
        Assert.Equal("max_tokens", result.Stopped);
    }
}
