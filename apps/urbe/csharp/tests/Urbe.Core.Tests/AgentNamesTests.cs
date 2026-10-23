using System.Text.Json.Nodes;
using Urbe.Core;
using Urbe.Core.Assistant;

namespace Urbe.Core.Tests;

/// <summary>UC-81: the agents' call names, on this device.</summary>
public sealed class AgentNamesTests
{
    private static AiConfig Config() => new() { CustomAgents = [new AiAgentDef("a1", "Revisor", "sparkle", "", "write", false, "Revise.")] };

    [Fact]
    public void ANameIsGivenFoundByAnyCaseAndAccentAndKeptOnlyOnTheDevice()
    {
        var c = Config();
        Assert.Equal("Assistente", AgentNames.CallName(AssistantAgent.Agents[0], c));
        Assert.Null(AgentNames.Set(c, "geral", "Jarvis"));
        Assert.Equal("Jarvis", AgentNames.CallName(AssistantAgent.Agents[0], c));
        Assert.Equal("geral", AgentNames.Find("jarvis", c)?.Id);
        Assert.Equal("geral", AgentNames.Find(" JARVIS ", c)?.Id);
        Assert.Equal("a1", AgentNames.Find("revisor", c)?.Id);
        Assert.Equal("pesquisador", AgentNames.Find("Pesquisador", c)?.Id);
        Assert.Null(AgentNames.Find("Sexta", c));
        // the id never changes; the agent introduces itself by its call name
        var named = AgentNames.Named(AssistantAgent.AgentById("geral"), c);
        Assert.Equal(("geral", "Jarvis"), (named.Id, named.Name));
        // kept in the Assistente's configuration (this device), and read back
        var back = AssistantStore.ConfigFromJson(AssistantStore.ConfigToJson(c));
        Assert.Equal("Jarvis", back.AgentNames["geral"]);
        // its own name (or nothing) gives the default back
        Assert.Null(AgentNames.Set(c, "geral", "Assistente"));
        Assert.False(c.AgentNames.ContainsKey("geral"));
    }

    [Fact]
    public void NamesAreUniqueAmongTheAgents()
    {
        var c = Config();
        Assert.Null(AgentNames.Set(c, "escritor", "Machado"));
        Assert.Equal("O agente Machado já usa esse nome.", AgentNames.Set(c, "geral", "machado"));
        Assert.Equal("O agente Revisor já usa esse nome.", AgentNames.Set(c, "geral", "Revisór"));
        Assert.Equal("O agente Pesquisador já usa esse nome.", AgentNames.Set(c, "geral", "Pesquisador"));
        Assert.NotNull(AgentNames.Problem("1ª", "geral", c));
        Assert.NotNull(AgentNames.Problem(new string('a', 25), "geral", c));
        Assert.Null(AgentNames.Problem("Sexta-Feira", "geral", c));
    }

    [Fact]
    public async Task ThePromptTellsTheNameOnlyWhenAskedAndTheToolRenamesAfterTheYes()
    {
        var c = Config();
        var agent = AssistantAgent.AgentById("geral");
        var plain = AssistantAgent.SystemPrompt(agent, "ask", "V", 1, null, null, null, new DateTime(2026, 10, 22));
        Assert.DoesNotContain("## Seu nome", plain);
        var naming = AgentNames.Naming(agent, c);
        var prompt = AssistantAgent.SystemPrompt(AgentNames.Named(agent, c), "ask", "V", 1, null, null, null, new DateTime(2026, 10, 22), naming);
        Assert.StartsWith(plain, prompt);
        Assert.Contains("## Seu nome\nSeu nome é Assistente. Os outros agentes do usuário são: Pesquisador, Escritor, Organizador, Revisor.", prompt);
        Assert.Contains("set_my_name", prompt);
        // UC-77: the app's guidance (an apostila becomes a page in the book format), after the 1.8.4's prompt
        var guided = AssistantAgent.SystemPrompt(agent, "ask", "V", 1, null, null, null, new DateTime(2026, 10, 22), guidance: AssistantAgent.UrbeGuidance);
        Assert.StartsWith(plain, guided);
        Assert.Contains("## No Urbe\n- Quando o pedido for uma apostila", guided);
        Assert.Contains("modelo book", guided);

        var host = new Host(c, "geral");
        var done = await AgentNames.Tool.Run(new JsonObject { ["name"] = "Jarvis" }, host);
        Assert.Equal("Pronto: agora você se chama Jarvis.", done.Content);
        Assert.Equal("Jarvis", c.AgentNames["geral"]);
        var refused = await Assert.ThrowsAsync<AiToolException>(() => AgentNames.Tool.Run(new JsonObject { ["name"] = "Revisor" }, host));
        Assert.Equal("O agente Revisor já usa esse nome.", refused.Message);
        Assert.Equal("memory", AgentNames.Tool.Access);
    }

    private sealed class Host(AiConfig config, string agentId) : IAssistantHost, IAssistantNames
    {
        private readonly DocumentStore _documents = new();
        public DocumentStore Documents => _documents;
        public KnowledgeIndex Knowledge => new(_documents);
        public (string? Path, string? Selection) EditorContext() => (null, null);
        public int ReadChars => 2000;
        public Task<UrbeDocument> CreateNoteAsync(string path, string content) => throw new NotSupportedException();
        public Task<UrbeDocument> SaveNoteAsync(UrbeDocument doc, string content) => throw new NotSupportedException();
        public Task<UrbeDocument> RenameNoteAsync(UrbeDocument doc, string newPath) => throw new NotSupportedException();
        public Task TrashNoteAsync(UrbeDocument doc) => throw new NotSupportedException();
        public Task CreateFolderAsync(string path) => throw new NotSupportedException();
        public void OpenNote(string path) { }
        public void Remember(string fact) { }
        public void SetPlan(IReadOnlyList<AiPlanStep> steps) { }
        public IAssistantNames? Names => this;
        public string? Rename(string name) => AgentNames.Set(config, agentId, name);
    }
}
