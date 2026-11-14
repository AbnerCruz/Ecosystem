using System.Net;
using System.Numerics;
using Lunet.Compiler;
using Lunet.Networking;

namespace Lunet.Tests;

/// <summary>
/// LUNET-501 (Fase 7): replaceable transports, network simulator, sessions with handshake and clock, typed messages,
/// replication with interpolation, client prediction with reconciliation, LAN discovery — and the phase gate: two
/// clients play a realtime demo under simulated latency, jitter and loss.
/// </summary>
public sealed class NetworkingRealtimeTests
{
    // ---------- Serialization ----------

    [Fact]
    public void Writer_And_Reader_RoundTripEveryType()
    {
        var w = new NetWriter(16);
        w.Write((byte)7); w.Write(true); w.Write((short)-3); w.Write((ushort)65000); w.Write(-123456); w.Write(3_000_000_000u);
        w.Write(-5L); w.Write(1.5f); w.Write(2.25); w.Write(new Vector2(3, -4)); w.WriteVarUInt(300); w.WriteVarInt(-70);
        w.Write("olá, mundo"); w.Write(new byte[] { 1, 2, 3 }); w.WriteQuantized(12.34f, 0, 100);
        var r = new NetReader(w.ToArray());
        Assert.Equal(7, r.ReadByte()); Assert.True(r.ReadBool()); Assert.Equal(-3, r.ReadInt16()); Assert.Equal(65000, r.ReadUInt16());
        Assert.Equal(-123456, r.ReadInt32()); Assert.Equal(3_000_000_000u, r.ReadUInt32()); Assert.Equal(-5L, r.ReadInt64());
        Assert.Equal(1.5f, r.ReadSingle()); Assert.Equal(2.25, r.ReadDouble()); Assert.Equal(new Vector2(3, -4), r.ReadVector2());
        Assert.Equal(300u, r.ReadVarUInt()); Assert.Equal(-70, r.ReadVarInt()); Assert.Equal("olá, mundo", r.ReadString());
        Assert.Equal([1, 2, 3], r.ReadBytes()); Assert.Equal(12.34f, r.ReadQuantized(0, 100), 2);
        Assert.Equal(0, r.Remaining);
    }

    [Fact]
    public void Reader_RejectsTruncatedAndHostileData()
    {
        Assert.Throws<InvalidDataException>(() => new NetReader(new byte[] { 1, 2 }).ReadInt32());
        Assert.Throws<InvalidDataException>(() => new NetReader(new byte[] { 0xff, 0xff, 0xff, 0xff, 0xff, 0xff }).ReadVarUInt());
        Assert.Throws<InvalidDataException>(() => new NetReader(BitConverter.GetBytes(float.NaN)).ReadSingle());
        Assert.Throws<InvalidDataException>(() => new NetReader(new byte[] { 2 }).ReadBool());
        var huge = new NetWriter(); huge.WriteVarUInt(1_000_000);
        Assert.Throws<InvalidDataException>(() => new NetReader(huge.ToArray()).ReadString());
        Assert.Throws<InvalidOperationException>(() => { var w = new NetWriter(); for (int i = 0; i < 70_000; i++) w.Write((byte)1); });
    }

    // ---------- Sessions ----------

    private sealed class Chat : INetSerializable
    {
        public string Text = "";
        public void Serialize(NetWriter writer) => writer.Write(Text);
        public void Deserialize(NetReader reader) => Text = reader.ReadString();
    }

    private sealed class Move : INetSerializable
    {
        public uint Sequence; public Vector2 Direction; public float Delta;
        public void Serialize(NetWriter w) { w.WriteVarUInt(Sequence); w.Write(Direction); w.Write(Delta); }
        public void Deserialize(NetReader r) { Sequence = r.ReadVarUInt(); Direction = r.ReadVector2(); Delta = r.ReadSingle(); }
    }

    private static NetworkSessionOptions Options(string name, string game = "jogo-teste", int version = 1, int max = 8) =>
        new() { GameId = game, ProtocolVersion = version, PlayerName = name, MaxPlayers = max, TickRate = 30 };

