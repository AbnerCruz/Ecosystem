using EcosystemAi.Cli;
using EcosystemAi.ProjectStore;

namespace AgentRuntime.Tests;

public sealed class R4InteractiveChatTests
{
    private static string Root() =>
        Path.Combine(Path.GetTempPath(), "r4-chat-" + Guid.NewGuid().ToString("N"));

    private static Dictionary<string, string> Args(string catalog, string workspace,
        string projectId, string sessionId) => new(StringComparer.Ordinal)
    {
        ["--catalog"] = catalog,
        ["--project"] = workspace,
        ["--project-id"] = projectId,
        ["--session-id"] = sessionId,
        ["--endpoint"] = "http://127.0.0.1:9123/chat/completions",
        ["--model"] = "mock-model",
        ["--budget-cents"] = "30",
        ["--max-call-cents"] = "5",
        ["--input-usd-per-million"] = "1",
        ["--output-usd-per-million"] = "3",
    };

    [Fact]
    public async Task Mensagens_sao_runners_reais_delegados_sem_criar_loop_de_provedor()
    {
        var root = Root();
        var workspace = Path.Combine(root, "workspace");
        var catalog = Path.Combine(root, "private", "catalog");
        Directory.CreateDirectory(workspace);
        var originalFile = Path.Combine(workspace, "README.md");
        File.WriteAllText(originalFile, "Não mexer");
        try
        {
            var store = new LocalProjectStore(catalog);
            var project = store.CreateProject("Meu projeto", workspace);
            var session = store.CreateSession(project.Id, "Chat");
            using var input = new StringReader("Primeira pergunta\nSegunda pergunta\n/sair\n");
            using var output = new StringWriter();
            var requests = new List<string[]>();
            async Task<int> FakeExecutor(string[] args)
            {
                requests.Add(args);
                var goalIndex = Array.IndexOf(args, "--goal");
                store.AppendTurn(project.Id, session.Id, "user", args[goalIndex + 1]);
                store.AppendTurn(project.Id, session.Id, "assistant", "Resposta mockada");
                await Task.CompletedTask;
                return 0;
            }

            var result = await CliInteractiveChat.RunAsync(
                Args(catalog, workspace, project.Id, session.Id),
                allowCreate: false, input, output, FakeExecutor);

            Assert.Equal(0, result);
            Assert.Equal(2, requests.Count);
            Assert.DoesNotContain("--use-history", requests[0]);
            Assert.Contains("--use-history", requests[1]);
            Assert.DoesNotContain("--allow-create", requests[0]);
            Assert.Equal("Primeira pergunta", requests[0][Array.IndexOf(requests[0], "--goal") + 1]);
            Assert.Equal("Segunda pergunta", requests[1][Array.IndexOf(requests[1], "--goal") + 1]);
            Assert.Equal("Não mexer", File.ReadAllText(originalFile));
            Assert.Equal(4, store.Read().Projects.Single().Sessions.Single().Turns.Count);
            Assert.Contains("orçamento por tarefa", output.ToString());
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Historico_preexistente_e_grant_de_escrita_explicito_sao_preservados()
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
            store.AppendTurn(project.Id, session.Id, "user", "Contexto anterior");
            using var input = new StringReader("Novo trabalho\n/sair\n");
            using var output = new StringWriter();
            string[]? dispatched = null;
            var result = await CliInteractiveChat.RunAsync(
                Args(catalog, workspace, project.Id, session.Id),
                allowCreate: true, input, output, args =>
                {
                    dispatched = args;
                    return Task.FromResult(0);
                });
            Assert.Equal(0, result);
            Assert.NotNull(dispatched);
            Assert.Contains("--use-history", dispatched);
            Assert.Contains("--allow-create", dispatched);
            Assert.DoesNotContain("--chat", dispatched);
            Assert.Equal("Contexto anterior",
                store.Read().Projects.Single().Sessions.Single().Turns.Single().Text);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Conversa_vazia_sair_e_ajuda_nao_chamam_provedor()
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
            using var input = new StringReader("\n/ajuda\n   \n" + new string('x', 16385) + "\n/sair\n");
            using var output = new StringWriter();
            var calls = 0;
            var result = await CliInteractiveChat.RunAsync(
                Args(catalog, workspace, project.Id, session.Id),
                allowCreate: false, input, output, _ =>
                {
                    calls++;
                    return Task.FromResult(0);
                });
            Assert.Equal(0, result);
            Assert.Equal(0, calls);
            Assert.Contains("Mensagem excede", output.ToString());
            Assert.Equal(2, store.Read().Revision);

            using var eof = new StringReader("");
            using var eofOutput = new StringWriter();
            Assert.Equal(0, await CliInteractiveChat.RunAsync(
                Args(catalog, workspace, project.Id, session.Id),
                false, eof, eofOutput, _ => throw new Exception("Não deve executar")));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Falha_na_tarefa_nao_dispara_retry_automatico_e_codigo_e_propagado()
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
            using var input = new StringReader("Faça isto\n/sair\n");
            using var output = new StringWriter();
            var calls = 0;
            var status = await CliInteractiveChat.RunAsync(
                Args(catalog, workspace, project.Id, session.Id), false,
                input, output, _ => { calls++; return Task.FromResult(1); });
            Assert.Equal(1, status);
            Assert.Equal(1, calls);
            Assert.Contains("não fará retry automático", output.ToString());
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Projeto_sessao_workspace_e_parametros_estranhos_sao_rejeitados_sem_modelo()
    {
        var root = Root();
        var workspace = Path.Combine(root, "workspace");
        var different = Path.Combine(root, "other");
        var catalog = Path.Combine(root, "catalog");
        Directory.CreateDirectory(workspace);
        Directory.CreateDirectory(different);
        try
        {
            var store = new LocalProjectStore(catalog);
            var project = store.CreateProject("P", workspace);
            var session = store.CreateSession(project.Id, "S");
            var args = Args(catalog, workspace, project.Id, session.Id);
            using var input = new StringReader("Olá\n");
            using var output = new StringWriter();
            var calls = 0;
            Task<int> Fake(string[] _) { calls++; return Task.FromResult(0); }

            var wrongWorkspace = Args(catalog, different, project.Id, session.Id);
            await Assert.ThrowsAsync<ArgumentException>(() =>
                CliInteractiveChat.RunAsync(wrongWorkspace, false, input, output, Fake));

            args["--session-id"] = Guid.NewGuid().ToString("N");
            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                CliInteractiveChat.RunAsync(args, false, input, output, Fake));

            args["--session-id"] = session.Id;
            args["--goal"] = "sem comando";
            await Assert.ThrowsAsync<ArgumentException>(() =>
                CliInteractiveChat.RunAsync(args, false, input, output, Fake));
            args.Remove("--goal");
            args["--budget-cents"] = "0";
            await Assert.ThrowsAsync<ArgumentException>(() =>
                CliInteractiveChat.RunAsync(args, false, input, output, Fake));
            Assert.Equal(0, calls);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Modo_Chat_recusa_outra_modalidade_sem_criar_dados()
    {
        var root = Root();
        try
        {
            var catalog = Path.Combine(root, "catalog");
            Assert.Equal(2, await EcosystemAiCli.RunAsync([
                "--chat", "--list", "--catalog", catalog]));
            Assert.Equal(2, await EcosystemAiCli.RunAsync([
                "--chat", "--create-project", "--catalog", catalog]));
            Assert.Equal(2, await EcosystemAiCli.RunAsync([
                "--chat", "--goal", "fazer algo", "--catalog", catalog]));
            Assert.False(File.Exists(Path.Combine(catalog, "catalog.json")));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
