using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using AgentRuntime;
using EcosystemAi.Cli;
using EcosystemAi.ProjectStore;
using EcosystemAi.RunJournal;

namespace AgentRuntime.Tests;

public sealed class R4WebArtifactPreviewTests
{
    private static string Root() => Path.Combine(Path.GetTempPath(),
        "r4-web-preview-" + Guid.NewGuid().ToString("N"));

    private static int Port()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try { return ((IPEndPoint)listener.LocalEndpoint).Port; }
        finally { listener.Stop(); }
    }

    private static async Task Seed(string journal, string id, string filename)
    {
        var log = new LocalRunEventLog(journal);
        var actor = new AgentIdentity("writer", "Writer", "executor");
        var now = DateTimeOffset.UtcNow;
        var artifacts = JsonSerializer.Serialize(new[]
        {
            new ArtifactRef("file:" + filename, "file", filename)
        });
        foreach (var entry in new[]
        {
            new RuntimeEvent(id, 1, now, EventKind.RunCreated, actor, "task", "project"),
            new RuntimeEvent(id, 2, now, EventKind.ToolCalled, actor, "task", "project",
                Tool: "files.write", ApprovedBy: "policy"),
            new RuntimeEvent(id, 3, now, EventKind.ToolResult, actor, "task", "project",
                Tool: "files.write", Data: new Dictionary<string, string> { ["artifacts"] = artifacts }),
            new RuntimeEvent(id, 4, now, EventKind.VerificationPassed, actor, "task", "project",
                Verification: "exists"),
            new RuntimeEvent(id, 5, now, EventKind.RunSucceeded, actor, "task", "project", Result: "succeeded")
        })
            await log.AppendAsync(entry, CancellationToken.None);
    }

    [Fact]
    public async Task Preview_ativo_exige_optin_e_escapa_conteudo_atual_sem_modificar_arquivo()
    {
        var root = Root();
        var workspace = Path.Combine(root, "workspace");
        var catalog = Path.Combine(root, "catalog");
        var journal = Path.Combine(root, "journal");
        var dir = Path.Combine(workspace, "docs");
        Directory.CreateDirectory(dir);
        var artifact = Path.Combine(dir, "resultado.md");
        const string text = "# Resultado\n<script>alert('conteudo-privado')</script>\n<em>bloco</em>";
        File.WriteAllText(artifact, text);
        try
        {
            var store = new LocalProjectStore(catalog);
            var p = store.CreateProject("Projeto", workspace);
            var s = store.CreateSession(p.Id, "Sessão");
            var id = "run-" + Guid.NewGuid().ToString("N");
            store.AppendRun(p.Id, s.Id,
                new RunReceipt(id, "succeeded", 0, "USD", true, "exists", DateTimeOffset.UtcNow));
            await Seed(journal, id, "docs/resultado.md");
            var revision = store.Read().Revision;

            async Task<string> GetPage(bool embed)
            {
                var port = Port();
                await using var app = CliWebUi.CreateApp(catalog, port, journal, embed);
                await app.StartAsync();
                try
                {
                    using var client = new HttpClient
                    {
                        BaseAddress = new Uri($"http://127.0.0.1:{port}")
                    };
                    return await client.GetStringAsync("/");
                }
                finally { await app.StopAsync(); }
            }

            var basic = await GetPage(embed: false);
            Assert.DoesNotContain("conteudo-privado", basic);
            Assert.Contains("resultado.md", basic);

            var preview = await GetPage(embed: true);
            Assert.Contains("Ver texto atual do arquivo (não histórico)", preview);
            Assert.Contains("&lt;script&gt;alert(", preview);
            Assert.Contains("&lt;em&gt;bloco&lt;/em&gt;", preview);
            Assert.DoesNotContain("<script>alert(", preview);
            Assert.DoesNotContain(workspace, preview);
            Assert.DoesNotContain("docs/resultado.md", preview);
            Assert.Equal(text, File.ReadAllText(artifact));
            Assert.Equal(revision, store.Read().Revision);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task Arquivos_maiores_que_limite_nao_viram_preview_ativo()
    {
        var root = Root();
        var workspace = Path.Combine(root, "workspace");
        var catalog = Path.Combine(root, "catalog");
        var journal = Path.Combine(root, "journal");
        Directory.CreateDirectory(workspace);
        var file = Path.Combine(workspace, "grande.md");
        File.WriteAllText(file, "HIDDEN-" + new string('x', CliArtifactTextPreview.MaxPreviewBytes));
        try
        {
            var store = new LocalProjectStore(catalog);
            var p = store.CreateProject("P", workspace);
            var s = store.CreateSession(p.Id, "S");
            var id = "run-" + Guid.NewGuid().ToString("N");
            store.AppendRun(p.Id, s.Id,
                new RunReceipt(id, "succeeded", 0, "USD", true, "exists", DateTimeOffset.UtcNow));
            await Seed(journal, id, "grande.md");
            var port = Port();
            await using var app = CliWebUi.CreateApp(catalog, port, journal, embedTextPreviews: true);
            await app.StartAsync();
            try
            {
                using var client = new HttpClient
                {
                    BaseAddress = new Uri($"http://127.0.0.1:{port}")
                };
                var html = await client.GetStringAsync("/");
                Assert.Contains("grande.md", html);
                Assert.DoesNotContain("HIDDEN-", html);
                Assert.DoesNotContain("Ver texto atual do arquivo (não histórico)", html);
            }
            finally { await app.StopAsync(); }
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task Flag_sem_journal_e_formas_de_execucao_incompatíveis_sao_recusadas()
    {
        var root = Root();
        Directory.CreateDirectory(root);
        try
        {
            Assert.Throws<ArgumentException>(() =>
                CliWebUi.CreateApp(Path.Combine(root, "catalog"), Port(), embedTextPreviews: true));
            Assert.Equal(2, await EcosystemAiCli.RunAsync([
                "--web-ui", "--catalog", Path.Combine(root, "catalog"), "--embed-text-artifacts"]));
            Assert.Equal(2, await EcosystemAiCli.RunAsync([
                "--web-ui", "--catalog", Path.Combine(root, "catalog"), "--journal",
                Path.Combine(root, "journal"), "--embed-text-artifacts", "--model", "mock"]));
        }
        finally { Directory.Delete(root, recursive: true); }
    }
}
