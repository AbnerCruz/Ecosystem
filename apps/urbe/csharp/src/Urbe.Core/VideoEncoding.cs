using System.Buffers.Binary;
using System.Text;

namespace Urbe.Core;

/// <param name="AudioWavPath">A 16-bit PCM WAV with the whole soundtrack (already mixed), or null for a silent video.</param>
public sealed record VideoEncodeSettings(int Width, int Height, int Fps, int FrameCount, string? AudioWavPath = null);

/// <summary>
/// CST-209: a platform's H.264 MP4 encoder (Android: MediaCodec + MediaMuxer; desktop: an ffmpeg found on the
/// computer). Frames arrive in order as opaque RGBA8888 rows of Width×4 bytes. Finishing means the file at the output
/// path is complete; the caller still verifies it before telling the person it exists.
/// </summary>
public interface IVideoEncoder : IAsyncDisposable
{
    string Name { get; }
    Task StartAsync(VideoEncodeSettings settings, string outputPath, CancellationToken cancel);
    Task WriteFrameAsync(ReadOnlyMemory<byte> rgba, CancellationToken cancel);
    Task FinishAsync(CancellationToken cancel);
}

/// <summary>PCM audio for the encoders and the mixer: 16-bit little-endian WAV files read and written as streams.</summary>
public static class VideoAudio
{
    public const int Rate = 44100, Channels = 2;

    public sealed record WavInfo(int Channels, int SampleRate, long DataOffset, long DataBytes)
    {
        public long Frames => DataBytes / (2 * Channels);
        public double Seconds => Frames / (double)SampleRate;
    }

    /// <summary>The format of a PCM16 WAV, or null when the stream is not one.</summary>
    public static WavInfo? ReadWavInfo(Stream stream)
    {
        var head = new byte[12];
        stream.Position = 0;
        if (stream.ReadAtLeast(head, 12, false) < 12 || Encoding.ASCII.GetString(head, 0, 4) != "RIFF" || Encoding.ASCII.GetString(head, 8, 4) != "WAVE") return null;
        int channels = 0, rate = 0, bits = 0, format = 0;
        var chunk = new byte[8];
        while (stream.ReadAtLeast(chunk, 8, false) == 8)
        {
            var id = Encoding.ASCII.GetString(chunk, 0, 4);
            long size = BinaryPrimitives.ReadUInt32LittleEndian(chunk.AsSpan(4));
            if (id == "fmt ")
            {
                var fmt = new byte[Math.Max(16, size)];
                if (stream.ReadAtLeast(fmt, (int)size, false) < 16) return null;
                format = BinaryPrimitives.ReadUInt16LittleEndian(fmt);
                channels = BinaryPrimitives.ReadUInt16LittleEndian(fmt.AsSpan(2));
                rate = BinaryPrimitives.ReadInt32LittleEndian(fmt.AsSpan(4));
                bits = BinaryPrimitives.ReadUInt16LittleEndian(fmt.AsSpan(14));
                if (size % 2 == 1) stream.ReadByte();
            }
            else if (id == "data")
            {
                if (format is not (1 or 0xFFFE) || bits != 16 || channels is < 1 or > 8 || rate is < 1000 or > 384000) return null;
                long available = stream.Length - stream.Position;
                // a streamed WAV may say 0 or 0xFFFFFFFF: the rest of the file is the data
                if (size == 0 || size > available) size = available;
                return new WavInfo(channels, rate, stream.Position, size - size % (2 * channels));
            }
            else stream.Seek(size + size % 2, SeekOrigin.Current);
        }
        return null;
    }

    public static void WriteWavHeader(Stream stream, int channels, int rate, long dataBytes)
    {
        var h = new byte[44];
        Encoding.ASCII.GetBytes("RIFF").CopyTo(h, 0);
        BinaryPrimitives.WriteUInt32LittleEndian(h.AsSpan(4), (uint)Math.Min(uint.MaxValue, 36 + dataBytes));
        Encoding.ASCII.GetBytes("WAVEfmt ").CopyTo(h, 8);
        BinaryPrimitives.WriteInt32LittleEndian(h.AsSpan(16), 16);
        BinaryPrimitives.WriteInt16LittleEndian(h.AsSpan(20), 1);
        BinaryPrimitives.WriteInt16LittleEndian(h.AsSpan(22), (short)channels);
        BinaryPrimitives.WriteInt32LittleEndian(h.AsSpan(24), rate);
        BinaryPrimitives.WriteInt32LittleEndian(h.AsSpan(28), rate * channels * 2);
        BinaryPrimitives.WriteInt16LittleEndian(h.AsSpan(32), (short)(channels * 2));
        BinaryPrimitives.WriteInt16LittleEndian(h.AsSpan(34), 16);
        Encoding.ASCII.GetBytes("data").CopyTo(h, 36);
        BinaryPrimitives.WriteUInt32LittleEndian(h.AsSpan(40), (uint)Math.Min(uint.MaxValue, dataBytes));
        stream.Write(h);
    }

