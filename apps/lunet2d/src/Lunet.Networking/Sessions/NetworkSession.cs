using System.Diagnostics;

namespace Lunet.Networking;

/// <summary>A player in the session. The host's own player is <see cref="NetworkSession.HostPeerId"/>.</summary>
public sealed class NetworkPeer
{
    internal NetworkPeer(int id, string name) { Id = id; Name = name; }
    public int Id { get; }
    public string Name { get; internal set; }
    /// <summary>Round-trip time in seconds, as measured by the session (0 for the host's own player).</summary>
    public double RoundTripTime { get; internal set; }
    public override string ToString() => $"{Name} (#{Id})";
}

public enum NetworkSessionState { Connecting, Connected, Disconnected }

/// <summary>Who may join and how fast the session runs.</summary>
public sealed record NetworkSessionOptions
{
    /// <summary>Only sessions of the same game and protocol version talk to each other.</summary>
    public required string GameId { get; init; }
    public int ProtocolVersion { get; init; } = 1;
    public string PlayerName { get; init; } = "Jogador";
    public int MaxPlayers { get; init; } = 8;
    /// <summary>Snapshots per second sent by the server.</summary>
    public double TickRate { get; init; } = 30;
    /// <summary>Messages a client may send per second before being dropped (and kicked when it insists).</summary>
    public int MaxMessagesPerSecond { get; init; } = 240;
    /// <summary>How far behind the server time remote objects are drawn, to interpolate smoothly between snapshots.</summary>
    public double InterpolationDelay { get; init; } = 0.1;
    public double HandshakeTimeout { get; init; } = 5;
}

/// <summary>
/// High-level multiplayer session (SPEC §8, LUNET-501) over any <see cref="INetworkTransport"/>: handshake with game and
/// version check, player list, typed messages (<see cref="Register{T}"/>), server-authoritative flow, clock sync
/// (<see cref="Clock"/>), per-client rate limiting and object replication (<see cref="Replication"/>). Single-threaded:
/// call <see cref="Update"/> from the game's Update; handlers run inside it, on the game thread.
/// </summary>
public sealed class NetworkSession : IDisposable
{
    public const int HostPeerId = 0;
    private const byte Hello = 1, Welcome = 2, Reject = 3, Joined = 4, Left = 5, Ping = 6, Pong = 7, User = 8;
    internal const byte Spawn = 9, Despawn = 10, Snapshot = 11;

    private readonly INetworkTransport _transport;
    private readonly Func<double> _now;
    private readonly Dictionary<int, NetworkPeer> _peers = new();
    private readonly Dictionary<int, double> _pendingSince = new();
    private readonly Dictionary<int, (double Window, int Count, int Strikes)> _rates = new();
    private readonly Dictionary<ushort, Func<NetReader, object>> _readers = new();
    private readonly Dictionary<Type, ushort> _ids = new();
    private readonly Dictionary<ushort, List<Action<NetworkPeer, object>>> _handlers = new();
    private readonly NetWriter _writer = new(512);
    private double _nextPing;

