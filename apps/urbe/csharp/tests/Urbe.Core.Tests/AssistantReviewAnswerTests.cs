using System.Text.Json.Nodes;
using Urbe.Core;
using Urbe.Core.Assistant;

namespace Urbe.Core.Tests;

/// <summary>
/// CST-079: the mandatory review of written work stays internal; the answer the person sees says what was done.
/// Owner's report: after the review, the final answer only said the work was reviewed and the quality confirmed.
/// </summary>
public sealed class AssistantReviewAnswerTests
{
    private sealed class Host : IAssistantHost
    {
        public DocumentStore Documents { get; } = new();
        public KnowledgeIndex Knowledge { get; }
        public int ReadChars => 16000;
        public Host() { Documents.Upsert(new() { Id = "n", Path = "Plano.md", Content = "Plano antigo.\n" }); Knowledge = new(Documents); }
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

    private static async Task<(List<AiChatMessage> Messages, string? ReviewPrompt)> Run(params AiResult[] steps)
    {
        var messages = new List<AiChatMessage> { new() { Id = "u", Role = "user", Content = [AiChatBlock.TextBlock("Reescreva o plano")] } };
        int step = 0;
        string? review = null;
        await AssistantAgent.RunAsync(new()
        {
            Host = new Host(), Agent = AssistantAgent.AgentById("geral"), Policy = "auto", Model = "m", Provider = new("p", "openai"),
            Messages = messages, OptimizeToolUse = true, MarkAiText = true, ReviewWrittenArtifacts = true,
            Stream = (_, request, _, _) =>
            {
                var last = request.Messages.LastOrDefault()?.Content.LastOrDefault(b => b.Type == "text")?.Text;
                if (last?.StartsWith("[Revisão obrigatória", StringComparison.Ordinal) == true) review = last;
                return Task.FromResult(step < steps.Length ? steps[step++] : Answer(AiBlock.TextBlock("fim")));
            }
        });
        return (messages, review);
    }

    private static string Visible(AiChatMessage m) => string.Join("\n", m.Content.Where(b => b.Type == "text" && !b.Hidden).Select(b => b.Text));

    [Fact]
    public async Task AReviewThatChangesNothingLeavesTheAnswerAboutTheWork()
    {
        var (messages, review) = await Run(
            Answer(AiBlock.ToolUse("w", "write_note", new JsonObject { ["path"] = "Plano.md", ["content"] = "# Plano\n\nTrês etapas novas com custos e prazos.\n" })),
            Answer(AiBlock.TextBlock("Reescrevi [[Plano]] com três etapas, custos e prazos.")),
            Answer(AiBlock.ToolUse("r", "read_note", new JsonObject { ["path"] = "Plano.md" })),
            Answer(AiBlock.TextBlock("Revisei o arquivo salvo e a qualidade está confirmada.")));
        Assert.NotNull(review);
        var final = messages.Last(m => m.Role == "assistant");
        Assert.NotEqual(true, final.Meta?.Draft);
        Assert.Equal("Reescrevi [[Plano]] com três etapas, custos e prazos.", Visible(final));
        // the review's own words stay internal, never shown as the answer
        Assert.DoesNotContain(messages.Where(m => m.Role == "assistant" && m.Meta?.Draft != true), m => Visible(m).Contains("qualidade está confirmada"));
    }

    [Fact]
    public async Task AReviewThatFixesSomethingAsksForAFullFinalAnswer()
    {
        var (messages, review) = await Run(
            Answer(AiBlock.ToolUse("w", "write_note", new JsonObject { ["path"] = "Plano.md", ["content"] = "# Plano\n\nEtapas.\n" })),
            Answer(AiBlock.TextBlock("Reescrevi o plano.")),
            Answer(AiBlock.ToolUse("e", "edit_note", new JsonObject { ["path"] = "Plano.md", ["old_text"] = "Etapas.", ["new_text"] = "Etapas com custos e prazos detalhados por fase." })),
            Answer(AiBlock.TextBlock("Reescrevi [[Plano]] e acrescentei custos e prazos por fase.")));
        Assert.Contains("o que foi feito", review);
        Assert.Contains("Não fale da revisão", review);
        Assert.Equal("Reescrevi [[Plano]] e acrescentei custos e prazos por fase.", Visible(messages.Last(m => m.Role == "assistant")));
    }
}
