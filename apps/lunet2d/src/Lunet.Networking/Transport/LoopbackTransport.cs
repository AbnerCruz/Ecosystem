namespace Lunet.Networking;

/// <summary>
/// An in-memory "network" for multiplayer on one device (several players, one process) and for deterministic tests.
/// Create transports from the same <see cref="LoopbackNetwork"/>; a server listens on a virtual port, clients connect
/// to "loopback" on that port. Delivery is immediate and in order; wrap in <see cref="NetworkSimulator"/> for bad networks.
/// </summary>
public sealed class LoopbackNetwork
{
    internal readonly Dictionary<int, LoopbackTransport> Servers = new();
    private int _nextPort = 40_000;
    internal int AllocatePort() { while (Servers.ContainsKey(_nextPort)) _nextPort++; return _nextPort++; }
    public LoopbackTransport CreateTransport() => new(this);
}

public sealed class LoopbackTransport : INetworkTransport
{
    private readonly LoopbackNetwork _network;
    private readonly Queue<TransportEvent> _inbox = new();
    private readonly Dictionary<int, (LoopbackTransport Remote, int RemoteId)> _links = new();
    private int _nextConnection = 1;
    private int _maxConnections;
    private (LoopbackTransport Server, int Port)? _pendingConnect;

    internal LoopbackTransport(LoopbackNetwork network) => _network = network;

    public bool IsServer { get; private set; }
    public bool IsRunning { get; private set; }
    public int MaxMessageSize => 64 * 1024;
    public int LocalPort { get; private set; }
    public TransportStatistics Statistics { get; } = new();

    public void StartServer(int port, int maxConnections)
    {
        if (IsRunning) throw new InvalidOperationException("Transporte já iniciado.");
        port = port == 0 ? _network.AllocatePort() : port;
        if (!_network.Servers.TryAdd(port, this)) throw new InvalidOperationException($"Porta {port} já em uso.");
        LocalPort = port;
        _maxConnections = Math.Max(1, maxConnections);
        IsServer = true;
        IsRunning = true;
    }

    public void Connect(string address, int port)
    {
        if (IsRunning) throw new InvalidOperationException("Transporte já iniciado.");
        IsRunning = true;
        if (!_network.Servers.TryGetValue(port, out var server) || !server.IsRunning)
        {
            _inbox.Enqueue(new TransportEvent(TransportEventKind.Disconnected, TransportEvent.ServerConnection, [], "Nenhum servidor nessa porta."));
            return;
        }
        _pendingConnect = (server, port);
    }

    public void Poll()
    {
        if (_pendingConnect is { } pending)
        {
            _pendingConnect = null;
            var server = pending.Server;
            if (server._links.Count >= server._maxConnections)
            {
                _inbox.Enqueue(new TransportEvent(TransportEventKind.Disconnected, TransportEvent.ServerConnection, [], "Servidor cheio."));
                return;
            }
            int id = server._nextConnection++;
            server._links[id] = (this, TransportEvent.ServerConnection);
            _links[TransportEvent.ServerConnection] = (server, id);
            server._inbox.Enqueue(new TransportEvent(TransportEventKind.Connected, id, []));
            _inbox.Enqueue(new TransportEvent(TransportEventKind.Connected, TransportEvent.ServerConnection, []));
        }
    }

    public void Send(int connectionId, ReadOnlySpan<byte> payload, DeliveryMode mode)
    {
        if (payload.Length > MaxMessageSize) throw new ArgumentException("Mensagem maior que o limite do transporte.");
        if (!_links.TryGetValue(connectionId, out var link)) return;
        Statistics.PacketsSent++;
        Statistics.BytesSent += payload.Length;
        link.Remote.Statistics.PacketsReceived++;
        link.Remote.Statistics.BytesReceived += payload.Length;
        link.Remote._inbox.Enqueue(new TransportEvent(TransportEventKind.Data, link.RemoteId, payload.ToArray()));
    }

    public void Disconnect(int connectionId, string reason = "")
    {
        if (!_links.Remove(connectionId, out var link)) return;
        link.Remote._links.Remove(link.RemoteId);
        link.Remote._inbox.Enqueue(new TransportEvent(TransportEventKind.Disconnected, link.RemoteId, [], reason.Length > 0 ? reason : "Desconectado."));
        _inbox.Enqueue(new TransportEvent(TransportEventKind.Disconnected, connectionId, [], reason.Length > 0 ? reason : "Desconectado."));
    }

    public double? RoundTripTime(int connectionId) => _links.ContainsKey(connectionId) ? 0 : null;

    public bool TryReceive(out TransportEvent transportEvent) => _inbox.TryDequeue(out transportEvent);

    public void Stop()
    {
        foreach (var id in _links.Keys.ToArray()) Disconnect(id, "Encerrado.");
        if (IsServer) _network.Servers.Remove(LocalPort);
        IsRunning = false;
    }

    public void Dispose() => Stop();
}