    private NetworkSession(INetworkTransport transport, NetworkSessionOptions options, Func<double>? clock)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        Options = options ?? throw new ArgumentNullException(nameof(options));
        if (string.IsNullOrWhiteSpace(options.GameId)) throw new ArgumentException("Informe o GameId.");
        if (options.TickRate is < 1 or > 120) throw new ArgumentOutOfRangeException(nameof(options), "TickRate entre 1 e 120.");
        var watch = Stopwatch.StartNew();
        _now = clock ?? (() => watch.Elapsed.TotalSeconds);
        Clock = new NetworkClock(_now);
        Replication = new NetworkReplication(this);
    }

    /// <summary>Starts a session as server with a local player (client-hosted) or, with <paramref name="dedicated"/>, without one.</summary>
    public static NetworkSession Host(INetworkTransport transport, int port, NetworkSessionOptions options, bool dedicated = false, Func<double>? clock = null)
    {
        var session = new NetworkSession(transport, options, clock) { IsServer = true, IsDedicated = dedicated };
        transport.StartServer(port, Math.Max(1, options.MaxPlayers - (dedicated ? 0 : 1)));
        session.State = NetworkSessionState.Connected;
        session.LocalPeerId = dedicated ? -1 : HostPeerId;
        if (!dedicated) session._peers[HostPeerId] = new NetworkPeer(HostPeerId, options.PlayerName);
        session.Clock.SetServer();
        return session;
    }

    public static NetworkSession Join(INetworkTransport transport, string address, int port, NetworkSessionOptions options, Func<double>? clock = null)
    {
        var session = new NetworkSession(transport, options, clock) { State = NetworkSessionState.Connecting, LocalPeerId = -1 };
        transport.Connect(address, port);
        session._pendingSince[TransportEvent.ServerConnection] = session._now();
        return session;
    }

    public NetworkSessionOptions Options { get; }
    private string PlayerName => (Options.PlayerName.Trim() is { Length: > 32 } longName ? longName[..32] : Options.PlayerName.Trim());
    public INetworkTransport Transport => _transport;
    public bool IsServer { get; private set; }
    public bool IsDedicated { get; private set; }
    public NetworkSessionState State { get; private set; }
    public string? DisconnectReason { get; private set; }
    /// <summary>This device's player id (-1 on a dedicated server or before joining).</summary>
    public int LocalPeerId { get; private set; }
    public IReadOnlyCollection<NetworkPeer> Peers => _peers.Values;
    public NetworkPeer? Peer(int id) => _peers.GetValueOrDefault(id);
    public NetworkClock Clock { get; }
    public NetworkReplication Replication { get; }
    public int Port => _transport.LocalPort;

    public event Action<NetworkPeer>? PeerJoined;
    public event Action<NetworkPeer>? PeerLeft;
    /// <summary>Client: the server accepted us.</summary>
    public event Action? Connected;
    public event Action<string>? Disconnected;

    // ---------- Messages ----------

    /// <summary>Declares a message type with a stable id (same on every device). Ids 0–65535; register before sending.</summary>
    public void Register<T>(ushort id) where T : INetSerializable, new()
    {
        if (_readers.ContainsKey(id)) throw new InvalidOperationException($"Já existe uma mensagem com id {id}.");
        _readers[id] = reader => reader.Read<T>();
        _ids[typeof(T)] = id;
    }

    public void On<T>(Action<NetworkPeer, T> handler) where T : INetSerializable, new()
    {
        var id = IdOf<T>();
        if (!_handlers.TryGetValue(id, out var list)) _handlers[id] = list = [];
        list.Add((peer, value) => handler(peer, (T)value));
    }

    private ushort IdOf<T>() => _ids.TryGetValue(typeof(T), out var id) ? id
        : throw new InvalidOperationException($"Registre {typeof(T).Name} com session.Register<{typeof(T).Name}>(id) antes.");

    private ReadOnlySpan<byte> Encode<T>(T message) where T : INetSerializable
    {
        _writer.Reset();
        _writer.Write(User);
        _writer.Write(IdOf<T>());
        message.Serialize(_writer);
        return _writer.Span;
    }

    /// <summary>Client → server (on the host, delivered locally to its own handlers as peer 0).</summary>
    public void SendToServer<T>(T message, DeliveryMode mode = DeliveryMode.ReliableOrdered) where T : INetSerializable, new()
    {
        if (IsServer)
        {
            if (!IsDedicated) Dispatch(_peers[HostPeerId], IdOf<T>(), Roundtrip(message));
            return;
        }
        if (State != NetworkSessionState.Connected) return;
        _transport.Send(TransportEvent.ServerConnection, Encode(message), mode);
    }

    /// <summary>Server → one client (to the host's own player, delivered locally).</summary>
    public void SendTo<T>(int peerId, T message, DeliveryMode mode = DeliveryMode.ReliableOrdered) where T : INetSerializable, new()
    {
        RequireServer();
        if (peerId == HostPeerId && !IsDedicated) { Dispatch(_peers[HostPeerId], IdOf<T>(), Roundtrip(message)); return; }
        if (_peers.ContainsKey(peerId)) _transport.Send(peerId, Encode(message), mode);
    }

    /// <summary>Server → every client (and the host's own player unless <paramref name="includeHost"/> is false).</summary>
    public void Broadcast<T>(T message, DeliveryMode mode = DeliveryMode.ReliableOrdered, int exceptPeer = -1, bool includeHost = true) where T : INetSerializable, new()
    {
        RequireServer();
        var bytes = Encode(message).ToArray();
        foreach (var peer in _peers.Values)
        {
            if (peer.Id == exceptPeer) continue;
            if (peer.Id == HostPeerId) { if (includeHost && !IsDedicated) Dispatch(peer, IdOf<T>(), Roundtrip(message)); continue; }
            _transport.Send(peer.Id, bytes, mode);
        }
    }

    private object Roundtrip<T>(T message) where T : INetSerializable, new()
    {
        // The host sees exactly what a client would receive (no shared references between "sender" and "receiver").
        var writer = new NetWriter();
        message.Serialize(writer);
        return new NetReader(writer.ToArray()).Read<T>();
    }

    internal void SendRaw(int peerId, ReadOnlySpan<byte> bytes, DeliveryMode mode)
    {
        if (peerId == HostPeerId && IsServer) return;
        _transport.Send(peerId, bytes, mode);
    }

    internal IEnumerable<int> RemotePeers => _peers.Keys.Where(id => id != HostPeerId);

    public void Kick(int peerId, string reason = "Removido pelo host.")
    {
        RequireServer();
        if (peerId == HostPeerId) return;
        _transport.Disconnect(peerId, reason);
        RemovePeer(peerId);
    }

    private void RequireServer()
    {
        if (!IsServer) throw new InvalidOperationException("Só o servidor (host) pode enviar para os jogadores.");
    }

    // ---------- Loop ----------

    /// <summary>Processes the network: call once per frame (Update). Handlers and events run here.</summary>
    public void Update()
    {
        _transport.Poll();
        while (_transport.TryReceive(out var e))
        {
            switch (e.Kind)
            {
                case TransportEventKind.Connected:
                    if (IsServer) _pendingSince[e.ConnectionId] = _now();
                    else SendHello();
                    break;
                case TransportEventKind.Disconnected:
                    if (IsServer) { _pendingSince.Remove(e.ConnectionId); RemovePeer(e.ConnectionId); }
                    else Close(e.Reason ?? "Conexão encerrada.");
                    break;
                case TransportEventKind.Data:
                    try { Handle(e.ConnectionId, e.Payload); }
                    catch (InvalidDataException) when (IsServer) { Kick(e.ConnectionId, "Mensagem inválida."); }
                    catch (InvalidDataException) { }
                    break;
            }
        }
        double now = _now();
        foreach (var (id, since) in _pendingSince.ToArray())
            if (now - since > Options.HandshakeTimeout)
            {
                _pendingSince.Remove(id);
                if (IsServer) _transport.Disconnect(id, "Sem identificação.");
                else { _transport.Disconnect(id, "Tempo esgotado."); Close("O servidor não respondeu."); }
            }
        if (!IsServer && State == NetworkSessionState.Connected && now >= _nextPing)
        {
            _nextPing = now + 0.5;
            _writer.Reset(); _writer.Write(Ping); _writer.Write(now);
            _transport.Send(TransportEvent.ServerConnection, _writer.Span, DeliveryMode.Unreliable);
        }
        Replication.Tick(now);
    }

    private void SendHello()
    {
        _writer.Reset();
        _writer.Write(Hello);
        _writer.Write(Options.ProtocolVersion);
        _writer.Write(Options.GameId);
        _writer.Write(PlayerName);
        _transport.Send(TransportEvent.ServerConnection, _writer.Span, DeliveryMode.ReliableOrdered);
    }

    private bool RateLimited(int peer)
    {
        double now = _now();
        var (window, count, strikes) = _rates.GetValueOrDefault(peer, (now, 0, 0));
        if (now - window >= 1) { window = now; count = 0; }
        count++;
        bool over = count > Options.MaxMessagesPerSecond;
        if (over && count == Options.MaxMessagesPerSecond + 1) strikes++;
        _rates[peer] = (window, count, strikes);
        if (strikes >= 3) { Kick(peer, "Mensagens demais."); return true; }
        return over;
    }

    private void Handle(int connection, byte[] payload)
    {
        var reader = new NetReader(payload);
        byte kind = reader.ReadByte();
        if (IsServer)
        {
            if (kind == Hello) { Accept(connection, reader); return; }
            if (!_peers.TryGetValue(connection, out var peer)) { _transport.Disconnect(connection, "Sem identificação."); return; }
            if (RateLimited(connection)) return;
            switch (kind)
            {
                case Ping:
                    var clientTime = reader.ReadDouble();
                    _writer.Reset(); _writer.Write(Pong); _writer.Write(clientTime); _writer.Write(Clock.ServerTime);
                    _transport.Send(connection, _writer.Span, DeliveryMode.Unreliable);
                    peer.RoundTripTime = _transport.RoundTripTime(connection) ?? peer.RoundTripTime;
                    break;
                case User:
                    var id = reader.ReadUInt16();
                    if (!_readers.TryGetValue(id, out var read)) throw new InvalidDataException("Mensagem desconhecida.");
                    Dispatch(peer, id, read(reader));
                    break;
                default:
                    throw new InvalidDataException("Clientes não enviam este tipo de mensagem.");
            }
            return;
        }
        switch (kind)
        {
            case Welcome:
                LocalPeerId = reader.ReadVarInt();
                var serverTime = reader.ReadDouble();
                Clock.Prime(serverTime);
                int count = (int)reader.ReadVarUInt();
                if (count > 1024) throw new InvalidDataException("Lista de jogadores inválida.");
                for (int i = 0; i < count; i++)
                {
                    var pid = reader.ReadVarInt();
                    _peers[pid] = new NetworkPeer(pid, reader.ReadString());
                }
                _peers[LocalPeerId] = new NetworkPeer(LocalPeerId, PlayerName.Length > 0 ? PlayerName : "Jogador " + LocalPeerId);
                _pendingSince.Remove(TransportEvent.ServerConnection);
                State = NetworkSessionState.Connected;
                _nextPing = 0;
                Connected?.Invoke();
                foreach (var p in _peers.Values) PeerJoined?.Invoke(p);
                break;
            case Reject:
                _pendingSince.Remove(TransportEvent.ServerConnection);
                Close(reader.ReadString());
                break;
            case Joined:
                var joinedId = reader.ReadVarInt();
                var joined = new NetworkPeer(joinedId, reader.ReadString());
                _peers[joinedId] = joined;
                PeerJoined?.Invoke(joined);
                break;
            case Left:
                var leftId = reader.ReadVarInt();
                if (_peers.Remove(leftId, out var left)) { Replication.OnPeerLeft(leftId); PeerLeft?.Invoke(left); }
                break;
            case Pong:
                Clock.OnPong(reader.ReadDouble(), reader.ReadDouble());
                if (_peers.TryGetValue(LocalPeerId, out var me)) me.RoundTripTime = Clock.RoundTripTime;
                break;
            case User:
                var messageId = reader.ReadUInt16();
                if (!_readers.TryGetValue(messageId, out var reading)) return; // newer server: ignore unknown messages
                Dispatch(_peers.GetValueOrDefault(HostPeerId) ?? new NetworkPeer(HostPeerId, "Servidor"), messageId, reading(reader));
                break;
            case Spawn or Despawn or Snapshot:
                Replication.OnServerMessage(kind, reader);
                break;
        }
    }

    private void Accept(int connection, NetReader reader)
    {
        if (_peers.ContainsKey(connection)) throw new InvalidDataException("Identificação repetida.");
        var version = reader.ReadInt32();
        var gameId = reader.ReadString();
        var name = reader.ReadString().Trim();
        string? problem = gameId != Options.GameId ? "Este servidor é de outro jogo."
            : version != Options.ProtocolVersion ? $"Versão diferente (servidor {Options.ProtocolVersion}, sua {version}). Atualize o jogo."
            : _peers.Count >= Options.MaxPlayers ? "A partida está cheia." : null;
        _pendingSince.Remove(connection);
        if (problem is not null)
        {
            _writer.Reset(); _writer.Write(Reject); _writer.Write(problem);
            _transport.Send(connection, _writer.Span, DeliveryMode.ReliableOrdered);
            _transport.Disconnect(connection, problem);
            return;
        }
        if (name.Length == 0) name = "Jogador " + connection;
        var peer = new NetworkPeer(connection, name);
        _writer.Reset();
        _writer.Write(Welcome);
        _writer.WriteVarInt(connection);
        _writer.Write(Clock.ServerTime);
        _writer.WriteVarUInt((uint)_peers.Count);
        foreach (var p in _peers.Values) { _writer.WriteVarInt(p.Id); _writer.Write(p.Name); }
        _transport.Send(connection, _writer.Span, DeliveryMode.ReliableOrdered);
        _writer.Reset(); _writer.Write(Joined); _writer.WriteVarInt(peer.Id); _writer.Write(peer.Name);
        var joined = _writer.ToArray();
        foreach (var other in RemotePeers) _transport.Send(other, joined, DeliveryMode.ReliableOrdered);
        _peers[connection] = peer;
        Replication.OnPeerJoined(connection);
        PeerJoined?.Invoke(peer);
    }

    private void RemovePeer(int id)
    {
        if (!_peers.Remove(id, out var peer)) return;
        _rates.Remove(id);
        _writer.Reset(); _writer.Write(Left); _writer.WriteVarInt(id);
        var left = _writer.ToArray();
        foreach (var other in RemotePeers) _transport.Send(other, left, DeliveryMode.ReliableOrdered);
        Replication.OnPeerLeft(id);
        PeerLeft?.Invoke(peer);
    }

    private void Dispatch(NetworkPeer from, ushort id, object message)
    {
        if (_handlers.TryGetValue(id, out var list))
            foreach (var handler in list.ToArray()) handler(from, message);
    }

    private void Close(string reason)
    {
        if (State == NetworkSessionState.Disconnected) return;
        State = NetworkSessionState.Disconnected;
        DisconnectReason = reason;
        Disconnected?.Invoke(reason);
    }

    public void Dispose()
    {
        _transport.Stop();
        Close("Sessão encerrada.");
    }
}

