using System.Runtime.InteropServices;
using Android.Media;
using Urbe.Client;
using Urbe.Core;

namespace Urbe.AndroidHost;

/// <summary>
/// CST-209 on Android: H.264 (MediaCodec, YUV 4:2:0 written into the codec's input Image by its real strides, so
/// planar and semi-planar devices both work) and AAC from the mixed WAV, muxed by MediaMuxer into an MP4. Each frame
/// gets its exact presentation time (index / fps). No WebView, no JavaScript. Needs DEVICE validation.
/// </summary>
internal sealed class AndroidVideoEncoder : IVideoEncoder
{
    private const int ColorFormatYuv420Flexible = 0x7F420888;
    private const long Timeout = 10_000;
    private MediaCodec? _video;
    private MediaMuxer? _muxer;
    private VideoEncodeSettings? _settings;
    private int _videoTrack = -1, _audioTrack = -1;
    private bool _started;
    private long _frame;
    private MediaFormat? _audioFormat;
    private readonly List<(byte[] Data, long Pts, MediaCodecBufferFlags Flags)> _audioSamples = [];
    private byte[] _y = [], _u = [], _v = [];

    public string Name => "MediaCodec";

    public static void Install()
    {
        PlatformVideo.CreateEncoder = () => new AndroidVideoEncoder();
        PlatformVideo.DecodeAudio = (source, target, cancel) => Task.Run(() => DecodePcm(source, target, cancel), cancel);
    }

    public Task StartAsync(VideoEncodeSettings settings, string outputPath, CancellationToken cancel) => Task.Run(() =>
    {
        _settings = settings;
        if (settings.AudioWavPath is { } wav) EncodeAudio(wav, cancel);
        var format = MediaFormat.CreateVideoFormat("video/avc", settings.Width, settings.Height);
        format.SetInteger(MediaFormat.KeyColorFormat, ColorFormatYuv420Flexible);
        format.SetInteger(MediaFormat.KeyBitRate, Math.Clamp(settings.Width * settings.Height * settings.Fps / 8, 1_000_000, 20_000_000));
        format.SetInteger(MediaFormat.KeyFrameRate, settings.Fps);
        format.SetInteger(MediaFormat.KeyIFrameInterval, 1);
        _video = MediaCodec.CreateEncoderByType("video/avc");
        var caps = _video.CodecInfo?.GetCapabilitiesForType("video/avc");
        if (caps?.ColorFormats is not { } formats || !formats.Contains(ColorFormatYuv420Flexible))
            throw new NotSupportedException("O codificador H.264 deste aparelho não aceita quadros YUV 4:2:0.");
        _video.Configure(format, null, null, MediaCodecConfigFlags.Encode);
        _video.Start();
        _muxer = new MediaMuxer(outputPath, MuxerOutputType.Mpeg4);
        int w = settings.Width, h = settings.Height;
        _y = new byte[w * h];
        _u = new byte[w / 2 * (h / 2)];
        _v = new byte[_u.Length];
    }, cancel);

    public Task WriteFrameAsync(ReadOnlyMemory<byte> rgba, CancellationToken cancel)
    {
        if (_video is null || _settings is null) throw new InvalidOperationException("O codificador não começou.");
        ToYuv(rgba.Span, _settings.Width, _settings.Height);
        int index;
        while ((index = _video.DequeueInputBuffer(Timeout)) < 0)
        {
            cancel.ThrowIfCancellationRequested();
            Drain(false);
        }
        using (var image = _video.GetInputImage(index) ?? throw new InvalidOperationException("O codificador não deu o quadro de entrada."))
            WriteImage(image, _settings.Width, _settings.Height);
        long pts = _frame * 1_000_000L / _settings.Fps;
        _video.QueueInputBuffer(index, 0, _settings.Width * _settings.Height * 3 / 2, pts, MediaCodecBufferFlags.None);
        _frame++;
        Drain(false);
        return Task.CompletedTask;
    }

    public Task FinishAsync(CancellationToken cancel) => Task.Run(() =>
    {
        if (_video is null || _settings is null || _muxer is null) throw new InvalidOperationException("O codificador não começou.");
        int index;
        while ((index = _video.DequeueInputBuffer(Timeout)) < 0) { cancel.ThrowIfCancellationRequested(); Drain(false); }
        _video.QueueInputBuffer(index, 0, 0, _frame * 1_000_000L / _settings.Fps, MediaCodecBufferFlags.EndOfStream);
        Drain(true, cancel);
        if (!_started) throw new IOException("O codificador não produziu vídeo.");
        _muxer.Stop();
    }, cancel);

