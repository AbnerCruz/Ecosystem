using Lunet.Studio;
using Lunet.Studio.Music;

namespace Lunet.Tests;

/// <summary>LUNET-500: the audio studio composes multi-track music and designs complex sound effects.</summary>
public sealed class MusicStudioTests
{
    [Theory]
    [InlineData(60, "C4")]
    [InlineData(69, "A4")]
    [InlineData(61, "C#4")]
    [InlineData(36, "C2")]
    public void NoteNames_RoundTrip(int midi, string name)
    {
        Assert.Equal(name, MusicTheory.Name(midi));
        Assert.Equal(midi, MusicTheory.Parse(name));
    }

    [Fact]
    public void Frequency_OfA4Is440() => Assert.Equal(440, MusicTheory.Frequency(69), 6);

    [Fact]
    public void DiatonicChords_OfCMajorAreCorrect()
    {
        var major = MusicTheory.Scale("major");
        Assert.Equal("C", MusicTheory.ChordName(MusicTheory.DiatonicChord(60, major, 1)));
        Assert.Equal("Dm", MusicTheory.ChordName(MusicTheory.DiatonicChord(60, major, 2)));
        Assert.Equal("G", MusicTheory.ChordName(MusicTheory.DiatonicChord(60, major, 5)));
        Assert.Equal("Am", MusicTheory.ChordName(MusicTheory.DiatonicChord(60, major, 6)));
        Assert.Equal("B°", MusicTheory.ChordName(MusicTheory.DiatonicChord(60, major, 7)));
        Assert.Equal("G7", MusicTheory.ChordName(MusicTheory.DiatonicChord(60, major, 5, seventh: true)));
        // pentatonic borrows its parent scale for chords
        Assert.Equal("Am", MusicTheory.ChordName(MusicTheory.DiatonicChord(57, MusicTheory.Scale("minor-pentatonic"), 1)));
    }

    [Fact]
    public void Snap_KeepsNotesInTheScale()
    {
        var major = MusicTheory.Scale("major");
        Assert.Equal(60, MusicTheory.Snap(61, 60, major));
        Assert.True(MusicTheory.InScale(MusicTheory.Snap(66, 60, major), 60, major));
    }

    [Fact]
    public void Melody_IsDeterministicInScaleAndEndsOnTheRoot()
    {
        var scale = MusicTheory.Scale("minor");
        var a = MusicTheory.Melody(7, 57, scale, [1, 6, 3, 7], 16, low: 57, high: 76);
        var b = MusicTheory.Melody(7, 57, scale, [1, 6, 3, 7], 16, low: 57, high: 76);
        Assert.Equal(a, b);
        Assert.NotEmpty(a);
        Assert.All(a, n => Assert.True(MusicTheory.InScale(n.Pitch, 57, scale)));
        Assert.All(a, n => Assert.True(n.Step + n.Length <= 64));
        Assert.Equal(9, a[^1].Pitch % 12); // A
        Assert.NotEqual(a, MusicTheory.Melody(8, 57, scale, [1, 6, 3, 7], 16, low: 57, high: 76));
    }

    [Fact]
    public void Grooves_TileToTheRequestedLength()
    {
        var notes = MusicTheory.Groove("rock", 32);
        Assert.Contains(notes, n => n.Pitch == 36 && n.Step == 16);
        Assert.All(notes, n => Assert.InRange(n.Step, 0, 31));
        Assert.Throws<ArgumentException>(() => MusicTheory.Groove("nope", 16));
    }

    [Fact]
    public void Starter_IsAValidMultiTrackSongThatRoundTrips()
    {
        var song = MusicSong.Starter("Tema", "epic", "minor", 57, 100);
        Assert.Equal(4, song.Tracks.Count);
        Assert.True(song.Tracks[3].Instrument.Drums);
        Assert.Equal([0, 1, 1], song.Order);
        var json = song.ToJson();
        Assert.Contains("\"Wave\": \"Pulse25\"", json);
        var again = MusicSong.Parse(json);
        Assert.Equal(song.Patterns.Sum(p => p.Notes.Count), again.Patterns.Sum(p => p.Notes.Count));
        Assert.Equal(song.DurationSeconds, again.DurationSeconds, 6);
    }

    [Theory]
    [InlineData("""{"Version":1,"Bpm":10,"Tracks":[{}],"Patterns":[{"Steps":16,"Notes":[]}],"Order":[0]}""")]
    [InlineData("""{"Version":1,"Tracks":[],"Patterns":[{"Steps":16,"Notes":[]}],"Order":[0]}""")]
    [InlineData("""{"Version":1,"Tracks":[{}],"Patterns":[{"Steps":16,"Notes":[]}],"Order":[3]}""")]
    [InlineData("""{"Version":1,"Tracks":[{}],"Patterns":[{"Steps":16,"Notes":[{"Track":2,"Step":0,"Pitch":60,"Length":1}]}],"Order":[0]}""")]
    [InlineData("""{"Version":1,"Tracks":[{}],"Patterns":[{"Steps":16,"Notes":[{"Track":0,"Step":16,"Pitch":60,"Length":1}]}],"Order":[0]}""")]
    [InlineData("""{"Version":2,"Tracks":[{}],"Patterns":[{"Steps":16}],"Order":[0]}""")]
    public void Parse_RejectsUnsafeSongs(string json) =>
        Assert.Throws<InvalidDataException>(() => MusicSong.Parse(json));