    /// <summary>
    /// The soundtrack: every clip (a PCM16 WAV of any rate and channels) placed at its start, trimmed, scaled by its
    /// volume and summed into a 44.1 kHz stereo WAV as long as the video. Streamed a second at a time; a sum that
    /// passes full scale is limited, never wrapped around.
    /// </summary>
    public static void Mix(IReadOnlyList<(VideoAudioClip Clip, Stream Wav)> clips, double duration, Stream output, CancellationToken cancel = default)
    {
        long total = (long)Math.Ceiling(Math.Max(0, duration) * Rate);
        var inputs = new List<(VideoAudioClip Clip, Stream Wav, WavInfo Info)>();
        foreach (var (clip, wav) in clips)
            if (ReadWavInfo(wav) is { } info) inputs.Add((clip, wav, info));
            else throw new InvalidDataException("Áudio não é WAV PCM 16 bits: " + clip.Path);
        WriteWavHeader(output, Channels, Rate, total * 4);
        const int block = Rate;
        var mix = new float[block * Channels];
        var bytes = new byte[block * Channels * 2];
        for (long at = 0; at < total; at += block)
        {
            cancel.ThrowIfCancellationRequested();
            int n = (int)Math.Min(block, total - at);
            Array.Clear(mix);
            foreach (var (clip, wav, info) in inputs)
                AddClip(clip, wav, info, at, n, mix);
            for (int i = 0; i < n * Channels; i++)
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(i * 2), (short)Math.Clamp(Math.Round(mix[i] * 32767), -32768, 32767));
            output.Write(bytes, 0, n * Channels * 2);
        }
        output.Flush();
    }

    private static void AddClip(VideoAudioClip clip, Stream wav, WavInfo info, long at, int n, float[] mix)
    {
        double ratio = info.SampleRate / (double)Rate;
        long clipStart = (long)Math.Round(clip.Start * Rate);
        long clipEnd = clip.Length is { } len ? clipStart + (long)Math.Round(len * Rate) : long.MaxValue;
        long from = Math.Max(at, clipStart), to = Math.Min(at + n, clipEnd);
        if (to <= from) return;
        double trim = clip.TrimStart * info.SampleRate;
        double s0 = trim + (from - clipStart) * ratio, s1 = trim + (to - clipStart) * ratio;
        long first = (long)Math.Floor(s0), last = Math.Min(info.Frames - 1, (long)Math.Ceiling(s1) + 1);
        if (first >= info.Frames || last < first) return;
        int count = (int)(last - first + 1);
        var raw = new byte[count * info.Channels * 2];
        wav.Position = info.DataOffset + first * info.Channels * 2;
        int got = wav.ReadAtLeast(raw, raw.Length, false) / (info.Channels * 2);
        float Sample(long frame, int channel)
        {
            long k = frame - first;
            if (k < 0 || k >= got) return 0;
            int c = info.Channels == 1 ? 0 : Math.Min(channel, info.Channels - 1);
            return BinaryPrimitives.ReadInt16LittleEndian(raw.AsSpan((int)(k * info.Channels + c) * 2)) / 32768f;
        }
        var volume = (float)clip.Volume;
        for (long o = from; o < to; o++)
        {
            double s = trim + (o - clipStart) * ratio;
            long i0 = (long)Math.Floor(s);
            float f = (float)(s - i0);
            int index = (int)(o - at) * Channels;
            for (int c = 0; c < Channels; c++)
                mix[index + c] += volume * (Sample(i0, c) * (1 - f) + Sample(i0 + 1, c) * f);
        }
    }
}

/// <summary>A track of an MP4 as its boxes declare it.</summary>
public sealed record Mp4Track(string Handler, string Codec, int Width, int Height, long SampleCount, double Seconds);

public sealed record Mp4Info(double Seconds, IReadOnlyList<Mp4Track> Tracks)
{
    public Mp4Track? Video => Tracks.FirstOrDefault(t => t.Handler == "vide");
    public Mp4Track? Audio => Tracks.FirstOrDefault(t => t.Handler == "soun");
}

/// <summary>
/// Reads the structure of an MP4 (ISO BMFF: ftyp, moov/trak/mdia, stsd, stsz) without decoding it: the check that
/// an export really produced a complete H.264 file with the frames and sound asked for, on every platform.
/// </summary>
public static class Mp4Inspection
{
    public static Mp4Info? Read(string path)
    {
        try { using var s = File.OpenRead(path); return Read(s); }
        catch (IOException) { return null; }
    }

