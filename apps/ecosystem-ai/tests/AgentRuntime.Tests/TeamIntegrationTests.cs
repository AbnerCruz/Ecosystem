using AgentRuntime.Testing;

namespace AgentRuntime.Tests;

public class TeamIntegrationTests
{
    private static WorkspaceSnapshot Initial() => new(0, new Dictionary<string, string> { ["a.txt"] = "base" });
    private static ChangeSet Change(WorkspaceSnapshot basis, string id = "c1", string path = "a.txt", string content = "novo", string task = "t1", string producer = "writer")
    {
        var w = new IsolatedWorkspace(basis); w.Write(path, content); return w.Seal(id, task, producer, "run-" + id);
    }
    private sealed class Gates : IChangeReviewer, ICombinedVerifier, IChangePolicy, IChangeAuthorizer
    {
        public Func<IntegrationCandidate, ChangeReview> Review { get; set; } = c => new(c, "reviewer", true, "conteúdo conferido");
        public Func<IntegrationCandidate, VerificationResult> Verify { get; set; } = c => VerificationResult.Pass(c.Change.TaskId, "combinado conferido", "conteúdo");
        public Func<IntegrationCandidate, ChangeClassification> Policy { get; set; } = _ => new(ChangeDisposition.Automatic, []);
        public Func<IntegrationCandidate, ChangeAuthorization> Authorize { get; set; } = c => new(c, true, "owner");
        public ValueTask<ChangeReview> ReviewAsync(IntegrationCandidate c, CancellationToken ct) => ValueTask.FromResult(Review(c));
        public ValueTask<VerificationResult> VerifyAsync(IntegrationCandidate c, CancellationToken ct) => ValueTask.FromResult(Verify(c));
        public ChangeClassification Classify(IntegrationCandidate c) => Policy(c);
        public ValueTask<ChangeAuthorization> AuthorizeAsync(IntegrationCandidate c, ChangeClassification cl, CancellationToken ct) => ValueTask.FromResult(Authorize(c));
        public ChangeIntegrator Integrator(bool approval = false) => new(Initial(), this, this, this, "owner", approval ? this : null);
    }

    [Fact]
    public void Overlay_e_proposta_selada_nao_alteram_base_nem_candidato_anterior()
    {
        var basis = Initial(); var w = new IsolatedWorkspace(basis); w.Write("a.txt", "v1");
        var c = w.Seal("c", "t", "p", "r"); w.Write("a.txt", "v2");
        Assert.Equal("base", basis.Files["a.txt"]); Assert.Equal("v1", c.Changes[0].After);
        Assert.Throws<NotSupportedException>(() => ((IDictionary<string, string>)basis.Files)["a.txt"] = "adulterado");
    }
    [Theory]
    [InlineData("../escape")][InlineData("/absolute")][InlineData("a//b")][InlineData("a/./b")][InlineData("a\\b")][InlineData("C:foo")]
    public void Caminhos_nao_portateis_falham(string path) => Assert.Throws<ArgumentException>(() => new IsolatedWorkspace(Initial()).Write(path, "x"));

