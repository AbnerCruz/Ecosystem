using AgentRuntime;
using AgentRuntime.Providers.ChatCompletions;
using AgentRuntime.Tools.Files;
using AgentWorkspace;
using EcosystemAi.ProjectStore;
using EcosystemAi.RunJournal;

namespace EcosystemAi.Product;

/// <summary>Configuração explícita do operador. Token transitório, nunca serializado.</summary>
public sealed class ProductModelSettings
{
    public required string Endpoint { get; init; }
    public required string Model { get; init; }
    [System.Text.Json.Serialization.JsonIgnore]
    public string? ApiKey { get; init; }
    public long RunBudgetCents { get; init; }
    public long MaxCallCents { get; init; }
    public decimal InputUsdPerMillion { get; init; }
    public decimal OutputUsdPerMillion { get; init; }
    public bool UseHistory { get; init; }

    public void Validate()
    {
        if (!Uri.TryCreate(Endpoint, UriKind.Absolute, out var uri)
            || (uri.Scheme != "https" && !(uri.Scheme == "http" && uri.IsLoopback))
            || uri.UserInfo.Length != 0 || uri.Fragment.Length != 0)
            throw new ArgumentException("Endpoint HTTPS completo, sem credenciais na URL.");
        if (string.IsNullOrWhiteSpace(Model) || Model.Length > 256 || Model.Any(char.IsControl))
            throw new ArgumentException("Informe o identificador do modelo.");
        if ((!uri.IsLoopback || !string.IsNullOrWhiteSpace(ApiKey))
            && (ApiKey is null || ApiKey.Length < SecretRedactor.MinimumLength))
            throw new ArgumentException("A chave precisa ter ao menos 8 caracteres e será mantida apenas em memória.");
        if (RunBudgetCents < 1 || MaxCallCents < 1 || MaxCallCents > RunBudgetCents
            || InputUsdPerMillion < 0 || OutputUsdPerMillion < 0)
            throw new ArgumentException("Configure orçamento positivo e preços não negativos.");
    }
}

public sealed record ProductRunResult(RunReceipt Receipt, string? Response);
public sealed record ProductTeamResult(ProductRunResult Producer, ProductRunResult? Reviewer);

/// <summary>
/// Adapter de Product portátil. Compõe o MESMO Workspace/Runner/ledger do Core,
/// catálogo e journal existentes; sem ASP.NET, scheduler ou loop próprio.
/// Quota conservadora por instância não é teto financeiro entre processos.
/// </summary>
public sealed class ProductExecutionHost
{
    public const int MaxText = 16 * 1024;
    private readonly string _catalog;
    private readonly string _journal;
    private readonly LocalProjectStore _store;
    private readonly Func<HttpClient> _http;
    private readonly long _authorizedCents;
    private readonly int _maxRuns;
    private long _reserved;
    private int _runs;
    private int _busy;

    public ProductExecutionHost(string catalog, string journal, long authorizedCents,
        int maxRuns = 8, Func<HttpClient>? httpFactory = null)
    {
        if (authorizedCents < 1 || maxRuns is < 1 or > 100)
            throw new ArgumentException("Configure a autorização da instância antes de executar.");
        _catalog = Path.GetFullPath(catalog);
        _journal = Path.GetFullPath(journal);
        _store = new LocalProjectStore(_catalog);
        _authorizedCents = authorizedCents;
        _maxRuns = maxRuns;
        _http = httpFactory ?? (() => new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
            { Timeout = TimeSpan.FromSeconds(90) });
    }

    public long ReservedCents => Interlocked.Read(ref _reserved);
    public long RemainingCents => Math.Max(0, _authorizedCents - ReservedCents);
    public int RemainingRuns => Math.Max(0, _maxRuns - Volatile.Read(ref _runs));
    public bool Busy => Volatile.Read(ref _busy) != 0;

