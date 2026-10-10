using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using EcosystemAi.Cli;
using EcosystemAi.ProjectStore;

namespace AgentRuntime.Tests;

public sealed class R4WebUiTests
{
    private static string NewRoot() => Path.Combine(Path.GetTempPath(),
        "r4-local-web-ui-" + Guid.NewGuid().ToString("N"));

    private static int FreeLoopbackPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try { return ((IPEndPoint)listener.LocalEndpoint).Port; }
        finally { listener.Stop(); }
    }

    private static string Token(string html)
    {
        var match = Regex.Match(html, "name='csrf' value='([0-9A-F]{64})'");
        Assert.True(match.Success, "HTML deve conter token CSRF somente no formulário.");
        return match.Groups[1].Value;
    }

    [Fact]
    public void Render_e_mobile_first_escapam_dados_e_nao_criam_motor_novo()
    {
        var root = NewRoot();
        var workspace = Path.Combine(root, "workspace");
        var catalogDir = Path.Combine(root, "catalog");
        Directory.CreateDirectory(workspace);
        try
        {
            var store = new LocalProjectStore(catalogDir);
            var project = store.CreateProject("<img src=x onerror=1>", workspace);
            var session = store.CreateSession(project.Id, "<svg onload=1>");
            store.AppendTurn(project.Id, session.Id, "user", "<script>ataque()</script>");
            var revision = store.Read().Revision;
            var html = CliWebUiHtml.Render(store.Read(), new string('A', 64));
            Assert.Contains("viewport-fit=cover", html);
            Assert.Contains("@media(max-width:760px)", html);
            Assert.Contains("action='/projects'", html);
            Assert.Contains("action='/sessions'", html);
            Assert.Contains("name='csrf'", html);
            Assert.Contains("&lt;script&gt;ataque()", html);
            Assert.Contains("&lt;img src=x onerror=1&gt;", html);
            Assert.DoesNotContain("<script>ataque()", html);
            Assert.DoesNotContain("<svg onload", html);
            Assert.DoesNotContain(workspace, html);
            Assert.DoesNotContain("fetch(", html);
            Assert.Equal(revision, store.Read().Revision);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Http_local_cria_projeto_e_sessao_no_store_original_sem_modificar_workspace()
    {
        var root = NewRoot();
        var workspace = Path.Combine(root, "workspace");
        var catalog = Path.Combine(root, "catalog");
        Directory.CreateDirectory(workspace);
        var file = Path.Combine(workspace, "leia.md");
        File.WriteAllText(file, "intocável");
        var port = FreeLoopbackPort();
        try
        {
            await using var app = CliWebUi.CreateApp(catalog, port);
            await app.StartAsync();
            try
            {
                using var http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
                {
                    BaseAddress = new Uri($"http://127.0.0.1:{port}")
                };
                var home = await http.GetAsync("/");
                Assert.Equal(HttpStatusCode.OK, home.StatusCode);
                Assert.Contains("no-store", home.Headers.CacheControl!.ToString());
                var originalHtml = await home.Content.ReadAsStringAsync();
                var csrf = Token(originalHtml);
                Assert.DoesNotContain("leia.md", originalHtml);
                Assert.False(File.Exists(Path.Combine(catalog, "catalog.json")));

                var postProject = await http.PostAsync("/projects",
                    new FormUrlEncodedContent(new Dictionary<string, string>
                    {
                        ["csrf"] = csrf, ["name"] = "Meu projeto", ["workspace"] = workspace
                    }));
                Assert.Equal(HttpStatusCode.SeeOther, postProject.StatusCode);
                var project = Assert.Single(new LocalProjectStore(catalog).Read().Projects);
                Assert.Equal("Meu projeto", project.Name);
                Assert.Equal(Path.GetFullPath(workspace), project.WorkspaceDirectory);

                var postSession = await http.PostAsync("/sessions",
                    new FormUrlEncodedContent(new Dictionary<string, string>
                    {
                        ["csrf"] = csrf, ["projectId"] = project.Id, ["title"] = "Primeira conversa"
                    }));
                Assert.Equal(HttpStatusCode.SeeOther, postSession.StatusCode);
                var created = Assert.Single(new LocalProjectStore(catalog).Read().Projects.Single().Sessions);
                Assert.Equal("Primeira conversa", created.Title);
                Assert.Equal(2, new LocalProjectStore(catalog).Read().Revision);
                Assert.Equal("intocável", File.ReadAllText(file));

                var refreshed = await http.GetStringAsync("/");
                Assert.Contains("Primeira conversa", refreshed);
                Assert.Contains("Meu projeto", refreshed);
                Assert.Contains($"id='s-{created.Id}'", refreshed);
                Assert.Equal(csrf, Token(refreshed));
            }
            finally { await app.StopAsync(); }
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Host_diferente_origem_externa_e_csrf_errado_sao_bloqueados()
    {
        var root = NewRoot();
        var workspace = Path.Combine(root, "workspace");
        var catalog = Path.Combine(root, "catalog");
        Directory.CreateDirectory(workspace);
        var port = FreeLoopbackPort();
        try
        {
            await using var app = CliWebUi.CreateApp(catalog, port);
            await app.StartAsync();
            try
            {
                using var http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
                {
                    BaseAddress = new Uri($"http://127.0.0.1:{port}")
                };
                var html = await http.GetStringAsync("/");
                var token = Token(html);

                using (var badHost = new HttpRequestMessage(HttpMethod.Get, "/"))
                {
                    badHost.Headers.Host = $"host-malicioso.example:{port}";
                    using var response = await http.SendAsync(badHost);
                    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
                }

                async Task<HttpResponseMessage> Submit(string csrf, bool foreignOrigin)
                {
                    using var request = new HttpRequestMessage(HttpMethod.Post, "/projects");
                    if (foreignOrigin)
                        request.Headers.TryAddWithoutValidation("Origin", "https://exemplo-malicioso.invalid");
                    request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
                    {
                        ["csrf"] = csrf, ["name"] = "Não criar", ["workspace"] = workspace
                    });
                    return await http.SendAsync(request);
                }
                using (var wrongToken = await Submit("B", false))
                    Assert.Equal(HttpStatusCode.Forbidden, wrongToken.StatusCode);
                using (var wrongOrigin = await Submit(token, true))
                    Assert.Equal(HttpStatusCode.Forbidden, wrongOrigin.StatusCode);

                using (var unsupported = await http.PostAsync("/projects",
                    new StringContent("{\"csrf\":\"" + token + "\"}")))
                    Assert.Equal(HttpStatusCode.UnsupportedMediaType, unsupported.StatusCode);

                Assert.False(File.Exists(Path.Combine(catalog, "catalog.json")));
            }
            finally { await app.StopAsync(); }
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Campos_extras_e_catalogo_dentro_do_workspace_recusados()
    {
        var root = NewRoot();
        var workspace = Path.Combine(root, "workspace");
        var catalog = Path.Combine(root, "catalog");
        Directory.CreateDirectory(workspace);
        var port = FreeLoopbackPort();
        try
        {
            await using var app = CliWebUi.CreateApp(catalog, port);
            await app.StartAsync();
            try
            {
                using var http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
                {
                    BaseAddress = new Uri($"http://127.0.0.1:{port}")
                };
                var token = Token(await http.GetStringAsync("/"));
                using var extra = await http.PostAsync("/projects",
                    new FormUrlEncodedContent(new Dictionary<string, string>
                    {
                        ["csrf"] = token, ["name"] = "Projeto", ["workspace"] = workspace,
                        ["grant"] = "fs.write"
                    }));
                Assert.Equal(HttpStatusCode.BadRequest, extra.StatusCode);
                Assert.False(File.Exists(Path.Combine(catalog, "catalog.json")));
            }
            finally { await app.StopAsync(); }

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                CliWebUi.CreateApp(catalog, 80));
            var store = new LocalProjectStore(Path.Combine(workspace, "catalog"));
            store.CreateProject("P", workspace);
            Assert.Throws<ArgumentException>(() =>
                CliWebUi.CreateApp(Path.Combine(workspace, "catalog"), FreeLoopbackPort()));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task WebUi_exige_modo_explicito_e_nao_admite_flags_de_execucao()
    {
        Assert.Equal(2, await EcosystemAiCli.RunAsync(["--port", "8765"]));
        Assert.Equal(2, await EcosystemAiCli.RunAsync([
            "--web-ui", "--catalog", "/tmp/catalog", "--model", "modelo"]));
        Assert.Equal(2, await EcosystemAiCli.RunAsync([
            "--web-ui", "--chat", "--catalog", "/tmp/catalog"]));
        Assert.Equal(2, await EcosystemAiCli.RunAsync([
            "--web-ui", "--catalog", "/tmp/catalog", "--port", "80"]));
    }
}
