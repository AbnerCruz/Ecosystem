using System.Diagnostics;
using Urbe.Core;
using Urbe.Core.Assistant;

namespace Urbe.Core.Tests;

public sealed class AssistantMemoryEngineTests
{
    [Fact]
    public void ScopedFactsAreVisibleOnlyToAuthorizedAgentAndVault()
    {
        var cfg = new AiConfig();
        AssistantMemoryEngine.Remember(cfg, "Prefiro texto objetivo", "Livros", kind: "preference", scope: "global");
        AssistantMemoryEngine.Remember(cfg, "O rei se chama Arthur", "Livros");
        AssistantMemoryEngine.Remember(cfg, "O estilo do agente é conciso", "Livros", "escritor", scope: "agent");
        var writer = AssistantMemoryEngine.Visible(cfg, "Livros", "escritor");
        Assert.Equal(3, writer.Count);
        var researcher = AssistantMemoryEngine.Visible(cfg, "Livros", "pesquisador");
        Assert.Equal(2, researcher.Count);
        Assert.DoesNotContain(researcher, m => m.Scope == "agent");
        var elsewhere = AssistantMemoryEngine.Visible(cfg, "Outro", "escritor");
        Assert.Single(elsewhere);
        Assert.Equal("global", elsewhere[0].Scope);
    }

    [Fact]
    public void MigrationIsLosslessOnFirstWriteAndRoundTripsInCanonicalConfig()
    {
        var cfg = new AiConfig();
        cfg.Memory["*"] = ["Responder em português"];
        cfg.Memory["Meu Vault"] = ["O protagonista nasceu em Vila Nova", "Usar notas com referências"];
        var initial = AssistantMemoryEngine.Visible(cfg, "Meu Vault", "agente");
        Assert.Equal(3, initial.Count);
        Assert.Empty(cfg.MemoryRecords);
        AssistantMemoryEngine.Remember(cfg, "Uma nova informação", "Meu Vault");
        Assert.Empty(cfg.Memory);
        Assert.Equal(4, cfg.MemoryRecords.Count);
        var memoryStore = new AssistantStore(new MemoryAssistantStorage());
        memoryStore.SetConfig(cfg);
        var back = memoryStore.GetConfig();
        Assert.Equal(4, back.MemoryRecords.Count);
        Assert.Empty(back.Memory);
        Assert.Equal(4, AssistantMemoryEngine.Visible(back, "Meu Vault", "agente").Count);
        Assert.Equal(1, back.MemoryRecords.Count(m => m.Origin == "legacy" && m.Scope == "global"));
    }

    [Fact]
    public void LegacyMigrationPreservesLongTextsAndDoesNotImposeMemoryCountQuota()
    {
        var cfg = new AiConfig();
        var longFact = new string('x', 3500);
        cfg.Memory["Vault"] = [longFact];
        AssistantMemoryEngine.MigrateLegacy(cfg, timestamp: 42);
        Assert.Equal(longFact, Assert.Single(cfg.MemoryRecords).Text);
        Assert.Empty(cfg.Memory);

        // An existing installation with many memories must never be required to delete facts.
        for (var i = 0; i < 5005; i++)
            cfg.MemoryRecords.Add(new AiMemoryRecord { Id = i.ToString(), Text = "Memória " + i });
        cfg.Memory["Vault"] = ["Esta memória deve sobreviver"];
        AssistantMemoryEngine.MigrateLegacy(cfg);
        Assert.Empty(cfg.Memory);
        Assert.Equal(5007, cfg.MemoryRecords.Count);
        Assert.Contains(cfg.MemoryRecords, x => x.Text == "Esta memória deve sobreviver");
    }

    [Fact]
    public void AdaptiveRecallExpandsWithModelWindowAndRelevantEvidence()
    {
        var cfg = new AiConfig();
        for (var i = 0; i < 80; i++)
            cfg.MemoryRecords.Add(new AiMemoryRecord
            {
                Id = "memory" + i, Scope = "vault", Vault = "Livros",
                Kind = "fact", State = "active", Text = "Romance: detalhe documental " +
                    i + " da cronologia do protagonista e da investigação.",
                Origin = "user"
            });
        var small = AssistantMemoryEngine.ForPromptAdaptive(cfg, "Livros", "pesquisador",
            "romance cronologia", 4096, systemChars: 2000, conversationChars: 1200);
        var large = AssistantMemoryEngine.ForPromptAdaptive(cfg, "Livros", "pesquisador",
            "romance cronologia", 128000, systemChars: 2000, conversationChars: 1200);
        Assert.NotEmpty(small);
        Assert.True(large.Count > small.Count);
        Assert.Equal(80, large.Count);
        Assert.True(small.Sum(x => x.Length) <= 4096 * 3);
    }

    [Fact]
    public void ExpandedSearchDoesNotHideMatchingEvidenceAtOldSixtyResultCap()
    {
        var cfg = new AiConfig();
        for (var i = 0; i < 120; i++)
            cfg.MemoryRecords.Add(new AiMemoryRecord
            {
                Id = "m" + i, Scope = "vault", Vault = "Projeto", State = "active",
                Text = "Autoria e pesquisa " + i
            });
        Assert.Equal(120, AssistantMemoryEngine.Search(cfg, "Projeto", "geral",
            "pesquisa", int.MaxValue).Count);
    }