    private static void Pump(double[] clock, double seconds, params NetworkSession[] sessions)
    {
        for (double t = 0; t < seconds; t += 1 / 120.0)
        {
            clock[0] += 1 / 120.0;
            foreach (var s in sessions) s.Update();
        }
    }

    [Fact]
    public void Handshake_AssignsIds_SharesPlayerList_AndAnnouncesJoinsAndLeaves()
    {
        var net = new LoopbackNetwork();
        double[] clock = [0];
        using var host = NetworkSession.Host(net.CreateTransport(), 0, Options("Ana"), clock: () => clock[0]);
        using var bob = NetworkSession.Join(net.CreateTransport(), "loopback", host.Port, Options("Bia"), () => clock[0]);
        Pump(clock, 0.1, host, bob);
        using var caio = NetworkSession.Join(net.CreateTransport(), "loopback", host.Port, Options("Caio"), () => clock[0]);
        var joinedOnBob = new List<string>();
        bob.PeerJoined += p => joinedOnBob.Add(p.Name);
        Pump(clock, 0.1, host, bob, caio);

        Assert.Equal(NetworkSessionState.Connected, bob.State);
        Assert.Equal(NetworkSessionState.Connected, caio.State);
        Assert.NotEqual(bob.LocalPeerId, caio.LocalPeerId);
        Assert.True(bob.LocalPeerId > 0);
        Assert.Equal(["Ana", "Bia", "Caio"], host.Peers.Select(p => p.Name).Order());
        Assert.Equal(["Ana", "Bia", "Caio"], caio.Peers.Select(p => p.Name).Order());
        Assert.Contains("Caio", joinedOnBob);

        caio.Dispose();
        Pump(clock, 0.1, host, bob);
        Assert.DoesNotContain(bob.Peers, p => p.Name == "Caio");
        Assert.DoesNotContain(host.Peers, p => p.Name == "Caio");
    }

    [Theory]
    [InlineData("outro-jogo", 1, 8, "outro jogo")]
    [InlineData("jogo-teste", 2, 8, "Versão diferente")]
    [InlineData("jogo-teste", 1, 1, "cheia")]
    public void Handshake_RejectsOtherGamesOldVersionsAndFullMatches(string game, int version, int max, string reason)
    {
        var net = new LoopbackNetwork();
        double[] clock = [0];
        using var host = NetworkSession.Host(net.CreateTransport(), 0, Options("Host", max: max) , clock: () => clock[0]);
        using var client = NetworkSession.Join(net.CreateTransport(), "loopback", host.Port, Options("X", game, version), () => clock[0]);
        Pump(clock, 0.1, host, client);
        Assert.Equal(NetworkSessionState.Disconnected, client.State);
        Assert.Contains(reason, client.DisconnectReason);
        Assert.Single(host.Peers);
    }

    [Fact]
    public void Join_TimesOutWhenNobodyAnswers()
    {
        var net = new LoopbackNetwork();
        double[] clock = [0];
        using var client = NetworkSession.Join(net.CreateTransport(), "loopback", 12345, Options("X"), () => clock[0]);
        Pump(clock, 0.1, client);
        Assert.Equal(NetworkSessionState.Disconnected, client.State);
    }

    [Fact]
    public void TypedMessages_FlowClientToServer_AndServerToEveryone_IncludingTheHostPlayer()
    {
        var net = new LoopbackNetwork();
        double[] clock = [0];
        using var host = NetworkSession.Host(net.CreateTransport(), 0, Options("Ana"), clock: () => clock[0]);
        using var bob = NetworkSession.Join(net.CreateTransport(), "loopback", host.Port, Options("Bia"), () => clock[0]);
        foreach (var s in new[] { host, bob }) s.Register<Chat>(1);
        var serverGot = new List<(int, string)>();
        var bobGot = new List<string>();
        var hostPlayerGot = new List<string>();
        host.On<Chat>((peer, chat) => serverGot.Add((peer.Id, chat.Text)));
        bob.On<Chat>((_, chat) => bobGot.Add(chat.Text));
        Pump(clock, 0.1, host, bob);

        bob.SendToServer(new Chat { Text = "oi do Bia" });
        host.SendToServer(new Chat { Text = "oi do host" }); // the host player talks to its own server locally
        Pump(clock, 0.05, host, bob);
        Assert.Contains((bob.LocalPeerId, "oi do Bia"), serverGot);
        Assert.Contains((NetworkSession.HostPeerId, "oi do host"), serverGot);

        host.On<Chat>((_, chat) => hostPlayerGot.Add(chat.Text));
        host.Broadcast(new Chat { Text = "rodada 2" });
        Pump(clock, 0.05, host, bob);
        Assert.Equal(["rodada 2"], bobGot);
        Assert.Contains("rodada 2", hostPlayerGot);
        Assert.Throws<InvalidOperationException>(() => bob.Broadcast(new Chat { Text = "x" }));
    }

