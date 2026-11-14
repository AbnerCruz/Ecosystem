using LiteNetLib;

namespace Lunet.Networking;

/// <summary>
/// Realtime UDP transport (SPEC §8, ADR 0011): LiteNetLib inside, none of its types in the API. Reliable, ordered,
/// sequenced and unreliable delivery on one socket, fragmentation, timeouts and RTT. A connection key keeps strangers'
/// games from joining by accident (it is NOT authentication). Use on trusted LANs; public internet needs a relay or
/// dedicated server with real authentication.
/// </summary>
public sealed class UdpTransport : INetworkTransport
{
    private readonly EventBasedNetListener _listener = new();
    private readonly NetManager _manager;
    private readonly Queue<TransportEvent> _inbox = new();
    private readonly Dictionary<int, NetPeer> _peers = new();
    private readonly string _key;
    private int _maxConnections = 8;
    private NetPeer? _server;

    /// <param name="connectionKey">Game identifier shared by host and clients (e.g. the GameId). Mismatches are refused.</param>
    public UdpTransport(string connectionKey, int disconnectTimeoutMs = 5000)
    {
        if (string.IsNullOrWhiteSpace(connectionKey) || connectionKey.Length > 128)
            throw new ArgumentException("Informe uma chave de conexão (ex.: o GameId do jogo).", nameof(connectionKey));
        _key = connectionKey;
        _manager = new NetManager(_listener)
        {
            AutoRecycle = true,
            DisconnectTimeout = Math.Clamp(disconnectTimeoutMs, 1000, 60_000),
            UnconnectedMessagesEnabled = false,
            UpdateTime = 15,
        };
        _listener.ConnectionRequestEvent += request =>
        {
            if (!IsServer || _peers.Count >= _maxConnections) { request.Reject(); return; }
            request.AcceptIfKey(_key);
        };
        _listener.PeerConnectedEvent += peer =>
        {
            int id = IsServer ? peer.Id + 1 : TransportEvent.ServerConnection;
            if (IsServer) _peers[id] = peer; else _server = peer;
            _inbox.Enqueue(new TransportEvent(TransportEventKind.Connected, id, []));
        };
        _listener.PeerDisconnectedEvent += (peer, info) =>
        {
            int id = IsServer ? peer.Id + 1 : TransportEvent.ServerConnection;
            if (IsServer) _peers.Remove(id); else _server = null;
            _inbox.Enqueue(new TransportEvent(TransportEventKind.Disconnected, id, [], info.Reason.ToString()));
        };
        _listener.NetworkReceiveEvent += (peer, reader, channel, method) =>
        {
            var bytes = reader.GetRemainingBytes();
            Statistics.PacketsReceived++;
            Statistics.BytesReceived += bytes.Length;
            _inbox.Enqueue(new TransportEvent(TransportEventKind.Data, IsServer ? peer.Id + 1 : TransportEvent.ServerConnection, bytes));
        };
    }

    public bool IsServer { get; private set; }
    public bool IsRunning => _manager.IsRunning;
    public int MaxMessageSize => 64 * 1024;
    public int LocalPort => _manager.LocalPort;
    public TransportStatistics Statistics { get; } = new();

    public void StartServer(int port, int maxConnections)
    {
        if (IsRunning) throw new InvalidOperationException("Transporte já iniciado.");
        _maxConnections = Math.Clamp(maxConnections, 1, 1024);
        IsServer = true;
        if (!_manager.Start(port)) throw new InvalidOperationException($"Não foi possível abrir a porta UDP {port}.");
    }

    public void Connect(string address, int port)
    {
        if (IsRunning) throw new InvalidOperationException("Transporte já iniciado.");
        if (!_manager.Start()) throw new InvalidOperationException("Não foi possível abrir um socket UDP.");
        _server = _manager.Connect(address, port, _key);
    }

    private static DeliveryMethod Map(DeliveryMode mode) => mode switch
    {
        DeliveryMode.Unreliable => DeliveryMethod.Unreliable,
        DeliveryMode.Sequenced => DeliveryMethod.Sequenced,
        DeliveryMode.Reliable => DeliveryMethod.ReliableUnordered,
        _ => DeliveryMethod.ReliableOrdered,
    };

    public void Send(int connectionId, ReadOnlySpan<byte> payload, DeliveryMode mode)
    {
        if (payload.Length > MaxMessageSize) throw new ArgumentException("Mensagem maior que o limite do transporte.");
        var peer = connectionId == TransportEvent.ServerConnection && !IsServer ? _server : _peers.GetValueOrDefault(connectionId);
        if (peer is null || peer.ConnectionState != ConnectionState.Connected) return;
        // Unreliable packets cannot be fragmented: send them reliably when larger than one datagram.
        var method = Map(mode);
        if (method is DeliveryMethod.Unreliable or DeliveryMethod.Sequenced && payload.Length > peer.GetMaxSinglePacketSize(method))
            method = DeliveryMethod.ReliableOrdered;
        peer.Send(payload, method);
        Statistics.PacketsSent++;
        Statistics.BytesSent += payload.Length;
    }

    public void Disconnect(int connectionId, string reason = "")
    {
        var peer = connectionId == TransportEvent.ServerConnection && !IsServer ? _server : _peers.GetValueOrDefault(connectionId);
        peer?.Disconnect();
    }

    public double? RoundTripTime(int connectionId)
    {
        var peer = connectionId == TransportEvent.ServerConnection && !IsServer ? _server : _peers.GetValueOrDefault(connectionId);
        return peer is null ? null : peer.RoundTripTime / 1000.0;
    }

    public void Poll() => _manager.PollEvents();
    public bool TryReceive(out TransportEvent transportEvent) => _inbox.TryDequeue(out transportEvent);

    public void Stop()
    {
        if (_manager.IsRunning) _manager.Stop(sendDisconnectMessages: true);
        _peers.Clear();
        _server = null;
    }

    public void Dispose() => Stop();
}