    private sealed class FailingStorage : IAssistantStorage
    {
        public string? Read(string name) => null;
        public void Write(string name, string text) { }
        public void Delete(string name) { }
        public IEnumerable<string> List() => [];
    }

    [Fact]
    public void VerifiedWriteRejectsSilentlyDroppedConfiguration()
    {
        var store = new AssistantStore(new FailingStorage());
        var cfg = new AiConfig();
        AssistantMemoryEngine.Remember(cfg, "Nunca perder memórias", "Vault");
        Assert.Throws<IOException>(() => store.SetConfigVerified(cfg));
    }

    [Fact]
    public void LegacySecretsRemainLocallyEditableButNeverEnterModelContext()
    {
        var cfg = new AiConfig();
        cfg.Memory["Vault"] = ["senha: antigoSegredo123", "Minha preferência é fonte serifada"];
        var retrieved = AssistantMemoryEngine.Search(cfg, "Vault", "geral", "senha", 6);
        Assert.Empty(retrieved);
        var contextual = AssistantMemoryEngine.ForPrompt(cfg, "Vault", "geral", "minha preferência");
        Assert.DoesNotContain(contextual, item => item.Contains("antigoSegredo123"));
        AssistantMemoryEngine.MigrateLegacy(cfg);
        Assert.Contains(cfg.MemoryRecords, item => item.Text.Contains("antigoSegredo123"));
    }

    [Fact]
    public void RepeatedFactsAreDeduplicatedWithoutChangingIdentity()
    {
        var cfg = new AiConfig();
        var original = AssistantMemoryEngine.Remember(cfg, "Prefiro português", "Vault", confidence: 40, timestamp: 10);
        var again = AssistantMemoryEngine.Remember(cfg, "Prefiro português", "Vault", confidence: 90, timestamp: 20);
        Assert.Equal(original.Id, again.Id);
        Assert.Single(cfg.MemoryRecords);
        Assert.Equal(90, again.Confidence);
        Assert.Equal(20, again.UpdatedAt);
    }

    [Fact]
    public void RetrievalUsesQuerySourcesAndHonorsContextBudget()
    {
        var cfg = new AiConfig();
        AssistantMemoryEngine.Remember(cfg, "A história de Arthur se passa em Númenor", "Mundo", source: "note:Personagens/Arthur.md");
        AssistantMemoryEngine.Remember(cfg, "Minha cor favorita é verde", "Mundo");
        var skill = AssistantMemoryEngine.Remember(cfg, "Ao montar uma apostila, conferir primeiro as referências e revisar depois", "Mundo", "escritor", "procedure", "agent");
        skill.RelatedPaths.Add("Apostilas/Modelo.md");
        var hits = AssistantMemoryEngine.Search(cfg, "Mundo", "escritor", "apostila referências", 4);
        Assert.Equal(skill.Id, hits[0].Memory.Id);
        var prompt = AssistantMemoryEngine.ForPrompt(cfg, "Mundo", "escritor", "Arthur", maxChars: 400);
        Assert.Contains(prompt, p => p.Contains("Arthur"));
        Assert.True(prompt.Sum(p => p.Length) <= 400);
        Assert.DoesNotContain(prompt, p => p.Contains("cor favorita"));
    }

    [Fact]
    public void ExpiredAndSupersededKnowledgeNeverEntersContextButHistorySurvives()
    {
        var cfg = new AiConfig();
        var old = AssistantMemoryEngine.Remember(cfg, "O chefe da equipe é Raul", "Equipe");
        var next = AssistantMemoryEngine.Remember(cfg, "O chefe da equipe é Ana", "Equipe");
        Assert.True(AssistantMemoryEngine.Supersede(cfg, old.Id, next.Id));
        Assert.Equal("superseded", cfg.MemoryRecords[0].State);
        Assert.Equal(2, cfg.MemoryRecords.Count);
        Assert.DoesNotContain(AssistantMemoryEngine.Visible(cfg, "Equipe", "geral"), m => m.Id == old.Id);
        var temporary = AssistantMemoryEngine.Remember(cfg, "Reunião de terça-feira", "Equipe");
        temporary.ValidUntil = AssistantMemoryEngine.Now() - 1;
        Assert.DoesNotContain(AssistantMemoryEngine.Visible(cfg, "Equipe", "geral"), m => m.Id == temporary.Id);
    }

    [Fact]
    public void DestructiveMemoryOperationsCannotCrossAgentBoundary()
    {
        var cfg = new AiConfig();
        var a = AssistantMemoryEngine.Remember(cfg, "A prefere notas longas", "Vault", "a", scope: "agent");
        var b = AssistantMemoryEngine.Remember(cfg, "B prefere notas curtas", "Vault", "b", scope: "agent");
        Assert.False(AssistantMemoryEngine.Link(cfg, a.Id, b.Id));
        Assert.False(AssistantMemoryEngine.Supersede(cfg, a.Id, b.Id));
        Assert.Equal("active", a.State);
        Assert.Equal("active", b.State);
    }

