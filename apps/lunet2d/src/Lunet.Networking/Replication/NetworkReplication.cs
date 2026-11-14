namespace Lunet.Networking;

/// <summary>
/// Server-authoritative replication (SPEC §8 Snapshots/Replication): the server spawns and despawns objects (reliable)
/// and sends the state of all objects at <see cref="NetworkSessionOptions.TickRate"/> (sequenced: a late snapshot never
/// overwrites a newer one). Every snapshot is complete, so a lost packet is repaired by the next one. Late joiners
/// receive every existing object.
/// </summary>
public sealed class NetworkReplication
{
    private readonly NetworkSession _session;
    private readonly Dictionary<string, Func<NetworkObject>> _factories = new(StringComparer.Ordinal);
    private readonly Dictionary<int, NetworkObject> _objects = new();
    private readonly NetWriter _writer = new(1024);
    private int _nextId = 1;
    private double _nextSnapshot;
    private double _lastSnapshotTime = double.MinValue;

    internal NetworkReplication(NetworkSession session) => _session = session;

    public IReadOnlyCollection<NetworkObject> Objects => _objects.Values;
    public NetworkObject? Find(int networkId) => _objects.GetValueOrDefault(networkId);
    public IEnumerable<T> All<T>() where T : NetworkObject => _objects.Values.OfType<T>();

    /// <summary>Raised on every device when an object appears / disappears.</summary>
    public event Action<NetworkObject>? Spawned;
    public event Action<NetworkObject>? Despawned;

    /// <summary>Declares how clients create objects of a kind (same kinds on every device).</summary>
    public void Register<T>(string kind, Func<T> create) where T : NetworkObject
    {
        if (string.IsNullOrWhiteSpace(kind) || kind.Length > 64) throw new ArgumentException("Nome de tipo inválido.", nameof(kind));
        _factories[kind] = create;
    }

    /// <summary>Server: creates an object everywhere. <paramref name="owner"/> is the controlling peer (-1 = server).</summary>
    public T Spawn<T>(string kind, int owner = -1, Action<T>? setup = null) where T : NetworkObject
    {
        if (!_session.IsServer) throw new InvalidOperationException("Só o servidor cria objetos de rede.");
        if (!_factories.TryGetValue(kind, out var factory)) throw new InvalidOperationException($"Registre o tipo '{kind}' antes.");
        var created = factory();
        if (created is not T obj) throw new InvalidOperationException($"O tipo '{kind}' não cria {typeof(T).Name}.");
        obj.NetworkId = _nextId++;
        obj.OwnerPeerId = owner;
        obj.Kind = kind;
        obj.Session = _session;
        setup?.Invoke(obj);
        _objects[obj.NetworkId] = obj;
        foreach (var peer in _session.RemotePeers) SendSpawn(peer, obj);
        obj.OnSpawned();
        Spawned?.Invoke(obj);
        return obj;
    }

    public void Despawn(NetworkObject obj)
    {
        if (!_session.IsServer) throw new InvalidOperationException("Só o servidor remove objetos de rede.");
        if (!_objects.Remove(obj.NetworkId)) return;
        _writer.Reset();
        _writer.Write(NetworkSession.Despawn);
        _writer.WriteVarInt(obj.NetworkId);
        foreach (var peer in _session.RemotePeers) _session.SendRaw(peer, _writer.Span, DeliveryMode.ReliableOrdered);
        obj.OnDespawned();
        Despawned?.Invoke(obj);
    }

    private void SendSpawn(int peer, NetworkObject obj)
    {
        _writer.Reset();
        _writer.Write(NetworkSession.Spawn);
        _writer.WriteVarInt(obj.NetworkId);
        _writer.Write(obj.Kind);
        _writer.WriteVarInt(obj.OwnerPeerId);
        foreach (var v in obj.VariableList) v.Write(_writer);
        _session.SendRaw(peer, _writer.Span, DeliveryMode.ReliableOrdered);
    }

    internal void OnPeerJoined(int peer)
    {
        foreach (var obj in _objects.Values) SendSpawn(peer, obj);
    }

