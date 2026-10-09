using EcosystemAi.Cli;
using EcosystemAi.ProjectStore;

namespace AgentRuntime.Tests;

public sealed class R4VisualSnapshotTests
{
    private static string NewRoot() => Path.Combine(Path.GetTempPath(),
        "ecosystem-r4-html-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Snapshot_projeta_catalogo_real_com_navegacao_custos_e_sem_rede()
    {
        var root = NewRoot();
        var projectDirectory = Path.Combine(root, "workspace");
        var catalogDirectory = Path.Combine(root, "catalog");
        var exportDirectory = Path.Combine(root, "exports");
        Directory.CreateDirectory(projectDirectory);
        Directory.CreateDirectory(exportDirectory);
        try
        {
            var store = new LocalProjectStore(catalogDirectory);
            var project = store.CreateProject("<script>alert(1)</script> Projeto", projectDirectory);
            var session = store.CreateSession(project.Id, "Sessão <img src=x onerror=alert(1)>");
            store.AppendTurn(project.Id, session.Id, "user", "Pergunta <b>privada</b>");
            store.CompleteRun(project.Id, session.Id,
                new RunReceipt("run-1", "succeeded", 29, "USD", true,
                    "verificado <svg/onload=alert(1)>", DateTimeOffset.UtcNow, CostEstimated: true),
                "Resposta & conclusão");
            var revision = store.Read().Revision;
            var output = Path.Combine(exportDirectory, "resumo.html");

            Assert.Equal(0, await EcosystemAiCli.RunAsync(
                ["--export-html", "--catalog", catalogDirectory, "--output", output]));
            Assert.True(File.Exists(output));
            var html = File.ReadAllText(output);
            Assert.Contains("<meta name=\"viewport\"", html);
            Assert.Contains("default-src 'none'", html);
            Assert.Contains("<details", html);
            Assert.Contains("29 unidades mínimas USD", html);
            Assert.Contains("estimado 29 unidades mínimas USD", html);
            Assert.Contains("Pergunta &lt;b&gt;privada&lt;/b&gt;", html);
            Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;", html);
            Assert.DoesNotContain("<script>", html, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("<img", html, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("<svg", html, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(projectDirectory, html, StringComparison.Ordinal);
            Assert.DoesNotContain("http://", html, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("https://", html, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(revision, store.Read().Revision);
            Assert.Single(store.Read().Projects);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void Exportacao_recusa_escrever_no_catalogo_ou_workspace_e_nao_sobrescreve()
    {
        var root = NewRoot();
        var workspace = Path.Combine(root, "workspace");
        var catalog = Path.Combine(root, "catalog");
        var export = Path.Combine(root, "out");
        Directory.CreateDirectory(workspace);
        Directory.CreateDirectory(export);
        try
        {
            var store = new LocalProjectStore(catalog);
            store.CreateProject("Projeto", workspace);
            var initial = store.Read().Revision;
            Assert.Throws<ArgumentException>(() => CliHtmlSnapshot.Export(catalog, Path.Combine(catalog, "dados.html")));
            Assert.Throws<ArgumentException>(() => CliHtmlSnapshot.Export(catalog, Path.Combine(workspace, "dados.html")));
            Assert.Throws<ArgumentException>(() => CliHtmlSnapshot.Export(catalog, Path.Combine(export, "dados.txt")));
            var output = Path.Combine(export, "relatorio.html");
            CliHtmlSnapshot.Export(catalog, output);
            var original = File.ReadAllText(output);
            Assert.Throws<IOException>(() => CliHtmlSnapshot.Export(catalog, output));
            Assert.Equal(original, File.ReadAllText(output));
            Assert.Equal(initial, store.Read().Revision);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task Exportacao_nao_inicializa_catalogo_ausente_e_rejeita_flags_de_execucao()
    {
        var root = NewRoot();
        Directory.CreateDirectory(root);
        try
        {
            var catalog = Path.Combine(root, "missing");
            var output = Path.Combine(root, "saida.html");
            Assert.Throws<FileNotFoundException>(() => CliHtmlSnapshot.Export(catalog, output));
            Assert.False(Directory.Exists(catalog));
            Assert.False(File.Exists(output));
            Assert.Equal(2, await EcosystemAiCli.RunAsync(
                ["--export-html", "--catalog", catalog, "--output", output, "--allow-create"]));
            Assert.Equal(2, await EcosystemAiCli.RunAsync(
                ["--catalog", catalog, "--output", output]));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void Saida_atraves_de_symlink_e_recusada()
    {
        if (OperatingSystem.IsWindows()) return;
        var root = NewRoot();
        Directory.CreateDirectory(root);
        var workspace = Path.Combine(root, "workspace");
        var catalog = Path.Combine(root, "catalog");
        var alias = Path.Combine(root, "alias");
        Directory.CreateDirectory(workspace);
        try
        {
            var store = new LocalProjectStore(catalog);
            store.CreateProject("Projeto", workspace);
            Directory.CreateSymbolicLink(alias, workspace);
            Assert.Throws<IOException>(() => CliHtmlSnapshot.Export(catalog, Path.Combine(alias, "dados.html")));
            Assert.False(File.Exists(Path.Combine(workspace, "dados.html")));
        }
        finally
        {
            if (Directory.Exists(alias)) Directory.Delete(alias);
            Directory.Delete(root, recursive: true);
        }
    }
}