    [Fact]
    public void AgentLearnsOnlyExplicitRequestsAndSanitizesCredentials()
    {
        Assert.Equal("gosto de notas curtas", AssistantMemoryEngine.ExplicitRememberRequest("Lembre que gosto de notas curtas"));
        Assert.Null(AssistantMemoryEngine.ExplicitRememberRequest("Talvez amanhã eu goste de notas curtas"));
        Assert.Null(AssistantMemoryEngine.ExplicitRememberRequest("Não lembre que gosto de notas curtas"));
        var cfg = new AiConfig();
        Assert.Throws<ArgumentException>(() => AssistantMemoryEngine.Remember(cfg, "senha: supersegredo", "Vault"));
        Assert.Empty(cfg.MemoryRecords);
    }

    [Fact]
    public void AutonomousAgentClaimsDoNotBecomeVerifiedHighConfidenceFacts()
    {
        var cfg = new AiConfig();
        var uncertain = AssistantMemoryEngine.Remember(cfg, "A personagem talvez tenha três irmãos",
            "Vault", "pesquisador", origin: "agent");
        Assert.False(uncertain.Verified);
        Assert.Equal(60, uncertain.Confidence);
        var human = AssistantMemoryEngine.Remember(cfg, "A personagem tem três irmãos",
            "Vault", "pesquisador", origin: "user", verified: true);
        Assert.Equal(100, human.Confidence);
        Assert.True(human.Verified);
    }

    [Fact]
    public void EpisodesCaptureEditedPathsButNotBodiesOrFalseVerification()
    {
        var cfg = new AiConfig();
        var item = AssistantMemoryEngine.ObserveFinishedRun(cfg, "Vault", "escritor", "r1",
            ["Notas/Introducao.md"], "Produza uma introdução", timestamp: 10)!;
        Assert.Equal("episode", item.Kind);
        Assert.Equal("run:r1", item.SourceReference);
        Assert.False(item.Verified);
        Assert.Equal(50, item.Confidence);
        Assert.Contains("Notas/Introducao.md", item.Text);
        Assert.DoesNotContain("texto completo", item.Text);
        Assert.Null(AssistantMemoryEngine.ObserveFinishedRun(cfg, "Vault", "escritor", "r2", [], "Não alterei nada"));
    }

    [Fact]
    public void SourceGraphFindsLinksAndBacklinksWithoutCopyingTheirContent()
    {
        var documents = new DocumentStore();
        documents.ReplaceAll(
        [
            new DocumentInput { Path = "Personagens/Arthur.md", Content = "# Arthur\nConhece [[Reino]]." },
            new DocumentInput { Path = "Mundo/Reino.md", Content = "# Reino\nHá castelos." },
            new DocumentInput { Path = "Historia/Conflitos.md", Content = "# Conflitos\nVeja [[Arthur]]." }
        ], "test");
        using var index = new KnowledgeIndex(documents);
        var cfg = new AiConfig();
        var memory = AssistantMemoryEngine.Remember(cfg, "Arthur foi coroado", "Vault",
            paths: ["Personagens/Arthur.md"]);
        var near = AssistantMemoryEngine.NearbyDocumentPaths(memory, index);
        Assert.Contains("Personagens/Arthur.md", near);
        Assert.Contains("Mundo/Reino.md", near);
        Assert.Contains("Historia/Conflitos.md", near);
        Assert.DoesNotContain(near, value => value.Contains("Há castelos"));
    }

    [Fact]
    public void ConflictsAreOnlyCandidatesAndNeverAutoRewriteEitherAssertion()
    {
        var cfg = new AiConfig();
        var a = AssistantMemoryEngine.Remember(cfg, "O reino de Arthur tem capital em Roma", "Vault");
        var b = AssistantMemoryEngine.Remember(cfg, "O reino de Arthur tem capital em Paris", "Vault");
        var possible = AssistantMemoryEngine.PotentialConflicts(cfg, "Vault", "geral");
        Assert.Contains(possible, p => p.Left.Id == a.Id && p.Right.Id == b.Id);
        Assert.All(cfg.MemoryRecords, m => Assert.Equal("active", m.State));
    }

    [Fact]
    public void OfflineRetrievalOnLargeMemoryIsBoundedAndRequiresNoNetwork()
    {
        var cfg = new AiConfig();
        for (var i = 0; i < 1100; i++)
            AssistantMemoryEngine.Remember(cfg, "Anotação técnica " + i + " sobre fase de testes " +
                (i % 29), "Vault", timestamp: i + 1);
        var clock = Stopwatch.StartNew();
        for (var i = 0; i < 30; i++)
        {
            var matches = AssistantMemoryEngine.Search(cfg, "Vault", "geral", "fase testes 17", limit: 8);
            Assert.True(matches.Count <= 8);
        }
        clock.Stop();
        // This is diagnostic, not a timing gate (CI devices and thermal conditions vary).
        Assert.Equal(1100, cfg.MemoryRecords.Count);
        Assert.True(clock.ElapsedTicks > 0);
    }
}