    public static Mp4Info? Read(Stream stream)
    {
        try
        {
            bool ftyp = false, mdat = false;
            Mp4Info? info = null;
            foreach (var (type, start, size) in Boxes(stream, 0, stream.Length))
            {
                if (type == "ftyp") ftyp = true;
                else if (type == "mdat" && size > 8) mdat = true;
                else if (type == "moov") info = Moov(stream, start, size);
            }
            return ftyp && mdat ? info : null;
        }
        catch (Exception e) when (e is EndOfStreamException or IOException or ArgumentException or OverflowException) { return null; }
    }

    /// <summary>Null when the file is what was asked for; otherwise why it is not.</summary>
    public static string? Problem(Mp4Info? info, VideoEncodeSettings expected)
    {
        if (info is null) return "o arquivo não é um MP4 completo";
        if (info.Video is not { } v) return "o MP4 não tem trilha de vídeo";
        if (v.Codec is not ("avc1" or "avc3")) return "o vídeo não está em H.264 (" + v.Codec + ")";
        if (v.Width != expected.Width || v.Height != expected.Height) return "tamanho " + v.Width + "×" + v.Height + " em vez de " + expected.Width + "×" + expected.Height;
        if (v.SampleCount != expected.FrameCount) return v.SampleCount + " quadros em vez de " + expected.FrameCount;
        if (expected.AudioWavPath is not null && info.Audio is null) return "o MP4 saiu sem o áudio";
        return null;
    }

    private static IEnumerable<(string Type, long Start, long Size)> Boxes(Stream s, long from, long end)
    {
        long at = from;
        var head = new byte[16];
        while (at + 8 <= end)
        {
            s.Position = at;
            s.ReadExactly(head, 0, 8);
            long size = BinaryPrimitives.ReadUInt32BigEndian(head);
            var type = Encoding.ASCII.GetString(head, 4, 4);
            int header = 8;
            if (size == 1) { s.ReadExactly(head, 8, 8); size = (long)BinaryPrimitives.ReadUInt64BigEndian(head.AsSpan(8)); header = 16; }
            else if (size == 0) size = end - at;
            if (size < header || at + size > end) yield break;
            yield return (type, at + header, size - header);
            at += size;
        }
    }

    private static byte[] Bytes(Stream s, long start, long size)
    {
        var b = new byte[Math.Min(size, 1 << 20)];
        s.Position = start;
        s.ReadExactly(b);
        return b;
    }

    private static Mp4Info Moov(Stream s, long start, long size)
    {
        double seconds = 0;
        var tracks = new List<Mp4Track>();
        foreach (var (type, at, len) in Boxes(s, start, start + size))
        {
            if (type == "mvhd") seconds = HeaderSeconds(Bytes(s, at, len));
            else if (type == "trak") tracks.Add(Trak(s, at, len));
        }
        return new Mp4Info(seconds, tracks);
    }

    private static double HeaderSeconds(byte[] b)
    {
        // mvhd/mdhd: version, then (v1) 8+8 or (v0) 4+4 bytes of dates, then timescale and duration
        bool v1 = b[0] == 1;
        int o = v1 ? 20 : 12;
        uint scale = BinaryPrimitives.ReadUInt32BigEndian(b.AsSpan(o));
        double duration = v1 ? BinaryPrimitives.ReadUInt64BigEndian(b.AsSpan(o + 4)) : BinaryPrimitives.ReadUInt32BigEndian(b.AsSpan(o + 4));
        return scale == 0 ? 0 : duration / scale;
    }

    private static Mp4Track Trak(Stream s, long start, long size)
    {
        string handler = "", codec = "";
        int width = 0, height = 0;
        long samples = 0;
        double seconds = 0;
        void Walk(long from, long length)
        {
            foreach (var (type, at, len) in Boxes(s, from, from + length))
            {
                switch (type)
                {
                    case "mdia" or "minf" or "stbl": Walk(at, len); break;
                    case "mdhd": seconds = HeaderSeconds(Bytes(s, at, len)); break;
                    case "hdlr": handler = Encoding.ASCII.GetString(Bytes(s, at, len), 8, 4); break;
                    case "stsd":
                        var d = Bytes(s, at, len);
                        if (BinaryPrimitives.ReadUInt32BigEndian(d.AsSpan(4)) > 0)
                        {
                            codec = Encoding.ASCII.GetString(d, 12, 4);
                            // a visual sample entry: 8 bytes of box, 6 reserved, 2 index, 16 pre-defined, then width and height
                            if (handler == "vide" && d.Length >= 8 + 8 + 28)
                            {
                                width = BinaryPrimitives.ReadUInt16BigEndian(d.AsSpan(8 + 32));
                                height = BinaryPrimitives.ReadUInt16BigEndian(d.AsSpan(8 + 34));
                            }
                        }
                        break;
                    case "stsz":
                        var z = Bytes(s, at, Math.Min(len, 12));
                        samples = BinaryPrimitives.ReadUInt32BigEndian(z.AsSpan(8));
                        break;
                }
            }
        }
        Walk(start, size);
        return new Mp4Track(handler, codec, width, height, samples, seconds);
    }
}
