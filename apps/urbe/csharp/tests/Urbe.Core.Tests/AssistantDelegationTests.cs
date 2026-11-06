using System.Reflection;
using System.Text.Json.Nodes;
using Urbe.Core.Assistant;

namespace Urbe.Core.Tests;

/// <summary>
/// CST-217: delegating to a configured specialist with a scoped brief. The specialist writes only inside the scope,
/// cannot delegate again, its artifact is read back from the vault (not taken from its words), and an independent
/// review counts only when the reviewer read the real file.
/// </summary>
public sealed class AssistantDelegationTests
{
    private sealed class Host : IAssistantHost, IAssistantDelegation
    {
        private int _seq = 10;
        public Host(params (string Path, string Content)[] notes)
        {
            foreach (var (path, content) in notes) Documents.Upsert(new DocumentInput { Id = "doc_" + _seq++, Path = path, Content = content });
            Knowledge = new KnowledgeIndex(Documents);
        }
        public DocumentStore Documents { get; } = new();
        public KnowledgeIndex Knowledge { get; }
        public int ReadChars => 2000;
        public (string? Path, string? Selection) EditorContext() => (null, null);
        public Task<UrbeDocument> CreateNoteAsync(string path, string content) =>
            Task.FromResult(Documents.Upsert(new DocumentInput { Id = "doc_" + _seq++, Path = path, Content = content }));
        public Task<UrbeDocument> SaveNoteAsync(UrbeDocument doc, string content) =>
            Task.FromResult(Documents.Upsert(new DocumentInput { Id = doc.Id, Path = doc.Path, Content = content }));
        public Task<UrbeDocument> RenameNoteAsync(UrbeDocument doc, string newPath) =>
            Task.FromResult(Documents.Upsert(new DocumentInput { Id = doc.Id, Path = newPath, Content = doc.Content }));
        public Task TrashNoteAsync(UrbeDocument doc) { Documents.Remove(doc.Id); return Task.CompletedTask; }
        public Task CreateFolderAsync(string path) => Task.CompletedTask;
        public void OpenNote(string path) { }
        public void Remember(string fact) { }
        public void SetPlan(IReadOnlyList<AiPlanStep> steps) { }
        public IAssistantDelegation? Delegation => this;
        public IReadOnlyList<AiDelegateTarget> Targets { get; } = [new("escritor", "Escritora", "", false), new("revisor", "Revisor", "", false)];
        public Task<string> ConsultAsync(string agentId, string task, string? conversationId = null) => Task.FromResult("");
        public List<(string Agent, AiDelegationBrief Brief)> Asked { get; } = [];
        public Func<string, AiDelegationBrief, Task<AiDelegationOutcome>>? Work { get; set; }
        public Task<AiDelegationOutcome> DelegateAsync(string agentId, AiDelegationBrief brief) { Asked.Add((agentId, brief)); return Work!(agentId, brief); }
    }

    private static AiChatMessage Call(string id, string name, JsonObject input) => new() { Role = "assistant", Content = [new() { Type = "tool_use", Id = id, Name = name, Input = input }] };
    private static AiChatMessage Result(string id, bool error = false) => new() { Role = "user", Content = [new() { Type = "tool_result", ToolUseId = id, Content = "…", IsError = error }] };

    private static Task<AiToolOutput> Delegate(Host host, JsonObject input) => AssistantDelegateTools.DelegateTo.Run(input, host);

    [Fact]
    public void Brief_AllowsOnlyItsScopeAndReviewersNothing()
    {
        var brief = new AiDelegationBrief("t", ["Projeto/Capítulos", "Notas/Índice.md"], [], "note", "Projeto/Capítulos/Um.md", 40);
        Assert.True(brief.Allows("Projeto/Capítulos/Um.md"));
        Assert.True(brief.Allows("projeto/capítulos/sub/Dois.md"));
        Assert.True(brief.Allows("Notas/Índice"));
        Assert.False(brief.Allows("Projeto/Capítulos.md.bak/x.md"));
        Assert.False(brief.Allows("Projeto/Outro.md"));
        Assert.False(brief.Allows("Notas/Índice 2.md"));
        Assert.False((brief with { Role = AiDelegationBrief.Reviewer }).Allows("Projeto/Capítulos/Um.md"));
        var back = AiDelegationBrief.FromJson(JsonNode.Parse(brief.ToJson().ToJsonString()))!;
        Assert.Equal(brief.Scope, back.Scope);
        Assert.Equal(brief with { Scope = back.Scope, Sources = back.Sources }, back);
        Assert.DoesNotContain("conversa", brief.Message("Assistente").Split('\n')[1]);
    }