    public async Task<ProductRunResult> ExecuteAsync(ProductModelSettings settings,
        string projectId, string sessionId, string goal, string? requiredResponseText = null,
        string? agentId = null, CancellationToken cancellationToken = default)
    {
        if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0)
            throw new InvalidOperationException("Uma execução já está ativa; nenhuma fila foi criada.");
        try { return await ExecuteCoreAsync(settings, projectId, sessionId, goal,
            requiredResponseText, agentId, cancellationToken); }
        finally { Volatile.Write(ref _busy, 0); }
    }

    public async Task<ProductTeamResult> ExecuteTeamAsync(ProductModelSettings settings,
        string projectId, string sessionId, string goal, string teamId,
        string? requiredResponseText = null, CancellationToken cancellationToken = default)
    {
        if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0)
            throw new InvalidOperationException("Uma execução já está ativa.");
        try
        {
            settings.Validate();
            var roster = new LocalAgentRosterStore(_catalog).Read();
            var team = roster.Teams.SingleOrDefault(t => t.Id == teamId && t.ProjectId == projectId)
                ?? throw new ArgumentException("Equipe não pertence ao projeto.");
            _ = new TeamPlan([new TeamAssignment(new TaskSpec("native-team", goal, ["response-present"]),
                team.Id, team.ProducerId, team.ReviewerId, [])]);
            var reviewObjective = requiredResponseText is null ? goal
                : goal + "\nCRITÉRIO EXATO DE ACEITE DA RESPOSTA:\n" + requiredResponseText;
            if (RemainingCents / settings.RunBudgetCents < 2 || RemainingRuns < 2
                || !ProductReviewRequest.TryCompose(reviewObjective, "x", out _))
                throw new InvalidOperationException("A equipe exige espaço para revisão, duas execuções e dois orçamentos completos.");
            var producer = await ExecuteCoreAsync(settings, projectId, sessionId, goal,
                requiredResponseText, team.ProducerId, cancellationToken);
            if (!producer.Receipt.Verified || producer.Receipt.Status != "succeeded"
                || producer.Response is null || !ProductReviewRequest.TryCompose(reviewObjective, producer.Response, out var reviewGoal))
                return new ProductTeamResult(producer, null);
            var producerSession = _store.Read().Projects.Single(p => p.Id == projectId)
                .Sessions.Single(s => s.Id == sessionId);
            var reviewSettings = new ProductModelSettings
            {
                Endpoint = settings.Endpoint, Model = settings.Model, ApiKey = settings.ApiKey,
                RunBudgetCents = settings.RunBudgetCents, MaxCallCents = settings.MaxCallCents,
                InputUsdPerMillion = settings.InputUsdPerMillion, OutputUsdPerMillion = settings.OutputUsdPerMillion,
                UseHistory = false
            };
            var reviewer = await ExecuteCoreAsync(reviewSettings, projectId, sessionId,
                reviewGoal, null, team.ReviewerId, cancellationToken);
            if (reviewer.Receipt.Verified && reviewer.Receipt.Status == "succeeded" && reviewer.Response is not null)
            {
                var reviewerSession = _store.Read().Projects.Single(p => p.Id == projectId)
                    .Sessions.Single(s => s.Id == sessionId);
                new LocalTeamReviewStore(_catalog).Record(projectId, sessionId, team.Id,
                    producer.Receipt.RunId, reviewer.Receipt.RunId,
                    producerSession.Turns.Count - 1, reviewerSession.Turns.Count - 1);
            }
            return new ProductTeamResult(producer, reviewer);
        }
        finally { Volatile.Write(ref _busy, 0); }
    }

    private async Task<ProductRunResult> ExecuteCoreAsync(ProductModelSettings settings,
        string projectId, string sessionId, string goal, string? requiredResponseText,
        string? agentId, CancellationToken cancellationToken)
    {
            cancellationToken.ThrowIfCancellationRequested();
            settings.Validate();
            if (string.IsNullOrWhiteSpace(goal) || goal.Length > MaxText || goal.Contains('\0'))
                throw new ArgumentException("Solicitação vazia ou maior que 16384 caracteres.");
            if (requiredResponseText is not null
                && (string.IsNullOrWhiteSpace(requiredResponseText) || requiredResponseText.Length > 512))
                throw new ArgumentException("Trecho de aceite deve ter de 1 a 512 caracteres.");
            var snapshot = _store.Read();
            var project = snapshot.Projects.SingleOrDefault(p => p.Id == projectId)
                ?? throw new KeyNotFoundException("Projeto inexistente.");
            var session = project.Sessions.SingleOrDefault(s => s.Id == sessionId)
                ?? throw new KeyNotFoundException("Sessão não pertence ao projeto.");
            RequireOutside(_catalog, project.WorkspaceDirectory);
            RequireOutside(_journal, project.WorkspaceDirectory);
            if (session.Turns.Count > 997 || session.Runs.Count >= 1000)
                throw new InvalidOperationException("Crie outra sessão antes de executar; histórico cheio.");
            var catalogFile = new FileInfo(Path.Combine(_catalog, "catalog.json"));
            if (catalogFile.Exists && catalogFile.Length > LocalProjectStore.MaxCatalogBytes - 256 * 1024)
                throw new InvalidOperationException("Catálogo sem espaço seguro para gravar o próximo resultado.");
            var localAgent = agentId is null ? null : new LocalAgentRosterStore(_catalog).Read()
                .Agents.SingleOrDefault(a => a.Id == agentId && a.ProjectId == projectId)
                ?? (agentId is null ? null : throw new ArgumentException("Agente não pertence ao projeto."));
            var redactor = new SecretRedactor();
            if (!string.IsNullOrWhiteSpace(settings.ApiKey)) redactor.Register(settings.ApiKey);
            if (requiredResponseText is not null && redactor.Redact(requiredResponseText) != requiredResponseText)
                throw new ArgumentException("Não use credenciais como critério de aceite.");
            var safeGoal = redactor.Redact(goal);
            var composed = settings.UseHistory && session.Turns.Count > 0
                ? ProductConversationContext.Compose(snapshot, projectId, sessionId, safeGoal) : safeGoal;
            if (requiredResponseText is not null)
                composed += "\nCRITÉRIO EXATO DE ACEITE DA RESPOSTA:\n" + requiredResponseText;
            if (composed.Length > 32 * 1024)
                throw new ArgumentException("Solicitação com histórico excede o contexto autorizado.");

            using var http = _http();
            var provider = new ChatCompletionsProvider(http, new Uri(settings.Endpoint), settings.ApiKey,
                settings.InputUsdPerMillion, settings.OutputUsdPerMillion);
            var context = ContextPath.Create(new(ContextLevel.Ecosystem, "ecosystem"),
                new(ContextLevel.Product, "ecosystem-ai"), new(ContextLevel.Project, project.Id));
            var host = new ToolHost().Register(new FilesReadTool(new FileSandbox(project.WorkspaceDirectory), context));
            var grant = ToolGrant.Of([FileToolIds.Read], [Permissions.AgentAct, Permissions.FsRead]);
            var clock = new ProductClock();
            var ledger = new InMemoryLedger(clock, "USD");
            var scope = new BudgetScope(BudgetScopeKind.Project, project.Id);
            ledger.SetLimits(scope, new BudgetLimits(Total: new Money(settings.RunBudgetCents, "USD"),
                PerOperation: new Money(settings.MaxCallCents, "USD")));
            var log = new LocalRunEventLog(_journal);
            var runner = new AgentRunner(new ProviderRegistry().Register(provider), host, ledger, log,
                clock, new ProductResponseVerifier(), redactor: redactor);
            var workspace = new WorkspaceSession("native:" + project.Id, runner, context, grant, grant, [scope]);
            var model = new ModelProfile(provider.ProviderId, settings.Model,
                new ModelCapabilities(true, false, true, false, false, 32_000), new Money(settings.MaxCallCents, "USD"));
            var identity = localAgent is null ? new AgentIdentity("native-assistant", "Assistente", "executor")
                : new AgentIdentity(localAgent.Id, localAgent.Name, "executor");
            var agent = new AgentDefinition(identity, model,
                "Atue apenas no projeto autorizado, com ferramentas anunciadas. Sem escrita/exclusão. " +
                "Relate evidências e limites; não invente execução de testes.\n" + (localAgent?.Instructions ?? ""), 8, grant);
            var runId = "native-run-" + Guid.NewGuid().ToString("N");
            var task = new TaskSpec("task:" + runId, composed,
                requiredResponseText is null ? ["response-present"] : ["response-contains:" + requiredResponseText]);
            if (_runs >= _maxRuns || RemainingCents < settings.RunBudgetCents)
                throw new InvalidOperationException("Autorização desta instância esgotada. Configure um novo teto explicitamente.");
            // Persistir solicitação antes de reservar/chamar o modelo. Nunca
            // executar se o catálogo já não permite registrar a operação.
            _store.AppendTurn(projectId, sessionId, "user", safeGoal);
            _reserved = checked(_reserved + settings.RunBudgetCents);
            _runs++;
            var result = await workspace.ExecuteAsync(runId, task, agent, cancellationToken: cancellationToken);
            var events = await log.ReadRunAsync(runId, CancellationToken.None);
            var response = events.Where(e => e.Kind == EventKind.ModelResponded && e.Payload is not null)
                .Select(e => ContentSerializer.FromJson(e.Payload!))
                .SelectMany(blocks => blocks.OfType<TextBlock>()).LastOrDefault()?.Text;
            response = response is null ? null : redactor.Redact(response);
            if (string.IsNullOrWhiteSpace(response) || response.Length > MaxText) response = null;
            var receipt = new RunReceipt(runId, result.State.Status.ToString().ToLowerInvariant(),
                events.Where(e => e.Kind == EventKind.ModelResponded).Sum(e => e.Cost?.Minor ?? 0),
                "USD", result.State.Verified, result.Verification is null ? null : redactor.Redact(result.Verification.Evidence),
                clock.UtcNow, provider.LastCostEstimated || !events.Any(e => e.Kind == EventKind.ModelResponded));
            _store.CompleteRun(projectId, sessionId, receipt, response);
            return new ProductRunResult(receipt, response);
    }

    public static void RequireOutside(string privateDirectory, string workspaceDirectory)
    {
        var root = Path.GetFullPath(privateDirectory);
        var workspace = Path.GetFullPath(workspaceDirectory);
        foreach (var path in new[] { root, workspace })
            for (DirectoryInfo? p = new(path); p is not null; p = p.Parent)
                if (p.LinkTarget is not null) throw new ArgumentException("Diretório simbólico não autorizado.");
        var relative = Path.GetRelativePath(workspace, root);
        if (relative == "." || (!Path.IsPathRooted(relative) && relative != ".."
            && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)))
            throw new ArgumentException("Catálogo e journal precisam ficar fora do workspace do agente.");
    }

    private sealed class ProductClock : IClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
        public ValueTask DelayAsync(TimeSpan delay, CancellationToken token) => new(Task.Delay(delay, token));
    }
}

