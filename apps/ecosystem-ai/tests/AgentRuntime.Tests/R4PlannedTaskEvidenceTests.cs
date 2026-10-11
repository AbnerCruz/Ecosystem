using System.Security.Cryptography;
using System.Text;
using EcosystemAi.Cli;
using EcosystemAi.ProjectStore;

namespace AgentRuntime.Tests;

public sealed class R4PlannedTaskEvidenceTests
{
    [Fact]
    public void Historico_de_recibos_nao_virou_aceite_e_detecta_deriva_sem_mutar_estado()
    {
        var root = Path.Combine(Path.GetTempPath(), "r4-evidence-" + Guid.NewGuid().ToString("N"));
        var workspace = Path.Combine(root, "workspace");
        Directory.CreateDirectory(workspace);
        try
        {
            var catalog = Path.Combine(root, "catalog");
            var store = new LocalProjectStore(catalog);
            var project = store.CreateProject("Projeto", workspace);
            var session = store.CreateSession(project.Id, "Sessão");
            var taskStore = new LocalPlannedTaskStore(catalog);
            var task = taskStore.Create(project.Id, session.Id, "Inspecionar",
                "Leia README.md", "Demonstrar evidência", "default");
            store.AppendTurn(project.Id, session.Id, "user", task.Goal);
            store.CompleteRun(project.Id, session.Id,
                new RunReceipt("run-a", "succeeded", 2, "USD", true, "presença", DateTimeOffset.UtcNow),
                "Resposta A");
            taskStore.RecordAttempt(task.Id, "response_verified", ["run-a"]);
            var source = store.Read();
            var plan = taskStore.Read();
            var persisted = Assert.Single(plan.Tasks);
            var attempt = Assert.Single(persisted.Attempts);
            var live = Assert.Single(Assert.Single(source.Projects).Sessions);
            Assert.True(CliPlannedTaskEvidence.IsCurrent(persisted, attempt, live, null));
            var html = CliWebUiHtml.Render(source, new string('A', 64), planned: plan);
            Assert.Contains("Resposta presente e verificada", html);
            Assert.Contains("Critério informado não significa aceite automático", html);

            var withoutReceipt = live with { Runs = [] };
            Assert.False(CliPlannedTaskEvidence.IsCurrent(persisted, attempt, withoutReceipt, null));
            var unverified = live with { Runs = [live.Runs[0] with { Verified = false }] };
            Assert.False(CliPlannedTaskEvidence.IsCurrent(persisted, attempt, unverified, null));
            var edited = new ProjectCatalog(source.Revision,
                [source.Projects[0] with { Sessions = [withoutReceipt] }]);
            var warning = CliWebUiHtml.Render(edited, new string('A', 64), planned: plan);
            Assert.Contains(System.Text.Encodings.Web.HtmlEncoder.Default.Encode(
                "Evidência histórica não confirmada no catálogo atual"), warning);
            Assert.DoesNotContain("Resposta presente e verificada", warning);
            Assert.Equal(plan.Revision, taskStore.Read().Revision); // GET e avaliação não gravam nada
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void Parecer_precisa_de_duas_execucoes_e_dos_hashes_atuais_de_respostas()
    {
        var now = DateTimeOffset.UtcNow;
        var pid = Guid.NewGuid().ToString("N");
        var sid = Guid.NewGuid().ToString("N");
        var task = new PlannedTaskEntry(Guid.NewGuid().ToString("N"),
            pid, sid, "Revisão", "Objetivo", "Critério", "default", now, false, []);
        var attempt = new PlannedTaskAttempt(Guid.NewGuid().ToString("N"),
            "review_recorded", ["run-producer", "run-reviewer"], now);
        var session = new SessionEntry(sid, "S", now,
            [new ConversationTurn("assistant", "Produção", now),
                new ConversationTurn("assistant", "Revisão", now)],
            [new RunReceipt("run-producer", "succeeded", 1, "USD", true, "ok", now),
                new RunReceipt("run-reviewer", "succeeded", 1, "USD", true, "ok", now)]);
        static string Hash(string text) => Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
        var review = new LocalTeamReview(Guid.NewGuid().ToString("N"), pid, sid,
            Guid.NewGuid().ToString("N"), "producer", "reviewer", "run-producer",
            "run-reviewer", 0, 1, Hash("Produção"), Hash("Revisão"),
            "pending_owner", null, now, null);
        var board = new TeamReviewBoard(1, [review]);
        Assert.True(CliPlannedTaskEvidence.IsCurrent(task, attempt, session, board));
        Assert.False(CliPlannedTaskEvidence.IsCurrent(task, attempt, session, null));
        Assert.False(CliPlannedTaskEvidence.IsCurrent(task, attempt, session,
            new TeamReviewBoard(0, [])));
        Assert.False(CliPlannedTaskEvidence.IsCurrent(task, attempt,
            session with { Turns = [session.Turns[0], session.Turns[1] with { Text = "Alterada" }] },
            board));
        Assert.False(CliPlannedTaskEvidence.IsCurrent(task, attempt,
            session with { Runs = [session.Runs[0], session.Runs[1] with { Verified = false }] },
            board));
        Assert.False(CliPlannedTaskEvidence.IsCurrent(task, attempt, session,
            new TeamReviewBoard(1, [review with { ProjectId = Guid.NewGuid().ToString("N") }])));
        Assert.False(CliPlannedTaskEvidence.IsCurrent(task, attempt, session,
            new TeamReviewBoard(1, [review with { ReviewerHash = Hash("outro") }])));
    }
}
