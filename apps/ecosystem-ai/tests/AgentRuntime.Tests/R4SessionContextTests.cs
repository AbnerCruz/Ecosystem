using EcosystemAi.Cli;
using EcosystemAi.ProjectStore;

namespace AgentRuntime.Tests;

public sealed class R4SessionContextTests
{
    private static string Tmp() => Path.Combine(Path.GetTempPath(), "r4-conversation-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void Reabre_sessao_e_prepara_ultima_conversa_em_JSON_sem_mudar_catalogo()
    {
        var dir = Tmp();
        var workspace = Path.Combine(dir, "workspace");
        var catalog = Path.Combine(dir, "catalog");
        Directory.CreateDirectory(workspace);
        try
        {
            var store = new LocalProjectStore(catalog);
            var project = store.CreateProject("Projeto", workspace);
            var session = store.CreateSession(project.Id, "Conversa");
            store.AppendTurn(project.Id, session.Id, "user", "Primeiro pedido");
            store.AppendTurn(project.Id, session.Id, "assistant", "Primeira resposta");
            var before = store.Read();
            var goal = CliSessionContext.Compose(before, project.Id, session.Id, "Segundo pedido");

            Assert.Contains("\"role\":\"user\"", goal);
            Assert.Contains("\"role\":\"assistant\"", goal);
            Assert.Contains("Primeiro pedido", goal);
            Assert.Contains("Primeira resposta", goal);
            Assert.Contains("Segundo pedido", goal);
            Assert.Equal(1, goal.Split("Segundo pedido", StringSplitOptions.None).Length - 1);
            Assert.True(goal.IndexOf("Primeiro pedido", StringComparison.Ordinal)
                < goal.IndexOf("Primeira resposta", StringComparison.Ordinal));
            Assert.Equal(before.Revision, store.Read().Revision);
            Assert.Equal(2, store.Read().Projects.Single().Sessions.Single().Turns.Count);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void Nunca_inclui_mais_de_dez_mensagens_e_elimina_antigas_sem_resumir()
    {
        var dir = Tmp();
        var workspace = Path.Combine(dir, "workspace");
        var catalog = Path.Combine(dir, "catalog");
        Directory.CreateDirectory(workspace);
        try
        {
            var store = new LocalProjectStore(catalog);
            var project = store.CreateProject("Projeto", workspace);
            var session = store.CreateSession(project.Id, "Histórico");
            for (var i = 0; i < 12; i++)
                store.AppendTurn(project.Id, session.Id, "user", "mensagem-" + i);
            var goal = CliSessionContext.Compose(store.Read(), project.Id, session.Id, "nova");
            Assert.DoesNotContain("mensagem-0\"", goal);
            Assert.DoesNotContain("mensagem-1\"", goal);
            Assert.Contains("mensagem-2", goal);
            Assert.Contains("mensagem-11", goal);
            Assert.Equal(10, goal.Split("\"role\":\"user\"", StringSplitOptions.None).Length - 1);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void Mensagem_recente_acima_do_teto_e_rejeitada_sem_buscar_conta_antiga()
    {
        var dir = Tmp();
        var workspace = Path.Combine(dir, "workspace");
        var catalog = Path.Combine(dir, "catalog");
        Directory.CreateDirectory(workspace);
        try
        {
            var store = new LocalProjectStore(catalog);
            var project = store.CreateProject("P", workspace);
            var session = store.CreateSession(project.Id, "S");
            store.AppendTurn(project.Id, session.Id, "user", "pequena");
            store.AppendTurn(project.Id, session.Id, "assistant", new string('x', 14000));
            Assert.Throws<InvalidOperationException>(() =>
                CliSessionContext.Compose(store.Read(), project.Id, session.Id, "nova"));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void Sessao_errada_ou_sem_mensagens_falha_fechada()
    {
        var dir = Tmp();
        var workspace = Path.Combine(dir, "workspace");
        var catalog = Path.Combine(dir, "catalog");
        Directory.CreateDirectory(workspace);
        try
        {
            var store = new LocalProjectStore(catalog);
            var project = store.CreateProject("P", workspace);
            var session = store.CreateSession(project.Id, "S");
            var snapshot = store.Read();
            Assert.Throws<InvalidOperationException>(() => CliSessionContext.Compose(
                snapshot, project.Id, session.Id, "nova"));
            Assert.Throws<KeyNotFoundException>(() => CliSessionContext.Compose(
                snapshot, project.Id, Guid.NewGuid().ToString("N"), "nova"));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public async Task Modo_requires_opt_in_e_recusa_consulta_e_nova_sessao()
    {
        Assert.Equal(2, await EcosystemAiCli.RunAsync(["--use-history"]));
        Assert.Equal(2, await EcosystemAiCli.RunAsync([
            "--show-run", "--use-history", "--journal", "/tmp/runs", "--run-id", "x"]));
        Assert.Equal(2, await EcosystemAiCli.RunAsync([
            "--list", "--use-history", "--catalog", "/tmp/catalog"]));
    }
}
