using Lunet.Android.Audio;
using Lunet.Runtime.Profiling;

namespace Lunet.Tests;

public sealed class PcmMusicTests
{
    private static MemoryStream Wave(byte[] pcm, int channels = 1, int bits = 16, bool metadata = false)
    {
        var stream = new MemoryStream();
        using (var w = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            w.Write("RIFF"u8); w.Write(0); w.Write("WAVE"u8);
            if (metadata) { w.Write("JUNK"u8); w.Write(3); w.Write(new byte[] { 1, 2, 3, 0 }); }
            w.Write("fmt "u8); w.Write(16); w.Write((ushort)1); w.Write((ushort)channels);
            w.Write(8000); w.Write(8000 * channels * bits / 8);
            w.Write((ushort)(channels * bits / 8)); w.Write((ushort)bits);
            w.Write("data"u8); w.Write(pcm.Length); w.Write(pcm);
            if (pcm.Length % 2 != 0) w.Write((byte)0);
            // Metadata after data must never be played.
            w.Write("LIST"u8); w.Write(4); w.Write(new byte[] { 9, 9, 9, 9 });
            stream.Position = 4; w.Write((int)stream.Length - 8);
        }
        stream.Position = 0;
        return stream;
    }

    [Theory]
    [InlineData(1, 8)] [InlineData(1, 16)] [InlineData(2, 8)] [InlineData(2, 16)]
    public void StreamsOnlyPcmFramesAndLoopsWithoutPlayingRiffMetadata(int channels, int bits)
    {
        var data = Enumerable.Range(0, 24).Select(i => (byte)i).ToArray();
        using var wave = PcmWaveStream.TryRead(Wave(data, channels, bits, metadata: true))!;
        Assert.Equal(8000, wave.SampleRate); Assert.Equal(channels, wave.Channels); Assert.Equal(bits, wave.BitsPerSample);
        var buffer = new byte[13];
        var output = new List<byte>();
        int count;
        while ((count = wave.Read(buffer, false)) != 0) { Assert.Equal(0, count % wave.FrameBytes); output.AddRange(buffer.Take(count)); }
        Assert.Equal(data, output);
        Assert.Equal(0, wave.Read(buffer, false));
        count = wave.Read(buffer, true);
        Assert.Equal(data.Take(count), buffer.Take(count));
    }

    [Fact]
    public void UnsupportedFormatsRemainAvailableToExistingBackend()
    {
        using var mp3 = new MemoryStream("ID3 compressed music"u8.ToArray());
        Assert.Null(PcmWaveStream.TryRead(mp3)); Assert.True(mp3.CanRead);
        using var floatWave = Wave(new byte[32]);
        floatWave.Position = 20; floatWave.WriteByte(3); // IEEE float WAV, kept on MediaPlayer.
        Assert.Null(PcmWaveStream.TryRead(floatWave)); Assert.True(floatWave.CanRead);
    }

    [Theory]
    [InlineData("truncated")] [InlineData("alignment")] [InlineData("byteRate")] [InlineData("noData")] [InlineData("empty")]
    public void RejectsCorruptPcmWithoutReadingBeyondDeclaredData(string fault)
    {
        using var source = Wave(new byte[16]);
        switch (fault)
        {
            case "truncated": source.SetLength(source.Length - 1); break;
            case "alignment": source.Position = 32; source.WriteByte(4); break;
            case "byteRate": source.Position = 28; source.WriteByte(1); break;
            case "noData": source.Position = 36; source.Write("JUNK"u8); break;
            case "empty": source.Position = 40; source.Write(new byte[4]); break;
        }
        Assert.Throws<InvalidDataException>(() => PcmWaveStream.TryRead(source));
    }

    [Fact]
    public void PlayerHandlesPartialWritesPrefillsAndDrainsShortTrackAtEnd()
    {
        var data = Enumerable.Range(0, 24).Select(i => (byte)i).ToArray();
        var output = new FakeOutput { MaxWrite = 4, NativeUnderruns = 3 };
        var errors = new List<string>();
        using var source = Wave(data);
        using var player = new PcmStreamPlayer(PcmWaveStream.TryRead(source)!, output, false, 0.5f, errors.Add);
        Assert.True(output.Stopped.Wait(3000, TestContext.Current.CancellationToken));
        Assert.Equal(data, output.Bytes.ToArray()); Assert.Equal(1, output.Plays);
        Assert.True(output.FirstPlayBytes > 0); Assert.Equal(3, player.UnderrunCount);
        Assert.Equal(0.5f, output.Volume); Assert.Empty(errors);
    }