    [Fact]
    public void Server_KicksClientsThatFloodOrSendGarbage()
    {
        var net = new LoopbackNetwork();
        double[] clock = [0];
        using var host = NetworkSession.Host(net.CreateTransport(), 0, Options("Ana") with { MaxMessagesPerSecond = 20 }, clock: () => clock[0]);
        using var flood = NetworkSession.Join(net.CreateTransport(), "loopback", host.Port, Options("Flood"), () => clock[0]);
        foreach (var s in new[] { host, flood }) s.Register<Chat>(1);
        Pump(clock, 0.1, host, flood);
        for (int second = 0; second < 4 && flood.State == NetworkSessionState.Connected; second++)
        {
            for (int i = 0; i < 100; i++) flood.SendToServer(new Chat { Text = "spam" });
            Pump(clock, 1.0, host, flood);
        }
        Assert.Equal(NetworkSessionState.Disconnected, flood.State);
        Assert.Single(host.Peers);

        using var evil = NetworkSession.Join(net.CreateTransport(), "loopback", host.Port, Options("Evil"), () => clock[0]);
        evil.Register<Chat>(1);
        Pump(clock, 0.1, host, evil);
        evil.Transport.Send(TransportEvent.ServerConnection, new byte[] { 8, 99, 0, 1, 2 }, DeliveryMode.ReliableOrdered);
        Pump(clock, 0.1, host, evil);
        Assert.Equal(NetworkSessionState.Disconnected, evil.State);
    }

    // ---------- Network simulator ----------

    private static (LoopbackTransport Server, LoopbackTransport Client) Pair(LoopbackNetwork net)
    {
        var server = net.CreateTransport();
        var client = net.CreateTransport();
        server.StartServer(0, 4);
        client.Connect("loopback", server.LocalPort);
        client.Poll();
        return (server, client);
    }

    private static List<byte[]> Drain(INetworkTransport t)
    {
        var list = new List<byte[]>();
        while (t.TryReceive(out var e)) if (e.Kind == TransportEventKind.Data) list.Add(e.Payload);
        return list;
    }

    [Fact]
    public void Simulator_DelaysByLatency_AndKeepsOrderedMessagesInOrderUnderJitter()
    {
        double now = 0;
        var (server, client) = Pair(new LoopbackNetwork());
        var sim = new NetworkSimulator(client, new NetworkConditions { Latency = 0.1, Jitter = 0.05 }, () => now, seed: 3);
        Drain(server);
        for (byte i = 0; i < 50; i++) sim.Send(TransportEvent.ServerConnection, [i], DeliveryMode.ReliableOrdered);
        now = 0.04; sim.Poll();
        Assert.Empty(Drain(server));
        now = 1; sim.Poll();
        var got = Drain(server).Select(p => p[0]).ToArray();
        Assert.Equal(Enumerable.Range(0, 50).Select(i => (byte)i), got);
    }

