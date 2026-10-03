using System.Text;
using System.Text.Json;

namespace AgentRuntime;

/// <summary>
/// O laço do agente (plano §5, §8): reserva orçamento → chama o provedor → sanitiza → executa ferramentas (permissão, escopo,
/// aprovação) → verifica de forma independente → retoma de erro com limite. Todo passo é um evento no log; o estado do run é
/// reconstruído do log. O runner nunca toca o mundo: só chama <see cref="ITool"/>s resolvidas por <see cref="ToolResolver"/>.
/// </summary>
public sealed class AgentRunner(
    ProviderRegistry providers,
    ToolHost host,
    ILedger ledger,
    IEventLog log,
    IClock clock,
    IVerifier verifier,
    IApprover? approver = null,
    SecretRedactor? redactor = null)
{
    private readonly SecretRedactor _redactor = redactor ?? SecretRedactor.None;

    private sealed class Run(RunRequest request)
    {
        public RunRequest Request { get; } = request;
        public List<Message> Messages { get; } = [];
        public long Sequence { get; set; }
        public int Steps { get; set; }
        public ResolvedTools Resolved { get; set; } = new([], []);
        public TaskRecord Task { get; } = new(request.Task);
        public VerificationResult? LastVerification { get; set; }
        public List<ArtifactRef> Artifacts { get; } = [];
        public string? LastProviderFailure { get; set; }
        public int ProviderFailureCount { get; set; }
        public string? LastVerificationFailure { get; set; }
        public int VerificationFailureCount { get; set; }
        public int VerificationAttempts { get; set; }
        public string? LastProgress { get; set; }
        public int ProgressCount { get; set; }
    }

    /// <summary>Inicia um run novo. Falha se o run já existe: retomar é <see cref="ResumeAsync"/>.</summary>
    public async Task<RunResult> RunAsync(RunRequest request, CancellationToken cancellationToken = default)
    {
        var existing = await log.ReadRunAsync(request.RunId, CancellationToken.None);
        if (existing.Count > 0) throw new InvalidOperationException($"O run '{request.RunId}' já existe: use ResumeAsync para retomá-lo.");

        var run = new Run(request);
        await EmitAsync(run, EventKind.RunCreated, result: "created", data: Data(("task", request.Task.Id)));
        run.Messages.Add(Message.User(_redactor.Redact(Prompt(request.Task))));
        run.Task.Start();
        ResolveTools(run);
        await EmitAsync(run, EventKind.ToolsResolved, result: DescribeResolution(run.Resolved));
        return await ExecuteAsync(run, null, [], new HashSet<string>(), [], cancellationToken);
    }

    /// <summary>
    /// Retoma um run a partir do log (plano §5.4, T-15): o estado e a conversa são reconstruídos dos eventos, sem estado ao lado.
    /// Chamada de ferramenta interrompida no meio só é refeita se a ferramenta for idempotente; senão o efeito é tratado como desconhecido.
    /// </summary>
    public async Task<RunResult> ResumeAsync(RunRequest request, CancellationToken cancellationToken = default)
    {
        var events = await log.ReadRunAsync(request.RunId, CancellationToken.None);
        var state = RunState.Replay(events);
        var run = new Run(request) { Sequence = state.LastSequence, Steps = state.Steps };
        run.Artifacts.AddRange(state.Artifacts);
        if (state.IsTerminal) return new RunResult(state, null, state.Artifacts);

        var rebuilt = ConversationRebuilder.Rebuild(events, _redactor.Redact(Prompt(request.Task)));
        run.Messages.AddRange(rebuilt.Messages);
        run.Task.Start();
        ResolveTools(run);
        await EmitAsync(run, EventKind.RunResumed, result: "resumed", data: Data(("from", state.Status.ToString())));
        return await ExecuteAsync(run, rebuilt.PendingFinalText, rebuilt.PendingUses, rebuilt.StartedCallIds, rebuilt.ResolvedResults, cancellationToken);
    }

    private static string Prompt(TaskSpec task) =>
        task.Acceptance.Count == 0
            ? task.Goal
            : $"{task.Goal}\n\nCritérios de aceite (verificados de forma independente):\n- {string.Join("\n- ", task.Acceptance)}";

    private void ResolveTools(Run run)
    {
        var r = run.Request;
        run.Resolved = ToolResolver.Resolve(host, r.OrganizationGrant, r.ProjectGrant, r.Agent.Grant, r.Context);
    }

    private static string DescribeResolution(ResolvedTools resolved) =>
        $"permitidas=[{string.Join(",", resolved.Tools.Select(t => t.Descriptor.CapabilityId))}] negadas=[{string.Join(",", resolved.Denied.Select(d => $"{d.CapabilityId}: {d.Reason}"))}]";

    private async Task<RunResult> ExecuteAsync(
        Run run,
        string? pendingFinalText,
        IReadOnlyList<ToolUseBlock> pendingUses,
        IReadOnlySet<string> startedCalls,
        IReadOnlyList<ToolResultBlock> priorResults,
        CancellationToken ct)
    {
        var request = run.Request;
        IModelProvider provider;
        try { provider = providers.Resolve(request.Agent.Model.ProviderId); }
        catch (UnknownProviderException ex)
        {
            await EmitAsync(run, EventKind.RunFailed, result: $"provedor desconhecido: {ex.ProviderId}", data: Data(("reason", "unknown-provider")));
            run.Task.Fail();
            return await FinishAsync(run);
        }

        if (pendingUses.Count > 0)
        {
            var results = new List<ContentBlock>(priorResults);
            var stop = await HandleToolUsesAsync(run, pendingUses, startedCalls, results, ct);
            if (stop is not null) return stop;
            run.Messages.Add(new Message(MessageRole.User, results));
        }

        while (true)
        {
            if (ct.IsCancellationRequested) return await CancelAsync(run);

            if (pendingFinalText is null)
            {
                if (run.Steps >= request.Agent.MaxSteps)
                    return await BlockAsync(run, BlockReason.StepLimit, $"limite de {request.Agent.MaxSteps} passos");

                var profile = request.Agent.Model;
                if (!ledger.TryReserve(request.BudgetScopes, profile.MaxCostPerCall, out var reservation, out var denial))
                {
                    await EmitAsync(run, EventKind.BudgetExceeded, result: $"reserva negada: {denial}");
                    return await BlockAsync(run, BlockReason.Budget, denial ?? "orçamento");
                }

                var specs = profile.Capabilities.Tools
                    ? run.Resolved.Tools.Select(t => new ToolSpec(t.Descriptor.CapabilityId, t.Descriptor.Description, t.Descriptor.InputSchemaJson)).ToList()
                    : [];
                await EmitAsync(run, EventKind.ModelRequested, model: profile.Model);

                ModelResponse response;
                try
                {
                    response = await provider.CompleteAsync(new ModelRequest(profile, _redactor.Redact(request.Agent.SystemPrompt), [.. run.Messages], specs), ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    ledger.Release(reservation!);
                    return await CancelAsync(run);
                }
                catch (Exception ex)
                {
                    ledger.Release(reservation!);
                    var (kind, transient) = ex is ProviderException pe ? (pe.Kind, pe.IsTransient) : ("unexpected-exception", false);
                    await EmitAsync(run, EventKind.ProviderFailed, model: profile.Model, result: kind,
                        data: Data(("kind", kind), ("transient", transient ? "true" : "false")));
                    run.ProviderFailureCount = run.LastProviderFailure == kind ? run.ProviderFailureCount + 1 : 1;
                    run.LastProviderFailure = kind;
                    if (!transient || run.ProviderFailureCount >= request.Policy.MaxEquivalentFailures)
                        return await BlockAsync(run, BlockReason.Provider, $"falha de provedor '{kind}' ({run.ProviderFailureCount}x)");
                    try { await clock.DelayAsync(TimeSpan.FromMilliseconds(100 * Math.Pow(2, run.ProviderFailureCount - 1)), ct); }
                    catch (OperationCanceledException) { return await CancelAsync(run); }
                    continue;
                }

                run.LastProviderFailure = null;
                run.ProviderFailureCount = 0;
                var settle = ledger.Settle(reservation!, response.Usage.Cost);

                // Raciocínio privado é descartado aqui: nunca registrado, nunca devolvido ao modelo (T-12).
                var content = response.Content
                    .Where(b => b is not ReasoningBlock)
                    .Select(b => b is TextBlock t ? t with { Text = _redactor.Redact(t.Text) } : b)
                    .ToList();
                await EmitAsync(run, EventKind.ModelResponded, model: profile.Model, cost: response.Usage.Cost, payload: ContentSerializer.ToJson(content));
                run.Steps++;
                run.Messages.Add(new Message(MessageRole.Assistant, content));

                if (settle.ExceededScopes.Count > 0)
                {
                    await EmitAsync(run, EventKind.BudgetExceeded, result: $"gasto real passou do reservado em {settle.Overrun}; escopos: {string.Join(",", settle.ExceededScopes)}");
                    return await BlockAsync(run, BlockReason.Budget, "orçamento ultrapassado");
                }

                var uses = content.OfType<ToolUseBlock>().ToList();
                if (uses.Count == 0)
                {
                    pendingFinalText = string.Join("\n", content.OfType<TextBlock>().Select(t => t.Text));
                }
                else
                {
                    var results = new List<ContentBlock>();
                    var stop = await HandleToolUsesAsync(run, uses, new HashSet<string>(), results, ct);
                    if (stop is not null) return stop;
                    run.Messages.Add(new Message(MessageRole.User, results));

                    var progress = string.Join("|", uses.Select(u => $"{u.Name}:{u.Input.GetRawText()}"))
                        + "=>" + string.Join("|", results.OfType<ToolResultBlock>().Select(r => $"{r.IsError}:{r.Content}"));
                    run.ProgressCount = run.LastProgress == progress ? run.ProgressCount + 1 : 1;
                    run.LastProgress = progress;
                    if (run.ProgressCount >= request.Policy.NoProgressRepeats)
                        return await BlockAsync(run, BlockReason.NoProgress, $"mesma chamada e mesmo resultado {run.ProgressCount}x");
                    continue;
                }
            }

            // Verificação independente: o modelo dizer "terminei" não conclui nada.
            run.Task.BeginVerification();
            run.VerificationAttempts++;
            var output = new RunOutput(pendingFinalText, [.. run.Artifacts]);
            VerificationResult verification;
            try { verification = await verifier.VerifyAsync(request.Task, output, ct); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { return await CancelAsync(run); }
            catch (Exception ex) { verification = VerificationResult.Fail(request.Task.Id, $"o verificador falhou ({ex.GetType().Name})"); }

            if (verification.Passed && string.IsNullOrWhiteSpace(verification.Evidence))
                verification = VerificationResult.Fail(request.Task.Id, "verificação aprovada sem evidência não conta");
            if (verification.Passed && !string.Equals(verification.TaskId, request.Task.Id, StringComparison.Ordinal))
                verification = VerificationResult.Fail(request.Task.Id, $"verificação é de outra tarefa ({verification.TaskId})");

            run.LastVerification = verification;
            if (verification.Passed)
            {
                run.Task.Complete(verification);
                await EmitAsync(run, EventKind.VerificationPassed, verification: verification.Evidence);
                await EmitAsync(run, EventKind.RunSucceeded, result: "succeeded");
                return await FinishAsync(run);
            }

            await EmitAsync(run, EventKind.VerificationFailed, verification: verification.Evidence,
                payload: $"A verificação falhou: {verification.Evidence}. Corrija e conclua novamente.");
            var fingerprint = Normalize(verification.Evidence);
            run.VerificationFailureCount = run.LastVerificationFailure == fingerprint ? run.VerificationFailureCount + 1 : 1;
            run.LastVerificationFailure = fingerprint;
            if (run.VerificationFailureCount >= request.Policy.MaxEquivalentFailures || run.VerificationAttempts >= request.Policy.MaxVerificationAttempts)
            {
                await EmitAsync(run, EventKind.EscalationRaised,
                    result: $"{run.VerificationFailureCount} falhas equivalentes de verificação em {run.VerificationAttempts} tentativas; requer outro agente, outro método ou revisão");
                return await BlockAsync(run, BlockReason.Escalation, "falhas equivalentes de verificação");
            }
            run.Task.Rework();
            run.Messages.Add(Message.User(_redactor.Redact($"A verificação falhou: {verification.Evidence}. Corrija e conclua novamente.")));
            pendingFinalText = null;
        }
    }

    /// <returns>Um <see cref="RunResult"/> se o run terminou/bloqueou durante o processamento; nulo para seguir.</returns>
    private async Task<RunResult?> HandleToolUsesAsync(Run run, IReadOnlyList<ToolUseBlock> uses, IReadOnlySet<string> started, List<ContentBlock> results, CancellationToken ct)
    {
        foreach (var use in uses)
        {
            var tool = run.Resolved.Find(use.Name);

            if (started.Contains(use.Id) && tool is not { Descriptor.Idempotent: true })
            {
                const string message = "execução interrompida antes do resultado; o efeito é desconhecido e a ferramenta não é idempotente, então não foi reexecutada";
                await ResultAsync(run, results, use, ToolOutcome.Error(message));
                continue;
            }

            if (tool is null)
            {
                var reason = run.Resolved.Denied.FirstOrDefault(d => d.CapabilityId == use.Name)?.Reason ?? "capability desconhecida ou não concedida";
                await DenyAsync(run, results, use, reason);
                continue;
            }

            var approvedBy = "policy";
            if (tool.Descriptor.Risk == RiskClass.Destructive)
            {
                await EmitAsync(run, EventKind.ApprovalRequired, tool: use.Name, approvedBy: "pending", payload: use.Input.GetRawText(), data: Data(("callId", use.Id)));
                if (approver is null)
                {
                    await EmitAsync(run, EventKind.EscalationRaised, result: $"operação destrutiva '{use.Name}' exige aprovação e não há aprovador");
                    return await BlockAsync(run, BlockReason.Escalation, $"aprovação pendente para '{use.Name}'");
                }
                var decision = await approver.RequestAsync(new ApprovalRequest(run.Request.RunId, run.Request.Agent.Identity, tool.Descriptor, use.Input.GetRawText()), ct);
                if (!decision.Approved)
                {
                    await DenyAsync(run, results, use, "aprovação negada");
                    continue;
                }
                approvedBy = decision.ApprovedBy;
                await EmitAsync(run, EventKind.ApprovalGranted, tool: use.Name, approvedBy: approvedBy, data: Data(("callId", use.Id)));
            }

            await EmitAsync(run, EventKind.ToolCalled, tool: use.Name, approvedBy: approvedBy, payload: use.Input.GetRawText(), data: Data(("callId", use.Id)));
            ToolOutcome outcome;
            try
            {
                outcome = await tool.InvokeAsync(new ToolInvocation(use.Id, use.Name, use.Input),
                    new ToolContext(run.Request.RunId, run.Request.Agent.Identity, run.Request.Context), ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { return await CancelAsync(run); }
            catch (Exception ex) { outcome = ToolOutcome.Error($"defeito na ferramenta ({ex.GetType().Name})"); }
            await ResultAsync(run, results, use, outcome);
        }
        return null;
    }

    private async Task DenyAsync(Run run, List<ContentBlock> results, ToolUseBlock use, string reason)
    {
        var block = new ToolResultBlock(use.Id, $"negado: {reason}", true);
        await EmitAsync(run, EventKind.ToolDenied, tool: use.Name, result: reason,
            payload: ContentSerializer.ToJson([block]), data: Data(("callId", use.Id)));
        results.Add(block);
    }

    private async Task ResultAsync(Run run, List<ContentBlock> results, ToolUseBlock use, ToolOutcome outcome)
    {
        var block = new ToolResultBlock(use.Id, _redactor.Redact(outcome.Content), outcome.IsError);
        var data = Data(("callId", use.Id));
        if (outcome.Artifacts.Count > 0)
        {
            run.Artifacts.AddRange(outcome.Artifacts);
            data["artifacts"] = JsonSerializer.Serialize(outcome.Artifacts);
        }
        await EmitAsync(run, EventKind.ToolResult, tool: use.Name, result: outcome.IsError ? "error" : "ok",
            payload: ContentSerializer.ToJson([block]), data: data);
        results.Add(block);
    }

    private async Task<RunResult> BlockAsync(Run run, BlockReason reason, string detail)
    {
        await EmitAsync(run, EventKind.RunBlocked, result: $"{reason}: {detail}", data: Data(("reason", reason.ToString())));
        run.Task.Block();
        return await FinishAsync(run);
    }

    private async Task<RunResult> CancelAsync(Run run)
    {
        await EmitAsync(run, EventKind.RunCancelled, result: "cancelled");
        run.Task.Cancel();
        return await FinishAsync(run);
    }

    private async Task<RunResult> FinishAsync(Run run)
    {
        var state = RunState.Replay(await log.ReadRunAsync(run.Request.RunId, CancellationToken.None));
        return new RunResult(state, run.LastVerification, state.Artifacts);
    }

    private async ValueTask EmitAsync(
        Run run,
        EventKind kind,
        string? tool = null,
        string? model = null,
        Money? cost = null,
        string? result = null,
        string? verification = null,
        string? approvedBy = null,
        string? payload = null,
        Dictionary<string, string>? data = null)
    {
        var r = run.Request;
        var @event = new RuntimeEvent(
            r.RunId,
            run.Sequence + 1,
            clock.UtcNow,
            kind,
            r.Agent.Identity,
            r.Task.Id,
            r.Context.ToString(),
            tool,
            model,
            cost,
            Scrub(result),
            Scrub(verification),
            approvedBy,
            Scrub(payload),
            data?.ToDictionary(kv => kv.Key, kv => _redactor.Redact(kv.Value)));
        var problems = EventContract.Validate(@event);
        if (problems.Count > 0) throw new InvalidOperationException($"Evento incompleto (defeito do runtime): {string.Join("; ", problems)}");
        await log.AppendAsync(@event, CancellationToken.None);
        run.Sequence = @event.Sequence;
    }

    private string? Scrub(string? text) => text is null ? null : _redactor.Redact(text);

    private static Dictionary<string, string> Data(params (string Key, string Value)[] pairs) =>
        pairs.ToDictionary(p => p.Key, p => p.Value);

    /// <summary>Impressão digital de uma falha: dois erros "equivalentes" se só diferem em caixa e espaços.</summary>
    private static string Normalize(string text)
    {
        var sb = new StringBuilder(text.Length);
        var space = false;
        foreach (var c in text.Trim())
        {
            if (char.IsWhiteSpace(c)) { space = true; continue; }
            if (space && sb.Length > 0) sb.Append(' ');
            space = false;
            sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString();
    }
}

/// <summary>Reconstrói a conversa de um run a partir dos eventos (a fonte de verdade), para retomada.</summary>
internal static class ConversationRebuilder
{
    internal sealed record Rebuilt(
        IReadOnlyList<Message> Messages,
        string? PendingFinalText,
        IReadOnlyList<ToolUseBlock> PendingUses,
        IReadOnlySet<string> StartedCallIds,
        IReadOnlyList<ToolResultBlock> ResolvedResults);

    public static Rebuilt Rebuild(IReadOnlyList<RuntimeEvent> events, string firstUserMessage)
    {
        var messages = new List<Message> { Message.User(firstUserMessage) };
        List<ToolUseBlock> turnUses = [];
        var resultBlocks = new List<ToolResultBlock>();
        var resolved = new HashSet<string>(StringComparer.Ordinal);
        var started = new HashSet<string>(StringComparer.Ordinal);
        string? finalText = null;

        void FlushTurn()
        {
            if (turnUses.Count > 0 && resultBlocks.Count == turnUses.Count)
                messages.Add(new Message(MessageRole.User, [.. resultBlocks]));
        }

        foreach (var e in events)
        {
            switch (e.Kind)
            {
                case EventKind.ModelResponded:
                {
                    FlushTurn();
                    var content = ContentSerializer.FromJson(e.Payload ?? "[]");
                    messages.Add(new Message(MessageRole.Assistant, content));
                    turnUses = content.OfType<ToolUseBlock>().ToList();
                    resultBlocks = [];
                    resolved.Clear();
                    started.Clear();
                    finalText = turnUses.Count == 0 ? string.Join("\n", content.OfType<TextBlock>().Select(t => t.Text)) : null;
                    break;
                }
                case EventKind.ToolCalled when e.Data is { } d && d.TryGetValue("callId", out var started1):
                    started.Add(started1);
                    break;
                case EventKind.ToolResult or EventKind.ToolDenied:
                    if (e.Payload is { } payload) resultBlocks.AddRange(ContentSerializer.FromJson(payload).OfType<ToolResultBlock>());
                    if (e.Data is { } rd && rd.TryGetValue("callId", out var id)) resolved.Add(id);
                    break;
                case EventKind.VerificationFailed:
                    messages.Add(Message.User(e.Payload ?? "A verificação falhou."));
                    turnUses = [];
                    finalText = null;
                    break;
            }
        }

        var pending = turnUses.Where(u => !resolved.Contains(u.Id)).ToList();
        if (pending.Count == 0) FlushTurn();
        return new Rebuilt(messages, finalText, pending, started, pending.Count == 0 ? [] : resultBlocks);
    }
}
