using System.Collections.Frozen;
using System.Text.Json;

namespace Hub.Core.Capabilities;

/// <summary>Registry derivado das definições locais. Não lê arquivos, rede ou Products.</summary>
public sealed class LocalCapabilityHost
{
    private readonly IReadOnlyList<LocalCapability> _definitions;
    private readonly IReadOnlySet<string> _declaredPermissions;

    public LocalCapabilityHost(IEnumerable<LocalCapability> definitions, IEnumerable<string> declaredPermissions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        ArgumentNullException.ThrowIfNull(declaredPermissions);
        var items = definitions.ToArray();
        if (items.Any(x => x is null)) throw new ArgumentException("Definição inválida.");
        if (items.GroupBy(x => x.Id).Any(x => x.Count() != 1)) throw new ArgumentException("Capability ambígua.");
        _definitions = Array.AsReadOnly(items);
        _declaredPermissions = declaredPermissions.ToFrozenSet(StringComparer.Ordinal);
    }

    public LocalHostSession Open(string actor, LocalContext context, IEnumerable<string> grantedPermissions)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(grantedPermissions);
        if (string.IsNullOrWhiteSpace(actor)) throw new ArgumentException("Ator precisa de identidade.");
        var effective = grantedPermissions.Where(_declaredPermissions.Contains).ToFrozenSet(StringComparer.Ordinal);
        return new LocalHostSession(actor, context, _definitions, effective);
    }
}

/// <summary>Context fixo, grants capturados, revogação explícita e uma chamada por sessão.</summary>
public sealed class LocalHostSession : IDisposable
{
    private readonly object _sync = new();
    private readonly string _actor;
    private readonly LocalContext _context;
    private readonly IReadOnlyList<LocalCapability> _definitions;
    private readonly IReadOnlySet<string> _permissions;
    private readonly HashSet<string> _seen = new(StringComparer.Ordinal);
    private readonly Queue<LocalFrame> _frames = new();
    private CancellationTokenSource? _active;
    private bool _closed;
    private long _sequence;
    public const int RequestLimit = 256;
    public const int JournalLimit = 64;

    internal LocalHostSession(string actor, LocalContext context, IReadOnlyList<LocalCapability> definitions,
        IReadOnlySet<string> permissions)
    {
        (_actor, _context, _definitions, _permissions) = (actor, context, definitions, permissions);
        Lifecycle("session.opened");
    }

    private void Lifecycle(string name)
    {
        _frames.Enqueue(new(++_sequence, "event", "", _actor, "", _context, Event: name));
        while (_frames.Count > JournalLimit) _frames.Dequeue();
    }

    public IReadOnlyList<LocalCapability> Discover()
    {
        lock (_sync)
            return Array.AsReadOnly((_closed ? [] : _definitions.Where(Allowed).ToArray()));
    }

    public IReadOnlyList<LocalFrame> Frames
    {
        get { lock (_sync) return Array.AsReadOnly(_frames.ToArray()); }
    }

    private bool Allowed(LocalCapability capability) => capability.Scope.Contains(_context)
        && capability.RequiredPermissions.All(_permissions.Contains);

    private LocalResponse Failure(LocalEnvelope request, string code)
    {
        Record(request, "error", code);
        return new(request.Id, null, code);
    }

    private void Record(LocalEnvelope request, string kind, string? error = null, int? percent = null)
    {
        lock (_sync)
        {
            _frames.Enqueue(new(++_sequence, kind, JournalId(request.Id), _actor, JournalId(request.Capability), _context, error, percent));
            while (_frames.Count > JournalLimit) _frames.Dequeue();
        }
    }

    private static string JournalId(string? value) => value is null ? "" : value.Length <= 128 ? value : value[..128];

    public async Task<LocalResponse> DispatchAsync(LocalEnvelope request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        LocalCapability? capability;
        CancellationTokenSource run;
        JsonElement input;
        lock (_sync)
        {
            if (_closed) return Failure(request, "SESSION_CLOSED");
            if (request.Protocol != LocalProtocol.Id) return Failure(request, "PROTOCOL_UNSUPPORTED");
            if (string.IsNullOrWhiteSpace(request.Id) || request.Id.Length > 128 || request.Source != _actor
                || request.Kind is not ("request" or "command")) return Failure(request, "INVALID_ENVELOPE");
            if (request.MinimumVersion is null || request.MinimumVersion.Build < 0 || request.MinimumVersion.Revision >= 0)
                return Failure(request, "VERSION_UNSUPPORTED");
            if (_active is not null) return Failure(request, "SESSION_BUSY");
            if (_seen.Contains(request.Id)) return Failure(request, "DUPLICATE_REQUEST");
            if (_seen.Count >= RequestLimit) return Failure(request, "SESSION_LIMIT");
            if (cancellationToken.IsCancellationRequested) return Failure(request, "CANCELLED");
            capability = _definitions.FirstOrDefault(c => c.Id == request.Capability && Allowed(c));
            if (capability is null) return Failure(request, "CAPABILITY_UNAVAILABLE");
            if (capability.Operation != request.Operation) return Failure(request, "OPERATION_UNSUPPORTED");
            if (capability.Version.Major != request.MinimumVersion.Major || capability.Version < request.MinimumVersion
                || (capability.Version.Major == 0 && capability.Version != request.MinimumVersion))
                return Failure(request, "VERSION_UNSUPPORTED");
            try
            {
                input = request.Input.Clone();
                if (!capability.ValidateInput(input)) return Failure(request, "INVALID_INPUT");
            }
            catch (Exception) { return Failure(request, "INVALID_INPUT"); }
            _seen.Add(request.Id);
            _active = run = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            Record(request, request.Kind);
        }
        try
        {
            var invocation = new LocalInvocation(_context, input, p =>
            {
                lock (_sync)
                    if (!_closed && ReferenceEquals(_active, run) && !run.IsCancellationRequested)
                        Record(request, "progress", percent: p);
            });
            var output = await capability.Handler(invocation, run.Token).ConfigureAwait(false);
            var validOutput = false;
            var copy = default(JsonElement);
            try
            {
                validOutput = capability.ValidateOutput(output);
                if (validOutput) copy = output.Clone();
            }
            catch (Exception)
            {
                validOutput = false;
            }
            lock (_sync)
            {
                if (_closed || run.IsCancellationRequested) return Failure(request, "CANCELLED");
                if (!validOutput) return Failure(request, "INVALID_OUTPUT");
                Record(request, "response");
                return new(request.Id, copy, null);
            }
        }
        catch (OperationCanceledException) when (run.IsCancellationRequested) { return Failure(request, "CANCELLED"); }
        // Mensagem arbitrária do handler nunca entra no journal/UI (pode conter segredo/texto).
        catch (Exception) { return Failure(request, "EXECUTION_FAILED"); }
        finally
        {
            lock (_sync) { _active = null; run.Dispose(); }
        }
    }

    public void Dispose()
    {
        CancellationTokenSource? active;
        lock (_sync)
        {
            if (_closed) return;
            _closed = true; active = _active;
            Lifecycle("session.closed");
        }
        // Cancel fora do lock: callbacks podem observar ou fechar a sessão.
        try { active?.Cancel(); } catch (ObjectDisposedException) { } catch (AggregateException) { }
    }
}