    [Fact]
    public void Simulator_LosesOnlyUnreliable_AndSequencedNeverGoesBackwards()
    {
        double now = 0;
        var (server, client) = Pair(new LoopbackNetwork());
        var sim = new NetworkSimulator(client, new NetworkConditions { Latency = 0.05, Jitter = 0.04, Loss = 0.5, Duplication = 0.2 }, () => now, seed: 7);
        Drain(server);
        for (int i = 0; i < 400; i++) sim.Send(TransportEvent.ServerConnection, BitConverter.GetBytes(i), DeliveryMode.Reliable);
        now = 2; sim.Poll();
        Assert.Equal(400, Drain(server).Select(p => BitConverter.ToInt32(p)).Distinct().Count());

        for (int i = 0; i < 400; i++) sim.Send(TransportEvent.ServerConnection, BitConverter.GetBytes(i), DeliveryMode.Unreliable);
        now = 4; sim.Poll();
        var unreliable = Drain(server).Count;
        Assert.InRange(unreliable, 120, 320); // ~50% lost, some duplicated

        for (int i = 0; i < 400; i++) { now += 0.01; sim.Send(TransportEvent.ServerConnection, BitConverter.GetBytes(i), DeliveryMode.Sequenced); sim.Poll(); }
        now += 1; sim.Poll();
        var sequenced = Drain(server).Select(p => BitConverter.ToInt32(p)).ToArray();
        Assert.NotEmpty(sequenced);
        Assert.True(sequenced.Zip(sequenced.Skip(1)).All(p => p.First < p.Second), "sequenced delivery went backwards");
    }

    [Fact]
    public void Simulator_ThrottlesBandwidth()
    {
        double now = 0;
        var (server, client) = Pair(new LoopbackNetwork());
        var sim = new NetworkSimulator(client, new NetworkConditions { Bandwidth = 10_000 }, () => now);
        Drain(server);
        for (int i = 0; i < 20; i++) sim.Send(TransportEvent.ServerConnection, new byte[972], DeliveryMode.Reliable); // 1000 B each with headers
        now = 1.0; sim.Poll();
        Assert.InRange(Drain(server).Count, 9, 11);
        now = 2.1; sim.Poll();
        Assert.InRange(Drain(server).Count, 9, 11);
        Assert.Throws<ArgumentOutOfRangeException>(() => new NetworkSimulator(client, new NetworkConditions { Loss = 2 }));
    }

    [Fact]
    public void Clock_ClientEstimatesServerTimeUnderLatency()
    {
        var net = new LoopbackNetwork();
        double[] clock = [100];
        double[] clientLocal = [5]; // the client's own clock starts somewhere else entirely
        var hostTransport = new NetworkSimulator(net.CreateTransport(), new NetworkConditions { Latency = 0.06 }, () => clock[0]);
        var clientTransport = new NetworkSimulator(net.CreateTransport(), new NetworkConditions { Latency = 0.06 }, () => clock[0]);
        using var host = NetworkSession.Host(hostTransport, 0, Options("Host"), clock: () => clock[0]);
        using var client = NetworkSession.Join(clientTransport, "loopback", host.Port, Options("C"), () => clock[0] - 95);
        Pump(clock, 3, host, client);
        Assert.True(client.Clock.Synchronized);
        Assert.Equal(host.Clock.ServerTime, client.Clock.ServerTime, 0.015);
        Assert.InRange(client.Clock.RoundTripTime, 0.1, 0.16);
    }

    // ---------- Replication, interpolation, prediction ----------

    private sealed class Avatar : NetworkObject
    {
        public readonly NetworkVariable<Vector2> Position = new();
        public readonly NetworkVariable<int> Score = new();
        public readonly NetworkVariable<uint> LastInput = new();
        public readonly NetworkVariable<string> Name = new("");
        protected override IEnumerable<NetworkVariableBase> Variables => [Position, Score, LastInput, Name];
    }

    private static void RegisterAvatar(params NetworkSession[] sessions)
    {
        foreach (var s in sessions) s.Replication.Register("avatar", () => new Avatar());
    }

