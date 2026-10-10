using System.Net;
using System.Net.Sockets;
using System.Text;
using EcosystemAi.Cli;
using EcosystemAi.ProjectStore;

namespace AgentRuntime.Tests;

public sealed class R4ProductDownloadsTests
{
    private static string Root() => Path.Combine(Path.GetTempPath(),
        "r4-download-" + Guid.NewGuid().ToString("N"));

    private static int Port()
    {
        var socket = new TcpListener(IPAddress.Loopback, 0);
        socket.Start();
        try { return ((IPEndPoint)socket.LocalEndpoint).Port; }
        finally { socket.Stop(); }
    }

    [Fact]
    public async Task HTTP_exporta_resposta_real_md_txt_e_custos_csv_sem_executar_modelo()
    {
        var root = Root();
        var workspace = Path.Combine(root, "workspace");
        var catalog = Path.Combine(root, "catalog");
        Directory.CreateDirectory(workspace);
        var untouched = Path.Combine(workspace, "untouched.md");
        File.WriteAllText(untouched, "preservado");
        try
        {
            var store = new LocalProjectStore(catalog);
            var project = store.CreateProject("Projeto", workspace);
            var session = store.CreateSession(project.Id, "Sessão");
            store.AppendTurn(project.Id, session.Id, "user", "Pedido privado");
            const string response = "# Resposta\n\nTexto real em **Markdown**: <script>alert(1)</script>\n";
            store.AppendTurn(project.Id, session.Id, "assistant", response);
            store.AppendRun(project.Id, session.Id,
                new RunReceipt("run-export-1", "succeeded", 27, "USD", true,
                    "resposta presente", DateTimeOffset.UtcNow, CostEstimated: true));

            var port = Port();
            await using var app = CliWebUi.CreateApp(catalog, port);
            await app.StartAsync();
            try
            {
                using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
                { BaseAddress = new Uri($"http://127.0.0.1:{port}") };
                var page = await client.GetStringAsync("/");
                Assert.Contains("Exportar registro de custos (.csv)", page);
                Assert.Contains("Salvar .md", page);
                Assert.Contains("Salvar .txt", page);
                Assert.Contains("/exports/responses/" + project.Id + "/" + session.Id + "/1/md", page);
                Assert.DoesNotContain("/exports/responses/" + project.Id + "/" + session.Id + "/0/md", page);

                using (var md = await client.GetAsync(
                    $"/exports/responses/{project.Id}/{session.Id}/1/md"))
                {
                    Assert.Equal(HttpStatusCode.OK, md.StatusCode);
                    Assert.Equal("text/markdown", md.Content.Headers.ContentType?.MediaType);
                    Assert.Equal("attachment", md.Content.Headers.ContentDisposition?.DispositionType);
                    Assert.EndsWith(".md", md.Content.Headers.ContentDisposition?.FileName);
                    Assert.Equal(response, await md.Content.ReadAsStringAsync());
                    Assert.Contains("no-store", md.Headers.CacheControl?.ToString());
                    Assert.Equal("nosniff", md.Headers.GetValues("X-Content-Type-Options").Single());
                }
                using (var txt = await client.GetAsync(
                    $"/exports/responses/{project.Id}/{session.Id}/1/txt"))
                {
                    Assert.Equal(HttpStatusCode.OK, txt.StatusCode);
                    Assert.Equal("text/plain", txt.Content.Headers.ContentType?.MediaType);
                    Assert.Equal(response, await txt.Content.ReadAsStringAsync());
                }
                using (var csv = await client.GetAsync("/exports/costs.csv"))
                {
                    Assert.Equal(HttpStatusCode.OK, csv.StatusCode);
                    Assert.Equal("text/csv", csv.Content.Headers.ContentType?.MediaType);
                    Assert.Equal("attachment", csv.Content.Headers.ContentDisposition?.DispositionType);
                    var data = await csv.Content.ReadAsStringAsync();
                    Assert.StartsWith("ProjectId,SessionId,RunId,RecordedAtUtc", data);
                    Assert.Contains("\"run-export-1\"", data);
                    Assert.Contains(",27,\"USD\",true", data);
                }
                Assert.Equal("preservado", File.ReadAllText(untouched));
                Assert.Single(store.Read().Projects.Single().Sessions.Single().Runs);
            }
            finally { await app.StopAsync(); }
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task Download_recusa_usuario_ids_errados_formatos_e_Host_externo()
    {
        var root = Root();
        var workspace = Path.Combine(root, "workspace");
        var catalog = Path.Combine(root, "catalog");
        Directory.CreateDirectory(workspace);
        try
        {
            var store = new LocalProjectStore(catalog);
            var project = store.CreateProject("P", workspace);
            var session = store.CreateSession(project.Id, "S");
            store.AppendTurn(project.Id, session.Id, "user", "Dado não exportável como resultado");
            store.AppendTurn(project.Id, session.Id, "assistant", "Resposta");

            var port = Port();
            await using var app = CliWebUi.CreateApp(catalog, port);
            await app.StartAsync();
            try
            {
                using var client = new HttpClient
                { BaseAddress = new Uri($"http://127.0.0.1:{port}") };
                foreach (var url in new[]
                {
                    $"/exports/responses/{project.Id}/{session.Id}/0/md",
                    $"/exports/responses/{project.Id}/{session.Id}/2/md",
                    $"/exports/responses/{project.Id}/{session.Id}/1/html",
                    $"/exports/responses/{Guid.NewGuid():N}/{session.Id}/1/md",
                    $"/exports/responses/{project.Id}/{Guid.NewGuid():N}/1/md",
                    $"/exports/responses/{project.Id}/{session.Id}/-1/md",
                    $"/exports/responses/{project.Id}/{session.Id}/abc/md"
                })
                {
                    using var bad = await client.GetAsync(url);
                    Assert.Equal(HttpStatusCode.NotFound, bad.StatusCode);
                }

                using var hostile = new HttpRequestMessage(HttpMethod.Get, "/exports/costs.csv");
                hostile.Headers.Host = "attacker.invalid";
                using var denied = await client.SendAsync(hostile);
                Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
            }
            finally { await app.StopAsync(); }
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void Csv_neutraliza_formula_em_RunId_e_exporta_apenas_recibos_canonicos()
    {
        var root = Root();
        var workspace = Path.Combine(root, "workspace");
        var catalog = Path.Combine(root, "catalog");
        Directory.CreateDirectory(workspace);
        try
        {
            var store = new LocalProjectStore(catalog);
            var p = store.CreateProject("=FÓRMULA", workspace);
            var s = store.CreateSession(p.Id, "S");
            store.AppendTurn(p.Id, s.Id, "user", "Sem efeito contábil");
            store.AppendRun(p.Id, s.Id, new RunReceipt("=1+1",
                "blocked", 0, "USD", false, null, DateTimeOffset.UtcNow));
            store.AppendRun(p.Id, s.Id, new RunReceipt("normal\"run",
                "succeeded", 9, "USD", true, "presente", DateTimeOffset.UtcNow));
            var snapshot = store.Read();
            var csv = Encoding.UTF8.GetString(CliProductDownloads.Costs(snapshot).Bytes);
            Assert.Contains("\"'=1+1\"", csv);
            Assert.Contains("\"normal\"\"run\"", csv);
            Assert.Contains(",0,\"USD\",false", csv);
            Assert.Contains(",9,\"USD\",false", csv);
            Assert.DoesNotContain("=FÓRMULA", csv); // nomes de projeto não entram no relatório.
            Assert.DoesNotContain("Sem efeito contábil", csv);

            Assert.Throws<KeyNotFoundException>(() =>
                CliProductDownloads.Response(snapshot, p.Id, s.Id, 0, "md"));
            Assert.Throws<ArgumentException>(() =>
                CliProductDownloads.Response(snapshot, p.Id, s.Id, -1, "md"));
            Assert.Throws<ArgumentException>(() =>
                CliProductDownloads.Response(snapshot, p.Id, s.Id, 0, "html"));
        }
        finally { Directory.Delete(root, recursive: true); }
    }
}