    [Fact]
    public void PauseResumeAndDisposeWorkWhenNativeOutputIsFull()
    {
        var output = new FakeOutput { Full = true, NativeUnderruns = 7 };
        var errors = new List<string>();
        var source = Wave(new byte[800]);
        var player = new PcmStreamPlayer(PcmWaveStream.TryRead(source)!, output, true, 1, errors.Add);
        try
        {
            Assert.True(SpinWait.SpinUntil(() => Volatile.Read(ref output.Plays) > 0, 3000));
            player.Pause();
            int writes = output.Writes;
            Assert.Equal(1, output.Pauses);
            Assert.Equal(writes, output.Writes); // Pause returned only after any in-flight write.
            player.SetVolume(0.2f); Assert.Equal(0.2f, output.Volume);
            player.Resume();
            Assert.True(SpinWait.SpinUntil(() => Volatile.Read(ref output.Plays) > 1, 3000));
            player.Pause();
        }
        finally { player.Dispose(); }
        Assert.Equal(7, player.UnderrunCount); Assert.True(output.Disposed);
        Assert.False(source.CanRead); Assert.Empty(errors);
        player.Dispose();
    }

    [Fact]
    public void NativeWriteFailureIsReportedAndWorkerStopsRatherThanSpinning()
    {
        var output = new FakeOutput { FailWrite = true };
        using var error = new ManualResetEventSlim();
        string? message = null;
        using var player = new PcmStreamPlayer(PcmWaveStream.TryRead(Wave(new byte[80]))!, output, true, 1,
            text => { message = text; error.Set(); });
        Assert.True(error.Wait(3000, TestContext.Current.CancellationToken)); Assert.Contains("Música PCM interrompida", message);
        Assert.Contains("-6", message); Assert.Equal(1, output.Writes); Assert.True(output.Stopped.IsSet);
    }

    [Fact]
    public void FailedNativeCounterIsUnavailableRatherThanFakeZeroOrProfilerCrash()
    {
        var output = new FakeOutput { Full = true, FailCounter = true };
        var errors = new List<string>();
        using var player = new PcmStreamPlayer(PcmWaveStream.TryRead(Wave(new byte[80]))!, output, true, 1, errors.Add);
        Assert.Null(player.UnderrunCount);
        Assert.Null(player.UnderrunCount);
        Assert.Single(errors);
        Assert.Contains("não medidos", errors[0]);
        player.Dispose();
        Assert.Null(player.UnderrunCount);
        Assert.Single(errors);
    }

    [Theory]
    [InlineData(0)] [InlineData(19)]
    public void ProfilerShowsMeasuredZeroAndNonzeroWithoutClaimingSoundPoolCoverage(long underruns)
    {
        var frames = new PreviewFrameStatistics(); frames.Record(0.02, 1, 1, 2, 0);
        var report = PreviewProfilerReport.Format(frames.Snapshot(), DateTimeOffset.UnixEpoch, false, 0, 0, 0, 0, 0, 0, 0, underruns);
        Assert.Contains($"Audio underruns (música WAV PCM, sessão): {underruns}", report);
        Assert.Contains("efeitos SoundPool e música comprimida: não medidos", report);
    }

    private sealed class FakeOutput : IPcmAudioOutput
    {
        public int BufferBytes => 64;
        public int NativeUnderruns;
        public int UnderrunCount => FailCounter ? throw new IOException("Saída indisponível") : NativeUnderruns;
        public int MaxWrite = int.MaxValue;
        public volatile bool Full;
        public bool FailWrite, FailCounter, Disposed;
        public int Plays, Pauses, Writes, FirstPlayBytes;
        public float Volume;
        public List<byte> Bytes { get; } = [];
        public ManualResetEventSlim Stopped { get; } = new();
        public int Write(byte[] buffer, int offset, int count)
        {
            Interlocked.Increment(ref Writes);
            if (FailWrite) return -6;
            if (Full) return 0;
            count = Math.Min(count, MaxWrite);
            Bytes.AddRange(buffer.AsSpan(offset, count).ToArray()); return count;
        }
        public void Play() { if (Plays == 0) FirstPlayBytes = Bytes.Count; Interlocked.Increment(ref Plays); }
        public void Pause() => Pauses++;
        public void Stop() => Stopped.Set();
        public void SetVolume(float volume) => Volume = volume;
        public void Dispose() => Disposed = true;
    }
}
