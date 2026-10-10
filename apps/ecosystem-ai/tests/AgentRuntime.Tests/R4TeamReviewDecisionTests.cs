using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using EcosystemAi.Cli;
using EcosystemAi.ProjectStore;

namespace AgentRuntime.Tests;

public sealed class R4TeamReviewDecisionTests
{
    private static string Root() => Path.Combine(Path.GetTempPath(),
        "r4-decisions-" + Guid.NewGuid().ToString("N"));

    private static (LocalProjectStore Store, ProjectEntry Project, SessionEntry Session,
        LocalAgentTeam Team, LocalAgentProfile Producer, LocalAgentProfile Reviewer) Seed(string root)
    {
        var work = Path.Combine(root, "workspace");
        Directory.CreateDirectory(work);
        var store = new LocalProjectStore(Path.Combine(root, "catalog"));
        var project = store.CreateProject("P", work);
        var session = store.CreateSession(project.Id, "Sessão");
        var roster = new LocalAgentRosterStore(Path.Combine(root, "catalog"));
        var producer = roster.CreateAgent(project.Id, "Produtor", "Entregar texto");
        var reviewer = roster.CreateAgent(project.Id, "Revisor", "Revisar texto");
        var team = roster.CreateTeam(project.Id, "Equipe C#", producer.Id, reviewer.Id);
        return (store, project, session, team, producer, reviewer);
    }

    private static void AppendRun(LocalProjectStore store, string project, string session,
        string runId, string answer)
    {
        store.AppendTurn(project, session, "user", "Tarefa de teste");
        store.CompleteRun(project, session,
            new RunReceipt(runId, "succeeded", 3, "USD", true,
                "resposta-presente", DateTimeOffset.UtcNow), answer);
    }

