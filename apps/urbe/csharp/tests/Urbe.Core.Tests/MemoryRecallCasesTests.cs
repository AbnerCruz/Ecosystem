using Urbe.Core.Assistant;

namespace Urbe.Core.Tests;

/// <summary>CST-087: the recall cases the plan names, each one checked; what cannot be found is said, never invented.</summary>
public sealed class MemoryRecallCasesTests
{
    private static AiConfig Vault(params (string Id, string Text, string[] Paths)[] memories)
    {
        var cfg = new AiConfig();
        foreach (var (id, text, paths) in memories)
            cfg.MemoryRecords.Add(new AiMemoryRecord { Id = id, Text = text, Kind = "fact", Scope = "vault", Vault = "V", RelatedPaths = [.. paths] });
        return cfg;
    }

    private static IReadOnlyList<string> Found(AiConfig cfg, string query) =>
        AssistantMemoryEngine.Search(cfg, "V", "geral", query, 5).Select(m => m.Memory.Id).ToList();

    [Theory]
    [InlineData("direta", "Qual é o prazo do relatório?", "prazo")]
    [InlineData("indireta", "Até quando preciso entregar aquele documento anual?", "prazo")]
    [InlineData("plural", "e os relatórios, quando?", "prazo")]
    [InlineData("dois documentos", "o que liga Orçamento.md e Cronograma.md?", "elo")]
    public void ThePertinentMemoryIsFound(string _, string query, string expected)
    {
        var cfg = Vault(
            ("prazo", "O prazo de entrega do relatório anual é 30 de novembro.", []),
            ("elo", "O cronograma depende do orçamento aprovado.", ["Orçamento.md", "Cronograma.md"]),
            ("outra", "A cor preferida da capa é verde.", []));
        Assert.Contains(expected, Found(cfg, query));
    }

    [Fact]
    public void ContradictionsComeTogetherAndAReplacementWins()
    {
        var cfg = Vault(("a", "A reunião é na sala 3.", []), ("b", "A reunião é na sala 5.", []));
        Assert.Equal(["a", "b"], Found(cfg, "em que sala é a reunião?").Order());   // both shown: the agent sees the conflict
        Assert.True(AssistantMemoryEngine.Supersede(cfg, "a", "b"));
        Assert.Equal(["b"], Found(cfg, "em que sala é a reunião?"));             // a changed version: only the current
    }

    [Fact]
    public void ARenamedNoteStillFindsItsMemory()
    {
        var cfg = Vault(("m", "As metas do trimestre estão definidas.", ["Metas.md"]));
        AssistantMemoryEngine.RetargetPaths(cfg, [("Metas.md", "Metas 2027.md")]);
        Assert.Contains("m", Found(cfg, "o que diz Metas 2027?"));
    }

    [Fact]
    public void WithoutAnAnswerNothingIsFound()
    {
        var cfg = Vault(("prazo", "O prazo de entrega do relatório anual é 30 de novembro.", []));
        Assert.Empty(Found(cfg, "qual é a capital da Mongólia?"));
    }

    /// <summary>
    /// A pure synonym ("data limite do informe" for "prazo do relatório") shares no word: the lexical search misses it,
    /// and the empty recall tells the agent so and to try the person's words, which do find it.
    /// </summary>
    [Fact]
    public async Task ASynonymMissIsToldAndTheRetryFindsIt()
    {
        var cfg = Vault(("prazo", "O prazo de entrega do relatório anual é 30 de novembro.", []));
        Assert.Empty(Found(cfg, "Qual a data limite do informe?"));
        var recall = AssistantMemoryTools.All.Single(t => t.Name == "recall_memory");
        var host = new MemoryHost(cfg);
        var miss = await recall.Run(new System.Text.Json.Nodes.JsonObject { ["query"] = "data limite do informe" }, host);
        Assert.Equal(AssistantMemoryTools.NothingFound, miss.Content);
        Assert.Contains("sinônimos", miss.Content);
        var hit = await recall.Run(new System.Text.Json.Nodes.JsonObject { ["query"] = "prazo do relatório" }, host);
        Assert.Contains("30 de novembro", hit.Content);
    }

    private sealed class MemoryHost(AiConfig cfg) : IAssistantHost
    {
        public Urbe.Core.DocumentStore Documents { get; } = new();
        public Urbe.Core.KnowledgeIndex Knowledge => new(Documents);
        public int ReadChars => 4000;
        public (string? Path, string? Selection) EditorContext() => (null, null);
        public Task<Urbe.Core.UrbeDocument> CreateNoteAsync(string path, string content) => throw new NotSupportedException();
        public Task<Urbe.Core.UrbeDocument> SaveNoteAsync(Urbe.Core.UrbeDocument doc, string content) => throw new NotSupportedException();
        public Task<Urbe.Core.UrbeDocument> RenameNoteAsync(Urbe.Core.UrbeDocument doc, string newPath) => throw new NotSupportedException();
        public Task TrashNoteAsync(Urbe.Core.UrbeDocument doc) => throw new NotSupportedException();
        public Task CreateFolderAsync(string path) => throw new NotSupportedException();
        public void OpenNote(string path) { }
        public void Remember(string fact) { }
        public void SetPlan(IReadOnlyList<AiPlanStep> steps) { }
        public IReadOnlyList<AiMemoryMatch> SearchMemory(string query, int limit) => AssistantMemoryEngine.Search(cfg, "V", "geral", query, limit);
    }
}
