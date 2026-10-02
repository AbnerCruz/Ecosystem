using Hub.Core;

namespace Hub.Tests;

public class TimelineTests
{
    const string Roadmap = """
    ## Fase 1 — Base

    - [x] P1-1 — **Primeiro**: feito.
      - Saída: x. Depende de: —. Evidência: y.
    - [x] P1-2 — **Segundo**.
      - Saída: x. Depende de: P1-1. Evidência: y.

    **Gate:** tudo pronto.
    *Estado do gate:* **aprovado** — ok.

    ## Fase 2 — Meio

    - [~] P2-1 — **Em validação**.
      - Depende de: P1-2. Evidência: z.
    - [ ] P2-2 — **Pronto para começar**.
      - Saída: x. Depende de: P2-1, DEC-0001 (registrada). Evidência: y.
    - [ ] P2-3 — **Depende de vários**.
      - Saída: x. Depende de: P1-1..P1-2, P2-2. Evidência: y.

    **Gate:** o meio funciona.
    *Estado do gate:* **aguardando**

    ## Fase 3 — Fim

    - [ ] P3-1 — **Só depois**.
      - Depende de: P2-3.

    **Gate:** fim.

    ## Fase 4 — Futuro sem itens

    **Gate:** ainda não detalhado.
    """;

    const string Decisions = """
    { "decisions": [
      { "id": "DEC-0001", "status": "decided", "title": "Já decidida", "decidedAt": "2026-10-01" },
      { "id": "DEC-0002", "status": "pending", "title": "Espera o proprietário", "blocking": true },
      { "id": "DEC-0003", "status": "pending", "title": "Não bloqueia" },
      { "status": "pending" }
    ] }
    """;

    static (string, string) H(string id, string task, string state, string verification = "[]") =>
        ($"docs/governance/handoffs/{id}.json", $$"""{ "message_id": "{{id}}", "task_id": "{{task}}", "state": "{{state}}", "verification": {{verification}} }""");

    static PastNowNext Build(params (string, string)[] handoffs) => TimelineBuilder.Build(Roadmap, Decisions, handoffs);

    [Fact]
    public void ReadsItemsStatesAndTitles()
    {
        var items = RoadmapReader.ReadItems(Roadmap);
        Assert.Equal(["P1-1", "P1-2", "P2-1", "P2-2", "P2-3", "P3-1"], items.Select(i => i.Id));
        Assert.Equal([ItemState.Done, ItemState.Done, ItemState.Verified, ItemState.Open, ItemState.Open, ItemState.Open], items.Select(i => i.State));
        Assert.Equal("Em validação", items[2].Title);
    }

    [Fact]
    public void ParsesDependenciesIncludingRangesAndIgnoringNonTasks()
    {
        var items = RoadmapReader.ReadItems(Roadmap).ToDictionary(i => i.Id);
        Assert.Empty(items["P1-1"].DependsOn);
        Assert.Equal(["P2-1"], items["P2-2"].DependsOn); // DEC-0001 não é tarefa
        Assert.Equal(["P1-1", "P1-2", "P2-2"], items["P2-3"].DependsOn);
    }

    [Fact]
    public void ReadsGateStatesWithNotStartedAsDefault()
    {
        var g = RoadmapReader.ReadGates(Roadmap);
        Assert.Equal([GateState.Approved, GateState.Waiting, GateState.NotStarted, GateState.NotStarted], g.Select(x => x.State));
        Assert.Equal("Base", g[0].Title);
    }

    [Fact]
    public void PastHasDoneItemsApprovedGatesAndDecidedDecisions()
    {
        var past = Build().Past.Select(e => e.Id).ToArray();
        Assert.Equal(["P1-1", "P1-2", "gate-fase-1", "DEC-0001"], past);
    }

    [Fact]
    public void NowHasVerifiedItemsWaitingGatesAndPendingDecisions()
    {
        var now = Build().Now;
        Assert.Equal(["P2-1", "gate-fase-2", "DEC-0002", "DEC-0003"], now.Select(e => e.Id));
        Assert.Equal("aguardando o proprietário (bloqueante)", now.Single(e => e.Id == "DEC-0002").Detail);
    }