    internal void OnPeerLeft(int peer)
    {
        if (!_session.IsServer) return;
        // Objects owned by a player who left go back to the server; the game decides whether to despawn them.
        foreach (var obj in _objects.Values.Where(o => o.OwnerPeerId == peer)) obj.OwnerPeerId = -1;
    }

    internal void Tick(double now)
    {
        if (!_session.IsServer || _objects.Count == 0 || now < _nextSnapshot) return;
        _nextSnapshot = Math.Max(now, _nextSnapshot + 1.0 / _session.Options.TickRate);
        if (!_session.RemotePeers.Any()) return;
        _writer.Reset();
        _writer.Write(NetworkSession.Snapshot);
        _writer.Write(_session.Clock.ServerTime);
        _writer.WriteVarUInt((uint)_objects.Count);
        foreach (var obj in _objects.Values)
        {
            _writer.WriteVarInt(obj.NetworkId);
            foreach (var v in obj.VariableList) v.Write(_writer);
        }
        foreach (var peer in _session.RemotePeers) _session.SendRaw(peer, _writer.Span, DeliveryMode.Sequenced);
    }

    internal void OnServerMessage(byte kind, NetReader reader)
    {
        switch (kind)
        {
            case NetworkSession.Spawn:
            {
                int id = reader.ReadVarInt();
                string type = reader.ReadString();
                int owner = reader.ReadVarInt();
                if (!_factories.TryGetValue(type, out var factory)) throw new InvalidDataException($"Tipo de objeto desconhecido: {type}.");
                var obj = factory();
                obj.NetworkId = id; obj.Kind = type; obj.OwnerPeerId = owner; obj.Session = _session;
                double time = _session.Clock.ServerTime;
                foreach (var v in obj.VariableList) v.Read(reader, time);
                _objects[id] = obj;
                obj.OnSpawned();
                Spawned?.Invoke(obj);
                break;
            }
            case NetworkSession.Despawn:
            {
                if (_objects.Remove(reader.ReadVarInt(), out var obj)) { obj.OnDespawned(); Despawned?.Invoke(obj); }
                break;
            }
            case NetworkSession.Snapshot:
            {
                double time = reader.ReadDouble();
                if (time <= _lastSnapshotTime) return;
                _lastSnapshotTime = time;
                uint count = reader.ReadVarUInt();
                for (int i = 0; i < count; i++)
                {
                    int id = reader.ReadVarInt();
                    // An object not spawned yet here cannot be decoded (its layout is unknown): skip this snapshot.
                    if (!_objects.TryGetValue(id, out var obj)) return;
                    foreach (var v in obj.VariableList) v.Read(reader, time);
                }
                break;
            }
        }
    }
}

/// <summary>
/// Client-side prediction with server reconciliation (SPEC §8): the owner applies its input immediately, remembers it,
/// and when the server's authoritative state for input N arrives, restarts from it and replays inputs after N. The
/// same <c>simulate</c> function must run on the server, so both agree.
/// </summary>
public sealed class ClientPrediction<TInput, TState>
{
    private readonly Func<TState, TInput, double, TState> _simulate;
    private readonly List<(uint Sequence, TInput Input, double Delta)> _pending = [];
    private uint _sequence;

    public ClientPrediction(TState initial, Func<TState, TInput, double, TState> simulate)
    {
        Current = initial;
        _simulate = simulate ?? throw new ArgumentNullException(nameof(simulate));
    }

    /// <summary>The predicted state to draw for the local player.</summary>
    public TState Current { get; private set; }
    public int PendingInputs => _pending.Count;
    public uint LastSequence => _sequence;

    /// <summary>Applies an input locally; send the returned sequence with the input to the server.</summary>
    public uint Apply(TInput input, double delta)
    {
        _sequence++;
        Current = _simulate(Current, input, delta);
        _pending.Add((_sequence, input, delta));
        if (_pending.Count > 1024) _pending.RemoveAt(0);
        return _sequence;
    }

    /// <summary>Server said: after input <paramref name="lastProcessed"/> the state is <paramref name="authoritative"/>.</summary>
    public void Reconcile(uint lastProcessed, TState authoritative)
    {
        _pending.RemoveAll(p => p.Sequence <= lastProcessed);
        var state = authoritative;
        foreach (var (_, input, delta) in _pending) state = _simulate(state, input, delta);
        Current = state;
    }
}
