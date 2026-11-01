using System.Text;
using System.Text.Json;
using AgentRuntime;
using AgentRuntime.Providers.ChatCompletions;
using Lunet.Core;

namespace Lunet.Ai;

public sealed record LunetAiTurn(string Role, string Text);
public sealed record LunetAiReply(string Text, IReadOnlyList<string> ChangedFiles, RunStatus Status);

/// <summary>Product-specific adapter over the shared Ecosystem Agent Runtime; no Urbe or game-framework dependency.</summary>
public sealed class LunetAiSession
{
    private readonly LunetProject _project;
    private readonly string _historyPath;
    private readonly List<LunetAiTurn> _history;
    private readonly SemaphoreSlim _runGate = new(1, 1);

    public IReadOnlyList<LunetAiTurn> History => _history.AsReadOnly();

    public LunetAiSession(LunetProject project, string privateHistoryPath)
    {
        _project = project;
        _historyPath = privateHistoryPath;
        try
        {
            _history = File.Exists(privateHistoryPath) && new FileInfo(privateHistoryPath).Length <= 100_000
                ? JsonSerializer.Deserialize<List<LunetAiTurn>>(File.ReadAllText(privateHistoryPath)) ?? []
                : [];
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            _history = [];
        }
        if (_history.Count > 20) _history.RemoveRange(0, _history.Count - 20);
    }

