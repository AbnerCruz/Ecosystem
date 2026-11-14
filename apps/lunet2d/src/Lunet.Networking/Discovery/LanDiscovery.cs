using System.Net;
using System.Net.Sockets;

namespace Lunet.Networking;

/// <summary>A game someone is hosting.</summary>
public sealed record LobbyInfo(string GameId, int ProtocolVersion, string Name, string Address, int Port, int Players, int MaxPlayers)
{
    public bool IsFull => Players >= MaxPlayers;
}

/// <summary>Lists games to join (SPEC §8 Lobby abstractions). LAN discovery is one implementation; online directories are others.</summary>
public interface ILobbyDirectory
{
    IReadOnlyList<LobbyInfo> Lobbies { get; }
    void Update();
}

/// <summary>Picks or creates a match (SPEC §8 Matchmaking abstractions).</summary>
public interface IMatchmaker
{
    /// <summary>A joinable lobby for the criteria, or null when the caller should host one.</summary>
    LobbyInfo? Find(string gameId, int protocolVersion);
}

/// <summary>Simplest matchmaking: the open lobby of the same game and version with the most players (fills games up).</summary>
public sealed class DirectoryMatchmaker(ILobbyDirectory directory) : IMatchmaker
{
    public LobbyInfo? Find(string gameId, int protocolVersion) =>
        directory.Lobbies.Where(l => l.GameId == gameId && l.ProtocolVersion == protocolVersion && !l.IsFull)
            .OrderByDescending(l => l.Players).ThenBy(l => l.Name, StringComparer.Ordinal).FirstOrDefault();
}

/// <summary>
/// LAN discovery: the host announces its game by UDP broadcast every second; players on the same Wi-Fi see it without
/// typing an address. Only the game id, version, a display name and the port travel; announcements expire after 4 s.
/// Some routers block broadcast (guest networks): typing the host's address still works.
/// </summary>
public sealed class LanDiscovery : ILobbyDirectory, IDisposable
{
    public const int DefaultPort = 47_777;
    private static ReadOnlySpan<byte> Magic => "LUNETLAN"u8;
    private readonly UdpClient _socket;
    private readonly int _port;
    private readonly Func<double> _now;
    private readonly Dictionary<string, (LobbyInfo Info, double Seen)> _seen = new(StringComparer.Ordinal);
    private LobbyInfo? _announce;
    private double _nextAnnounce;
    private readonly IPEndPoint _target;

    /// <param name="broadcastAddress">Where announcements go (default: the LAN broadcast address; tests use loopback).</param>
    public LanDiscovery(int port = DefaultPort, IPAddress? broadcastAddress = null, Func<double>? clock = null)
    {
        _port = port;
        _socket = new UdpClient(AddressFamily.InterNetwork) { EnableBroadcast = true };
        _socket.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        _socket.Client.Bind(new IPEndPoint(IPAddress.Any, port));
        _target = new IPEndPoint(broadcastAddress ?? IPAddress.Broadcast, port);
        var watch = System.Diagnostics.Stopwatch.StartNew();
        _now = clock ?? (() => watch.Elapsed.TotalSeconds);
    }

    public IReadOnlyList<LobbyInfo> Lobbies
    {
        get
        {
            double now = _now();
            return _seen.Values.Where(v => now - v.Seen < 4).Select(v => v.Info).OrderBy(l => l.Name, StringComparer.Ordinal).ToArray();
        }
    }

    /// <summary>Starts announcing a hosted game (<paramref name="info"/>.Address is ignored: receivers use the sender's).</summary>
    public void Announce(LobbyInfo? info) { _announce = info; _nextAnnounce = 0; }

    public static byte[] Encode(LobbyInfo info)
    {
        var writer = new NetWriter(128);
        foreach (var b in Magic) writer.Write(b);
        writer.Write((byte)1);
        writer.Write(info.GameId);
        writer.Write(info.ProtocolVersion);
        writer.Write(info.Name.Length > 32 ? info.Name[..32] : info.Name);
        writer.Write((ushort)info.Port);
        writer.Write((byte)Math.Clamp(info.Players, 0, 255));
        writer.Write((byte)Math.Clamp(info.MaxPlayers, 1, 255));
        return writer.ToArray();
    }

    public static LobbyInfo? Decode(byte[] packet, string address)
    {
        try
        {
            if (packet.Length < Magic.Length + 1 || !packet.AsSpan(0, Magic.Length).SequenceEqual(Magic)) return null;
            var reader = new NetReader(packet.AsMemory(Magic.Length));
            if (reader.ReadByte() != 1) return null;
            var info = new LobbyInfo(reader.ReadString(), reader.ReadInt32(), reader.ReadString(), address, reader.ReadUInt16(), reader.ReadByte(), reader.ReadByte());
            return info.GameId.Length is > 0 and <= 128 && info.Port > 0 ? info : null;
        }
        catch (InvalidDataException) { return null; }
    }

    public void Update()
    {
        double now = _now();
        if (_announce is { } announce && now >= _nextAnnounce)
        {
            _nextAnnounce = now + 1;
            try { _socket.Send(Encode(announce), _target); }
            catch (SocketException) { /* no network right now: keep trying */ }
        }
        while (_socket.Available > 0)
        {
            IPEndPoint? from = null;
            byte[] packet;
            try { packet = _socket.Receive(ref from); }
            catch (SocketException) { break; }
            if (from is null || Decode(packet, from.Address.ToString()) is not { } info) continue;
            _seen[$"{info.Address}:{info.Port}"] = (info, now);
        }
    }

    public void Dispose() => _socket.Dispose();
}
