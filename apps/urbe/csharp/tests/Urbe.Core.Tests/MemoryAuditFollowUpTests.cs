using System.Text.Json.Nodes;
using Urbe.Core.Assistant;

namespace Urbe.Core.Tests;

/// <summary>Owner's memory audit (beta.54, PR #952), the parts left open: whole episodes and paging without gaps.</summary>
public sealed class MemoryAuditFollowUpTests
{
    [Fact]
    public void AnEpisodeKeepsTheWholeRequestAndEveryChangedFile()
    {
        var cfg = new AiConfig();
        var request = "Reorganize o capítulo três " + string.Concat(Enumerable.Repeat("com cuidado e atenção aos detalhes, ", 12)) + "FIM-DO-PEDIDO\ncom quebra de linha.";
        var paths = Enumerable.Range(1, 20).Select(i => "Livro/Capítulo " + i + " com um nome bem comprido para ver se algo é cortado no caminho.md").ToList();
        var episode = AssistantMemoryEngine.ObserveFinishedRun(cfg, "V", "geral", "r1", paths, request)!;
        Assert.Contains("FIM-DO-PEDIDO com quebra de linha.", episode.Text);
        Assert.DoesNotContain("…", episode.Text);
        Assert.Equal(20, episode.RelatedPaths.Count);
        Assert.All(paths, p => Assert.Contains(p, episode.Text));
    }

    /// <summary>recall_memory read page after page returns every match once, in order, nothing skipped or cut.</summary>
    [Fact]
    public async Task PagingThroughRecallReturnsEveryMemoryExactlyOnce()
    {
        var cfg = new AiConfig();
        for (int i = 0; i < 40; i++)
            cfg.MemoryRecords.Add(new AiMemoryRecord { Id = "m" + i.ToString("00"), Text = "Projeto Atlas, decisão número " + i + ": " + new string('x', 300), Kind = "decision", Scope = "vault", Vault = "V" });
        var host = new Host(cfg);
        var recall = AssistantMemoryTools.All.Single(t => t.Name == "recall_memory");
        var seen = new List<string>();
        var offset = 0;
        for (int page = 0; page < 100; page++)
        {
            var args = new JsonObject { ["query"] = "projeto atlas", ["offset"] = offset, ["max_chars"] = 2000 };
            Assert.Empty(AssistantTools.Validate(recall, args));
            var text = (await recall.Run(args, host)).Content;
            Assert.DoesNotContain("Registro incompleto", text);
            seen.AddRange(System.Text.RegularExpressions.Regex.Matches(text, @"^- (m\d\d) ", System.Text.RegularExpressions.RegexOptions.Multiline).Select(m => m.Groups[1].Value));
            var next = System.Text.RegularExpressions.Regex.Match(text, @"Próximo offset: (\d+)");
            if (!next.Success) { Assert.Contains("Fim das memórias correspondentes.", text); break; }
            Assert.True(int.Parse(next.Groups[1].Value) > offset);
            offset = int.Parse(next.Groups[1].Value);
        }
        Assert.Equal(40, seen.Count);
        Assert.Equal(40, seen.Distinct().Count());
    }

    private sealed class Host(AiConfig cfg) : IAssistantHost
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

/// <summary>Owner's audit, cases 02–08: preferences said in many ways, across conversations, learned and kept straight.</summary>
public sealed class MemoryPreferenceFormsTests
{
    private static IReadOnlyList<AiMemoryRecord> Say(AiConfig cfg, string text, string conversation) =>
        AssistantMemoryEngine.CaptureUserStatements(cfg, "V", "alice", text, "conversation:" + conversation);

    private static List<string> Active(AiConfig cfg) =>
        [.. cfg.MemoryRecords.Where(m => m.State == "active" && m.Kind != "episode").Select(m => m.Text)];

    [Theory]
    [InlineData("Eu gosto de café sem açúcar.")]
    [InlineData("Na verdade, gosto de café sem açúcar.")]
    [InlineData("Mudei de ideia: gosto de café sem açúcar.")]
    [InlineData("Amo café sem açúcar.")]
    [InlineData("Adorei o café sem açúcar de ontem, mas o pão estava duro.")]
    [InlineData("Detesto café com açúcar.")]
    [InlineData("Não suporto café com açúcar.")]
    public void EveryCommonFormIsLearned(string said) =>
        Assert.Single(Say(new AiConfig(), said, "c1"));