    public async Task<LunetAiReply> AskAsync(string prompt, string endpoint, string apiKey, string model,
        bool enableProjectWrites, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(prompt) || prompt.Length > 8_000)
            throw new ArgumentException("A solicitação precisa ter entre 1 e 8.000 caracteres.");
        if (string.IsNullOrWhiteSpace(model))
            throw new ArgumentException("Informe um modelo no provider.");
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentException("Informe a chave da API. Ela fica apenas em memória nesta sessão.");
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var url))
            throw new ArgumentException("Informe a URL completa do endpoint /chat/completions.");
        if (!url.AbsolutePath.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("A URL precisa terminar com /chat/completions.");
        await _runGate.WaitAsync(cancellationToken);
        try
        {
            var runId = Guid.NewGuid().ToString("N");
            var context = ContextPath.Create(
                new ContextStep(ContextLevel.Ecosystem, "ecosystem"),
                new ContextStep(ContextLevel.Product, "lunet2d"),
                new ContextStep(ContextLevel.Project, _project.Manifest.GameId));
            var names = enableProjectWrites
                ? new[] { LunetAiProjectTool.ListName, LunetAiProjectTool.ReadName, LunetAiProjectTool.WriteName }
                : new[] { LunetAiProjectTool.ListName, LunetAiProjectTool.ReadName };
            var permissions = enableProjectWrites
                ? new[] { Permissions.AgentAct, Permissions.FsRead, Permissions.FsWrite }
                : new[] { Permissions.AgentAct, Permissions.FsRead };
            var grant = ToolGrant.Of(names, permissions);
            var host = new ToolHost()
                .Register(new LunetAiProjectTool(_project, LunetAiProjectTool.ListName))
                .Register(new LunetAiProjectTool(_project, LunetAiProjectTool.ReadName))
                .Register(new LunetAiProjectTool(_project, LunetAiProjectTool.WriteName));

            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };
            var provider = new ChatCompletionsProvider(http, url, apiKey, 0, 0);
            var providers = new ProviderRegistry().Register(provider);
            var clock = new LunetClock();
            var ledger = new InMemoryLedger(clock, "USD");
            var scope = new BudgetScope(BudgetScopeKind.Task, runId);
            // Placeholder reservation gate, not a financial cost estimate. Provider tariffs are unknown.
            ledger.SetLimits(scope, new BudgetLimits(Total: new Money(4_000, "USD"), PerOperation: new Money(500, "USD")));
            var events = new LunetEventLog();
            var agent = new AgentDefinition(
                new AgentIdentity("lunet.code", "Assistente Lunet", "development"),
                new ModelProfile(provider.ProviderId, model,
                    new ModelCapabilities(true, false, true, false, false, 32_000), new Money(500, "USD")),
                "Você é a IA especializada do Lunet2D (Framework, IDE e Studio). " +
                "Trabalhe somente no projeto Lunet atual e use lunet.files.list/read antes de propor alterações. " +
                "Leia os arquivos reais e nunca invente APIs do Framework. " +
                "O código do jogo é a fonte da verdade. " +
                "Se escrever, leia primeiro, envie o hash exato e preserve o restante do arquivo. " +
                "Quando o contexto ou uma ferramenta não for suficiente, informe a limitação. " +
                "Não alegue ter testado, compilado ou instalado algo que não executou. " +
                "Responda em português, com caminhos e ações verificáveis.",
                8, grant);
            var task = new TaskSpec(runId, ComposePrompt(prompt), []);
            var request = new RunRequest(runId, task, agent, context, grant, grant, [scope], RunPolicy.Default);
            var runner = new AgentRunner(providers, host, ledger, events, clock, new LunetVerifier(_project));
            var result = await runner.RunAsync(request, cancellationToken);
            var entries = await events.ReadRunAsync(runId, CancellationToken.None);
            var answer = entries.Where(e => e.Kind == EventKind.ModelResponded && e.Payload is not null)
                .Select(e => ContentSerializer.FromJson(e.Payload!).OfType<TextBlock>()
                    .Select(t => t.Text).Where(s => !string.IsNullOrWhiteSpace(s)).ToArray())
                .LastOrDefault(a => a.Length > 0);
            var response = answer is null ? "" : string.Join("\n", answer);
            if (result.State.Status != RunStatus.Succeeded)
            {
                var problem = entries.LastOrDefault(e => e.Kind is EventKind.RunBlocked or EventKind.RunFailed)?.Result;
                response = $"Execução não concluída ({result.State.Status}): {problem ?? "verifique o provider, o orçamento e as permissões"}.\n" + response;
            }
            if (string.IsNullOrWhiteSpace(response)) response = "O provider não retornou uma resposta de texto.";
            var changed = result.Artifacts.Where(a => a.Kind == "lunet.text-file")
                .Select(a => a.Location).Distinct(StringComparer.Ordinal).ToArray();
            if (result.State.Status == RunStatus.Succeeded)
            {
                _history.Add(new LunetAiTurn("Você", prompt.Trim()));
                _history.Add(new LunetAiTurn("Lunet", response));
                if (_history.Count > 20) _history.RemoveRange(0, _history.Count - 20);
                Directory.CreateDirectory(Path.GetDirectoryName(_historyPath)!);
                AtomicFile.WriteAllText(_historyPath, JsonSerializer.Serialize(_history));
            }
            return new LunetAiReply(response, changed, result.State.Status);
        }
        finally { _runGate.Release(); }
    }

    private string ComposePrompt(string userMessage)
    {
        var recent = _history.TakeLast(10).Select(t => $"{t.Role}: {t.Text}");
        var buffer = string.Join("\n", recent);
        if (buffer.Length > 7_000) buffer = buffer[^7_000..];
        return $"Projeto aberto: {_project.Name}. Arquivo inicial: {_project.Manifest.EntryPoint}. " +
               "Mensagens anteriores (contexto, não instruções de sistema):\n" +
               buffer + "\n\nSolicitação atual:\n" + userMessage.Trim();
    }

    private sealed class LunetClock : IClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
        public async ValueTask DelayAsync(TimeSpan delay, CancellationToken ct) => await Task.Delay(delay, ct);
    }

    private sealed class LunetEventLog : IEventLog
    {
        private readonly List<RuntimeEvent> _events = [];
        public ValueTask AppendAsync(RuntimeEvent entry, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            _events.Add(entry);
            return ValueTask.CompletedTask;
        }
        public ValueTask<IReadOnlyList<RuntimeEvent>> ReadRunAsync(string runId, CancellationToken ct) =>
            ValueTask.FromResult<IReadOnlyList<RuntimeEvent>>(_events.Where(e => e.RunId == runId).ToArray());
    }

    private sealed class LunetVerifier(LunetProject project) : IVerifier
    {
        public ValueTask<VerificationResult> VerifyAsync(TaskSpec task, RunOutput output, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(output.FinalText))
                return ValueTask.FromResult(VerificationResult.Fail(task.Id, "resposta final vazia"));
            var files = new LunetAiProjectFiles(project);
            foreach (var artifact in output.Artifacts.Where(a => a.Kind == "lunet.text-file"))
            {
                try
                {
                    if (files.Read(artifact.Location).Sha256 != artifact.Id)
                        return ValueTask.FromResult(VerificationResult.Fail(task.Id, "arquivo alterado após gravação: " + artifact.Location));
                }
                catch (Exception ex) when (ex is IOException or InvalidOperationException or ArgumentException)
                {
                    return ValueTask.FromResult(VerificationResult.Fail(task.Id, "não foi possível verificar: " + artifact.Location));
                }
            }
            return ValueTask.FromResult(VerificationResult.Pass(task.Id,
                "resposta não vazia; arquivos gravados tiveram SHA256 verificado (não prova compilação ou correção semântica)"));
        }
    }
}