    [Fact]
    public void Replication_SpawnsUpdatesAndDespawns_AndLateJoinersSeeEverything()
    {
        var net = new LoopbackNetwork();
        double[] clock = [0];
        using var host = NetworkSession.Host(net.CreateTransport(), 0, Options("Ana"), clock: () => clock[0]);
        using var bob = NetworkSession.Join(net.CreateTransport(), "loopback", host.Port, Options("Bia"), () => clock[0]);
        RegisterAvatar(host, bob);
        Pump(clock, 0.1, host, bob);
        var avatar = host.Replication.Spawn<Avatar>("avatar", owner: bob.LocalPeerId, a => { a.Position.Value = new(10, 20); a.Name.Value = "herói"; });
        Pump(clock, 0.1, host, bob);
        var seen = Assert.Single(bob.Replication.All<Avatar>());
        Assert.Equal(avatar.NetworkId, seen.NetworkId);
        Assert.True(seen.IsOwner);
        Assert.Equal("herói", seen.Name.Value);

        avatar.Position.Value = new(50, 60);
        avatar.Score.Value = 7;
        Pump(clock, 0.1, host, bob);
        Assert.Equal(new Vector2(50, 60), seen.Position.Value);
        Assert.Equal(7, seen.Score.Value);

        using var caio = NetworkSession.Join(net.CreateTransport(), "loopback", host.Port, Options("Caio"), () => clock[0]);
        RegisterAvatar(caio);
        Pump(clock, 0.1, host, bob, caio);
        Assert.Equal(new Vector2(50, 60), Assert.Single(caio.Replication.All<Avatar>()).Position.Value);

        host.Replication.Despawn(avatar);
        Pump(clock, 0.1, host, bob, caio);
        Assert.Empty(bob.Replication.Objects);
        Assert.Empty(caio.Replication.Objects);
        Assert.Throws<InvalidOperationException>(() => bob.Replication.Spawn<Avatar>("avatar"));
    }

    [Fact]
    public void Interpolation_BlendsBetweenSnapshots()
    {
        var variable = new NetworkVariable<Vector2>();
        var reader = (Vector2 v) => { var w = new NetWriter(); w.Write(v); return new NetReader(w.ToArray()); };
        variable.Read(reader(new(0, 0)), 1.0);
        variable.Read(reader(new(10, 0)), 2.0);
        Assert.Equal(new Vector2(5, 0), variable.At(1.5));
        Assert.Equal(new Vector2(0, 0), variable.At(0.5));
        Assert.Equal(new Vector2(10, 0), variable.At(3));
        variable.Read(reader(new(99, 0)), 1.5); // a late, older snapshot never rewinds the history
        Assert.Equal(new Vector2(5, 0), variable.At(1.5));
    }

    [Fact]
    public void Prediction_ReplaysUnacknowledgedInputsAfterReconcile()
    {
        static Vector2 Step(Vector2 p, Vector2 input, double dt) => p + input * (float)(100 * dt);
        var prediction = new ClientPrediction<Vector2, Vector2>(Vector2.Zero, Step);
        for (int i = 0; i < 10; i++) prediction.Apply(new Vector2(1, 0), 0.1);
        Assert.Equal(100, prediction.Current.X, 3);
        // the server processed 6 inputs but was blocked by a wall at x = 40
        prediction.Reconcile(6, new Vector2(40, 0));
        Assert.Equal(4, prediction.PendingInputs);
        Assert.Equal(80, prediction.Current.X, 3);
    }

