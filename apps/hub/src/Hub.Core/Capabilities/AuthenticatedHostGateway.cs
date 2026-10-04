using System.Collections.Concurrent;
using System.Text.Json;

namespace Hub.Core.Capabilities;

/// <summary>
/// Adapta a Host API pública a uma identidade já autenticada pelo binding externo.
/// Não autentica Android/Binder: o adapter de plataforma entrega a identidade do peer e não existe
/// caminho para payload sobrescrever essa identidade.
/// </summary>
public sealed class AuthenticatedHostGateway : IDisposable
{
    private sealed class Entry : IDisposable
    {
        internal readonly object Sync = new();
        internal readonly string Peer;
        internal readonly string Actor;
        internal readonly LocalHostSession Session;
        internal CancellationTokenSource? ActiveCancellation;
        internal string? ActiveRequestId;
        internal bool ActiveExplicitCancel;
        internal bool Closed;

        internal Entry(string peer, string actor, LocalHostSession session)
            => (Peer, Actor, Session) = (peer, actor, session);

        public void Dispose()
        {
            CancellationTokenSource? cancellation;
            lock (Sync)
            {
                if (Closed) return;
                Closed = true;
                cancellation = ActiveCancellation;
            }
            try { cancellation?.Cancel(); } catch (ObjectDisposedException) { } catch (AggregateException) { }
            Session.Dispose();
        }
    }

    private readonly LocalCapabilityHost _host;
    private readonly ConcurrentDictionary<string, Entry> _sessions = new(StringComparer.Ordinal);
    private bool _disposed;

    public const int MaxSessions = 4;
    public static readonly TimeSpan DefaultDeadline = TimeSpan.FromSeconds(10);
    public static readonly TimeSpan MaximumDeadline = TimeSpan.FromSeconds(30);

    public AuthenticatedHostGateway(LocalCapabilityHost host)
        => _host = host ?? throw new ArgumentNullException(nameof(host));

    public GatewayOpenResult Open(string peer, string actor, LocalContext context, IEnumerable<string> grants)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (string.IsNullOrWhiteSpace(peer) || string.IsNullOrWhiteSpace(actor))
            return new(null, "IDENTITY_REQUIRED");
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(grants);

        if (_sessions.Values.Count(entry => !entry.Closed) >= MaxSessions)
            return new(null, "SESSION_LIMIT");

        LocalHostSession local;
        try { local = _host.Open(actor, context, grants); }
        catch (ArgumentException) { return new(null, "INVALID_CONTEXT"); }

        for (var attempt = 0; attempt < 4; attempt++)
        {
            var id = Convert.ToHexString(Guid.NewGuid().ToByteArray()).ToLowerInvariant();
            var entry = new Entry(peer, actor, local);
            if (_sessions.TryAdd(id, entry)) return new(id, null);
        }