    [Fact]
    public async Task Mudanca_independente_integra_sobre_base_antiga_e_conflito_exige_retrabalho()
    {
        var g = new Gates(); var i = g.Integrator(); var basis = i.Current;
        Assert.Equal(IntegrationStatus.Integrated, (await i.IntegrateAsync(Change(basis), default)).Status);
        var conflict = await i.IntegrateAsync(Change(basis, "c2", content: "outro"), default);
        Assert.Equal(IntegrationStatus.Conflict, conflict.Status); Assert.Equal(["a.txt"], conflict.Conflicts);
        Assert.Equal(IntegrationStatus.Integrated, (await i.IntegrateAsync(Change(basis, "c3", "b.txt"), default)).Status);
        Assert.Equal(IntegrationStatus.Integrated, (await i.IntegrateAsync(Change(i.Current, "c4", content: "corrigido"), default)).Status);
        Assert.Equal("corrigido", i.Current.Files["a.txt"]); Assert.Equal(3, i.Current.Revision);
    }
    [Theory]
    [InlineData("self")][InlineData("stale")][InlineData("failed")][InlineData("no-evidence")]
    public async Task Revisao_propria_antiga_reprovada_ou_sem_evidencia_bloqueia(string kind)
    {
        var g = new Gates { Review = c => new(kind == "stale" ? c with { Id = c.Id } : c, kind == "self" ? "writer" : "reviewer", kind != "failed", kind == "no-evidence" ? "" : "e") };
        var i = g.Integrator(); Assert.Equal(IntegrationStatus.ReviewFailed, (await i.IntegrateAsync(Change(i.Current), default)).Status);
        Assert.Equal(0, i.Current.Revision);
    }
    [Theory]
    [InlineData("failed")][InlineData("wrong-task")][InlineData("empty")]
    public async Task Verificacao_do_combinado_falha_fechado(string kind)
    {
        var g = new Gates { Verify = c => new(kind == "wrong-task" ? "wrong" : c.Change.TaskId, kind != "failed", kind == "empty" ? "" : "e", []) };
        var i = g.Integrator(); Assert.Equal(IntegrationStatus.VerificationFailed, (await i.IntegrateAsync(Change(i.Current), default)).Status);
        Assert.Equal("base", i.Current.Files["a.txt"]);
    }
    [Theory]
    [InlineData("missing")][InlineData("actor")][InlineData("candidate")][InlineData("denied")]
    public async Task Critico_exige_autorizacao_do_proprietario_para_candidato_exato(string kind)
    {
        var g = new Gates { Policy = _ => new(ChangeDisposition.OwnerAuthorization, ["security"]),
            Authorize = c => new(kind == "candidate" ? c with { Id = c.Id } : c, kind != "denied", kind == "actor" ? "writer" : "owner") };
        var i = g.Integrator(kind != "missing");
        Assert.Equal(IntegrationStatus.Escalated, (await i.IntegrateAsync(Change(i.Current), default)).Status); Assert.Equal(0, i.Current.Revision);
    }
    [Fact]
    public async Task Autorizacao_nao_contorna_revogacao_de_politica()
    {
        var calls = 0;
        var g = new Gates { Policy = _ => new(++calls == 1 ? ChangeDisposition.OwnerAuthorization : ChangeDisposition.Deny, ["security"]) };
        var i = g.Integrator(true); Assert.Equal(IntegrationStatus.Denied, (await i.IntegrateAsync(Change(i.Current), default)).Status);
        Assert.Equal(0, i.Current.Revision);
    }
    [Fact]
    public async Task Cancelamento_depois_da_verificacao_nao_publica_nem_consume_id()
    {
        using var ct = new CancellationTokenSource(); var g = new Gates { Verify = c => { ct.Cancel(); return VerificationResult.Pass(c.Change.TaskId, "e"); } };
        var i = g.Integrator(); var c = Change(i.Current);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await i.IntegrateAsync(c, ct.Token));
        Assert.Equal(0, i.Current.Revision); g.Verify = x => VerificationResult.Pass(x.Change.TaskId, "e");
        Assert.Equal(IntegrationStatus.Integrated, (await i.IntegrateAsync(c, default)).Status);
        Assert.Equal(IntegrationStatus.AlreadyIntegrated, (await i.IntegrateAsync(c, default)).Status);
    }
    [Fact]
    public async Task Duas_submissoes_concorrentes_sao_reconciliadas_sem_perda()
    {
        var i = new Gates().Integrator(); var basis = i.Current;
        var results = await Task.WhenAll(i.IntegrateAsync(Change(basis), default).AsTask(), i.IntegrateAsync(Change(basis, "c2", "b.txt"), default).AsTask());
        Assert.All(results, r => Assert.Equal(IntegrationStatus.Integrated, r.Status));
        Assert.Equal(2, i.Current.Revision); Assert.Equal(2, i.Current.Files.Count);
    }
    private static TeamAssignment Assignment(string id, params string[] dependencies) => new(new(id, "produzir", ["conteúdo"]), "team", "writer", "reviewer", dependencies);
    [Fact]
    public void Plano_recusa_ciclo_dependencia_ausente_e_autorrevisao()
    {
        Assert.Throws<ArgumentException>(() => new TeamPlan([Assignment("a", "b"), Assignment("b", "a")]));
        Assert.Throws<ArgumentException>(() => new TeamPlan([Assignment("a", "missing")]));
        Assert.Throws<ArgumentException>(() => new TeamPlan([Assignment("a") with { ReviewerId = "writer" }]));
    }
    [Fact]
    public async Task Dependencia_so_libera_depois_de_integracao_real()
    {
        var plan = new TeamPlan([Assignment("t1"), Assignment("t2", "t1")]);
        var g = new Gates { Review = c => new(c, "reviewer", false, "precisa corrigir") }; var i = g.Integrator();
        var receipt = await i.IntegrateAsync(Change(i.Current), default);
        Assert.Throws<InvalidOperationException>(() => plan.Record(receipt)); Assert.Equal("t1", Assert.Single(plan.Ready).Task.Id);
        g.Review = c => new(c, "reviewer", true, "ok"); plan.Record(await i.IntegrateAsync(Change(i.Current, "retry"), default));
        Assert.Equal("t2", Assert.Single(plan.Ready).Task.Id);
    }

    private sealed class RuntimeReviewer : IChangeReviewer
    {
        public int Calls { get; private set; }
        public async ValueTask<ChangeReview> ReviewAsync(IntegrationCandidate c, CancellationToken ct)
        {
            Calls++;
            var rig = new Rig();
            rig.Probe("artifact.read", behavior: (_, _) => ToolOutcome.Ok(c.Combined.Files["a.txt"]));
            rig.Provider.ThenToolUse("read", "artifact.read", "{}").ThenText("revisado");
            rig.Verifier = new DelegateVerifier((task, _) => (c.Combined.Files["a.txt"] == "produto" ? VerificationResult.Pass(task.Id, "leitura conferida") : VerificationResult.Fail(task.Id, "conteúdo incorreto")));
            var request = Rig.Request(Rig.Grant(["artifact.read"])) with { RunId = "review-" + c.Id, Task = new(c.Change.TaskId, "revisar", ["produto"]),
                Agent = Rig.Request().Agent with { Identity = new("reviewer", "Revisor", "review") } };
            request = request with { Agent = request.Agent with { Grant = Rig.Grant(["artifact.read"]) } };
            var run = await rig.Runner().RunAsync(request, ct);
            return new(c, "reviewer", run.State.Status == RunStatus.Succeeded, run.Verification?.Evidence ?? "");
        }
    }
    [Fact]
    public async Task Dois_agentes_do_runtime_produzem_revisam_e_integram_sem_escrever_no_canonico()
    {
        var reviewer = new RuntimeReviewer(); var g = new Gates();
        var i = new ChangeIntegrator(Initial(), reviewer, g, g, "owner"); var isolated = new IsolatedWorkspace(i.Current);
        var rig = new Rig();
        rig.Probe("artifact.write", risk: RiskClass.Write, permissions: [Permissions.FsWrite], behavior: (_, _) => { isolated.Write("a.txt", "produto"); return ToolOutcome.Ok("produzido", new ArtifactRef("a", "text", "a.txt")); });
        rig.Provider.ThenToolUse("write", "artifact.write", "{}").ThenText("pronto");
        rig.Verifier = new DelegateVerifier((task, _) => (isolated.Read("a.txt") == "produto" ? VerificationResult.Pass(task.Id, "overlay conferido") : VerificationResult.Fail(task.Id, "ausente")));
        var request = Rig.Request(Rig.Grant(["artifact.write"])) with { Task = new("t1", "produzir", ["produto"]),
            Agent = Rig.Request(Rig.Grant(["artifact.write"])).Agent with { Identity = new("writer", "Produtor", "write") } };
        var result = await rig.Runner().RunAsync(request); Assert.Equal(RunStatus.Succeeded, result.State.Status);
        Assert.Equal("base", i.Current.Files["a.txt"]);
        var receipt = await i.IntegrateAsync(isolated.Seal("c1", "t1", "writer", request.RunId), default);
        Assert.Equal(IntegrationStatus.Integrated, receipt.Status); Assert.Equal("produto", i.Current.Files["a.txt"]); Assert.Equal(1, reviewer.Calls);
    }
}