    /// <summary>
    /// The Fase 7 gate, automated: a host and two clients play a realtime demo over a 4G-like simulated network (latency,
    /// jitter, loss, duplication). Each client predicts its own avatar and reconciles with the server; remote avatars are
    /// interpolated. After the input stops, every view agrees with the server.
    /// </summary>
    [Fact]
    public void Gate_TwoClientsPlayRealtimeDemo_UnderSimulatedLatencyJitterAndLoss()
    {
        var net = new LoopbackNetwork();
        double[] clock = [0];
        var conditions = new NetworkConditions { Latency = 0.08, Jitter = 0.03, Loss = 0.08, Duplication = 0.02 };
        INetworkTransport Sim(int seed) => new NetworkSimulator(net.CreateTransport(), conditions, () => clock[0], seed);
        using var host = NetworkSession.Host(Sim(1), 0, Options("Servidor"), dedicated: true, clock: () => clock[0]);
        using var alice = NetworkSession.Join(Sim(2), "loopback", host.Port, Options("Alice"), () => clock[0]);
        using var bob = NetworkSession.Join(Sim(3), "loopback", host.Port, Options("Bob"), () => clock[0]);
        var all = new[] { host, alice, bob };
        foreach (var s in all) { s.Register<Move>(10); s.Replication.Register("avatar", () => new Avatar()); }
        static Vector2 Step(Vector2 p, Vector2 input, double dt) =>
            Vector2.Clamp(p + Vector2.Normalize(input == Vector2.Zero ? Vector2.UnitX : input) * (input == Vector2.Zero ? 0 : (float)(120 * dt)), new(-500), new(500));

        var avatars = new Dictionary<int, Avatar>();
        host.PeerJoined += peer => avatars[peer.Id] = host.Replication.Spawn<Avatar>("avatar", peer.Id, a => a.Position.Value = new(peer.Id * 50, 0));
        host.On<Move>((peer, move) =>
        {
            if (!avatars.TryGetValue(peer.Id, out var a) || move.Sequence <= a.LastInput.Value) return;
            a.Position.Value = Step(a.Position.Value, move.Direction, Math.Clamp(move.Delta, 0, 0.1)); // server validates dt
            a.LastInput.Value = move.Sequence;
        });

        Pump(clock, 1.0, all);
        Assert.Equal(2, host.Replication.Objects.Count);
        var predictions = new Dictionary<NetworkSession, ClientPrediction<Vector2, Vector2>>();
        foreach (var c in new[] { alice, bob })
        {
            var mine = c.Replication.All<Avatar>().Single(a => a.IsOwner);
            predictions[c] = new ClientPrediction<Vector2, Vector2>(mine.Position.Value, Step);
        }

        const double dt = 1 / 60.0;
        double maxPredictionError = 0;
        for (int frame = 0; frame < 240; frame++) // 4 seconds of play
        {
            clock[0] += dt;
            foreach (var (c, prediction) in predictions)
            {
                var direction = c == alice ? new Vector2(1, frame % 120 < 60 ? 1 : -1) : new Vector2(-1, 0.5f);
                var sequence = prediction.Apply(direction, dt);
                c.SendToServer(new Move { Sequence = sequence, Direction = direction, Delta = (float)dt }, DeliveryMode.ReliableOrdered);
                var mine = c.Replication.All<Avatar>().Single(a => a.IsOwner);
                prediction.Reconcile(mine.LastInput.Value, mine.Position.Value);
            }
            foreach (var s in all) s.Update();
            foreach (var (c, prediction) in predictions)
            {
                var server = avatars[c.LocalPeerId].Position.Value;
                maxPredictionError = Math.Max(maxPredictionError, (prediction.Current - server).Length());
            }
        }
        // Predicted views run ahead of the server by about one round trip of movement (~0.25 s × 120 px/s), never wildly off.
        Assert.InRange(maxPredictionError, 0, 60);

        Pump(clock, 1.5, all); // input stops; the network settles
        foreach (var (c, prediction) in predictions)
        {
            var mine = c.Replication.All<Avatar>().Single(a => a.IsOwner);
            prediction.Reconcile(mine.LastInput.Value, mine.Position.Value);
            Assert.Equal(0, (prediction.Current - avatars[c.LocalPeerId].Position.Value).Length(), 0.01);
            Assert.Equal(240u, mine.LastInput.Value); // reliable input: nothing lost even with 8% loss
            foreach (var remote in c.Replication.All<Avatar>().Where(a => !a.IsOwner))
            {
                var truth = avatars[remote.OwnerPeerId].Position.Value;
                Assert.Equal(0, (remote.Position.Interpolated - truth).Length(), 0.01);
            }
        }
        Assert.True(alice.Clock.Synchronized && bob.Clock.Synchronized);
    }

    // ---------- Real sockets ----------

    [Fact]
    public async Task Udp_HostAndClientExchangeOverRealSockets()
    {
        using var serverTransport = new UdpTransport("jogo-teste");
        using var host = NetworkSession.Host(serverTransport, 0, Options("Host"));
        using var client = NetworkSession.Join(new UdpTransport("jogo-teste"), "127.0.0.1", host.Port, Options("Cliente"));
        foreach (var s in new[] { host, client }) s.Register<Chat>(1);
        string? received = null;
        host.On<Chat>((_, chat) => received = chat.Text);
        var deadline = DateTime.UtcNow.AddSeconds(8);
        bool sent = false;
        while (DateTime.UtcNow < deadline && received is null)
        {
            host.Update();
            client.Update();
            if (!sent && client.State == NetworkSessionState.Connected) { client.SendToServer(new Chat { Text = "via UDP" }); sent = true; }
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }
        Assert.Equal("via UDP", received);
        Assert.True(serverTransport.Statistics.BytesReceived > 0);
    }