    private static int Port()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try { return ((IPEndPoint)listener.LocalEndpoint).Port; }
        finally { listener.Stop(); }
    }

    [Fact]
    public async Task Fluxo_de_dupla_execucao_cria_parecer_pendente_e_decisao_local_por_POST()
    {
        var root = Root();
        try
        {
            var (store, project, session, team, producer, reviewer) = Seed(root);
            var catalog = Path.Combine(root, "catalog");
            var original = Path.Combine(root, "workspace", "important.md");
            File.WriteAllText(original, "intocado");
            var submitted = new List<string[]>();
            var config = new WebTaskSettings(
                "http://127.0.0.1:59999/v1/chat/completions",
                "mock", 20, 5, 1, 3, 80, 4, ReviewTeams: true);
            var runner = new CliWebTaskRunner(catalog, null, config, args =>
            {
                submitted.Add(args);
                var id = args[Array.IndexOf(args, "--agent-profile-id") + 1];
                AppendRun(store, project.Id, session.Id, "run-" + submitted.Count,
                    id == producer.Id ? "Texto <script>alert(1)</script>" :
                    "Parecer <img src=x onerror=1> precisa de testes.");
                return Task.FromResult(0);
            });

            Assert.Equal(WebTaskState.Reviewed, (await runner.SubmitAsync(
                project.Id, session.Id, "Investigue C#", "team:" + team.Id)).State);
            Assert.Equal(2, submitted.Count);
            Assert.Equal(40, runner.Board.ReservedCents);
            var reviewStore = new LocalTeamReviewStore(catalog);
            var review = Assert.Single(reviewStore.Read().Reviews);
            Assert.Equal("pending_owner", review.Decision);
            Assert.Equal(producer.Id, review.ProducerId);
            Assert.Equal(reviewer.Id, review.ReviewerId);
            Assert.Equal("run-1", review.ProducerRunId);
            Assert.Equal("run-2", review.ReviewerRunId);
            Assert.Equal(2, store.Read().Projects.Single().Sessions.Single().Runs.Count);

            var port = Port();
            await using var app = CliWebUi.CreateApp(catalog, port);
            await app.StartAsync();
            try
            {
                using var http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
                { BaseAddress = new Uri($"http://127.0.0.1:{port}") };
                var home = await http.GetStringAsync("/");
                Assert.Contains("action='/review-decisions'", home);
                Assert.Contains("Parecer aguardando decisão do operador", home);
                Assert.DoesNotContain("action='/tasks'", home);
                var csrf = Regex.Match(home, "name='csrf' value='([0-9A-F]{64})'")
                    .Groups[1].Value;
                Assert.Equal(64, csrf.Length);

                async Task<HttpResponseMessage> Post(string code, string note) =>
                    await http.PostAsync("/review-decisions", new FormUrlEncodedContent(
                        new Dictionary<string, string>
                        {
                            ["csrf"] = csrf, ["reviewId"] = review.Id,
                            ["decision"] = code, ["note"] = note
                        }));

                using (var bad = await Post("integrated", "Quero integrar"))
                    Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
                using (var blank = await Post("owner_accepted", ""))
                    Assert.Equal(HttpStatusCode.BadRequest, blank.StatusCode);
                using (var accepted = await Post("owner_accepted", "Li o parecer; requer validação"))
                    Assert.Equal(HttpStatusCode.SeeOther, accepted.StatusCode);
                using (var duplicate = await Post("owner_rejected", "Mudança posterior"))
                    Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);

                var result = Assert.Single(new LocalTeamReviewStore(catalog).Read().Reviews);
                Assert.Equal("owner_accepted", result.Decision);
                Assert.Equal("Li o parecer; requer validação", result.Note);
                Assert.NotNull(result.DecidedAt);
                var updated = await http.GetStringAsync("/");
                Assert.Contains("Parecer aceito", updated);
                Assert.Contains("não autoriza escrita, merge nem integração", updated);
                Assert.DoesNotContain("action='/review-decisions'", updated);
            }
            finally { await app.StopAsync(); }

            Assert.Equal("intocado", File.ReadAllText(original));
            Assert.Equal(2, store.Read().Projects.Single().Sessions.Single().Runs.Count);
            Assert.False(File.Exists(Path.Combine(catalog, "integration-receipt.json")));
            Assert.True(File.Exists(Path.Combine(catalog, "team-reviews.json")));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void Sem_dois_recibos_ou_com_parecer_adulterado_nao_aceita()
    {
        var root = Root();
        try
        {
            var (store, project, session, team, _, _) = Seed(root);
            var catalog = Path.Combine(root, "catalog");
            var records = new LocalTeamReviewStore(catalog);
            AppendRun(store, project.Id, session.Id, "producer", "Texto");
            Assert.Throws<InvalidOperationException>(() => records.Record(
                project.Id, session.Id, team.Id, "producer", "reviewer", 1, 3));
            AppendRun(store, project.Id, session.Id, "reviewer", "Parecer");
            var entry = records.Record(project.Id, session.Id, team.Id,
                "producer", "reviewer", 1, 3);
            Assert.Throws<InvalidOperationException>(() => records.Record(
                project.Id, session.Id, team.Id, "producer", "reviewer", 1, 3));
            Assert.Throws<ArgumentException>(() => records.Decide(entry.Id, "integrated", "x"));
            Assert.Throws<ArgumentException>(() => records.Decide(entry.Id, "owner_rejected", ""));
            Assert.Throws<KeyNotFoundException>(() =>
                records.Decide(Guid.NewGuid().ToString("N"), "owner_rejected", "Sem parecer"));

            var file = Path.Combine(catalog, "team-reviews.json");
            var corrupted = File.ReadAllText(file).Replace("pending_owner", "owner_accepted");
            File.WriteAllText(file, corrupted);
            Assert.Throws<InvalidDataException>(() => new LocalTeamReviewStore(catalog).Read());
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task CSRF_campo_extra_e_cliente_sem_parecer_nao_autorizam_decisao()
    {
        var root = Root();
        try
        {
            var (store, project, session, team, _, _) = Seed(root);
            AppendRun(store, project.Id, session.Id, "producer", "Texto");
            AppendRun(store, project.Id, session.Id, "reviewer", "Parecer");
            var catalog = Path.Combine(root, "catalog");
            var review = new LocalTeamReviewStore(catalog).Record(
                project.Id, session.Id, team.Id, "producer", "reviewer", 1, 3);
            var port = Port();
            await using var app = CliWebUi.CreateApp(catalog, port);
            await app.StartAsync();
            try
            {
                using var http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
                { BaseAddress = new Uri($"http://127.0.0.1:{port}") };
                var csrf = Regex.Match(await http.GetStringAsync("/"),
                    "name='csrf' value='([0-9A-F]{64})'").Groups[1].Value;
                var valid = new Dictionary<string, string>
                {
                    ["csrf"] = csrf, ["reviewId"] = review.Id,
                    ["decision"] = "owner_rejected", ["note"] = "Precisa melhorar"
                };
                using (var wrong = await http.PostAsync("/review-decisions",
                    new FormUrlEncodedContent(valid.Where(x => x.Key != "csrf")
                        .ToDictionary(x => x.Key, x => x.Value))))
                    Assert.Equal(HttpStatusCode.Forbidden, wrong.StatusCode);
                valid["grant"] = "fs.write";
                using (var extra = await http.PostAsync("/review-decisions",
                    new FormUrlEncodedContent(valid)))
                    Assert.Equal(HttpStatusCode.BadRequest, extra.StatusCode);
                var item = Assert.Single(new LocalTeamReviewStore(catalog).Read().Reviews);
                Assert.Equal("pending_owner", item.Decision);
                Assert.Null(item.DecidedAt);
            }
            finally { await app.StopAsync(); }
        }
        finally { Directory.Delete(root, true); }
    }
}
