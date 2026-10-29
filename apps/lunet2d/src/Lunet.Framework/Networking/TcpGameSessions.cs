using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;

namespace Lunet.Networking;

/// <summary>Eventos que o servidor recebe. O ID é atribuído pelo servidor, nunca pelo cliente.</summary>
public enum NetworkHostEventKind { Joined, Input, Left }

/// <summary>Uma conexão ou ação recebida pela autoridade da partida.</summary>
public readonly record struct NetworkHostEvent(NetworkHostEventKind Kind, int PeerId, byte[] Payload);

/// <summary>Mensagens que um cliente recebe do servidor.</summary>
public enum NetworkClientEventKind { State }

/// <summary>Estado publicado exclusivamente pela autoridade da partida.</summary>
public readonly record struct NetworkClientEvent(NetworkClientEventKind Kind, byte[] Payload);

/// <summary>
/// Host TCP de escopo inicial para partidas LAN, turnos e co-op de baixa frequência.
/// Não implementa UDP realtime, criptografia, autenticação, lobby nem replicação automática.
/// Execute simulação/validação na thread do jogo lendo <see cref="TryReceive"/>.
/// </summary>
public sealed class TcpGameHost : IAsyncDisposable
{
    private readonly TcpListener _listener;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly ConcurrentDictionary<int, NetworkTcpPeer> _peers = new();
    private readonly ConcurrentBag<Task> _readers = new();
    private readonly Channel<NetworkHostEvent> _incoming =
        Channel.CreateBounded<NetworkHostEvent>(new BoundedChannelOptions(512)
        {
            SingleReader = false,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait
        });
    private Task _acceptTask = Task.CompletedTask;
    private int _lastPeerId;
    private int _disposed;

    private TcpGameHost(IPAddress bindAddress, int port) =>
        _listener = new TcpListener(bindAddress, port);

    /// <summary>Porta efetivamente atribuída. Permite 0 para testes com porta efêmera.</summary>
    public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

    /// <summary>Quantos peers permanecem conectados.</summary>
    public int ConnectedPeers => _peers.Count;

    /// <summary>
    /// Começa a ouvir conexões. Prefira <see cref="IPAddress.Loopback"/> em testes;
    /// usar Any abre uma porta na rede e requer configuração segura da infraestrutura.
    /// </summary>
    public static Task<TcpGameHost> StartAsync(
        IPAddress bindAddress, int port, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bindAddress);
        if (port is < 0 or > 65535) throw new ArgumentOutOfRangeException(nameof(port));
        cancellationToken.ThrowIfCancellationRequested();
        var host = new TcpGameHost(bindAddress, port);
        host._listener.Start();
        host._acceptTask = Task.Run(host.AcceptLoopAsync);
        return Task.FromResult(host);
    }

    /// <summary>Retira evento pronto sem bloquear Update/Draw do jogo.</summary>
    public bool TryReceive(out NetworkHostEvent message) => _incoming.Reader.TryRead(out message);

    /// <summary>Aguarda próximo evento, indicado para testes ou código fora do loop gráfico.</summary>
    public ValueTask<NetworkHostEvent> ReceiveAsync(CancellationToken cancellationToken = default) =>
        _incoming.Reader.ReadAsync(cancellationToken);

    /// <summary>
    /// Envia estado decidido e validado no host a todos os clientes. Nunca use payload
    /// vindo de um cliente como estado confiável sem validação do jogo.
    /// </summary>
    public async Task BroadcastStateAsync(byte[] payload, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payload);
        if (payload.Length > NetworkFrameCodec.MaxPayloadBytes)
            throw new ArgumentOutOfRangeException(nameof(payload), "Payload excede 8 KiB.");
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        foreach (var (peerId, peer) in _peers)
        {
            try
            {
                await peer.SendAsync(NetworkFrameKind.State, payload, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is IOException or SocketException or ObjectDisposedException)
            {
                RemovePeer(peerId);
            }
        }
    }

    private async Task AcceptLoopAsync()
    {
        try
        {
            while (!_lifetime.IsCancellationRequested)
            {
                var socket = await _listener.AcceptTcpClientAsync(_lifetime.Token).ConfigureAwait(false);
                socket.NoDelay = true;
                var peerId = Interlocked.Increment(ref _lastPeerId);
                var peer = new NetworkTcpPeer(socket);
                if (!_peers.TryAdd(peerId, peer))
                {
                    peer.Close();
                    continue;
                }
                _readers.Add(ProcessPeerAsync(peerId, peer));
            }
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch (ObjectDisposedException) when (_lifetime.IsCancellationRequested) { }
        catch (SocketException) when (_lifetime.IsCancellationRequested) { }
    }

    private async Task ProcessPeerAsync(int peerId, NetworkTcpPeer peer)
    {
        try
        {
            var idBytes = new byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(idBytes, peerId);
            await peer.SendAsync(NetworkFrameKind.Welcome, idBytes, _lifetime.Token).ConfigureAwait(false);
            await _incoming.Writer.WriteAsync(
                new NetworkHostEvent(NetworkHostEventKind.Joined, peerId, []), _lifetime.Token).ConfigureAwait(false);
            while (!_lifetime.IsCancellationRequested)
            {
                var frame = await NetworkFrameCodec.ReadAsync(peer.Stream, _lifetime.Token).ConfigureAwait(false);
                // O cliente envia intenção, nunca posição/snapshot autoritativo.
                if (frame.Kind != NetworkFrameKind.Input)
                    break;
                await _incoming.Writer.WriteAsync(
                    new NetworkHostEvent(NetworkHostEventKind.Input, peerId, frame.Payload),
                    _lifetime.Token).ConfigureAwait(false);
            }
        }
        catch (Exception exception) when (exception is IOException or SocketException or ObjectDisposedException or OperationCanceledException) { }
        finally
        {
            RemovePeer(peerId);
        }
    }

    private void RemovePeer(int peerId)
    {
        if (_peers.TryRemove(peerId, out var peer))
        {
            peer.Close();
            _incoming.Writer.TryWrite(new NetworkHostEvent(NetworkHostEventKind.Left, peerId, []));
        }
    }

    /// <summary>Para de ouvir e fecha conexões; não altera saves nem arquivos do jogo.</summary>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _lifetime.Cancel();
        _listener.Stop();
        foreach (var peerId in _peers.Keys) RemovePeer(peerId);
        await _acceptTask.ConfigureAwait(false);
        await Task.WhenAll(_readers.ToArray()).ConfigureAwait(false);
        _incoming.Writer.TryComplete();
        _lifetime.Dispose();
    }
}

