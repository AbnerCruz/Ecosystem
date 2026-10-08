using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using AgentRuntime;
using AgentRuntime.Providers.ChatCompletions;
using AgentRuntime.Tools.Files;
using AgentWorkspace;

return await EcosystemAiCli.RunAsync(args);

public static class EcosystemAiCli
{
    private const string Usage = """
Ecosystem AI — R4 CLI inicial (Product; nao e a UI Android definitiva)

  dotnet run --project src/EcosystemAi.Cli -- \\
    --project /pasta/existente --goal "Inspecione README.md" \\
    --endpoint https://SEU-PROVEDOR/api/v1/chat/completions \\
    --model ID_DO_MODELO --budget-cents 30 --max-call-cents 5 \\
    --input-usd-per-million 1 --output-usd-per-million 3

Chave: ECOAI_API_KEY via ambiente (nunca argumento; sem gravação).
A pasta precisa existir. Somente files.read por padrão.
--allow-create permite APENAS arquivos novos (nunca sobrescrever).
--accept-exists arquivo.txt ativa verificador no disco (para tarefas de escrita).
Preços são fornecidos pelo operador; quando o provider nao devolve usage.cost,
o custo é ESTIMADO, não garantido. Nenhum background job, memória ou chat persistente.
""";

    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0 || args.Contains("--help", StringComparer.Ordinal))
        {
            Console.WriteLine(Usage);
            return 0;
        }
        Dictionary<string, string> fields = new(StringComparer.Ordinal);
        bool allowCreate = false;
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == "--allow-create") { allowCreate = true; continue; }
            if (!args[i].StartsWith("--", StringComparison.Ordinal) || i + 1 >= args.Length
                || args[i + 1].StartsWith("--", StringComparison.Ordinal) || !fields.TryAdd(args[i], args[++i]))
            {
                Console.Error.WriteLine("Parâmetros inválidos. Use --help.");
                return 2;
            }
        }
        try
        {
            string Need(string name) => fields.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value)
                ? value : throw new ArgumentException($"Falta {name}.");
            var allowed = new[] { "--project", "--goal", "--endpoint", "--model", "--budget-cents",
                "--max-call-cents", "--input-usd-per-million", "--output-usd-per-million", "--accept-exists" };
            if (fields.Keys.Except(allowed, StringComparer.Ordinal).Any())
                throw new ArgumentException("Parâmetro desconhecido.");
            var root = Path.GetFullPath(Need("--project"));
            if (!Directory.Exists(root)) throw new ArgumentException("O diretório de projeto precisa existir.");
            var goal = Need("--goal");
            var endpoint = new Uri(Need("--endpoint"), UriKind.Absolute);
            var model = Need("--model");
            var budgetCents = long.Parse(Need("--budget-cents"), CultureInfo.InvariantCulture);
            var callCents = long.Parse(Need("--max-call-cents"), CultureInfo.InvariantCulture);
            var priceIn = decimal.Parse(Need("--input-usd-per-million"), CultureInfo.InvariantCulture);
            var priceOut = decimal.Parse(Need("--output-usd-per-million"), CultureInfo.InvariantCulture);
            if (budgetCents < 1 || callCents < 1 || callCents > budgetCents)
                throw new ArgumentException("Orçamento por operação e total devem ser positivos; por operação <= total.");

            var secret = Environment.GetEnvironmentVariable("ECOAI_API_KEY");
            if (string.IsNullOrWhiteSpace(secret) && !endpoint.IsLoopback)
                throw new ArgumentException("Configure ECOAI_API_KEY para endpoint não-local.");

            // O endpoint não pode redirecionar a chave para outro host.
            using var http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
                { Timeout = TimeSpan.FromSeconds(90) };
            var provider = new ChatCompletionsProvider(http, endpoint, secret, priceIn, priceOut);
            var providers = new ProviderRegistry().Register(provider);
            var contextId = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(root))).ToLowerInvariant()[..16];
            var context = ContextPath.Create(
                new(ContextLevel.Ecosystem, "ecosystem"),
                new(ContextLevel.Product, "ecosystem-ai"),
                new(ContextLevel.Project, contextId));
            var sandbox = new FileSandbox(root);
            var host = new ToolHost().Register(new FilesReadTool(sandbox, context));
            var capabilities = new List<string> { FileToolIds.Read };
            var permissions = new List<string> { Permissions.AgentAct, Permissions.FsRead };
            if (allowCreate)
            {
                host.Register(new FilesWriteTool(sandbox, context));
                capabilities.Add(FileToolIds.Write);
                permissions.Add(Permissions.FsWrite);
            }
            var grant = ToolGrant.Of(capabilities, permissions);
            var clock = new WallClock();
            var ledger = new InMemoryLedger(clock, "USD");
            var scope = new BudgetScope(BudgetScopeKind.Project, contextId);
            ledger.SetLimits(scope, new BudgetLimits(Total: new Money(budgetCents, "USD"),
                PerOperation: new Money(callCents, "USD")));
            var log = new LocalRunEvents();
            IVerifier verifier = fields.TryGetValue("--accept-exists", out var file)
                ? new FileExistsVerifier(sandbox)
                : new ResponsePresentVerifier();
            var redactor = new SecretRedactor();
            if (!string.IsNullOrWhiteSpace(secret) && secret.Length >= SecretRedactor.MinimumLength)
                redactor.Register(secret);
            var runner = new AgentRunner(providers, host, ledger, log, clock, verifier, redactor: redactor);
            var workspace = new WorkspaceSession("cli:" + contextId, runner, context, grant, grant, [scope]);
            var profile = new ModelProfile(provider.ProviderId, model,
                new ModelCapabilities(true, false, true, false, false, 32_000), new Money(callCents, "USD"));
            var agent = new AgentDefinition(
                new AgentIdentity("cli-assistant", "Assistente", "executor"), profile,
                "Você opera no projeto autorizado. Use apenas as ferramentas anunciadas; sem autorização para sobrescrever ou excluir. Relate resultados e limites. Não invente testes ou verificações.",
                8, grant);
            var task = new TaskSpec("cli-task", goal, file is null ? ["response-present"] : ["exists:" + file]);
            using var cancel = new CancellationTokenSource();
            ConsoleCancelEventHandler handler = (_, e) => { e.Cancel = true; cancel.Cancel(); };
            Console.CancelKeyPress += handler;
            try
            {
                var result = await workspace.ExecuteAsync("cli-run-" + Guid.NewGuid().ToString("N"), task, agent,
                    cancellationToken: cancel.Token);
                var events = await log.ReadRunAsync(result.State.RunId, CancellationToken.None);
                foreach (var entry in events.Where(e => e.Kind is EventKind.ToolCalled or EventKind.ToolDenied
                    or EventKind.ToolResult or EventKind.ProviderFailed))
                    Console.WriteLine($"{entry.Kind}: {entry.Tool ?? entry.Result ?? "resposta"}");
                var final = events.Where(e => e.Kind == EventKind.ModelResponded && e.Payload is not null)
                    .Select(e => ContentSerializer.FromJson(e.Payload!))
                    .SelectMany(x => x.OfType<TextBlock>()).LastOrDefault()?.Text;
                if (!string.IsNullOrWhiteSpace(final)) Console.WriteLine("\n" + redactor.Redact(final));
                var spent = events.Where(e => e.Kind == EventKind.ModelResponded).Sum(e => e.Cost?.Minor ?? 0);
                Console.WriteLine($"Status: {result.State.Status}");
                Console.WriteLine($"Verificação: {result.Verification?.Evidence ?? "ausente"}");
                Console.WriteLine($"Custo: {(provider.LastCostEstimated ? "estimado " : "")}{spent} centavos USD; limite local {budgetCents}");
                foreach (var artifact in result.Artifacts) Console.WriteLine($"Artefato: {artifact.Location}");
                return result.State.Status == RunStatus.Succeeded ? 0 : 1;
            }
            finally { Console.CancelKeyPress -= handler; }
        }
        catch (Exception e) when (e is ArgumentException or FormatException or OverflowException or UriFormatException)
        {
            Console.Error.WriteLine("Configuração inválida: " + e.Message);
            return 2;
        }
        catch (OperationCanceledException) { Console.Error.WriteLine("Execução cancelada."); return 130; }
    }

    private sealed class WallClock : IClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
        public ValueTask DelayAsync(TimeSpan delay, CancellationToken token) => new(Task.Delay(delay, token));
    }

    // Adapter local para a porta IEventLog; estado e replay continuam no Core.
    private sealed class LocalRunEvents : IEventLog
    {
        private readonly Dictionary<string, List<RuntimeEvent>> _events = new(StringComparer.Ordinal);
        public ValueTask AppendAsync(RuntimeEvent entry, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (!_events.TryGetValue(entry.RunId, out var list)) _events[entry.RunId] = list = [];
            if (entry.Sequence != list.Count + 1) throw new InvalidOperationException("Sequência inconsistente.");
            list.Add(entry);
            return ValueTask.CompletedTask;
        }
        public ValueTask<IReadOnlyList<RuntimeEvent>> ReadRunAsync(string id, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            return ValueTask.FromResult<IReadOnlyList<RuntimeEvent>>(_events.TryGetValue(id, out var list)
                ? list.ToArray() : []);
        }
    }

    // Verifica apenas PRESENÇA de resposta; não é verificação da verdade das afirmações.
    private sealed class ResponsePresentVerifier : IVerifier
    {
        public ValueTask<VerificationResult> VerifyAsync(TaskSpec task, RunOutput output, CancellationToken token) =>
            ValueTask.FromResult(!string.IsNullOrWhiteSpace(output.FinalText)
                ? VerificationResult.Pass(task.Id, "Resposta não vazia; conteúdo/qualidade ainda exigem avaliação humana",
                    "response-present")
                : VerificationResult.Fail(task.Id, "Nenhuma resposta textual", "response-present"));
    }
}
