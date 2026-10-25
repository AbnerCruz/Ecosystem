using System.Buffers.Binary;

namespace Lunet.Android.Audio;

// Host-internal streaming reader. No framework API or project-format change.
internal sealed class PcmWaveStream : IDisposable
{
    private readonly Stream _source;
    private readonly long _start, _length;
    private long _remaining;
    public int SampleRate { get; }
    public int Channels { get; }
    public int BitsPerSample { get; }
    public int FrameBytes => Channels * BitsPerSample / 8;

    private PcmWaveStream(Stream source, long start, long length, int rate, int channels, int bits)
    {
        _source = source;
        _start = start;
        _length = _remaining = length;
        SampleRate = rate;
        Channels = channels;
        BitsPerSample = bits;
        source.Position = start;
    }

    // Ownership transfers only on success. Unsupported codecs stay on MediaPlayer.
    public static PcmWaveStream? TryRead(Stream source)
    {
        if (!source.CanSeek || !source.CanRead) throw new ArgumentException("WAV requer stream legível e seekable.", nameof(source));
        source.Position = 0;
        if (source.Length < 12) return null;
        Span<byte> header = stackalloc byte[16];
        source.ReadExactly(header[..12]);
        if (!header[..4].SequenceEqual("RIFF"u8) || !header[8..12].SequenceEqual("WAVE"u8)) return null;
        long end = 8L + BinaryPrimitives.ReadUInt32LittleEndian(header[4..8]);
        if (end > source.Length || end < 12) throw new InvalidDataException("WAV truncado: tamanho RIFF inválido.");
        int rate = 0, channels = 0, bits = 0, alignment = 0;
        uint byteRate = 0;
        long dataStart = -1, dataSize = 0;
        bool hasFormat = false, supported = false;
        for (int chunk = 0; source.Position < end; chunk++)
        {
            if (chunk >= 4096 || end - source.Position < 8) throw new InvalidDataException("Cabeçalho WAV inválido.");
            source.ReadExactly(header[..8]);
            uint size = BinaryPrimitives.ReadUInt32LittleEndian(header[4..8]);
            long start = source.Position, next = start + size + (size & 1);
            if (next > end) throw new InvalidDataException("Chunk WAV truncado.");
            if (header[..4].SequenceEqual("fmt "u8))
            {
                if (hasFormat || size < 16) throw new InvalidDataException("Formato WAV inválido.");
                hasFormat = true;
                source.ReadExactly(header);
                supported = BinaryPrimitives.ReadUInt16LittleEndian(header) == 1;
                channels = BinaryPrimitives.ReadUInt16LittleEndian(header[2..]);
                uint sampleRate = BinaryPrimitives.ReadUInt32LittleEndian(header[4..]);
                rate = sampleRate <= int.MaxValue ? (int)sampleRate : 0;
                byteRate = BinaryPrimitives.ReadUInt32LittleEndian(header[8..]);
                alignment = BinaryPrimitives.ReadUInt16LittleEndian(header[12..]);
                bits = BinaryPrimitives.ReadUInt16LittleEndian(header[14..]);
            }
            else if (header[..4].SequenceEqual("data"u8))
            {
                if (dataStart >= 0) throw new InvalidDataException("WAV contém múltiplos chunks data.");
                dataStart = start;
                dataSize = size;
            }
            source.Position = next;
        }
        if (!hasFormat || dataStart < 0) throw new InvalidDataException("WAV sem formato ou áudio.");
        if (!supported || channels is not (1 or 2) || bits is not (8 or 16) || rate is < 4000 or > 192000) return null;
        int frameBytes = channels * bits / 8;
        if (alignment != frameBytes || byteRate != (long)rate * frameBytes || dataSize == 0 || dataSize % frameBytes != 0)
            throw new InvalidDataException("Alinhamento/tamanho PCM inválido.");
        return new PcmWaveStream(source, dataStart, dataSize, rate, channels, bits);
    }

    public int Read(byte[] buffer, bool loop)
    {
        if (buffer.Length < FrameBytes) throw new ArgumentException("Buffer menor que um frame PCM.", nameof(buffer));
        if (_remaining == 0)
        {
            if (!loop) return 0;
            _source.Position = _start;
            _remaining = _length;
        }
        int count = (int)Math.Min(_remaining, buffer.Length - buffer.Length % FrameBytes);
        _source.ReadExactly(buffer.AsSpan(0, count));
        _remaining -= count;
        return count;
    }

    public void Dispose() => _source.Dispose();
}