    /// <summary>RGBA → BT.601 limited-range Y, and U/V averaged over each 2×2 block.</summary>
    private void ToYuv(ReadOnlySpan<byte> rgba, int w, int h)
    {
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int p = (y * w + x) * 4;
                int r = rgba[p], g = rgba[p + 1], b = rgba[p + 2];
                _y[y * w + x] = (byte)(((66 * r + 129 * g + 25 * b + 128) >> 8) + 16);
            }
        int cw = w / 2;
        for (int y = 0; y < h / 2; y++)
            for (int x = 0; x < cw; x++)
            {
                int r = 0, g = 0, b = 0;
                for (int dy = 0; dy < 2; dy++)
                    for (int dx = 0; dx < 2; dx++)
                    {
                        int p = ((y * 2 + dy) * w + x * 2 + dx) * 4;
                        r += rgba[p]; g += rgba[p + 1]; b += rgba[p + 2];
                    }
                r /= 4; g /= 4; b /= 4;
                _u[y * cw + x] = (byte)(((-38 * r - 74 * g + 112 * b + 128) >> 8) + 128);
                _v[y * cw + x] = (byte)(((112 * r - 94 * g - 18 * b + 128) >> 8) + 128);
            }
    }

    /// <summary>Each plane written at its direct-buffer address by its own row and pixel strides.</summary>
    private void WriteImage(Image image, int w, int h)
    {
        var planes = image.GetPlanes() ?? throw new InvalidOperationException("Quadro sem planos.");
        void Plane(Image.Plane plane, byte[] data, int width, int height)
        {
            var address = Android.Runtime.JNIEnv.GetDirectBufferAddress(plane.Buffer!.Handle);
            if (address == IntPtr.Zero) throw new InvalidOperationException("O codificador não expôs a memória do quadro.");
            int row = plane.RowStride, pixel = plane.PixelStride;
            for (int y = 0; y < height; y++)
            {
                var line = address + y * row;
                if (pixel == 1) Marshal.Copy(data, y * width, line, width);
                else
                    for (int x = 0; x < width; x++) Marshal.WriteByte(line, x * pixel, data[y * width + x]);
            }
        }
        Plane(planes[0], _y, w, h);
        Plane(planes[1], _u, w / 2, h / 2);
        Plane(planes[2], _v, w / 2, h / 2);
    }

    private void Drain(bool endOfStream, CancellationToken cancel = default)
    {
        var info = new MediaCodec.BufferInfo();
        while (true)
        {
            cancel.ThrowIfCancellationRequested();
            int index = _video!.DequeueOutputBuffer(info, Timeout);
            if (index == (int)MediaCodecInfoState.TryAgainLater) { if (!endOfStream) return; continue; }
            if (index == (int)MediaCodecInfoState.OutputFormatChanged)
            {
                if (_started) throw new IOException("O formato do vídeo mudou no meio da codificação.");
                _videoTrack = _muxer!.AddTrack(_video.OutputFormat);
                if (_audioFormat is not null) _audioTrack = _muxer.AddTrack(_audioFormat);
                _muxer.Start();
                _started = true;
                // the whole AAC track, already encoded, goes in once the muxer runs
                foreach (var (data, pts, flags) in _audioSamples)
                {
                    using var buffer = Java.Nio.ByteBuffer.Wrap(data);
                    var sample = new MediaCodec.BufferInfo();
                    sample.Set(0, data.Length, pts, flags);
                    _muxer.WriteSampleData(_audioTrack, buffer, sample);
                }
                _audioSamples.Clear();
                continue;
            }
            if (index < 0) continue;
            var output = _video.GetOutputBuffer(index)!;
            if ((info.Flags & MediaCodecBufferFlags.CodecConfig) != 0) info.Size = 0;
            if (info.Size > 0)
            {
                if (!_started) throw new IOException("O codificador entregou vídeo antes do formato.");
                output.Position(info.Offset);
                output.Limit(info.Offset + info.Size);
                _muxer!.WriteSampleData(_videoTrack, output, info);
            }
            _video.ReleaseOutputBuffer(index, false);
            if ((info.Flags & MediaCodecBufferFlags.EndOfStream) != 0) return;
        }
    }

    /// <summary>The mixed soundtrack to AAC-LC in memory (a few MB for minutes of sound), before the video starts.</summary>
    private void EncodeAudio(string wav, CancellationToken cancel)
    {
        using var file = File.OpenRead(wav);
        var wavInfo = VideoAudio.ReadWavInfo(file) ?? throw new InvalidDataException("Trilha sonora não é WAV PCM 16 bits.");
        var format = MediaFormat.CreateAudioFormat("audio/mp4a-latm", wavInfo.SampleRate, wavInfo.Channels);
        format.SetInteger(MediaFormat.KeyAacProfile, 2);   // AAC-LC
        format.SetInteger(MediaFormat.KeyBitRate, 160_000);
        format.SetInteger(MediaFormat.KeyMaxInputSize, 16384);
        using var codec = MediaCodec.CreateEncoderByType("audio/mp4a-latm");
        codec.Configure(format, null, null, MediaCodecConfigFlags.Encode);
        codec.Start();
        file.Position = wavInfo.DataOffset;
        long remaining = wavInfo.DataBytes, frames = 0;
        int frameBytes = wavInfo.Channels * 2;
        var chunk = new byte[8192 / frameBytes * frameBytes];
        var info = new MediaCodec.BufferInfo();
        bool inputDone = false;
        try
        {
            while (true)
            {
                cancel.ThrowIfCancellationRequested();
                if (!inputDone && codec.DequeueInputBuffer(Timeout) is var inIndex and >= 0)
                {
                    long pts = frames * 1_000_000L / wavInfo.SampleRate;
                    int n = (int)Math.Min(chunk.Length, remaining);
                    n = n > 0 ? file.ReadAtLeast(chunk, n, false) / frameBytes * frameBytes : 0;
                    if (n <= 0) { codec.QueueInputBuffer(inIndex, 0, 0, pts, MediaCodecBufferFlags.EndOfStream); inputDone = true; }
                    else
                    {
                        var buffer = codec.GetInputBuffer(inIndex)!;
                        buffer.Clear();
                        buffer.Put(chunk, 0, n);
                        codec.QueueInputBuffer(inIndex, 0, n, pts, MediaCodecBufferFlags.None);
                        remaining -= n;
                        frames += n / frameBytes;
                    }
                }
                int outIndex = codec.DequeueOutputBuffer(info, Timeout);
                if (outIndex == (int)MediaCodecInfoState.OutputFormatChanged) { _audioFormat = codec.OutputFormat; continue; }
                if (outIndex < 0) continue;
                if ((info.Flags & MediaCodecBufferFlags.CodecConfig) == 0 && info.Size > 0)
                {
                    var output = codec.GetOutputBuffer(outIndex)!;
                    var data = new byte[info.Size];
                    output.Position(info.Offset);
                    output.Get(data, 0, info.Size);
                    _audioSamples.Add((data, info.PresentationTimeUs, info.Flags & ~MediaCodecBufferFlags.EndOfStream));
                }
                codec.ReleaseOutputBuffer(outIndex, false);
                if ((info.Flags & MediaCodecBufferFlags.EndOfStream) != 0) break;
            }
        }
        finally { codec.Stop(); }
        if (_audioFormat is null) throw new IOException("O codificador de áudio não informou o formato AAC.");
    }

    /// <summary>A sound file of the vault decoded to PCM16 at its own rate and channels (the mixer resamples).</summary>
    private static bool DecodePcm(string source, string target, CancellationToken cancel)
    {
        using var extractor = new MediaExtractor();
        extractor.SetDataSource(source);
        int track = -1;
        MediaFormat? format = null;
        for (int i = 0; i < extractor.TrackCount; i++)
        {
            var f = extractor.GetTrackFormat(i);
            if (f.GetString(MediaFormat.KeyMime) is { } mime && mime.StartsWith("audio/", StringComparison.Ordinal)) { track = i; format = f; break; }
        }
        if (track < 0 || format is null) return false;
        extractor.SelectTrack(track);
        using var codec = MediaCodec.CreateDecoderByType(format.GetString(MediaFormat.KeyMime)!);
        codec.Configure(format, null, null, MediaCodecConfigFlags.None);
        codec.Start();
        int channels = format.GetInteger(MediaFormat.KeyChannelCount), rate = format.GetInteger(MediaFormat.KeySampleRate);
        using var output = File.Create(target);
        output.Write(new byte[44]);
        long written = 0;
        var info = new MediaCodec.BufferInfo();
        bool inputDone = false;
        try
        {
            while (true)
            {
                cancel.ThrowIfCancellationRequested();
                if (!inputDone && codec.DequeueInputBuffer(Timeout) is var inIndex and >= 0)
                {
                    int size = extractor.ReadSampleData(codec.GetInputBuffer(inIndex)!, 0);
                    if (size < 0) { codec.QueueInputBuffer(inIndex, 0, 0, 0, MediaCodecBufferFlags.EndOfStream); inputDone = true; }
                    else { codec.QueueInputBuffer(inIndex, 0, size, extractor.SampleTime, MediaCodecBufferFlags.None); extractor.Advance(); }
                }
                int outIndex = codec.DequeueOutputBuffer(info, Timeout);
                if (outIndex == (int)MediaCodecInfoState.OutputFormatChanged)
                {
                    channels = codec.OutputFormat.GetInteger(MediaFormat.KeyChannelCount);
                    rate = codec.OutputFormat.GetInteger(MediaFormat.KeySampleRate);
                    continue;
                }
                if (outIndex < 0) continue;
                if (info.Size > 0)
                {
                    var buffer = codec.GetOutputBuffer(outIndex)!;
                    var bytes = new byte[info.Size];
                    buffer.Position(info.Offset);
                    buffer.Get(bytes, 0, info.Size);
                    output.Write(bytes);
                    written += bytes.Length;
                }
                codec.ReleaseOutputBuffer(outIndex, false);
                if ((info.Flags & MediaCodecBufferFlags.EndOfStream) != 0) break;
            }
        }
        finally { codec.Stop(); }
        output.Position = 0;
        VideoAudio.WriteWavHeader(output, channels, rate, written);
        return written > 0;
    }

    public ValueTask DisposeAsync()
    {
        try { _video?.Stop(); } catch (Java.Lang.IllegalStateException) { }
        _video?.Release();
        _video = null;
        if (_muxer is { } muxer)
        {
            try { muxer.Release(); } catch (Java.Lang.IllegalStateException) { }
            _muxer = null;
        }
        return ValueTask.CompletedTask;
    }
}
