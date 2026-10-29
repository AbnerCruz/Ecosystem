using System.Text.Json.Nodes;
using Urbe.Core;
using Urbe.Core.Assistant;

namespace Urbe.Core.Tests;

/// <summary>
/// CST-089: an agent looks up its own recent conversations (summaries, then one in detail), never another agent's,
/// and never the text the Urbe keeps hidden (internal reviews, system context).
/// </summary>
public sealed class AssistantConversationToolsTests
{
    private sealed class History(IReadOnlyList<AiConversation> mine) : IAssistantConversations
    {
        public IReadOnlyList<AiConversation> Recent() => mine;
    }

    private static AiChatMessage User(string text) => new() { Id = Guid.NewGuid().ToString("N"), Role = "user", Content = [AiChatBlock.TextBlock(text)] };
    private static AiChatMessage Assistant(params AiChatBlock[] blocks) => new() { Id = Guid.NewGuid().ToString("N"), Role = "assistant", Content = [.. blocks] };
    private static long Ms(int day, int hour) => new DateTimeOffset(2026, 10, day, hour, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();

    private static readonly IReadOnlyList<AiConversation> Mine =
    [
        new()
        {
            Id = "c2", Title = "Dicionário do protoidioma", AgentId = "escritor", Created = Ms(27, 9), Updated = Ms(27, 10),
            Messages =
            [
                User("Revise os verbetes do dicionário A"),
                Assistant(new AiChatBlock { Type = "tool_use", Id = "t", Name = "edit_note", Input = new JsonObject { ["path"] = "Dicionário A.md" } }),
                new() { Id = "r", Role = "user", Meta = new AiChatMeta { ToolResults = true }, Content = [new AiChatBlock { Type = "tool_result", ToolUseId = "t", Content = "Editada: Dicionário A.md." }] },
                Assistant(AiChatBlock.TextBlock("Revisei 42 verbetes em [[Dicionário A]]."), new AiChatBlock { Type = "text", Text = "[Revisão interna] segredo da revisão", Hidden = true }),
            ],
            Runs = [new() { Id = "run", Changes = [new AiChange("d", new AiNoteSnap("Dicionário A.md", "a"), new AiNoteSnap("Dicionário A.md", "b"))] }]
        },
        new()
        {
            Id = "c1", Title = "Plano de estudos", AgentId = "escritor", Created = Ms(20, 8), Updated = Ms(20, 9),
            Messages = [User("Monte um plano de estudos de física"), Assistant(AiChatBlock.TextBlock("Montei o plano em três fases."))]
        },
    ];

    private static Task<AiToolOutput> Run(AiTool tool, JsonObject input, IAssistantConversations? history) =>
        tool.Run(input, new FakeHost(history));

    private sealed class FakeHost(IAssistantConversations? history) : IAssistantHost
    {
        public DocumentStore Documents { get; } = new();
        public KnowledgeIndex Knowledge => new(Documents);
        public int ReadChars => 16000;
        public IAssistantConversations? Conversations => history;
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
    public async Task RecentConversationsComeAsSummariesNewestFirst()
    {
        var text = (await Run(AssistantConversationTools.List, new JsonObject(), new History(Mine))).Content;
        Assert.True(text.IndexOf("Dicionário do protoidioma", StringComparison.Ordinal) < text.IndexOf("Plano de estudos", StringComparison.Ordinal));
        Assert.Contains("id: c2", text);
        Assert.Contains("Pedido: Revise os verbetes do dicionário A", text);
        Assert.Contains("Resposta: Revisei 42 verbetes em [[Dicionário A]].", text);
        Assert.Contains("Arquivos alterados: Dicionário A.md", text);
        Assert.Contains("27/10/2026", text);
        Assert.DoesNotContain("segredo da revisão", text);
    }

    [Fact]
    public async Task AQueryKeepsOnlyTheConversationsAboutIt()
    {
        var text = (await Run(AssistantConversationTools.List, new JsonObject { ["query"] = "fisica" }, new History(Mine))).Content;
        Assert.Contains("Plano de estudos", text);
        Assert.DoesNotContain("Dicionário do protoidioma", text);
        Assert.Contains("Nenhuma", (await Run(AssistantConversationTools.List, new JsonObject { ["query"] = "astronomia" }, new History(Mine))).Content);
    }

    [Fact]
    public async Task OneConversationIsReadInDetailWithoutWhatIsHidden()
    {
        var text = (await Run(AssistantConversationTools.Read, new JsonObject { ["id"] = "c2" }, new History(Mine))).Content;
        Assert.Contains("Usuário: Revise os verbetes do dicionário A", text);
        Assert.Contains("[ferramenta edit_note: Dicionário A.md]", text);
        Assert.Contains("Assistente: Revisei 42 verbetes", text);
        Assert.DoesNotContain("segredo da revisão", text);
        Assert.DoesNotContain("Editada: Dicionário A.md.", text);
    }

    [Fact]
    public async Task AnotherAgentsConversationIsNotThere()
    {
        // the host gives only the run's agent's conversations: an id from elsewhere is unknown
        var error = await Assert.ThrowsAsync<AiToolException>(() => Run(AssistantConversationTools.Read, new JsonObject { ["id"] = "de-outro" }, new History(Mine)));
        Assert.Contains("não está entre as suas", error.Message);
        await Assert.ThrowsAsync<AiToolException>(() => Run(AssistantConversationTools.List, new JsonObject(), null));
    }
}
