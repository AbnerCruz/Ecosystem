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
        internal DateTimeOffset LastActivityUtc;
        internal bool Closed;

        internal Entry(string peer, string actor, LocalHostSession session, DateTimeOffset now)
        {
            Peer = peer;
            Actor = actor;
            Session = session;
            LastActivityUtc = now;
        }

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
    private readonly TimeProvider _clock;
    private readonly object _openSync = new();
    private readonly ConcurrentDictionary<string, Entry> _sessions = new(StringComparer.Ordinal);
    private bool _disposed;

    public const int MaxSessions = 4;
    public static readonly TimeSpan DefaultDeadline = TimeSpan.FromSeconds(10);
    public static readonly TimeSpan MaximumDeadline = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan SessionIdleTimeout = TimeSpan.FromMinutes(2);

    public AuthenticatedHostGateway(LocalCapabilityHost host, TimeProvider? clock = null)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _clock = clock ?? TimeProvider.System;
    }

    public GatewayOpenResult Open(string peer, string actor, LocalContext context, IEnumerable<string> grants)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (string.IsNullOrWhiteSpace(peer) || string.IsNullOrWhiteSpace(actor))
            return new(null, "IDENTITY_REQUIRED");
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(grants);

        lock (_openSync)
        {
            ExpireIdle();
            if (_sessions.Values.Count(entry => !entry.Closed) >= MaxSessions)
                return new(null, "SESSION_LIMIT");

            LocalHostSession local;
            try { local = _host.Open(actor, context, grants); }
            catch (ArgumentException) { return new(null, "INVALID_CONTEXT"); }

            for (var attempt = 0; attempt < 4; attempt++)
            {
                var id = Convert.ToHexString(Guid.NewGuid().ToByteArray()).ToLowerInvariant();
                var entry = new Entry(peer, actor, local, _clock.GetUtcNow());
                if (_sessions.TryAdd(id, entry)) return new(id, null);
            }

            local.Dispose();
            return new(null, "SESSION_LIMIT");
        }
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
        CancellationTokenSource deadlineCancellation;
        lock (entry.Sync)
        {
            if (entry.Closed) return new(requestId, null, "SESSION_CLOSED", null);
            if (entry.ActiveCancellation is not null)
                return new(requestId, null, null, "SESSION_BUSY");
            deadlineCancellation = new CancellationTokenSource(effectiveDeadline);
            run = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadlineCancellation.Token);
            entry.ActiveCancellation = run;
            entry.ActiveRequestId = requestId;
            entry.ActiveExplicitCancel = false;
            entry.LastActivityUtc = _clock.GetUtcNow();
        }

        var dispatch = entry.Session.DispatchAsync(new LocalEnvelope(
            LocalProtocol.Id, requestId, entry.Actor, "request", capability, operation, minimumVersion, input), run.Token);
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = run.Token.Register(() => cancelled.TrySetResult());

        var winner = await Task.WhenAny(dispatch, cancelled.Task).ConfigureAwait(false);
        if (winner != dispatch)
        {
            var early = CancellationOutcome(entry, requestId, run, deadlineCancellation, cancellationToken);
            _ = ObserveLateAsync(dispatch, entry, run, deadlineCancellation);
            return early;
        }

        try
        {
            var response = await dispatch.ConfigureAwait(false);
            if (run.IsCancellationRequested && response.Error == "CANCELLED")
                return CancellationOutcome(entry, requestId, run, deadlineCancellation, cancellationToken);
            return new(response.RequestId, response.Output, response.Error, null);
        }
        finally
        {
            Cleanup(entry, run, deadlineCancellation);
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
                entry.LastActivityUtc = _clock.GetUtcNow();
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
        ExpireIdle();
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
        ExpireIdle();
        if (!_sessions.TryGetValue(sessionId, out var found) || found.Peer != peer) return false;
        lock (found.Sync)
        {
            if (found.Closed) return false;
            found.LastActivityUtc = _clock.GetUtcNow();
        }
        entry = found;
        return true;
    }

    private void ExpireIdle()
    {
        if (_disposed) return;
        var now = _clock.GetUtcNow();
        foreach (var pair in _sessions.ToArray())
        {
            var expire = false;
            lock (pair.Value.Sync)
            {
                expire = !pair.Value.Closed && pair.Value.ActiveCancellation is null
                    && now - pair.Value.LastActivityUtc >= SessionIdleTimeout;
            }
            if (!expire) continue;
            if (_sessions.TryGetValue(pair.Key, out var current) &&
                ReferenceEquals(current, pair.Value) &&
                _sessions.TryRemove(pair.Key, out var removed))
                removed.Dispose();
        }
    }

    private GatewayInvokeResult CancellationOutcome(Entry entry, string requestId,
        CancellationTokenSource run, CancellationTokenSource deadlineCancellation,
        CancellationToken callerCancellation)
    {
        bool explicitCancel;
        bool closed;
        lock (entry.Sync)
        {
            explicitCancel = ReferenceEquals(entry.ActiveCancellation, run) && entry.ActiveExplicitCancel;
            closed = entry.Closed;
        }

        if (callerCancellation.IsCancellationRequested || explicitCancel || closed)
            return new(requestId, null, "CANCELLED", null);
        if (deadlineCancellation.IsCancellationRequested)
            return new(requestId, null, null, "DEADLINE_EXCEEDED");
        return new(requestId, null, "CANCELLED", null);
    }

    private async Task ObserveLateAsync(Task<LocalResponse> dispatch, Entry entry,
        CancellationTokenSource run, CancellationTokenSource deadlineCancellation)
    {
        try { _ = await dispatch.ConfigureAwait(false); }
        catch (Exception) { /* late failure is intentionally not surfaced after terminal cancellation */ }
        finally { Cleanup(entry, run, deadlineCancellation); }
    }

    private void Cleanup(Entry entry, CancellationTokenSource run, CancellationTokenSource deadlineCancellation)
    {
        lock (entry.Sync)
        {
            if (ReferenceEquals(entry.ActiveCancellation, run))
            {
                entry.ActiveCancellation = null;
                entry.ActiveRequestId = null;
                entry.ActiveExplicitCancel = false;
                entry.LastActivityUtc = _clock.GetUtcNow();
            }
        }
        run.Dispose();
        deadlineCancellation.Dispose();
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
