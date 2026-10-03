using System.Collections.Concurrent;
using System.Text.Json;

namespace AgentRuntime.Testing;

/// <summary>
/// Provedor roteirizado: devolve respostas (ou falhas) na ordem em que foram enfileiradas. Implementação 1 do contrato de provedor (T-14).
/// Registra cada requisição recebida, para os testes provarem o que o modelo viu (e o que NÃO viu).
/// </summary>
public sealed class ScriptedModelProvider(string providerId) : IModelProvider
{
    private readonly ConcurrentQueue<Func<ModelRequest, CancellationToken, ModelResponse>> _script = new();
    private readonly ConcurrentQueue<ModelRequest> _requests = new();
    private int _calls;

    public string ProviderId { get; } = providerId;

    public int Calls => Volatile.Read(ref _calls);

    public IReadOnlyList<ModelRequest> Requests => [.. _requests];

    public static Usage Cost(long minor, string currency = "BRL") => new(10, 10, new Money(minor, currency));

    public ScriptedModelProvider Then(ModelResponse response) => Then((_, _) => response);

    public ScriptedModelProvider Then(Func<ModelRequest, CancellationToken, ModelResponse> step)
    {
        _script.Enqueue(step);
        return this;
    }

    public ScriptedModelProvider ThenText(string text, long cost = 1) =>
        Then(new ModelResponse([new TextBlock(text)], StopReason.EndTurn, Cost(cost)));

    public ScriptedModelProvider ThenToolUse(string callId, string capability, string argumentsJson, long cost = 1) =>
        Then(new ModelResponse([new ToolUseBlock(callId, capability, Json(argumentsJson))], StopReason.ToolUse, Cost(cost)));

    public ScriptedModelProvider ThenFail(string kind, bool transient = false) =>
        Then((_, _) => throw new ProviderException(kind, $"falha roteirizada: {kind}", transient));

    public static JsonElement Json(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    public ValueTask<ModelResponse> CompleteAsync(ModelRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Interlocked.Increment(ref _calls);
        _requests.Enqueue(request);
        if (!_script.TryDequeue(out var step))
            throw new ProviderException("script-exhausted", "o roteiro do provedor acabou", false);
        return ValueTask.FromResult(step(request, cancellationToken));
    }
}

/// <summary>
/// Provedor que reproduz gravações JSON (uma por chamada). Implementação 2, independente da primeira, do mesmo contrato (T-14):
/// prova que o contrato é do Core e não de uma classe de teste.
/// </summary>
public sealed class RecordedModelProvider : IModelProvider
{
    private readonly Queue<string> _recordings;

    public RecordedModelProvider(string providerId, IEnumerable<string> recordings)
    {
        ProviderId = providerId;
        _recordings = new Queue<string>(recordings);
    }

    public string ProviderId { get; }

    public static string Record(ModelResponse response) =>
        JsonSerializer.Serialize(new
        {
            content = JsonDocument.Parse(ContentSerializer.ToJson(response.Content)).RootElement,
            stop = response.Stop.ToString(),
            input = response.Usage.InputTokens,
            output = response.Usage.OutputTokens,
            cost = response.Usage.Cost.Minor,
            currency = response.Usage.Cost.Currency,
        });

    public ValueTask<ModelResponse> CompleteAsync(ModelRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string recording;
        lock (_recordings)
        {
            if (!_recordings.TryDequeue(out recording!))
                throw new ProviderException("recording-exhausted", "a gravação acabou", false);
        }
        using var doc = JsonDocument.Parse(recording);
        var root = doc.RootElement;
        var content = ContentSerializer.FromJson(root.GetProperty("content").GetRawText());
        var stop = Enum.Parse<StopReason>(root.GetProperty("stop").GetString()!);
        var usage = new Usage(root.GetProperty("input").GetInt64(), root.GetProperty("output").GetInt64(),
            new Money(root.GetProperty("cost").GetInt64(), root.GetProperty("currency").GetString()!));
        return ValueTask.FromResult(new ModelResponse(content, stop, usage));
    }
}

/// <summary>Relógio controlado: o tempo só anda quando o teste manda, e esperar apenas avança o relógio (sem dormir de verdade).</summary>
public sealed class FakeClock(DateTimeOffset? start = null) : IClock
{
    private DateTimeOffset _now = start ?? new DateTimeOffset(2026, 10, 3, 12, 0, 0, TimeSpan.Zero);
    private int _delays;

