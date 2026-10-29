using System.Buffers.Binary;
using System.Net;
using Lunet.Networking;

namespace Lunet.Tests;

/// <summary>Primeiro gate de F7: transporte real, IDs do host, framing e proteção de memória.</summary>
public sealed class NetworkingFoundationTests
{
    [Fact]
    public async Task TcpHost_TwoClients_ReceivesInputsWithServerAssignedIdentity_AndBroadcastsState()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        await using var host = await TcpGameHost.StartAsync(IPAddress.Loopback, 0, timeout.Token);
        await using var alice = await TcpGameClient.ConnectAsync("127.0.0.1", host.Port, timeout.Token);
        await using var bob = await TcpGameClient.ConnectAsync("127.0.0.1", host.Port, timeout.Token);

        Assert.NotEqual(alice.PeerId, bob.PeerId);
        Assert.True(alice.PeerId > 0);
        Assert.True(bob.PeerId > 0);

        var firstJoin = await host.ReceiveAsync(timeout.Token);
        var secondJoin = await host.ReceiveAsync(timeout.Token);
        Assert.Equal(NetworkHostEventKind.Joined, firstJoin.Kind);
        Assert.Equal(NetworkHostEventKind.Joined, secondJoin.Kind);
        Assert.Contains(alice.PeerId, new[] { firstJoin.PeerId, secondJoin.PeerId });
        Assert.Contains(bob.PeerId, new[] { firstJoin.PeerId, secondJoin.PeerId });

        await alice.SendInputAsync(new byte[] { 3, 9 }, timeout.Token);
        await bob.SendInputAsync(new byte[] { 7 }, timeout.Token);
        var inputs = new[] { await host.ReceiveAsync(timeout.Token), await host.ReceiveAsync(timeout.Token) };
        Assert.All(inputs, msg => Assert.Equal(NetworkHostEventKind.Input, msg.Kind));
        Assert.Contains(inputs, msg => msg.PeerId == alice.PeerId && msg.Payload.SequenceEqual(new byte[] { 3, 9 }));
        Assert.Contains(inputs, msg => msg.PeerId == bob.PeerId && msg.Payload.SequenceEqual(new byte[] { 7 }));

        await host.BroadcastStateAsync(new byte[] { 42, 43 }, timeout.Token);
        var stateAlice = await alice.ReceiveAsync(timeout.Token);
        var stateBob = await bob.ReceiveAsync(timeout.Token);
        Assert.Equal(NetworkClientEventKind.State, stateAlice.Kind);
        Assert.Equal(NetworkClientEventKind.State, stateBob.Kind);
        Assert.Equal(new byte[] { 42, 43 }, stateAlice.Payload);
        Assert.Equal(new byte[] { 42, 43 }, stateBob.Payload);
    }

    [Fact]
    public async Task FrameCodec_RoundTripsAndRejectsOversizedFramesBeforeAllocation()
    {
        using var stream = new MemoryStream();
        await NetworkFrameCodec.WriteAsync(stream, NetworkFrameKind.Input, new byte[] { 1, 2, 3 }, CancellationToken.None);
        stream.Position = 0;
        var frame = await NetworkFrameCodec.ReadAsync(stream, CancellationToken.None);
        Assert.Equal(NetworkFrameKind.Input, frame.Kind);
        Assert.Equal(new byte[] { 1, 2, 3 }, frame.Payload);

        var header = new byte[5];
        header[0] = (byte)NetworkFrameKind.Input;
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(1), NetworkFrameCodec.MaxPayloadBytes + 1);
        using var invalid = new MemoryStream(header);
        await Assert.ThrowsAsync<InvalidDataException>(async () => await NetworkFrameCodec.ReadAsync(invalid, CancellationToken.None));
    }

    [Fact]
    public async Task FrameCodec_RejectsTruncatedFrames()
    {
        using var truncated = new MemoryStream(new byte[] { (byte)NetworkFrameKind.Input, 4, 0, 0, 0, 1 });
        await Assert.ThrowsAsync<EndOfStreamException>(async () => await NetworkFrameCodec.ReadAsync(truncated, CancellationToken.None));
    }

    [Fact]
    public async Task HostRejectsClientStateForgery_AndDisconnectsOffendingPeer()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        await using var host = await TcpGameHost.StartAsync(IPAddress.Loopback, 0, timeout.Token);
        using var malicious = new System.Net.Sockets.TcpClient();
        await malicious.ConnectAsync(IPAddress.Loopback, host.Port, timeout.Token);
        var stream = malicious.GetStream();
        var welcome = await NetworkFrameCodec.ReadAsync(stream, timeout.Token);
        Assert.Equal(NetworkFrameKind.Welcome, welcome.Kind);
        var id = BinaryPrimitives.ReadInt32LittleEndian(welcome.Payload);
        Assert.Equal(NetworkHostEventKind.Joined, (await host.ReceiveAsync(timeout.Token)).Kind);

        await NetworkFrameCodec.WriteAsync(stream, NetworkFrameKind.State, new byte[] { 99 }, timeout.Token);
        var left = await host.ReceiveAsync(timeout.Token);
        Assert.Equal(NetworkHostEventKind.Left, left.Kind);
        Assert.Equal(id, left.PeerId);
    }

    [Fact]
    public async Task InputSizeIsBoundedBeforeNetworkWrite()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        await using var host = await TcpGameHost.StartAsync(IPAddress.Loopback, 0, timeout.Token);
        await using var client = await TcpGameClient.ConnectAsync("127.0.0.1", host.Port, timeout.Token);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.SendInputAsync(new byte[NetworkFrameCodec.MaxPayloadBytes + 1], timeout.Token).AsTask());
    }
}
