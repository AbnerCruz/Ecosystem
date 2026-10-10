using EcosystemAi.Cli;
using EcosystemAi.ProjectStore;

namespace AgentRuntime.Tests;

public sealed class R4CatalogManagementTests
{
    private static string Root() => Path.Combine(Path.GetTempPath(),
        "r4-catalog-manager-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Comandos_criam_projeto_e_sessao_sem_modelo_e_preservam_workspace()
    {
        var root = Root();
        var workspace = Path.Combine(root, "workspace");
        var catalog = Path.Combine(root, "private", "catalog");
        Directory.CreateDirectory(workspace);
        var document = Path.Combine(workspace, "important.md");
        File.WriteAllText(document, "Manter intacto");
        try
        {
            Assert.Equal(0, await EcosystemAiCli.RunAsync([
                "--create-project", "--catalog", catalog, "--project", workspace,
                "--project-name", "Meu app"]));
            var store = new LocalProjectStore(catalog);
            var initial = store.Read();
            var project = Assert.Single(initial.Projects);
            Assert.Equal("Meu app", project.Name);
            Assert.Equal(Path.GetFullPath(workspace), project.WorkspaceDirectory);
            Assert.Empty(project.Sessions);
            Assert.Equal(1, initial.Revision);

            Assert.Equal(0, await EcosystemAiCli.RunAsync([
                "--create-session", "--catalog", catalog, "--project-id", project.Id,
                "--session-title", "Nova conversa"]));
            var next = store.Read();
            var session = Assert.Single(Assert.Single(next.Projects).Sessions);
            Assert.Equal("Nova conversa", session.Title);
            Assert.Empty(session.Turns);
            Assert.Empty(session.Runs);
            Assert.Equal(2, next.Revision);
            Assert.Equal("Manter intacto", File.ReadAllText(document));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Menu_interativo_executa_operacoes_reais_no_mesmo_catalogo()
    {
        var root = Root();
        var workspace = Path.Combine(root, "workspace");
        var catalog = Path.Combine(root, "private", "catalog");
        Directory.CreateDirectory(workspace);
        try
        {
            using var input = new StringReader($"2\n{workspace}\nProjeto do menu\n1\n0\n");
            using var output = new StringWriter();
            Assert.Equal(0, CliCatalogManagement.Manage(catalog, input, output));
            Assert.Contains("Projeto criado:", output.ToString());
            Assert.Contains("Projetos: 1", output.ToString());

            var project = Assert.Single(new LocalProjectStore(catalog).Read().Projects);
            using var nextInput = new StringReader($"3\n{project.Id}\nSessão do menu\n1\n0\n");
            using var nextOutput = new StringWriter();
            Assert.Equal(0, CliCatalogManagement.Manage(catalog, nextInput, nextOutput));
            Assert.Contains("Sessão criada:", nextOutput.ToString());
            Assert.Contains("Sessão do menu", nextOutput.ToString());
            Assert.Single(new LocalProjectStore(catalog).Read().Projects.Single().Sessions);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Flags_ambiguas_rejeitadas_sem_criar_catalogo()
    {
        var root = Root();
        var workspace = Path.Combine(root, "workspace");
        var catalog = Path.Combine(root, "catalog");
        Directory.CreateDirectory(workspace);
        try
        {
            Assert.Equal(2, await EcosystemAiCli.RunAsync([
                "--create-project", "--allow-create", "--catalog", catalog,
                "--project", workspace, "--project-name", "P"]));
            Assert.Equal(2, await EcosystemAiCli.RunAsync([
                "--create-project", "--create-session", "--catalog", catalog,
                "--project", workspace, "--project-name", "P"]));
            Assert.Equal(2, await EcosystemAiCli.RunAsync([
                "--create-session", "--catalog", catalog, "--project-id",
                Guid.NewGuid().ToString("N"), "--session-title", "S"]));
            Assert.False(File.Exists(Path.Combine(catalog, "catalog.json")));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Catalogo_dentro_do_workspace_e_workspace_inexistente_recusados()
    {
        var root = Root();
        var workspace = Path.Combine(root, "workspace");
        Directory.CreateDirectory(workspace);
        try
        {
            var unsafeCatalog = Path.Combine(workspace, "private", "catalog");
            Assert.Throws<ArgumentException>(() =>
                CliCatalogManagement.CreateProject(unsafeCatalog, workspace, "Projeto"));
            Assert.False(File.Exists(Path.Combine(unsafeCatalog, "catalog.json")));
            Assert.Equal(2, await EcosystemAiCli.RunAsync([
                "--create-project", "--catalog", Path.Combine(root, "catalog"),
                "--project", Path.Combine(root, "missing"), "--project-name", "P"]));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void Menu_em_EOF_ou_entrada_errada_termina_sem_modificar_catalogo()
    {
        var root = Root();
        try
        {
            using var input = new StringReader("opcao desconhecida\n");
            using var output = new StringWriter();
            Assert.Equal(0, CliCatalogManagement.Manage(Path.Combine(root, "catalog"), input, output));
            Assert.Contains("Opção inválida", output.ToString());
            Assert.False(File.Exists(Path.Combine(root, "catalog", "catalog.json")));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
}