/// <summary>
/// The shared clock (SPEC §8 NetworkClock): on the server it is local time; on a client it estimates server time from
/// ping/pong, trusting the samples with the lowest round trip (least queuing), and never jumps backwards.
/// </summary>
public sealed class NetworkClock
{
    private readonly Func<double> _now;
    private double _offset;
    private bool _server;
    private double _bestRtt = double.MaxValue;
    private double _lastReported;

    internal NetworkClock(Func<double> now) => _now = now;

    /// <summary>Seconds on the server's clock, comparable across all devices of the session.</summary>
    public double ServerTime
    {
        get
        {
            var t = _server ? _now() : _now() + _offset;
            if (t < _lastReported) t = _lastReported;
            _lastReported = t;
            return t;
        }
    }

    public double RoundTripTime { get; private set; }
    public bool Synchronized { get; private set; }

    internal void SetServer() { _server = true; Synchronized = true; }

    internal void Prime(double serverTime) => _offset = serverTime - _now();

    internal void OnPong(double clientSent, double serverTime)
    {
        double now = _now();
        double rtt = Math.Max(0, now - clientSent);
        RoundTripTime = RoundTripTime == 0 ? rtt : RoundTripTime * 0.8 + rtt * 0.2;
        double offset = serverTime + rtt / 2 - now;
        // Prefer low-RTT samples; slowly forget the best so a changed route is learned.
        _bestRtt = Math.Min(_bestRtt * 1.02, double.MaxValue);
        if (rtt <= _bestRtt * 1.25 || !Synchronized)
        {
            _bestRtt = Math.Min(_bestRtt, rtt);
            _offset = Synchronized ? _offset * 0.7 + offset * 0.3 : offset;
            Synchronized = true;
        }
    }
}
