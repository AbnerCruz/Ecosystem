using System.Security.Cryptography;
using System.Text;
using AgentRuntime;
using EcosystemAi.Product;
using EcosystemAi.ProjectStore;

namespace AgentRuntime.Tests;

public sealed class ReviewedProjectFileTests
{
    private sealed class Fixture : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "reviewed-file-" + Guid.NewGuid().ToString("N"));
        public string Catalog => Path.Combine(Root, "catalog");
        public string Journal => Path.Combine(Root, "journal");
        public string Workspace => Path.Combine(Root, "workspace");
        public LocalProjectStore Store { get; }
        public ProjectEntry Project { get; }
        public SessionEntry Session { get; }
        public ReviewedProjectFileHost Host { get; }
        public const string Response = "# Produção revisada\n\nTexto completo: ação, ç, 🏙️.\n";
        public Fixture()
        {
            Directory.CreateDirectory(Workspace);
            Store = new LocalProjectStore(Catalog);
            Project = Store.CreateProject("Projeto", Workspace);
            Session = Store.CreateSession(Project.Id, "Conversa");
            Store.AppendTurn(Project.Id, Session.Id, "user", "Produza um texto");
            Store.AppendTurn(Project.Id, Session.Id, "assistant", Response);
            Host = new ReviewedProjectFileHost(Catalog, Journal);
        }
        public ReviewedProjectFile Prepare(string name = "resultado.md") => Host.Prepare(Project.Id, Session.Id, 1, name);
        public void Dispose() => Directory.Delete(Root, true);
    }

    [Fact]
    public async Task Revisao_exata_publica_bytes_reais_sem_mudar_catalogo_ou_criar_run_ficticio()
    {
        using var f = new Fixture();
        var before = File.ReadAllBytes(Path.Combine(f.Catalog, "catalog.json"));
        var proposal = f.Prepare();
        Assert.Empty(Directory.GetFileSystemEntries(f.Workspace));
        Assert.Equal(Fixture.Response, proposal.Content);
        var result = await f.Host.PublishAsync(proposal, true);
        var bytes = File.ReadAllBytes(Path.Combine(f.Workspace, "resultado.md"));
        Assert.Equal(Encoding.UTF8.GetBytes(Fixture.Response), bytes);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(), result.Sha256);
        Assert.Equal(IntegrationStatus.Integrated, result.Integration.Status);
        Assert.Equal("local-operator", result.Integration.ReviewerId);
        Assert.NotEqual(result.Integration.ProducerId, result.Integration.ReviewerId);
        Assert.Single(Directory.GetFiles(f.Workspace));
        Assert.Equal(before, File.ReadAllBytes(Path.Combine(f.Catalog, "catalog.json")));
        Assert.Empty(f.Store.Read().Projects.Single().Sessions.Single().Runs);
        Assert.False(Directory.Exists(f.Journal));
    }

    [Fact]
    public async Task Recusa_cancelamento_e_confirmacao_repetida_nao_publicam()
    {
        using var f = new Fixture();
        var proposal = f.Prepare();
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Host.PublishAsync(proposal, false));
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => f.Host.PublishAsync(proposal, true, cancel.Token));
        Assert.Empty(Directory.GetFileSystemEntries(f.Workspace));
        await f.Host.PublishAsync(proposal, true);
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Host.PublishAsync(proposal, true));
        Assert.Single(Directory.GetFiles(f.Workspace));
    }

    [Fact]
    public async Task Criacao_concorrente_nao_sobrescreve_arquivo_e_requer_nova_revisao()
    {
        using var f = new Fixture();
        var proposal = f.Prepare();
        var existing = Path.Combine(f.Workspace, "resultado.md");
        File.WriteAllText(existing, "trabalho concorrente");
        await Assert.ThrowsAsync<IOException>(() => f.Host.PublishAsync(proposal, true));
        Assert.Equal("trabalho concorrente", File.ReadAllText(existing));
        Assert.Single(Directory.GetFileSystemEntries(f.Workspace));
        Assert.Throws<IOException>(() => f.Prepare("RESULTADO.MD"));
    }

    [Fact]
    public async Task Duas_propostas_com_mesmo_destino_so_publicam_uma_vez()
    {
        using var f = new Fixture();
        var first = f.Prepare(); var second = f.Prepare();
        var attempts = await Task.WhenAll(new[] { first, second }.Select(async p =>
        {
            try { await f.Host.PublishAsync(p, true); return true; }
            catch (IOException) { return false; }
        }));
        Assert.Single(attempts, success => success);
        Assert.Single(Directory.GetFileSystemEntries(f.Workspace));
    }

    [Theory]
    [InlineData("../escape.md")]
    [InlineData("/escape.md")]
    [InlineData("folder/file.md")]
    [InlineData("folder\\file.md")]
    [InlineData(".catalog.md")]
    [InlineData("CON.md")]
    [InlineData("LPT1.txt")]
    [InlineData("script.cs")]
    [InlineData("a.md:stream")]
    [InlineData("a.md\n")]
    [InlineData("a.md.")]
    public void Nomes_nao_portateis_ou_executaveis_sao_recusados(string name)
    {
        using var f = new Fixture();
        Assert.Throws<ArgumentException>(() => f.Prepare(name));
        Assert.Empty(Directory.GetFileSystemEntries(f.Workspace));
    }

    [Fact]
    public async Task Proposta_de_outro_host_e_troca_do_workspace_sao_recusadas()
    {
        using var f = new Fixture();
        var proposal = f.Prepare();
        var otherHost = new ReviewedProjectFileHost(f.Catalog, f.Journal);
        await Assert.ThrowsAsync<ArgumentException>(() => otherHost.PublishAsync(proposal, true));
        var otherRoot = Path.Combine(f.Root, "other"); Directory.CreateDirectory(otherRoot);
        f.Store.RebindWorkspace(f.Project.Id, f.Workspace, otherRoot);
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Host.PublishAsync(proposal, true));
        Assert.Empty(Directory.GetFileSystemEntries(f.Workspace));
        Assert.Empty(Directory.GetFileSystemEntries(otherRoot));
    }

    [Fact]
    public void Turno_do_usuario_indice_invalido_e_sessao_de_outro_projeto_nao_sao_resposta()
    {
        using var f = new Fixture();
        Assert.Throws<ArgumentException>(() => f.Host.Prepare(f.Project.Id, f.Session.Id, 0, "a.md"));
        Assert.Throws<ArgumentException>(() => f.Host.Prepare(f.Project.Id, f.Session.Id, 99, "a.md"));
        var other = f.Store.CreateProject("Outro", f.Workspace);
        Assert.Throws<KeyNotFoundException>(() => f.Host.Prepare(other.Id, f.Session.Id, 1, "a.md"));
        Directory.CreateDirectory(Path.Combine(f.Workspace, "directory.md"));
        Assert.Throws<IOException>(() => f.Prepare("directory.md"));
    }

    [Fact]
    public async Task Link_no_destino_e_raiz_substituida_por_link_nao_escapam_do_projeto()
    {
        if (OperatingSystem.IsWindows()) return; // Criar links exige privilégio específico no Windows.
        using var f = new Fixture();
        var outside = Path.Combine(f.Root, "outside"); Directory.CreateDirectory(outside);
        File.CreateSymbolicLink(Path.Combine(f.Workspace, "link.md"), Path.Combine(outside, "missing.md"));
        Assert.Throws<IOException>(() => f.Prepare("link.md"));
        File.Delete(Path.Combine(f.Workspace, "link.md"));
        var proposal = f.Prepare();
        var moved = Path.Combine(f.Root, "old-workspace"); Directory.Move(f.Workspace, moved);
        Directory.CreateSymbolicLink(f.Workspace, outside);
        await Assert.ThrowsAsync<ArgumentException>(() => f.Host.PublishAsync(proposal, true));
        Assert.Empty(Directory.GetFiles(outside));
        Assert.Empty(Directory.GetFiles(moved));
    }
}
