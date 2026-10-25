using Android.Media;

namespace Lunet.Android.Audio;

internal sealed class AndroidPcmAudioOutput : IPcmAudioOutput
{
    private readonly AudioTrack _track;
    public int BufferBytes { get; }
    public int UnderrunCount => _track.UnderrunCount;

    public AndroidPcmAudioOutput(PcmWaveStream wave)
    {
        var channels = wave.Channels == 1 ? ChannelOut.Mono : ChannelOut.Stereo;
        var encoding = wave.BitsPerSample == 8 ? Android.Media.Encoding.Pcm8bit : Android.Media.Encoding.Pcm16bit;
        int minimum = AudioTrack.GetMinBufferSize(wave.SampleRate, channels, encoding);
        if (minimum <= 0) throw new NotSupportedException("Formato PCM não suportado pela saída de áudio.");
        // At least 80 ms; ample headroom for a managed worker without a silent underrun workaround.
        BufferBytes = Math.Max(minimum * 2, wave.SampleRate * wave.FrameBytes * 80 / 1000);
        BufferBytes += (wave.FrameBytes - BufferBytes % wave.FrameBytes) % wave.FrameBytes;
        using var attributes = new AudioAttributes.Builder()!.SetUsage(AudioUsageKind.Game)!
            .SetContentType(AudioContentType.Music)!.Build()!;
        using var format = new AudioFormat.Builder()!.SetSampleRate(wave.SampleRate)!
            .SetEncoding(encoding)!.SetChannelMask(channels)!.Build()!;
        _track = new AudioTrack.Builder()!.SetAudioAttributes(attributes)!.SetAudioFormat(format)!
            .SetTransferMode(AudioTrackMode.Stream)!.SetBufferSizeInBytes(BufferBytes)!.Build()!;
        if (_track.State != AudioTrackState.Initialized)
        {
            _track.Release();
            _track.Dispose();
            throw new InvalidOperationException("Não foi possível inicializar a saída PCM.");
        }
    }
    public int Write(byte[] buffer, int offset, int count) => _track.Write(buffer, offset, count, WriteMode.NonBlocking);
    public void Play() => _track.Play();
    public void Pause() => _track.Pause();
    public void Stop() => _track.Stop();
    public void SetVolume(float volume) => _track.SetVolume(volume);
    public void Dispose() { _track.Release(); _track.Dispose(); }
}