/// <summary>
/// Cliente TCP do protocolo inicial Lunet. Chamadas de rede são assíncronas;
/// consumir <see cref="TryReceive"/> no Update mantém o frame não bloqueante.
/// </summary>
public sealed class TcpGameClient : IAsyncDisposable
{
    private readonly NetworkTcpPeer _peer;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly Channel<NetworkClientEvent> _incoming =
        Channel.CreateBounded<NetworkClientEvent>(new BoundedChannelOptions(512)
        {
            SingleReader = false,
            SingleWriter = true,
            FullMode = BoundedChannelFullMode.Wait
        });
    private readonly Task _reader;
    private int _disposed;

    private TcpGameClient(NetworkTcpPeer peer, int peerId)
    {
        _peer = peer;
        PeerId = peerId;
        _reader = Task.Run(ReadLoopAsync);
    }

    /// <summary>Identidade emitida pelo host para esta conexão.</summary>
    public int PeerId { get; }

    /// <summary>Conecta-se a IP/DNS e porta de um host já ativo.</summary>
    public static async Task<TcpGameClient> ConnectAsync(
        string host, int port, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        if (port is < 1 or > 65535) throw new ArgumentOutOfRangeException(nameof(port));
        var socket = new TcpClient { NoDelay = true };
        try
        {
            await socket.ConnectAsync(host, port, cancellationToken).ConfigureAwait(false);
            var peer = new NetworkTcpPeer(socket);
            var welcome = await NetworkFrameCodec.ReadAsync(peer.Stream, cancellationToken).ConfigureAwait(false);
            if (welcome.Kind != NetworkFrameKind.Welcome || welcome.Payload.Length != 4)
                throw new InvalidDataException("Handshake inválido.");
            var id = BinaryPrimitives.ReadInt32LittleEndian(welcome.Payload);
            if (id <= 0) throw new InvalidDataException("Identidade de conexão inválida.");
            return new TcpGameClient(peer, id);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    /// <summary>Envia intenção de ação ao host, que deverá validá-la e simular o estado.</summary>
    public ValueTask SendInputAsync(byte[] payload, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payload);
        if (payload.Length > NetworkFrameCodec.MaxPayloadBytes)
            throw new ArgumentOutOfRangeException(nameof(payload), "Payload excede 8 KiB.");
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        return _peer.SendAsync(NetworkFrameKind.Input, payload, cancellationToken);
    }

    /// <summary>Consome estado sem bloquear a thread gráfica.</summary>
    public bool TryReceive(out NetworkClientEvent message) => _incoming.Reader.TryRead(out message);

    /// <summary>Recebe estado, para testes ou execução fora da thread gráfica.</summary>
    public ValueTask<NetworkClientEvent> ReceiveAsync(CancellationToken cancellationToken = default) =>
        _incoming.Reader.ReadAsync(cancellationToken);

    private async Task ReadLoopAsync()
    {
        try
        {
            while (!_lifetime.IsCancellationRequested)
            {
                var frame = await NetworkFrameCodec.ReadAsync(_peer.Stream, _lifetime.Token).ConfigureAwait(false);
                if (frame.Kind != NetworkFrameKind.State)
                    break;
                await _incoming.Writer.WriteAsync(
                    new NetworkClientEvent(NetworkClientEventKind.State, frame.Payload),
                    _lifetime.Token).ConfigureAwait(false);
            }
        }
        catch (Exception exception) when (exception is IOException or SocketException or ObjectDisposedException or OperationCanceledException) { }
        finally { _incoming.Writer.TryComplete(); }
    }

    /// <summary>Encerra a conexão, liberando o listener de entrada.</summary>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _lifetime.Cancel();
        _peer.Close();
        await _reader.ConfigureAwait(false);
        _lifetime.Dispose();
    }
}

/// <summary>Write gate: frames concorrentes nunca podem se misturar no mesmo TCP stream.</summary>
internal sealed class NetworkTcpPeer
{
    private readonly TcpClient _client;
    private readonly SemaphoreSlim _writer = new(1, 1);
    internal NetworkTcpPeer(TcpClient client) => _client = client;
    internal NetworkStream Stream => _client.GetStream();

    internal async ValueTask SendAsync(NetworkFrameKind kind, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
    {
        await _writer.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await NetworkFrameCodec.WriteAsync(Stream, kind, payload, cancellationToken).ConfigureAwait(false);
        }
        finally { _writer.Release(); }
    }

    internal void Close() => _client.Dispose();
}
