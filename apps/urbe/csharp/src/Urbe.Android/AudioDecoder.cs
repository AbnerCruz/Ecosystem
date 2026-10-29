using Android.Content;
using Android.Media;
using Urbe.Client;
using Urbe.Core;

namespace Urbe.AndroidHost;

/// <summary>
/// CST-058 on Android: an audio file of the vault decoded by the platform (MediaExtractor + MediaCodec, so m4a, mp3,
/// ogg, wav, flac and whatever the device supports) into the 16 kHz mono 16-bit WAV the speech recognizer reads,
/// written as it is decoded (an hour of audio is never in memory). Android 13+ transcribes a file (VoiceSpeech).
/// </summary>
internal static class AudioDecoder
{
    public static void Install(Context context)
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(33)) return;
        PlatformAudio.DecodeToWav = (source, target, progress, cancel) => Task.Run(() => Decode(source, target, progress, cancel), cancel);
        PlatformAudio.TranscribeWav = async (wav, cancel) =>
        {
            var text = VoiceSpeech.TranscribeFileAsync(context, wav, preferOnDevice: true);
            var stop = Task.Delay(Timeout.Infinite, cancel);
            if (await Task.WhenAny(text, stop) != text) cancel.ThrowIfCancellationRequested();
            return await text;
        };
    }

    private static bool Decode(string source, string target, IProgress<double>? progress, CancellationToken cancel)
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
        var mimeType = format.GetString(MediaFormat.KeyMime)!;
        long duration = format.ContainsKey(MediaFormat.KeyDuration) ? format.GetLong(MediaFormat.KeyDuration) : 0;
        using var codec = MediaCodec.CreateDecoderByType(mimeType);
        codec.Configure(format, null, null, MediaCodecConfigFlags.None);
        codec.Start();
        int channels = format.GetInteger(MediaFormat.KeyChannelCount), rate = format.GetInteger(MediaFormat.KeySampleRate);
        var converter = new LongAudio.Mono16k(channels, rate);
        using var output = File.Create(target);
        output.Write(new byte[44]);   // the header, once the length is known
        long written = 0;
        var info = new MediaCodec.BufferInfo();
        bool inputDone = false, outputDone = false;
        try
        {
            while (!outputDone)
            {
                cancel.ThrowIfCancellationRequested();
                if (!inputDone)
                {
                    int inIndex = codec.DequeueInputBuffer(10_000);
                    if (inIndex >= 0)
                    {
                        var buffer = codec.GetInputBuffer(inIndex)!;
                        int size = extractor.ReadSampleData(buffer, 0);
                        if (size < 0) { codec.QueueInputBuffer(inIndex, 0, 0, 0, MediaCodecBufferFlags.EndOfStream); inputDone = true; }
                        else
                        {
                            codec.QueueInputBuffer(inIndex, 0, size, extractor.SampleTime, MediaCodecBufferFlags.None);
                            if (duration > 0) progress?.Report(Math.Clamp(extractor.SampleTime / (double)duration, 0, 1));
                            extractor.Advance();
                        }
                    }
                }
                int outIndex = codec.DequeueOutputBuffer(info, 10_000);
                if (outIndex == (int)MediaCodecInfoState.OutputFormatChanged)
                {
                    var changed = codec.OutputFormat;
                    channels = changed.GetInteger(MediaFormat.KeyChannelCount);
                    rate = changed.GetInteger(MediaFormat.KeySampleRate);
                    converter = new LongAudio.Mono16k(channels, rate);
                    continue;
                }
                if (outIndex < 0) continue;
                if (info.Size > 0)
                {
                    var buffer = codec.GetOutputBuffer(outIndex)!;
                    var bytes = new byte[info.Size];
                    buffer.Position(info.Offset);
                    buffer.Get(bytes, 0, info.Size);
                    var pcm = new short[bytes.Length / 2];
                    Buffer.BlockCopy(bytes, 0, pcm, 0, pcm.Length * 2);
                    var mono = converter.Push(pcm);
                    var outBytes = new byte[mono.Length * 2];
                    Buffer.BlockCopy(mono, 0, outBytes, 0, outBytes.Length);
                    output.Write(outBytes);
                    written += outBytes.Length;
                }
                codec.ReleaseOutputBuffer(outIndex, false);
                if ((info.Flags & MediaCodecBufferFlags.EndOfStream) != 0) outputDone = true;
            }
        }
        finally { codec.Stop(); }
        // the header with the real length (LongAudio.Wav writes the same 44 bytes)
        var header = LongAudio.Wav([]);
        BitConverter.GetBytes((int)(36 + written)).CopyTo(header, 4);
        BitConverter.GetBytes((int)written).CopyTo(header, 40);
        output.Seek(0, SeekOrigin.Begin);
        output.Write(header);
        progress?.Report(1);
        return written > 0;
    }
}