    [Fact]
    public async Task ScopedHost_RefusesWritesOutsideTheScopeBeforeWritingAndDoesNotDelegateAgain()
    {
        var inner = new Host(("Dentro/A.md", "a"), ("Fora.md", "f"));
        var host = new AssistantScopedHost(inner, new AiDelegationBrief("t", ["Dentro"], [], "note", "Dentro/A.md", 10));
        await host.SaveNoteAsync(inner.Documents.Get("Dentro/A.md")!, "a2");
        await host.CreateNoteAsync("Dentro/B.md", "b");
        var error = await Assert.ThrowsAsync<AiToolException>(() => host.SaveNoteAsync(inner.Documents.Get("Fora.md")!, "mudou"));
        Assert.Contains("Fora do escopo", error.Message);
        await Assert.ThrowsAsync<AiToolException>(() => host.CreateNoteAsync("Outra/C.md", "c"));
        await Assert.ThrowsAsync<AiToolException>(() => host.RenameNoteAsync(inner.Documents.Get("Dentro/A.md")!, "Fora/A.md"));
        await Assert.ThrowsAsync<AiToolException>(() => host.TrashNoteAsync(inner.Documents.Get("Fora.md")!));
        Assert.Equal("f", inner.Documents.Get("Fora.md")!.Content);
        Assert.Null(inner.Documents.Get("Outra/C.md"));
        Assert.Null(host.Delegation);
        Assert.Null(host.Appearance);
        Assert.Null(host.Plugins);
    }

    [Fact]
    public void ScopedHost_ForwardsEveryCapabilityOfTheAppsHost()
    {
        var declared = typeof(AssistantScopedHost).GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(p => p.Name).ToHashSet();
        var missing = typeof(IAssistantHost).GetProperties().Select(p => p.Name).Where(n => !declared.Contains(n)).ToList();
        Assert.True(missing.Count == 0, "não repassa: " + string.Join(", ", missing));
        var methods = typeof(AssistantScopedHost).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Select(m => m.Name).ToHashSet();
        var missingMethods = typeof(IAssistantHost).GetMethods().Where(m => !m.IsSpecialName).Select(m => m.Name).Where(n => !methods.Contains(n)).ToList();
        Assert.True(missingMethods.Count == 0, "não repassa: " + string.Join(", ", missingMethods));
    }

    [Fact]
    public async Task Delegate_ReadsTheRealArtifactBackFromTheVault()
    {
        var host = new Host(("Fontes/Ideia.md", "ideia"));
        host.Work = async (agent, brief) =>
        {
            var created = await host.CreateNoteAsync(brief.ArtifactPath!, "# Capítulo\n\nTexto real escrito.");
            return new AiDelegationOutcome("c_9", "Escritora", "ARTEFATO: Livro/Cap1.md; JUSTIFICATIVA: usei Fontes/Ideia.md.", [new AiChange(created.Id, null, new AiNoteSnap(created.Path, created.Content))], "end", []);
        };
        var output = await Delegate(host, new JsonObject { ["agent_id"] = "escritor", ["task"] = "Escreva o capítulo 1", ["expected"] = "note", ["artifact_path"] = "Livro/Cap1", ["sources"] = new JsonArray("Fontes/Ideia.md") });
        var (agent, brief) = Assert.Single(host.Asked);
        Assert.Equal("escritor", agent);
        Assert.Equal(["Livro/Cap1.md"], brief.Scope);
        Assert.Equal(["Fontes/Ideia.md"], brief.Sources);
        Assert.Equal(AiDelegationBrief.DefaultSteps, brief.MaxSteps);
        Assert.Contains("ARTEFATO (lido agora do vault): Livro/Cap1.md · revisão " + AssistantRunJournal.Sha("# Capítulo\n\nTexto real escrito.")![..12], output.Content);
        Assert.Contains("Texto real escrito.", output.Content);
        Assert.Contains("criou Livro/Cap1.md", output.Content);
    }