    public DateTimeOffset UtcNow => _now;

    public int DelayCount => Volatile.Read(ref _delays);

    public void Advance(TimeSpan by) => _now += by;

    public ValueTask DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Interlocked.Increment(ref _delays);
        _now += delay;
        return ValueTask.CompletedTask;
    }
}

public sealed class InMemoryEventLog : IEventLog
{
    private readonly object _gate = new();
    private readonly Dictionary<string, List<RuntimeEvent>> _runs = [];

    public ValueTask AppendAsync(RuntimeEvent @event, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (!_runs.TryGetValue(@event.RunId, out var list)) _runs[@event.RunId] = list = [];
            if (@event.Sequence != list.Count + 1)
                throw new InvalidOperationException($"Log append-only: esperado Sequence {list.Count + 1}, veio {@event.Sequence}.");
            list.Add(@event);
        }
        return ValueTask.CompletedTask;
    }

    public ValueTask<IReadOnlyList<RuntimeEvent>> ReadRunAsync(string runId, CancellationToken cancellationToken)
    {
        lock (_gate)
            return ValueTask.FromResult<IReadOnlyList<RuntimeEvent>>(_runs.TryGetValue(runId, out var list) ? [.. list] : []);
    }

    public IReadOnlyList<RuntimeEvent> All
    {
        get { lock (_gate) return [.. _runs.Values.SelectMany(l => l)]; }
    }
}

public sealed class InjectedLogFailure() : Exception("falha injetada no log (simula queda do processo)");

/// <summary>Log que "cai" depois de N appends: simula a morte do processo no meio de um run, para testar a retomada (T-15).</summary>
public sealed class FaultInjectingEventLog(IEventLog inner, int failAfterAppends) : IEventLog
{
    private int _appends;

    public bool Armed { get; set; } = true;

    public ValueTask AppendAsync(RuntimeEvent @event, CancellationToken cancellationToken)
    {
        if (Armed && Interlocked.Increment(ref _appends) > failAfterAppends) throw new InjectedLogFailure();
        return inner.AppendAsync(@event, cancellationToken);
    }

    public ValueTask<IReadOnlyList<RuntimeEvent>> ReadRunAsync(string runId, CancellationToken cancellationToken) =>
        inner.ReadRunAsync(runId, cancellationToken);
}

public sealed class InMemorySecretStore : ISecretStore
{
    private readonly Dictionary<string, string> _secrets = [];

    public InMemorySecretStore Put(string name, string value, SecretRedactor? redactor = null)
    {
        _secrets[name] = value;
        redactor?.Register(value);
        return this;
    }

    public ValueTask<string?> ResolveAsync(SecretRef reference, CancellationToken cancellationToken) =>
        ValueTask.FromResult(_secrets.TryGetValue(reference.Name, out var v) ? v : null);
}

public sealed class FixedApprover(bool approve, string by = "human:test") : IApprover
{
    private int _requests;

    public int Requests => Volatile.Read(ref _requests);

    public ValueTask<ApprovalDecision> RequestAsync(ApprovalRequest request, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _requests);
        return ValueTask.FromResult(approve ? new ApprovalDecision(true, by) : ApprovalDecision.Deny);
    }
}

public sealed class DelegateVerifier(Func<TaskSpec, RunOutput, VerificationResult> verify) : IVerifier
{
    private int _calls;

    public int Calls => Volatile.Read(ref _calls);

    public static DelegateVerifier AlwaysPass(string evidence = "verificado") =>
        new((task, _) => VerificationResult.Pass(task.Id, evidence));

    public static DelegateVerifier AlwaysFail(string evidence = "critério não atendido") =>
        new((task, _) => VerificationResult.Fail(task.Id, evidence));

    public ValueTask<VerificationResult> VerifyAsync(TaskSpec task, RunOutput output, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _calls);
        return ValueTask.FromResult(verify(task, output));
    }
}
