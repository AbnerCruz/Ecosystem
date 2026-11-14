namespace Lunet.Networking;

/// <summary>Conditions of a simulated network. <see cref="Presets"/> covers common cases.</summary>
public sealed record NetworkConditions
{
    /// <summary>One-way delay in seconds and its random variation.</summary>
    public double Latency { get; init; }
    public double Jitter { get; init; }
    /// <summary>Probability 0–1 that an unreliable packet is lost (reliable ones arrive late instead, like a resend).</summary>
    public double Loss { get; init; }
    public double Duplication { get; init; }
    /// <summary>Bytes per second; 0 = unlimited.</summary>
    public int Bandwidth { get; init; }

    public static NetworkConditions Perfect { get; } = new();
    public static IReadOnlyList<(string Name, NetworkConditions Conditions)> Presets { get; } =
    [
        ("Perfeita", Perfect),
        ("Wi-Fi bom", new() { Latency = 0.015, Jitter = 0.005, Loss = 0.002 }),
        ("4G", new() { Latency = 0.05, Jitter = 0.02, Loss = 0.01 }),
        ("3G ruim", new() { Latency = 0.15, Jitter = 0.06, Loss = 0.05, Bandwidth = 48_000 }),
        ("Outro continente", new() { Latency = 0.12, Jitter = 0.01, Loss = 0.005 }),
        ("Caótica", new() { Latency = 0.25, Jitter = 0.12, Loss = 0.15, Duplication = 0.05, Bandwidth = 24_000 }),
    ];

    public void Validate()
    {
        static bool Ok(double v, double max) => double.IsFinite(v) && v >= 0 && v <= max;
        if (!Ok(Latency, 5) || !Ok(Jitter, 5) || !Ok(Loss, 1) || !Ok(Duplication, 1) || Bandwidth < 0)
            throw new ArgumentOutOfRangeException(nameof(NetworkConditions), "Condições de rede fora dos limites.");
    }
}

/// <summary>
/// Network Simulator (SPEC §8): wraps any transport and delays, drops, duplicates and throttles what this side sends,
/// keeping the guarantees of each <see cref="DeliveryMode"/> (ordered stays ordered; sequenced drops stale packets;
/// reliable is never lost, only late). Wrap both ends for a symmetric link. Deterministic for a seed and clock.
/// </summary>
public sealed class NetworkSimulator : INetworkTransport
{
    private readonly INetworkTransport _inner;
    private readonly Func<double> _clock;
    private readonly Random _random;
    private readonly List<Pending> _pending = [];
    private readonly Dictionary<int, double> _lastOrderedRelease = new();
    private readonly Dictionary<int, long> _sequenceSent = new();
    private readonly Dictionary<int, long> _sequenceReleased = new();
    private double _bandwidthFreeAt;
    private long _order;

    private sealed record Pending(int Connection, byte[] Payload, DeliveryMode Mode, double ReleaseAt, long Sequence, long Order);

    public NetworkSimulator(INetworkTransport inner, NetworkConditions conditions, Func<double>? clock = null, int seed = 1)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        conditions.Validate();
        Conditions = conditions;
        var watch = System.Diagnostics.Stopwatch.StartNew();
        _clock = clock ?? (() => watch.Elapsed.TotalSeconds);
        _random = new Random(seed);
    }

    public NetworkConditions Conditions { get; set; }
    public INetworkTransport Inner => _inner;
    public bool IsServer => _inner.IsServer;
    public bool IsRunning => _inner.IsRunning;
    public int MaxMessageSize => _inner.MaxMessageSize;
    public int LocalPort => _inner.LocalPort;
    public TransportStatistics Statistics => _inner.Statistics;
    /// <summary>Packets waiting to be "on the wire".</summary>
    public int InFlight => _pending.Count;

    public void StartServer(int port, int maxConnections) => _inner.StartServer(port, maxConnections);
    public void Connect(string address, int port) => _inner.Connect(address, port);
    public void Disconnect(int connectionId, string reason = "") => _inner.Disconnect(connectionId, reason);
    public double? RoundTripTime(int connectionId) => _inner.RoundTripTime(connectionId) is { } rtt ? rtt + 2 * Conditions.Latency : null;
    public bool TryReceive(out TransportEvent transportEvent) => _inner.TryReceive(out transportEvent);
    public void Stop() { _pending.Clear(); _inner.Stop(); }
    public void Dispose() { _pending.Clear(); _inner.Dispose(); }

    public void Send(int connectionId, ReadOnlySpan<byte> payload, DeliveryMode mode)
    {
        var c = Conditions;
        double now = _clock();
        bool reliable = mode is DeliveryMode.Reliable or DeliveryMode.ReliableOrdered;
        if (!reliable && _random.NextDouble() < c.Loss) { Statistics.PacketsDropped++; return; }
        double delay = c.Latency + (c.Jitter > 0 ? (_random.NextDouble() * 2 - 1) * c.Jitter : 0);
        if (reliable && _random.NextDouble() < c.Loss) delay += 2 * c.Latency + 0.05; // lost once, resent after a round trip
        delay = Math.Max(0, delay);
        double release = now + delay;
        if (c.Bandwidth > 0)
        {
            double start = Math.Max(now, _bandwidthFreeAt);
            _bandwidthFreeAt = start + (payload.Length + 28) / (double)c.Bandwidth; // + UDP/IP headers
            release = Math.Max(release, _bandwidthFreeAt);
        }
        if (mode == DeliveryMode.ReliableOrdered)
        {
            release = Math.Max(release, _lastOrderedRelease.GetValueOrDefault(connectionId));
            _lastOrderedRelease[connectionId] = release;
        }
        long sequence = 0;
        if (mode == DeliveryMode.Sequenced) sequence = _sequenceSent[connectionId] = _sequenceSent.GetValueOrDefault(connectionId) + 1;
        var bytes = payload.ToArray();
        _pending.Add(new Pending(connectionId, bytes, mode, release, sequence, _order++));
        if (!reliable && _random.NextDouble() < c.Duplication)
            _pending.Add(new Pending(connectionId, bytes, mode, release + _random.NextDouble() * Math.Max(0.001, c.Jitter), sequence, _order++));
    }

    public void Poll()
    {
        double now = _clock();
        if (_pending.Count > 0)
        {
            var due = _pending.Where(p => p.ReleaseAt <= now).OrderBy(p => p.ReleaseAt).ThenBy(p => p.Order).ToList();
            foreach (var p in due)
            {
                _pending.Remove(p);
                if (p.Mode == DeliveryMode.Sequenced)
                {
                    var last = _sequenceReleased.GetValueOrDefault(p.Connection);
                    if (p.Sequence <= last) { Statistics.PacketsDropped++; continue; } // stale or duplicate: the receiver drops it
                    _sequenceReleased[p.Connection] = p.Sequence;
                }
                _inner.Send(p.Connection, p.Payload, p.Mode);
            }
        }
        _inner.Poll();
    }
}
