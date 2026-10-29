using System.Buffers.Binary;

namespace Lunet.Networking;

/// <summary>Tipos internos do protocolo v1. Nunca desserialize objetos arbitrários da rede.</summary>
internal enum NetworkFrameKind : byte
{
    Welcome = 1,
    Input = 2,
    State = 3
}

internal readonly record struct NetworkFrame(NetworkFrameKind Kind, byte[] Payload);

/// <summary>Enquadramento binário de comprimento limitado. TCP é um fluxo, não uma fila de mensagens.</summary>
internal static class NetworkFrameCodec
{
    internal const int MaxPayloadBytes = 8 * 1024;
    private const int HeaderBytes = 5;

    internal static async ValueTask WriteAsync(
        Stream stream, NetworkFrameKind kind, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (payload.Length > MaxPayloadBytes)
            throw new ArgumentOutOfRangeException(nameof(payload), "Payload excede 8 KiB.");
        if (!Enum.IsDefined(kind))
            throw new InvalidDataException("Tipo de mensagem desconhecido.");

        var data = new byte[HeaderBytes + payload.Length];
        data[0] = (byte)kind;
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(1, 4), payload.Length);
        payload.CopyTo(data.AsMemory(HeaderBytes));
        await stream.WriteAsync(data, cancellationToken).ConfigureAwait(false);
    }

    internal static async ValueTask<NetworkFrame> ReadAsync(Stream stream, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var header = new byte[HeaderBytes];
        await stream.ReadExactlyAsync(header, cancellationToken).ConfigureAwait(false);
        var kind = (NetworkFrameKind)header[0];
        var length = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(1, 4));
        if (!Enum.IsDefined(kind))
            throw new InvalidDataException("Tipo de mensagem desconhecido.");
        if (length < 0 || length > MaxPayloadBytes)
            throw new InvalidDataException("Comprimento de mensagem inválido.");
        var payload = new byte[length];
        await stream.ReadExactlyAsync(payload, cancellationToken).ConfigureAwait(false);
        return new NetworkFrame(kind, payload);
    }
}