    [Fact]
    public async Task Delegate_AClaimedArtifactThatIsNotInTheVaultIsReportedAsNotDelivered()
    {
        var host = new Host();
        host.Work = (_, _) => Task.FromResult(new AiDelegationOutcome("c_9", "Escritora", "ARTEFATO: Livro/Cap1.md — pronto!", [], "end", []));
        var output = await Delegate(host, new JsonObject { ["agent_id"] = "escritor", ["task"] = "Escreva", ["expected"] = "note", ["artifact_path"] = "Livro/Cap1.md", ["reviewer_id"] = "revisor" });
        Assert.Contains("ARTEFATO NÃO ENTREGUE", output.Content);
        Assert.Contains("Revisão independente não feita", output.Content);
        Assert.Single(host.Asked);
    }

    [Fact]
    public async Task Delegate_AReviewCountsOnlyWhenTheReviewerReadTheRealFile()
    {
        foreach (var reads in new[] { false, true })
        {
            var host = new Host(("Livro/Cap1.md", "# Capítulo\n\nTexto."));
            host.Work = (agent, brief) => Task.FromResult(agent == "escritor"
                ? new AiDelegationOutcome("c_1", "Escritora", "Feito.", [], "end", [])
                : new AiDelegationOutcome("c_2", "Revisor", "PARECER: aprovado", [], "end",
                    reads ? [Call("r1", "read_note", new JsonObject { ["path"] = "Livro/Cap1" }), Result("r1")] : [Call("r1", "search_notes", new JsonObject { ["query"] = "Cap1" }), Result("r1")]));
            var output = await Delegate(host, new JsonObject { ["agent_id"] = "escritor", ["task"] = "Revise o capítulo", ["expected"] = "note", ["artifact_path"] = "Livro/Cap1.md", ["reviewer_id"] = "revisor" });
            var review = host.Asked[1].Brief;
            Assert.True(review.IsReviewer);
            Assert.Empty(review.Scope);
            Assert.Equal(AssistantRunJournal.Sha("# Capítulo\n\nTexto."), review.ReviewSha);
            Assert.Contains(reads ? "REVISÃO INDEPENDENTE de Revisor (leu Livro/Cap1.md, mesma revisão)" : "REVISÃO NÃO ACEITA", output.Content);
        }
    }

    [Fact]
    public async Task Delegate_RefusesUnknownAgentsArtifactsOutsideTheScopeAndSelfReview()
    {
        var host = new Host();
        host.Work = (_, _) => throw new InvalidOperationException("não deveria delegar");
        await Assert.ThrowsAsync<AiToolException>(() => Delegate(host, new JsonObject { ["agent_id"] = "ninguem", ["task"] = "x", ["expected"] = "text" }));
        await Assert.ThrowsAsync<AiToolException>(() => Delegate(host, new JsonObject { ["agent_id"] = "escritor", ["task"] = "x", ["expected"] = "note", ["artifact_path"] = "A.md", ["scope"] = new JsonArray("B") }));
        await Assert.ThrowsAsync<AiToolException>(() => Delegate(host, new JsonObject { ["agent_id"] = "escritor", ["task"] = "x", ["expected"] = "text", ["reviewer_id"] = "escritor" }));
        await Assert.ThrowsAsync<AiToolException>(() => Delegate(host, new JsonObject { ["agent_id"] = "escritor", ["task"] = "x", ["expected"] = "note" }));
        var scoped = new AssistantScopedHost(host, new AiDelegationBrief("t", [], [], "text", null, 5));
        await Assert.ThrowsAsync<AiToolException>(() => AssistantDelegateTools.DelegateTo.Run(new JsonObject { ["agent_id"] = "escritor", ["task"] = "x", ["expected"] = "text" }, scoped));
        Assert.Empty(host.Asked);
    }

    [Fact]
    public void Conversation_KeepsItsBriefAndOldConversationsHaveNone()
    {
        var conv = new AiConversation { Id = "c1", Brief = new AiDelegationBrief("t", ["A"], ["S.md"], "note", "A/X.md", 12) };
        var back = AssistantStore.ConversationFromJson(AssistantStore.ConversationToJson(conv));
        Assert.Equal("A/X.md", back.Brief!.ArtifactPath);
        Assert.Equal(12, back.Brief.MaxSteps);
        var old = AssistantStore.ConversationToJson(new AiConversation { Id = "c2" });
        Assert.False(old.ContainsKey("brief"));
        Assert.Null(AssistantStore.ConversationFromJson(old).Brief);
    }

    [Fact]
    public void AssistantToolsAll_DoesNotCarryTheDelegationTool() =>
        Assert.DoesNotContain(AssistantTools.All, t => t.Name == AssistantDelegateTools.DelegateName);
}
