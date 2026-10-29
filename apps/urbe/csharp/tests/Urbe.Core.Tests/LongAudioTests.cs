using Urbe.Core;

namespace Urbe.Core.Tests;

/// <summary>
/// CST-058: a long recording (a class) becomes a note to review. Decoded audio is brought to what the recognizer reads
/// (16 kHz, mono, 16-bit), cut in pauses about every 45 s so no word is split, and each part's text goes into the note
/// with the time it starts at.
/// </summary>
public sealed class LongAudioTests
{
    [Fact]
    public void StereoAt48kBecomesMonoAt16k()
    {
        // 48 kHz stereo, left 1000 and right 3000 on every frame: 3 frames in → 1 sample out of 2000
        var interleaved = new short[48000 * 2];
        for (int i = 0; i < interleaved.Length; i += 2) { interleaved[i] = 1000; interleaved[i + 1] = 3000; }
        var mono = LongAudio.ToMono16k(interleaved, channels: 2, sampleRate: 48000);
        Assert.Equal(16000, mono.Length);
        Assert.All(mono, s => Assert.Equal(2000, s));
    }

    [Fact]
    public void AToneKeepsItsShapeAfterResampling()
    {
        // 440 Hz at 44.1 kHz, one second: the resampled signal has the same frequency (zero crossings)
        var source = Enumerable.Range(0, 44100).Select(i => (short)(10000 * Math.Sin(2 * Math.PI * 440 * i / 44100.0))).ToArray();
        var mono = LongAudio.ToMono16k(source, 1, 44100);
        Assert.InRange(mono.Length, 15999, 16001);
        int crossings = 0;
        for (int i = 1; i < mono.Length; i++) if (mono[i - 1] < 0 != mono[i] < 0) crossings++;
        Assert.InRange(crossings, 870, 890);   // 2 × 440
    }

    private static short[] Speech(params (double Seconds, bool Loud)[] spans)
    {
        var samples = new List<short>();
        var random = new Random(7);
        foreach (var (seconds, loud) in spans)
            for (int i = 0; i < seconds * 16000; i++) samples.Add((short)(loud ? random.Next(-9000, 9000) : random.Next(-40, 40)));
        return [.. samples];
    }

    [Fact]
    public void PartsAreCutInThePausesNearEvery45Seconds()
    {
        // speech with pauses at 41–42 s and 88–89 s, then speech to 130 s
        var pcm = Speech((41, true), (1, false), (46, true), (1, false), (41, true));
        var parts = LongAudio.Plan(pcm.Length, (start, count) => pcm.AsSpan((int)start, count).ToArray());
        Assert.Equal(3, parts.Count);
        Assert.Equal(0, parts[0].Start);
        Assert.InRange(parts[1].Start / 16000.0, 41, 42);    // inside the first pause
        Assert.InRange(parts[2].Start / 16000.0, 88, 89);    // inside the second
        Assert.Equal(pcm.Length, parts[^1].Start + parts[^1].Length);
        for (int i = 1; i < parts.Count; i++) Assert.Equal(parts[i - 1].Start + parts[i - 1].Length, parts[i].Start);   // nothing lost or repeated
    }

    [Fact]
    public void WithoutPausesThePartsStillEndNearTheTarget()
    {
        var pcm = Speech((100, true));
        var parts = LongAudio.Plan(pcm.Length, (start, count) => pcm.AsSpan((int)start, count).ToArray());
        Assert.All(parts.Take(parts.Count - 1), p => Assert.InRange(p.Length / 16000.0, 38, 52));
        Assert.Equal(pcm.Length, parts.Sum(p => (long)p.Length));
    }

    [Fact]
    public void AShortRecordingIsOnePart() =>
        Assert.Single(LongAudio.Plan(16000 * 20, (s, c) => new short[c]));

    [Fact]
    public void AWavHasTheHeaderTheRecognizerReads()
    {
        var wav = LongAudio.Wav([1, 2, 3]);
        Assert.Equal(44 + 6, wav.Length);
        Assert.Equal("RIFF", System.Text.Encoding.ASCII.GetString(wav, 0, 4));
        Assert.Equal("WAVE", System.Text.Encoding.ASCII.GetString(wav, 8, 4));
        Assert.Equal(16000, BitConverter.ToInt32(wav, 24));
        Assert.Equal(1, BitConverter.ToInt16(wav, 22));    // mono
        Assert.Equal(16, BitConverter.ToInt16(wav, 34));   // 16-bit
        Assert.Equal(6, BitConverter.ToInt32(wav, 40));
    }

    [Fact]
    public void TheNoteLinksTheAudioAndTimesEachPart()
    {
        var note = LongAudio.Note("Aulas/Física 12.m4a",
            [(TimeSpan.Zero, "Hoje vamos falar de energia."), (TimeSpan.FromSeconds(44.6), null), (TimeSpan.FromMinutes(61) + TimeSpan.FromSeconds(3), "E acabou.")]);
        Assert.StartsWith("# Física 12 — transcrição\n", note);
        Assert.Contains("![[Aulas/Física 12.m4a]]", note);
        Assert.Contains("revis", note);   // it says it is to be reviewed
        Assert.Contains("**[00:00]** Hoje vamos falar de energia.", note);
        Assert.Contains("**[00:44]** (trecho não reconhecido)", note);
        Assert.Contains("**[1:01:03]** E acabou.", note);
    }

    [Fact]
    public void TheNoteGoesNextToTheAudioWithAFreeName()
    {
        Assert.Equal("Aulas/Física 12 — transcrição.md", LongAudio.NotePath("Aulas/Física 12.m4a", _ => false));
        Assert.Equal("Física 12 — transcrição 2.md", LongAudio.NotePath("Física 12.m4a", p => p == "Física 12 — transcrição.md"));
    }
}
