namespace Lunet.Networking;

/// <summary>How a message travels. Realtime state uses <see cref="Unreliable"/>/<see cref="Sequenced"/>; events and chat use <see cref="ReliableOrdered"/>.</summary>
public enum DeliveryMode
{
    /// <summary>May be lost, duplicated-free, any order. Cheapest: positions that are resent every tick.</summary>
    Unreliable,
    /// <summary>May be lost; older packets arriving after newer ones are dropped.</summary>
    Sequenced,
    /// <summary>Always arrives, any order.</summary>
    Reliable,
    /// <summary>Always arrives, in order. Default for game events.</summary>
    ReliableOrdered,
}

public enum TransportEventKind { Connected, Data, Disconnected }

/// <summary>Something that happened on the transport. On a client, the server is connection <see cref="ServerConnection"/>.</summary>
public readonly record struct TransportEvent(TransportEventKind Kind, int ConnectionId, byte[] Payload, string? Reason = null)
{
    public const int ServerConnection = 0;
}

/// <summary>Counters for diagnostics overlays and tests.</summary>
public sealed class TransportStatistics
{
    public long PacketsSent, PacketsReceived, BytesSent, BytesReceived, PacketsDropped;
    public void Reset() => PacketsSent = PacketsReceived = BytesSent = BytesReceived = PacketsDropped = 0;
}

/// <summary>
/// The replaceable transport under every Lunet session (SPEC §8, ADR 0011): UDP (LiteNetLib inside, no types exposed),
/// in-memory loopback for same-device play and tests, or any other implementation. Not thread-safe: call it from the
/// game thread; <see cref="Poll"/> once per frame turns network activity into events.
/// </summary>
public interface INetworkTransport : IDisposable
{
    bool IsServer { get; }
    bool IsRunning { get; }
    /// <summary>Largest payload accepted by <see cref="Send"/>.</summary>
    int MaxMessageSize { get; }
    TransportStatistics Statistics { get; }

    /// <summary>Listens for clients. <paramref name="port"/> 0 picks a free port (see <see cref="LocalPort"/>).</summary>
    void StartServer(int port, int maxConnections);
    void Connect(string address, int port);
    int LocalPort { get; }

    void Send(int connectionId, ReadOnlySpan<byte> payload, DeliveryMode mode);
    void Disconnect(int connectionId, string reason = "");
    /// <summary>Round-trip time to a connection in seconds, when the transport measures it (else null).</summary>
    double? RoundTripTime(int connectionId);

    void Poll();
    bool TryReceive(out TransportEvent transportEvent);
    void Stop();
}