    [Fact]
    public void AStrongWordContradictsTheMildOneAcrossConversations()
    {
        var cfg = new AiConfig();
        Say(cfg, "Gosto de strogonoff.", "c1");
        Say(cfg, "Odeio strogonoff.", "c2");                        // no "não", still the opposite
        Assert.Equal(["Odeio strogonoff."], Active(cfg));
        Say(cfg, "Mudei de ideia: voltei a gostar de strogonoff.", "c3");
        Assert.Equal(["Mudei de ideia: voltei a gostar de strogonoff."], Active(cfg));
        Assert.Equal(3, cfg.MemoryRecords.Count);                     // nothing erased: the old ones stay, replaced
        Say(cfg, "Na verdade, não gosto mais de strogonoff.", "c4");
        Assert.Equal(["Na verdade, não gosto mais de strogonoff."], Active(cfg));
    }

    [Fact]
    public void PraiseThenCriticismLearnsThePraiseAndKeepsTheSpecificApart()
    {
        var cfg = new AiConfig();
        Say(cfg, "Adorei o strogonoff, mas o arroz estava sem sal.", "c1");   // case 08
        Say(cfg, "Gosto de strogonoff de frango, mas não de porco.", "c2");   // more specific: refines, both stay
        Assert.Equal(2, Active(cfg).Count);
        var specific = cfg.MemoryRecords.Single(m => m.Text.StartsWith("Gosto de strogonoff de frango", StringComparison.Ordinal));
        Assert.Single(specific.RelatedMemoryIds);
        Say(cfg, "Não gosto de strogonoff.", "c3");                             // the general one turns: replaced
        Assert.Contains("Não gosto de strogonoff.", Active(cfg));
        Assert.DoesNotContain("Adorei o strogonoff, mas o arroz estava sem sal.", Active(cfg));
    }

    [Fact]
    public void DifferentThingsNeverReplaceEachOther()
    {
        var cfg = new AiConfig();
        Say(cfg, "Gosto de café.", "c1");
        Say(cfg, "Não gosto de chá.", "c2");
        Say(cfg, "Odeio acordar cedo.", "c3");
        Assert.Equal(3, Active(cfg).Count);
    }
}

/// <summary>Owner's audit: "the person only sees episodic memories in context". What the person said comes first.</summary>
public sealed class MemoryContextTests
{
    [Fact]
    public void WhatThePersonSaidReachesTheContextBeforeManyRunLogs()
    {
        var cfg = new AiConfig();
        for (int i = 0; i < 60; i++)
            AssistantMemoryEngine.ObserveFinishedRun(cfg, "V", "alice", "r" + i, ["Notas/Resumo " + i + ".md"], "Escreva um resumo da aula " + i);
        AssistantMemoryEngine.CaptureUserStatements(cfg, "V", "alice", "Prefiro respostas curtas, em tópicos.", "conversation:c1");
        AssistantMemoryEngine.CaptureUserStatements(cfg, "V", "alice", "Sempre que eu pedir um resumo, inclua as fontes.", "conversation:c2");
        var prompt = string.Join("\n", AssistantMemoryEngine.ForPromptWithin(cfg, "V", "alice", "Escreva um resumo da aula de hoje", 3000));
        Assert.Contains("Prefiro respostas curtas", prompt);
        Assert.Contains("inclua as fontes", prompt);
    }
}

/// <summary>Owner's audit (UC-87): the memory graph with many memories stays quick to build and complete.</summary>
public sealed class MemoryGraphScaleTests
{
    [Fact]
    public void TwoThousandMemoriesBuildQuicklyWithEveryOneOnTheGraph()
    {
        var cfg = new AiConfig();
        string[] kinds = ["preference", "decision", "procedure", "fact", "episode"];
        for (int i = 0; i < 2000; i++)
            cfg.MemoryRecords.Add(new AiMemoryRecord
            {
                Id = "m" + i, Kind = kinds[i % kinds.Length], Scope = "agent", AgentId = "alice", Vault = "V",
                Text = "Lembrança " + i + " sobre o projeto " + (i % 50), RelatedPaths = [ "Notas/N" + (i % 300) + ".md" ],
                RelatedMemoryIds = i > 0 && i % 7 == 0 ? ["m" + (i - 1)] : []
            });
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var records = MemoryGraph.Records(cfg, "V", "alice");
        var graph = MemoryGraph.Build("Memória", records, AssistantMemoryEngine.PotentialConflicts(cfg, "V", "alice"));
        clock.Stop();
        Assert.Equal(2000, records.Count);
        Assert.True(graph.Nodes.Count >= 2000 + 300, "nós: " + graph.Nodes.Count);   // every memory and every source note
        if (Environment.GetEnvironmentVariable("URBE_PERF") == "1") Assert.True(clock.ElapsedMilliseconds < 1500, clock.ElapsedMilliseconds + " ms");
    }
}