        local.Dispose();
        return new(null, "SESSION_LIMIT");
    }

    public GatewayDiscoveryResult Discover(string peer, string sessionId)
    {
        if (!TryOwned(peer, sessionId, out var entry)) return new([], "SESSION_CLOSED");
        return new(entry.Session.Discover()
            .Select(capability => new GatewayCapability(capability.Id, capability.Version.ToString(3),
                capability.Operation, capability.Lifecycle))
            .ToArray(), null);
    }

    public async Task<GatewayInvokeResult> InvokeAsync(string peer, string sessionId, string requestId,
        string capability, string operation, Version minimumVersion, JsonElement input,
        TimeSpan? deadline = null, CancellationToken cancellationToken = default)
    {
        if (!TryOwned(peer, sessionId, out var entry))
            return new(requestId, null, "SESSION_CLOSED", null);

        var effectiveDeadline = deadline ?? DefaultDeadline;
        if (effectiveDeadline <= TimeSpan.Zero || effectiveDeadline > MaximumDeadline)
            return new(requestId, null, null, "DEADLINE_INVALID");

        CancellationTokenSource run;
        lock (entry.Sync)
        {
            if (entry.Closed) return new(requestId, null, "SESSION_CLOSED", null);
            if (entry.ActiveCancellation is not null)
                return new(requestId, null, null, "SESSION_BUSY");
            run = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            run.CancelAfter(effectiveDeadline);
            entry.ActiveCancellation = run;
            entry.ActiveRequestId = requestId;
            entry.ActiveExplicitCancel = false;
        }

        try
        {
            var response = await entry.Session.DispatchAsync(new LocalEnvelope(
                LocalProtocol.Id, requestId, entry.Actor, "request", capability, operation, minimumVersion, input), run.Token)
                .ConfigureAwait(false);

            bool explicitCancel;
            lock (entry.Sync) explicitCancel = ReferenceEquals(entry.ActiveCancellation, run) && entry.ActiveExplicitCancel;
            if (run.IsCancellationRequested && !cancellationToken.IsCancellationRequested
                && !explicitCancel && response.Error == "CANCELLED")
                return new(requestId, null, null, "DEADLINE_EXCEEDED");

            return new(response.RequestId, response.Output, response.Error, null);
        }
        finally
        {
            lock (entry.Sync)
            {
                if (ReferenceEquals(entry.ActiveCancellation, run))
                {
                    entry.ActiveCancellation = null;
                    entry.ActiveRequestId = null;
                    entry.ActiveExplicitCancel = false;
                }
            }
            run.Dispose();
        }
    }

    public bool Cancel(string peer, string sessionId, string requestId)
    {
        if (!TryOwned(peer, sessionId, out var entry)) return false;
        CancellationTokenSource? cancellation = null;
        lock (entry.Sync)
        {
            if (!entry.Closed && entry.ActiveRequestId == requestId)
            {
                entry.ActiveExplicitCancel = true;
                cancellation = entry.ActiveCancellation;
            }
        }
        if (cancellation is null) return false;
        try { cancellation.Cancel(); return true; }
        catch (ObjectDisposedException) { return false; }
        catch (AggregateException) { return false; }
    }

    public bool Close(string peer, string sessionId)
    {
        if (!_sessions.TryGetValue(sessionId, out var entry) || entry.Peer != peer) return false;
        if (!_sessions.TryRemove(sessionId, out entry)) return false;
        entry.Dispose();
        return true;
    }

    /// <summary>Usado pelo lifecycle do binding quando uma identidade de peer é perdida.</summary>
    public int Disconnect(string peer)
    {
        if (string.IsNullOrWhiteSpace(peer)) return 0;
        var closed = 0;
        foreach (var pair in _sessions.ToArray())
        {
            if (pair.Value.Peer != peer || !_sessions.TryRemove(pair.Key, out var entry)) continue;
            entry.Dispose();
            closed++;
        }
        return closed;
    }

    private bool TryOwned(string peer, string sessionId, out Entry entry)
    {
        entry = null!;
        if (_disposed || string.IsNullOrWhiteSpace(peer) || string.IsNullOrWhiteSpace(sessionId)) return false;
        if (!_sessions.TryGetValue(sessionId, out var found) || found.Peer != peer || found.Closed) return false;
        entry = found;
        return true;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var pair in _sessions.ToArray())
            if (_sessions.TryRemove(pair.Key, out var entry)) entry.Dispose();
    }
}

public sealed record GatewayOpenResult(string? SessionId, string? Error)
{
    public bool Succeeded => Error is null && SessionId is not null;
}

public sealed record GatewayCapability(string Id, string Version, string Operation, string Lifecycle);

public sealed record GatewayDiscoveryResult(IReadOnlyList<GatewayCapability> Capabilities, string? Error)
{
    public bool Succeeded => Error is null;
}

public sealed record GatewayInvokeResult(string RequestId, JsonElement? Output, string? HostError, string? TransportError)
{
    public bool Succeeded => HostError is null && TransportError is null && Output is not null;
}
