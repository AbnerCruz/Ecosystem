using System.Buffers.Binary;
using Lunet.Studio;

namespace Lunet.Tests;

public sealed class WaveAudioDocumentTests
{
    private static byte[] Wave8Stereo()
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        w.Write("RIFF"u8); w.Write(36 + 8);
        w.Write("WAVEfmt "u8); w.Write(16); w.Write((short)1); w.Write((short)2);
        w.Write(8000); w.Write(16000); w.Write((short)2); w.Write((short)8);
        w.Write("data"u8); w.Write(8);
        w.Write(new byte[] { 0, 255, 128, 128, 64, 192, 255, 0 });
        return ms.ToArray();
    }

    [Fact]
    public void Pcm8StereoIsDecodedAndConvertedTo16BitOnExport()
    {
        var source = Wave8Stereo();
        var wav = WaveAudioDocument.Parse(source);
        Assert.Equal(2, wav.Channels);
        Assert.Equal(4, wav.FrameCount);
        Assert.Equal(8000, wav.SampleRate);
        var rendered = wav.Render(0, wav.DurationSeconds);
        Assert.Equal(44 + 4 * 2 * 2, rendered.Length);
        Assert.Equal(16, BinaryPrimitives.ReadInt16LittleEndian(rendered.AsSpan(34)));
        Assert.Equal(short.MinValue, BinaryPrimitives.ReadInt16LittleEndian(rendered.AsSpan(44)));
        Assert.Equal((short)32512, BinaryPrimitives.ReadInt16LittleEndian(rendered.AsSpan(46)));
        Assert.Equal((short)0, BinaryPrimitives.ReadInt16LittleEndian(rendered.AsSpan(48)));
        Assert.Equal(4, WaveAudioDocument.Parse(rendered).FrameCount);
        Assert.Equal(source, Wave8Stereo()); // fonte nunca é mutada
    }

    [Fact]
    public void CanTrimAndApplyGainWithoutModifyingOriginal()
    {
        var original = WaveAudioDocument.CreateEffect(440, 1.0, WaveShape.Sine);
        var full = original.Render(0, original.DurationSeconds);
        var clipped = original.Render(0.25, 0.5, 0.5f);
        var parsed = WaveAudioDocument.Parse(clipped);
        Assert.Equal(5513, parsed.FrameCount);
        Assert.InRange(parsed.DurationSeconds, 0.249, 0.251);
        Assert.Equal(full, original.Render(0, original.DurationSeconds));
        Assert.True(clipped.Length < full.Length);
    }

    [Fact]
    public void LoopPointsAreStoredAsStandardSmplAndRemainPcmPlayable()
    {
        var doc = WaveAudioDocument.CreateEffect(600, 1, WaveShape.Triangle);
        var wav = doc.Render(0.1, 0.8, 1.5f, 0.2, 0.7);
        Assert.True(wav.AsSpan(36, 4).SequenceEqual("smpl"u8));
        Assert.Equal(60, BinaryPrimitives.ReadInt32LittleEndian(wav.AsSpan(40, 4)));
        Assert.Equal(1, BinaryPrimitives.ReadInt32LittleEndian(wav.AsSpan(72, 4)));
        int loopStart = BinaryPrimitives.ReadInt32LittleEndian(wav.AsSpan(88, 4));
        int loopEnd = BinaryPrimitives.ReadInt32LittleEndian(wav.AsSpan(92, 4));
        Assert.True(loopStart > 0 && loopEnd > loopStart);
        var restored = WaveAudioDocument.Parse(wav);
        Assert.InRange(restored.DurationSeconds, 0.699, 0.701);
    }

    [Fact]
    public void NoiseSynthIsDeterministicAndDifferentShapesDiffer()
    {
        var noiseSource1 = WaveAudioDocument.CreateEffect(110, .25, WaveShape.Noise);
        var noiseSource2 = WaveAudioDocument.CreateEffect(110, .25, WaveShape.Noise);
        var squareSource = WaveAudioDocument.CreateEffect(110, .25, WaveShape.Square);
        var noise1 = noiseSource1.Render(0, noiseSource1.DurationSeconds);
        var noise2 = noiseSource2.Render(0, noiseSource2.DurationSeconds);
        var square = squareSource.Render(0, squareSource.DurationSeconds);
        Assert.Equal(noise1, noise2);
        Assert.NotEqual(noise1, square);
        Assert.Equal(16, BinaryPrimitives.ReadUInt16LittleEndian(noise1.AsSpan(34)));
    }

    [Fact]
    public void GetPeaksRespectsColumnCountAndRange()
    {
        var wav = WaveAudioDocument.Parse(Wave8Stereo());
        var columns = wav.GetPeaks(3);
        Assert.Equal(3, columns.Length);
        Assert.All(columns, c => { Assert.InRange(c.Minimum, -1, 1); Assert.InRange(c.Maximum, -1, 1); });
        Assert.Contains(columns, c => c.Minimum < 0);
    }

    [Fact]
    public void RejectsTruncatedUnsupportedAndMalformedWavs()
    {
        var bytes = Wave8Stereo();
        Assert.Throws<InvalidDataException>(() => WaveAudioDocument.Parse(bytes.AsSpan(0, bytes.Length - 1)));
        var badCodec = (byte[])bytes.Clone();
        BinaryPrimitives.WriteInt16LittleEndian(badCodec.AsSpan(20, 2), 3);
        Assert.Throws<NotSupportedException>(() => WaveAudioDocument.Parse(badCodec));
        var misaligned = (byte[])bytes.Clone();
        BinaryPrimitives.WriteInt16LittleEndian(misaligned.AsSpan(32, 2), 1);
        Assert.Throws<InvalidDataException>(() => WaveAudioDocument.Parse(misaligned));
    }

    [Fact]
    public void RejectsInvalidTrimGainAndLoopRanges()
    {
        var doc = WaveAudioDocument.CreateEffect(440, 1, WaveShape.Sine);
        Assert.Throws<ArgumentOutOfRangeException>(() => doc.Render(-1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => doc.Render(0.8, 0.4));
        Assert.Throws<ArgumentOutOfRangeException>(() => doc.Render(0, 1, 3f));
        Assert.Throws<ArgumentOutOfRangeException>(() => doc.Render(0, 1, 1, .9, .2));
        Assert.Throws<ArgumentOutOfRangeException>(() => WaveAudioDocument.CreateEffect(0, 1, WaveShape.Sine));
    }
}