    [Fact]
    public void NextHasOnlyOpenRoadmapItemsAndGatesOfPhasesThatHaveItems()
    {
        var next = Build().Next;
        Assert.Equal(["P2-2", "P2-3", "P3-1", "gate-fase-3"], next.Select(e => e.Id));
        Assert.DoesNotContain(next, e => e.Id == "gate-fase-4"); // fase futura sem itens: não se fabrica
    }

    [Fact]
    public void NextSaysWhetherAnItemIsReadyOrWhatItWaitsFor()
    {
        var next = Build().Next.ToDictionary(e => e.Id);
        Assert.Equal("depende de P2-1", next["P2-2"].Detail);   // P2-1 está [~], não concluída
        Assert.Equal("depende de P2-2", next["P2-3"].Detail);   // P1-1 e P1-2 estão concluídas
        Assert.Equal("depende de P2-3", next["P3-1"].Detail);
    }

    [Fact]
    public void ReadyMeansEveryDependencyIsDone()
    {
        var roadmap = "- [x] P1-1 — **A**.\n- [ ] P1-2 — **B**.\n  - Depende de: P1-1.\n";
        var next = TimelineBuilder.Build(roadmap, """{ "decisions": [] }""", []).Next;
        Assert.Equal("pronta", Assert.Single(next).Detail);
    }

    [Fact]
    public void HandoffsInProgressAreNowAndDoneArePast()
    {
        var r = Build(H("HO-a", "P2-1", "review"), H("HO-b", "P1-1", "done"), H("HO-c", "P1-2", "cancelled"));
        Assert.Contains(r.Now, e => e.Id == "HO-a" && e.Kind == EntryKind.Handoff);
        Assert.Contains(r.Past, e => e.Id == "HO-b");
        Assert.DoesNotContain(r.Now.Concat(r.Past), e => e.Id == "HO-c");
    }

    [Fact]
    public void PendingHumanValidationAppearsInNow()
    {
        var v = """[ { "check": "Abrir no aparelho", "kind": "human", "result": "pending" }, { "check": "Teste", "kind": "automated", "result": "passed" } ]""";
        var human = Build(H("HO-d", "P2-1", "review", v)).Now.Where(e => e.Kind == EntryKind.HumanValidation).ToArray();
        Assert.Equal("Abrir no aparelho", Assert.Single(human).Title);
    }

    [Fact]
    public void BrokenSourcesYieldEmptyResultsNotExceptions()
    {
        var r = TimelineBuilder.Build("", "não é json", [("x.json", "{ quebrado"), ("y.json", "[]"), ("z.json", """{ "state": "done" }""")]);
        Assert.Empty(r.Past); Assert.Empty(r.Now); Assert.Empty(r.Next);
    }

    [Fact]
    public void RealRepositoryNextOnlyContainsWhatTheRoadmapDeclares()
    {
        string? root = null;
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "MANIFEST.md")) && File.Exists(Path.Combine(d.FullName, "ROADMAP.md"))) { root = d.FullName; break; }
        Assert.NotNull(root);

        var roadmap = File.ReadAllText(Path.Combine(root!, "ROADMAP.md"));
        var handoffs = Directory.EnumerateFiles(Path.Combine(root!, "docs", "governance", "handoffs"), "*.json")
            .Select(f => (Path.GetRelativePath(root!, f).Replace('\\', '/'), File.ReadAllText(f))).ToList();
        var r = TimelineBuilder.Build(roadmap, File.ReadAllText(Path.Combine(root!, "docs", "governance", "decisions.json")), handoffs);

        var open = RoadmapReader.ReadItems(roadmap).Where(i => i.State == ItemState.Open).Select(i => i.Id).ToHashSet();
        var gates = RoadmapReader.ReadGates(roadmap).Select(g => $"gate-fase-{g.Phase}").ToHashSet();
        Assert.NotEmpty(r.Past);
        Assert.NotEmpty(r.Next);

        // Faixas do ROADMAP real ("P3-4..P3-6") são expandidas: P3-7 depende de P3-5, que está no meio da faixa.
        var p37 = RoadmapReader.ReadItems(roadmap).Single(i => i.Id == "P3-7");
        Assert.Contains("P3-5", p37.DependsOn);
        Assert.All(r.Next, e => Assert.True(open.Contains(e.Id) || gates.Contains(e.Id), $"Next contém '{e.Id}', que o ROADMAP não declara"));
        Assert.All(r.Next, e => Assert.Equal("ROADMAP.md", e.Source));
    }
}