/// <summary>Verifica texto real. Presença/trecho não provam verdade, qualidade ou integração de arquivos.</summary>
public sealed class ProductResponseVerifier : IVerifier
{
    public ValueTask<VerificationResult> VerifyAsync(TaskSpec task, RunOutput output, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(output.FinalText) || output.FinalText.Length > ProductExecutionHost.MaxText)
            return ValueTask.FromResult(VerificationResult.Fail(task.Id, "Resposta vazia ou acima do limite persistível."));
        foreach (var criterion in task.Acceptance)
        {
            if (criterion == "response-present") continue;
            const string prefix = "response-contains:";
            if (!criterion.StartsWith(prefix, StringComparison.Ordinal) || criterion.Length == prefix.Length)
                return ValueTask.FromResult(VerificationResult.Fail(task.Id, "Critério desconhecido: verificação recusada."));
            if (!output.FinalText.Contains(criterion[prefix.Length..], StringComparison.Ordinal))
                return ValueTask.FromResult(VerificationResult.Fail(task.Id, "Trecho exato de aceite ausente na resposta."));
        }
        return ValueTask.FromResult(task.Acceptance.Count == 0
            ? VerificationResult.Fail(task.Id, "Nenhum critério de aceite foi declarado.")
            : VerificationResult.Pass(task.Id, task.Acceptance.All(c => c == "response-present")
                ? "Resposta presente; conteúdo/qualidade exigem avaliação humana."
                : "Trecho exato confirmado no texto da resposta; não prova verdade ou qualidade.", task.Acceptance.ToArray()));
    }
}
