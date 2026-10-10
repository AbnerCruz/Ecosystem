using EcosystemAi.Cli;
using EcosystemAi.ProjectStore;

namespace AgentRuntime.Tests;

public sealed class R4ChatSessionBudgetTests
{
    private static string NewRoot() =>
        Path.Combine(Path.GetTempPath(), "r4-chat-budget-" + Guid.NewGuid().ToString("N"));

    private static Dictionary<string, string> Settings(string catalog, string workspace,
        string projectId, string sessionId) => new(StringComparer.Ordinal)
    {
        ["--catalog"] = catalog,
        ["--project"] = workspace,
        ["--project-id"] = projectId,
        ["--session-id"] = sessionId,
        ["--endpoint"] = "http://127.0.0.1:9123/chat/completions",
        ["--model"] = "mock",
        ["--budget-cents"] = "30",
        ["--max-call-cents"] = "5",
        ["--input-usd-per-million"] = "1",
        ["--output-usd-per-million"] = "3",
    };

    [Fact]
    public async Task Teto_reserva_todo_orcamento_antes_do_run_e_reduz_ultimo_turno()
    {
        var root = NewRoot();
        var workspace = Path.Combine(root, "workspace");
        var catalog = Path.Combine(root, "catalog");
        Directory.CreateDirectory(workspace);
        var original = Path.Combine(workspace, "important.md");
        File.WriteAllText(original, "Intocado");
        try
        {
            var store = new LocalProjectStore(catalog);
            var project = store.CreateProject("P", workspace);
            var session = store.CreateSession(project.Id, "Sessão alvo");
            store.AppendRun(project.Id, session.Id,
                new RunReceipt("run-existing", "succeeded", 10, "USD", true,
                    "verified", DateTimeOffset.UtcNow));
            var other = store.CreateSession(project.Id, "Outra sessão");
            store.AppendRun(project.Id, other.Id,
                new RunReceipt("run-other", "succeeded", 100, "USD", true,
                    "verified", DateTimeOffset.UtcNow));
            var revision = store.Read().Revision;
            var goals = new List<string[]>();
            using var input = new StringReader("Um\nDois\nTrês\n/sair\n");
            using var output = new StringWriter();
            var result = await CliInteractiveChat.RunAsync(
                Settings(catalog, workspace, project.Id, session.Id),
                allowCreate: false, input, output,
                args => { goals.Add(args); return Task.FromResult(0); },
                sessionBudgetCents: 55);

            Assert.Equal(0, result);
            Assert.Equal(2, goals.Count);
            Assert.Equal("30", goals[0][Array.IndexOf(goals[0], "--budget-cents") + 1]);
            Assert.Equal("15", goals[1][Array.IndexOf(goals[1], "--budget-cents") + 1]);
            Assert.Equal("5", goals[1][Array.IndexOf(goals[1], "--max-call-cents") + 1]);
            Assert.Equal("Um", goals[0][Array.IndexOf(goals[0], "--goal") + 1]);
            Assert.Equal("Dois", goals[1][Array.IndexOf(goals[1], "--goal") + 1]);
            Assert.DoesNotContain("--session-budget-cents", goals[0]);
            Assert.Contains("Teto da sessão atingido", output.ToString());
            Assert.Contains("recibos prévios: 10", output.ToString());
            Assert.Equal(revision, store.Read().Revision);
            Assert.Equal("Intocado", File.ReadAllText(original));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Teto_menor_que_limite_de_operacao_ajusta_budget_e_maxcall()
    {
        var root = NewRoot();
        var workspace = Path.Combine(root, "workspace");
        var catalog = Path.Combine(root, "catalog");
        Directory.CreateDirectory(workspace);
        try
        {
            var store = new LocalProjectStore(catalog);
            var project = store.CreateProject("P", workspace);
            var session = store.CreateSession(project.Id, "S");
            using var input = new StringReader("Primeiro\nSegundo\n");
            using var output = new StringWriter();
            var dispatched = new List<string[]>();
            var result = await CliInteractiveChat.RunAsync(
                Settings(catalog, workspace, project.Id, session.Id),
                false, input, output, a => { dispatched.Add(a); return Task.FromResult(0); },
                sessionBudgetCents: 3);
            Assert.Equal(0, result);
            var args = Assert.Single(dispatched);
            Assert.Equal("3", args[Array.IndexOf(args, "--budget-cents") + 1]);
            Assert.Equal("3", args[Array.IndexOf(args, "--max-call-cents") + 1]);
            Assert.Contains("Teto da sessão atingido", output.ToString());
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Recibos_antigos_e_moeda_diferente_bloqueiam_teto_ao_abrir()
    {
        var root = NewRoot();
        var workspace = Path.Combine(root, "workspace");
        var catalog = Path.Combine(root, "catalog");
        Directory.CreateDirectory(workspace);
        try
        {
            var store = new LocalProjectStore(catalog);
            var project = store.CreateProject("P", workspace);
            var session = store.CreateSession(project.Id, "S");
            store.AppendRun(project.Id, session.Id,
                new RunReceipt("run-legacy", "succeeded", 25, "USD",
                    true, "done", DateTimeOffset.UtcNow));
            using var input = new StringReader("Não enviar\n");
            using var output = new StringWriter();
            var result = await CliInteractiveChat.RunAsync(
                Settings(catalog, workspace, project.Id, session.Id), false,
                input, output, _ => throw new Exception("Nenhum gasto permitido"), 20);
            Assert.Equal(0, result);
            Assert.Contains("Teto da sessão atingido", output.ToString());

            var brl = store.CreateSession(project.Id, "BRL");
            store.AppendRun(project.Id, brl.Id,
                new RunReceipt("run-brl", "succeeded", 1, "BRL",
                    true, "done", DateTimeOffset.UtcNow));
            using var empty = new StringReader("");
            using var output2 = new StringWriter();
            await Assert.ThrowsAsync<InvalidDataException>(() =>
                CliInteractiveChat.RunAsync(
                    Settings(catalog, workspace, project.Id, brl.Id), false,
                    empty, output2, _ => Task.FromResult(0), 50));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Flag_apenas_no_chat_e_teto_invalido_recusado_sem_chamada()
    {
        var root = NewRoot();
        Directory.CreateDirectory(root);
        try
        {
            var catalog = Path.Combine(root, "catalog");
            Assert.Equal(2, await EcosystemAiCli.RunAsync(["--session-budget-cents", "50"]));
            Assert.Equal(2, await EcosystemAiCli.RunAsync([
                "--web-ui", "--catalog", catalog, "--session-budget-cents", "50"]));
            Assert.Equal(2, await EcosystemAiCli.RunAsync([
                "--chat", "--catalog", catalog, "--session-budget-cents", "0"]));
            Assert.False(File.Exists(Path.Combine(catalog, "catalog.json")));
        }
        finally { Directory.Delete(root, true); }
    }
}
