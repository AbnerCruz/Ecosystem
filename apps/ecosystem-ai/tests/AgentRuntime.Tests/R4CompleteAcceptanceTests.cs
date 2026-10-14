using EcosystemAi.Cli;
using EcosystemAi.ProjectStore;

namespace AgentRuntime.Tests;

public sealed class R4CompleteAcceptanceTests
{
    [Fact]
    public void Solicitação_preserva_critério_após_objetivo_longo_sem_alterar_planejamento()
    {
        var task = new PlannedTaskEntry("task", "project", "session", "Título",
            new string('a', 5000), "A última condição deve chegar ao revisor.",
            "default", DateTimeOffset.UtcNow, false, []);
        var request = CliPlannedTaskRequest.Compose(task);
        Assert.Contains(task.Goal, request);
        Assert.EndsWith(task.Acceptance, request);
        Assert.True(CliWebTaskRunner.TryComposeReviewGoal(request, "Resultado completo", out var review));
        Assert.Contains(request, review);
        Assert.Contains("Resultado completo", review);
        Assert.Contains("\nSOLICITAÇÃO ORIGINAL COMPLETA:\n", review);
        Assert.Empty(task.Attempts);
    }

    [Fact]
    public void Excesso_não_corta_critério_nem_resultado_para_fabricar_revisão()
    {
        var task = new PlannedTaskEntry("task", "project", "session", "Título",
            new string('a', CliWebTaskRunner.MaxGoalLength), "Critério", "default",
            DateTimeOffset.UtcNow, false, []);
        Assert.Throws<ArgumentException>(() => CliPlannedTaskRequest.Compose(task));
        Assert.False(CliWebTaskRunner.TryComposeReviewGoal("Objetivo",
            new string('r', CliWebTaskRunner.MaxGoalLength), out var request));
        Assert.Empty(request);
    }

    [Fact]
    public async Task Resultado_longo_mantém_recibo_produtor_sem_gastar_com_parecer_parcial()
    {
        var root = Path.Combine(Path.GetTempPath(), "acceptance-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "workspace"));
        try
        {
            var catalog = Path.Combine(root, "catalog");
            var store = new LocalProjectStore(catalog);
            var project = store.CreateProject("Projeto", Path.Combine(root, "workspace"));
            var session = store.CreateSession(project.Id, "Sessão");
            var roster = new LocalAgentRosterStore(catalog);
            var producer = roster.CreateAgent(project.Id, "Produtor", "Produzir");
            var reviewer = roster.CreateAgent(project.Id, "Revisor", "Revisar");
            var team = roster.CreateTeam(project.Id, "Equipe", producer.Id, reviewer.Id);
            var calls = 0;
            var settings = new WebTaskSettings("http://127.0.0.1:59999/v1/chat/completions",
                "mock", 20, 5, 1, 2, 80, 4, ReviewTeams: true);
            var runner = new CliWebTaskRunner(catalog, null, settings, args =>
            {
                calls++;
                store.AppendTurn(project.Id, session.Id, "user", args[Array.IndexOf(args, "--goal") + 1]);
                store.CompleteRun(project.Id, session.Id,
                    new RunReceipt("producer-run", "succeeded", 1, "USD", true,
                        "resposta presente", DateTimeOffset.UtcNow), new string('r', 16000));
                return Task.FromResult(0);
            });
            var result = await runner.SubmitAsync(project.Id, session.Id,
                new string('g', 1000) + "Critério no fim", "team:" + team.Id);
            Assert.Equal(WebTaskState.ReviewIncomplete, result.State);
            Assert.Equal(1, calls);
            Assert.Equal(20, runner.Board.ReservedCents);
            Assert.Single(store.Read().Projects.Single().Sessions.Single().Runs);
            Assert.Empty(new LocalTeamReviewStore(catalog).Read().Reviews);

            var refused = await runner.SubmitAsync(project.Id, session.Id,
                new string('g', CliWebTaskRunner.MaxGoalLength), "team:" + team.Id);
            Assert.Equal(WebTaskState.Invalid, refused.State);
            Assert.Equal(1, calls);
            Assert.Equal(20, runner.Board.ReservedCents);
        }
        finally { Directory.Delete(root, true); }
    }
}