    [Fact]
    public void Udp_RefusesAnotherGamesKey()
    {
        using var host = NetworkSession.Host(new UdpTransport("jogo-a"), 0, Options("Host", "jogo-a"));
        using var client = NetworkSession.Join(new UdpTransport("jogo-b"), "127.0.0.1", host.Port, Options("X", "jogo-b"));
        var deadline = DateTime.UtcNow.AddSeconds(8);
        while (DateTime.UtcNow < deadline && client.State == NetworkSessionState.Connecting) { host.Update(); client.Update(); Thread.Sleep(10); }
        Assert.Equal(NetworkSessionState.Disconnected, client.State);
    }

    [Fact]
    public void LanDiscovery_EncodesAndFindsHostedGames()
    {
        var info = new LobbyInfo("jogo-teste", 1, "Sala da Ana", "", 7777, 2, 4);
        var decoded = LanDiscovery.Decode(LanDiscovery.Encode(info), "192.168.0.9");
        Assert.Equal(info with { Address = "192.168.0.9" }, decoded);
        Assert.Null(LanDiscovery.Decode([1, 2, 3], "x"));

        int port = 47_000 + Random.Shared.Next(0, 900);
        using var listener = new LanDiscovery(port, IPAddress.Loopback);
        using var announcer = new LanDiscovery(port + 1000, IPAddress.Loopback);
        typeof(LanDiscovery).GetField("_target", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(announcer, new IPEndPoint(IPAddress.Loopback, port));
        announcer.Announce(info);
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline && listener.Lobbies.Count == 0) { announcer.Update(); listener.Update(); Thread.Sleep(20); }
        var found = Assert.Single(listener.Lobbies);
        Assert.Equal("Sala da Ana", found.Name);
        Assert.Equal(found, new DirectoryMatchmaker(listener).Find("jogo-teste", 1));
        Assert.Null(new DirectoryMatchmaker(listener).Find("jogo-teste", 2));
    }

    [Fact]
    public void NetworkApi_IsVisibleToGamesCompiledOnTheDevice()
    {
        var compiler = new GameCompiler(new LoadedAssembliesReferenceProvider(typeof(Lunet.Game).Assembly));
        var game = """
            using System.Collections.Generic; using System.Numerics; using Lunet; using Lunet.Networking;
            public sealed class Player : NetworkObject
            {
                public readonly NetworkVariable<Vector2> Position = new();
                protected override IEnumerable<NetworkVariableBase> Variables => [Position];
            }
            public sealed class OnlineGame : Game
            {
                private NetworkSession? _session;
                protected override void Initialize()
                {
                    _session = NetworkSession.Host(new UdpTransport("meu-jogo"), 7777, new NetworkSessionOptions { GameId = "meu-jogo" });
                    _session.Replication.Register("player", () => new Player());
                }
                protected override void Update(GameTime time) => _session?.Update();
            }
            """;
        var result = compiler.Compile("online_game2", [new SourceFile("Game.cs", game)]);
        Assert.True(result.Success, string.Join("\n", result.Diagnostics));
    }

    [Fact]
    public void MultiplayerGuide_FullExampleCompilesAsAGame()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "docs", "guides", "multiplayer.md"))) dir = dir.Parent;
        var guide = File.ReadAllText(Path.Combine(dir!.FullName, "docs", "guides", "multiplayer.md"));
        int start = guide.IndexOf("```csharp\n", StringComparison.Ordinal) + "```csharp\n".Length;
        var code = guide[start..guide.IndexOf("\n```", start, StringComparison.Ordinal)];
        var result = new GameCompiler(new LoadedAssembliesReferenceProvider(typeof(Lunet.Game).Assembly)).Compile("guide_arena", [new SourceFile("Game.cs", code)]);
        Assert.True(result.Success, string.Join("\n", result.Diagnostics));
    }
}
