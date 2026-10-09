using System.Text;
using System.Text.Json;
using AgentRuntime;
using EcosystemAi.Cli;
using EcosystemAi.ProjectStore;
using EcosystemAi.RunJournal;

namespace AgentRuntime.Tests;

public sealed class R4ArtifactTextPreviewTests
{
    private static string NewRoot() => Path.Combine(Path.GetTempPath(),
        "r4-previews-" + Guid.NewGuid().ToString("N"));

    private static async Task SeedRun(string journal, string runId, string location)
    {
        var log = new LocalRunEventLog(journal);
        var actor = new AgentIdentity("writer", "Writer", "agent");
        var at = DateTimeOffset.UtcNow;
        var artifacts = JsonSerializer.Serialize(new[]
        {
            new ArtifactRef("file:" + location, "file", location)
        });
        var events = new[]
        {
            new RuntimeEvent(runId, 1, at, EventKind.RunCreated, actor, "task", "project"),
            new RuntimeEvent(runId, 2, at, EventKind.ToolCalled, actor, "task", "project",
                Tool: "files.write", ApprovedBy: "policy"),
            new RuntimeEvent(runId, 3, at, EventKind.ToolResult, actor, "task", "project",
                Tool: "files.write", Data: new Dictionary<string, string> { ["artifacts"] = artifacts }),
            new RuntimeEvent(runId, 4, at, EventKind.VerificationPassed, actor, "task", "project",
                Verification: "exists"),
            new RuntimeEvent(runId, 5, at, EventKind.RunSucceeded, actor, "task", "project",
                Result: "succeeded")
        };
        foreach (var item in events) await log.AppendAsync(item, CancellationToken.None);
    }

    [Fact]
    public async Task Preview_de_texto_exige_flag_e_escapa_HTML_sem_alterar_catalogo_ou_arquivo()
    {
        var root = NewRoot();
        var workspace = Path.Combine(root, "workspace");
        var catalog = Path.Combine(root, "catalog");
        var journal = Path.Combine(root, "journal");
        var output = Path.Combine(root, "out");
        Directory.CreateDirectory(workspace);
        Directory.CreateDirectory(output);
        try
        {
            Directory.CreateDirectory(Path.Combine(workspace, "docs"));
            var artifact = Path.Combine(workspace, "docs", "saida.md");
            const string original = "# Resultado\n<script>alert('privado')</script>\nconteúdo & restante";
            File.WriteAllText(artifact, original);
            var store = new LocalProjectStore(catalog);
            var project = store.CreateProject("P", workspace);
            var session = store.CreateSession(project.Id, "Sessão");
            var id = "run-" + Guid.NewGuid().ToString("N");
            store.AppendRun(project.Id, session.Id,
                new RunReceipt(id, "succeeded", 0, "USD", true, "exists", DateTimeOffset.UtcNow));
            await SeedRun(journal, id, "docs/saida.md");
            var rev = store.Read().Revision;

            var withoutPreview = Path.Combine(output, "default.html");
            await CliHtmlSnapshot.ExportAsync(catalog, withoutPreview, journal);
            Assert.DoesNotContain("alert(", File.ReadAllText(withoutPreview));

            var withPreview = Path.Combine(output, "preview.html");
            Assert.Equal(0, await EcosystemAiCli.RunAsync([
                "--export-html", "--catalog", catalog, "--journal", journal,
                "--output", withPreview, "--embed-text-artifacts"]));
            var html = File.ReadAllText(withPreview);
            Assert.Contains("Visualizar texto atual do arquivo", html);
            Assert.Contains("&lt;script&gt;alert(", html);
            Assert.Contains("conteúdo &amp; restante", html);
            Assert.DoesNotContain("<script>alert", html);
            Assert.DoesNotContain(workspace, html);
            Assert.Equal(original, File.ReadAllText(artifact));
            Assert.Equal(rev, store.Read().Revision);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void Arquivos_grandes_binarios_nao_textuais_e_caminhos_externos_nao_sao_lidos()
    {
        var root = NewRoot();
        var workspace = Path.Combine(root, "workspace");
        Directory.CreateDirectory(workspace);
        try
        {
            File.WriteAllText(Path.Combine(root, "fora.md"), "SEGREDO-FORA");
            File.WriteAllText(Path.Combine(workspace, "grande.md"),
                new string('x', CliArtifactTextPreview.MaxPreviewBytes + 1));
            File.WriteAllBytes(Path.Combine(workspace, "binario.md"), [0, 1, 2, 3]);
            File.WriteAllBytes(Path.Combine(workspace, "quebrado.md"), [0xC3, 0x28]);
            File.WriteAllText(Path.Combine(workspace, "foto.png"), "não mostrar png");
            var total = 0;
            var count = 0;
            string? Read(string path) => CliArtifactTextPreview.Read(
                new ArtifactRef("file:" + path, "file", path), workspace, ref total, ref count);
            Assert.Null(Read("../fora.md"));
            Assert.Null(Read("grande.md"));
            Assert.Null(Read("binario.md"));
            Assert.Null(Read("quebrado.md"));
            Assert.Null(Read("foto.png"));
            Assert.Equal(0, total);
            Assert.Equal(0, count);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void Preview_recusa_alias_simbolico_mesmo_quando_dentro_do_workspace()
    {
        if (OperatingSystem.IsWindows()) return;
        var root = NewRoot();
        var workspace = Path.Combine(root, "workspace");
        Directory.CreateDirectory(workspace);
        try
        {
            var path = Path.Combine(workspace, "original.md");
            File.WriteAllText(path, "conteúdo de link");
            File.CreateSymbolicLink(Path.Combine(workspace, "alias.md"), path);
            var total = 0;
            var count = 0;
            var result = CliArtifactTextPreview.Read(
                new ArtifactRef("file:alias.md", "file", "alias.md"), workspace, ref total, ref count);
            Assert.Null(result);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Sem_flag_e_sem_journal_nao_aceita_exportar_preview()
    {
        Assert.Equal(2, await EcosystemAiCli.RunAsync(["--embed-text-artifacts"]));
        Assert.Equal(2, await EcosystemAiCli.RunAsync([
            "--export-html", "--catalog", "/tmp/no", "--output", "/tmp/out.html",
            "--embed-text-artifacts"]));
    }

    [Fact]
    public void Limite_de_16_arquivos_e_128_KiB_impede_exportacao_ilimitada()
    {
        var root = NewRoot();
        Directory.CreateDirectory(root);
        try
        {
            var path = Path.Combine(root, "texto.md");
            File.WriteAllText(path, new string('x', 16 * 1024));
            var total = 0;
            var count = 0;
            for (var i = 0; i < CliArtifactTextPreview.MaxTotalPreviewBytes / 16384; i++)
                Assert.NotNull(CliArtifactTextPreview.Read(
                    new ArtifactRef("f", "file", "texto.md"), root, ref total, ref count));
            Assert.Null(CliArtifactTextPreview.Read(
                new ArtifactRef("f", "file", "texto.md"), root, ref total, ref count));
            Assert.Equal(CliArtifactTextPreview.MaxTotalPreviewBytes, total);
            Assert.Equal(8, count);
        }
        finally { Directory.Delete(root, true); }
    }
}