    [Fact]
    public void Render_ProducesAudibleBoundedStereoOfTheRightLength()
    {
        var song = MusicSong.Starter("Tema", bpm: 120);
        song.Loop = false;
        var samples = new MusicRenderer(22_050).Render(song, cancellationToken: TestContext.Current.CancellationToken);
        double expected = song.DurationSeconds * 22_050 * 2;
        Assert.InRange(samples.Length, expected, expected + 22_050 * 2 * 2.6);
        Assert.All(samples, s => Assert.InRange(s, -1f, 1f));
        var rms = Math.Sqrt(samples.Average(s => (double)s * s));
        Assert.InRange(rms, 0.02, 0.7);
        // stereo: the chords are panned left, so the channels differ
        Assert.Contains(Enumerable.Range(0, samples.Length / 2), i => Math.Abs(samples[2 * i] - samples[2 * i + 1]) > 1e-3);
    }

    [Fact]
    public void Render_IsDeterministicAndLoopingSongsHaveExactLength()
    {
        var song = MusicSong.Starter("Loop", bpm: 140);
        var renderer = new MusicRenderer(16_000);
        var a = renderer.Render(song, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(a, renderer.Render(song, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal((int)Math.Round(song.TotalSteps * song.SecondsPerStep * 16_000) * 2, a.Length);
    }

    [Fact]
    public void Render_MuteAndSoloChangeTheMix()
    {
        var song = MusicSong.Starter("Mix");
        song.Loop = false;
        var renderer = new MusicRenderer(11_025);
        var full = renderer.Render(song, cancellationToken: TestContext.Current.CancellationToken);
        song.Tracks = song.Tracks.Select((t, i) => i == 3 ? t with { Solo = true } : t).ToList();
        var drumsOnly = renderer.Render(song, cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotEqual(full, drumsOnly);
        song.Tracks = song.Tracks.Select(t => t with { Solo = false, Muted = true }).ToList();
        Assert.All(renderer.Render(song, cancellationToken: TestContext.Current.CancellationToken), s => Assert.Equal(0f, s));
    }

    [Fact]
    public void RenderWav_IsReadableByTheAudioEditorWithLoopMarker()
    {
        var song = MusicSong.Starter("Wav", bpm: 160);
        var wav = new MusicRenderer(22_050).RenderWav(song, onlyPattern: 0, cancellationToken: TestContext.Current.CancellationToken);
        var document = WaveAudioDocument.Parse(wav);
        Assert.Equal(2, document.Channels);
        Assert.Equal(22_050, document.SampleRate);
        var looped = new MusicRenderer(22_050).RenderWav(song, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Contains("smpl"u8.ToArray(), Chunks(looped));
    }

    private static IEnumerable<byte[]> Chunks(byte[] wav)
    {
        for (int i = 12; i + 8 <= wav.Length;)
        {
            yield return wav[i..(i + 4)];
            i += 8 + BitConverter.ToInt32(wav, i + 4);
        }
    }

    [Fact]
    public void AllPresets_RenderWithoutNaN()
    {
        var song = MusicSong.Empty("Presets");
        song.Loop = false;
        song.Tracks = Instrument.Presets.Select(i => new MusicTrack { Name = i.Name, Instrument = i }).ToList();
        song.Patterns[0].Notes = Instrument.Presets.Select((p, t) => new MusicNote(t, t, p.Drums ? 36 : 60, 2)).ToList();
        var samples = new MusicRenderer(11_025).Render(song, cancellationToken: TestContext.Current.CancellationToken);
        Assert.All(samples, s => Assert.True(float.IsFinite(s)));
        Assert.Contains(samples, s => Math.Abs(s) > 0.01f);
    }

    // ---------- Sound designer ----------

    [Fact]
    public void SoundDesigner_EveryCategoryGeneratesValidVariedSounds()
    {
        foreach (var (id, _) in SoundDesigner.Categories)
        {
            var a = SoundDesigner.Generate(id, 1);
            var b = SoundDesigner.Generate(id, 2);
            Assert.Equal(a, SoundDesigner.Generate(id, 1));
            Assert.NotEqual(a, b);
            var samples = SoundDesigner.Render(a);
            Assert.Equal((int)Math.Ceiling(a.Duration * SoundDesigner.SampleRate), samples.Length);
            Assert.All(samples, s => Assert.InRange(s, -1f, 1f));
            Assert.Contains(samples, s => Math.Abs(s) > 0.05f);
        }
    }

    [Fact]
    public void SoundDesigner_MutateStaysValidAndJsonRoundTrips()
    {
        var effect = SoundDesigner.Generate("laser", 3);
        var variation = SoundDesigner.Mutate(effect, 9);
        SoundDesigner.Validate(variation);
        Assert.NotEqual(effect, variation);
        Assert.Equal(variation, SoundDesigner.Parse(SoundDesigner.ToJson(variation)));
        Assert.Throws<InvalidDataException>(() => SoundDesigner.Parse("""{"StartHz":99999}"""));
        var wav = SoundDesigner.RenderWav(effect);
        Assert.Equal(1, WaveAudioDocument.Parse(wav).Channels);
    }
}
