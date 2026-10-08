using System.Net;
using System.Text;
using System.Text.Json;
using AgentRuntime.Providers.ChatCompletions;
using AgentRuntime.Testing;
using AgentRuntime.Tools.Files;
using AgentWorkspace;

namespace AgentRuntime.Tests;

/// <summary>Prova R4 end-to-end no Product: provider HTTP fake + mesmo Workspace/Runner + Tool de arquivo real.</summary>
public class R4ProductPipelineTests
{
    private sealed class SequenceHandler(params string[] responses) : HttpMessageHandler
    {
        private int _next;
        public List<string> Requests { get; } = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            if (_next >= responses.Length) throw new InvalidOperationException("Model foi chamado além do cenário.");
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responses[_next++], Encoding.UTF8, "application/json")
            };
        }
    }

    [Fact]
    public async Task Product_usa_workspace_runtime_e_capability_reais_sem_servidor_externo()
    {
        var root = Path.Combine(Path.GetTempPath(), "ecosystem-r4-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(Path.Combine(root, "README.md"), "conteudo do projeto R4");
            var toolReply = """
            {"choices":[{"message":{"tool_calls":[{"id":"read-1","type":"function","function":{"name":"files.read","arguments":"{\"path\":\"README.md\"}"}}]},"finish_reason":"tool_calls"}],"usage":{"cost":0.01,"prompt_tokens":30,"completion_tokens":15}}
            """;
            var answerReply = """
            {"choices":[{"message":{"content":"O projeto contém conteudo do projeto R4."},"finish_reason":"stop"}],"usage":{"cost":0.02,"prompt_tokens":45,"completion_tokens":20}}
            """;
            var handler = new SequenceHandler(toolReply, answerReply);
            using var http = new HttpClient(handler);
            var provider = new ChatCompletionsProvider(http, new Uri("https://provider.example.test/v1/chat/completions"), "not-a-real-secret-token", 0, 0);
            var rig = new Rig();
            var scope = Rig.Context;
            rig.Host.Register(new FilesReadTool(new FileSandbox(root), scope));
            var clock = new FakeClock();
            var ledger = new InMemoryLedger(clock, "USD");
            var budget = new BudgetScope(BudgetScopeKind.Project, "real-file-r4");
            ledger.SetLimits(budget, new BudgetLimits(Total: new Money(10, "USD")));
            rig.Verifier = new DelegateVerifier((task, output) => output.FinalText.Contains("conteudo do projeto R4", StringComparison.Ordinal)
                ? VerificationResult.Pass(task.Id, "texto retornado após leitura do arquivo real")
                : VerificationResult.Fail(task.Id, "arquivo não foi inspecionado"));
            var grant = ToolGrant.Of([FileToolIds.Read], [Permissions.AgentAct, Permissions.FsRead]);
            var workspace = new WorkspaceSession("r4-real-file", rig.Runner(ledger: ledger, extraProvider: provider),
                scope, grant, grant, [budget]);
            var agent = new AgentDefinition(new AgentIdentity("r4", "Agent", "tester"),
                new ModelProfile(provider.ProviderId, "test-model", new ModelCapabilities(true, false, true, false, false, 4000), new Money(5, "USD")),
                "Leia o arquivo permitido.", 4, grant);
            var result = await workspace.ExecuteAsync("r4-run", new TaskSpec("read-real-file", "Leia README.md", ["texto real"]), agent);
            Assert.Equal(RunStatus.Succeeded, result.State.Status);
            Assert.Equal(2, handler.Requests.Count);
            using var first = JsonDocument.Parse(handler.Requests[0]);
            using var second = JsonDocument.Parse(handler.Requests[1]);
            Assert.Equal(FileToolIds.Read, first.RootElement.GetProperty("tools")[0].GetProperty("function").GetProperty("name").GetString());
            Assert.Equal("tool", second.RootElement.GetProperty("messages")[3].GetProperty("role").GetString());
            Assert.Contains("conteudo do projeto R4", second.RootElement.GetProperty("messages")[3].GetProperty("content").GetString());
            Assert.DoesNotContain("not-a-real-secret-token", handler.Requests[0]);
            Assert.DoesNotContain("not-a-real-secret-token", handler.Requests[1]);
            var events = rig.Events("r4-run");
            Assert.Contains(events, e => e.Kind == EventKind.ToolCalled && e.Tool == FileToolIds.Read);
            Assert.Contains(events, e => e.Kind == EventKind.VerificationPassed);
            Assert.All(events, e => Assert.Equal(scope.ToString(), e.ContextRef));
            Assert.Equal(3, events.Where(e => e.Kind == EventKind.ModelResponded).Sum(e => e.Cost!.Value.Minor));
            Assert.Equal(7, ledger.Remaining(budget)!.Value.Minor);
        }
        finally { Directory.Delete(root, recursive: true); }
    }
}
