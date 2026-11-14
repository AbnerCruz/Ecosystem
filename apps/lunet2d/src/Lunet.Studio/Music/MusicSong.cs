using System.Text.Json;
using System.Text.Json.Serialization;

namespace Lunet.Studio.Music;

/// <summary>A note in a pattern. <see cref="Track"/> indexes <see cref="MusicSong.Tracks"/>; on a drum track the pitch picks the drum.</summary>
public sealed record MusicNote(int Track, int Step, int Pitch, int Length, float Velocity = 0.8f);

public enum Waveform { Sine, Square, Pulse25, Pulse12, Triangle, Saw, Noise, Fm }

/// <summary>A synthesized instrument: oscillators, envelope, filter and modulation. All numbers are plain and JSON-friendly.</summary>
public sealed record Instrument
{
    public string Name { get; init; } = "Instrumento";
    public Waveform Wave { get; init; } = Waveform.Square;
    /// <summary>A second oscillator mixed in (0 = off), detuned in cents, for thicker sounds.</summary>
    public Waveform Wave2 { get; init; } = Waveform.Saw;
    public float Mix2 { get; init; }
    public float DetuneCents { get; init; } = 7;
    public int Octave2 { get; init; }
    public float Attack { get; init; } = 0.005f;
    public float Decay { get; init; } = 0.12f;
    public float Sustain { get; init; } = 0.6f;
    public float Release { get; init; } = 0.15f;
    /// <summary>Low-pass cutoff in Hz (20000 = open) and resonance 0–0.95.</summary>
    public float Cutoff { get; init; } = 20000;
    public float Resonance { get; init; }
    /// <summary>How much the envelope opens the filter (0–1): the "wah" of plucks and basses.</summary>
    public float FilterEnvelope { get; init; }
    public float VibratoHz { get; init; } = 5.5f;
    public float VibratoCents { get; init; }
    /// <summary>Pitch glide at the start of each note, in semitones (negative = from below), over <see cref="SlideTime"/> seconds.</summary>
    public float SlideSemitones { get; init; }
    public float SlideTime { get; init; } = 0.05f;
    /// <summary>FM: modulator ratio and depth (only for <see cref="Waveform.Fm"/>).</summary>
    public float FmRatio { get; init; } = 2;
    public float FmDepth { get; init; } = 1.5f;
    /// <summary>True for a drum kit: notes choose drum sounds by pitch (see <see cref="MusicTheory.DrumLanes"/>).</summary>
    public bool Drums { get; init; }
    public float Gain { get; init; } = 0.8f;

    public static IReadOnlyList<Instrument> Presets { get; } =
    [
        new() { Name = "Lead quadrado", Wave = Waveform.Square, Attack = 0.004f, Decay = 0.1f, Sustain = 0.7f, Release = 0.08f, VibratoCents = 12, Gain = 0.5f },
        new() { Name = "Lead pulso", Wave = Waveform.Pulse25, Attack = 0.004f, Decay = 0.08f, Sustain = 0.6f, Release = 0.1f, VibratoCents = 15, Gain = 0.5f },
        new() { Name = "Piano suave", Wave = Waveform.Triangle, Wave2 = Waveform.Sine, Mix2 = 0.4f, Octave2 = 1, Attack = 0.002f, Decay = 0.6f, Sustain = 0.15f, Release = 0.35f, Gain = 0.8f },
        new() { Name = "Sino FM", Wave = Waveform.Fm, FmRatio = 3.5f, FmDepth = 2.2f, Attack = 0.001f, Decay = 1.1f, Sustain = 0f, Release = 0.6f, Gain = 0.6f },
        new() { Name = "Pluck", Wave = Waveform.Saw, Attack = 0.001f, Decay = 0.18f, Sustain = 0f, Release = 0.12f, Cutoff = 900, Resonance = 0.4f, FilterEnvelope = 0.8f, Gain = 0.6f },
        new() { Name = "Pad", Wave = Waveform.Saw, Wave2 = Waveform.Saw, Mix2 = 0.8f, DetuneCents = 12, Attack = 0.45f, Decay = 0.5f, Sustain = 0.8f, Release = 0.9f, Cutoff = 2200, Resonance = 0.15f, VibratoCents = 6, Gain = 0.4f },
        new() { Name = "Cordas", Wave = Waveform.Saw, Wave2 = Waveform.Pulse25, Mix2 = 0.5f, DetuneCents = 9, Attack = 0.12f, Decay = 0.3f, Sustain = 0.85f, Release = 0.4f, Cutoff = 3500, VibratoCents = 10, Gain = 0.45f },
        new() { Name = "Baixo", Wave = Waveform.Square, Wave2 = Waveform.Sine, Mix2 = 0.6f, Octave2 = -1, Attack = 0.003f, Decay = 0.15f, Sustain = 0.7f, Release = 0.06f, Cutoff = 700, Resonance = 0.3f, FilterEnvelope = 0.5f, Gain = 0.8f },
        new() { Name = "Baixo synth", Wave = Waveform.Saw, Attack = 0.002f, Decay = 0.2f, Sustain = 0.5f, Release = 0.08f, Cutoff = 400, Resonance = 0.7f, FilterEnvelope = 0.9f, Gain = 0.7f },
        new() { Name = "Flauta", Wave = Waveform.Sine, Wave2 = Waveform.Noise, Mix2 = 0.04f, Attack = 0.06f, Decay = 0.1f, Sustain = 0.85f, Release = 0.15f, VibratoCents = 18, VibratoHz = 5, Gain = 0.7f },
        new() { Name = "Órgão", Wave = Waveform.Sine, Wave2 = Waveform.Sine, Mix2 = 0.6f, Octave2 = 1, Attack = 0.01f, Decay = 0.05f, Sustain = 1f, Release = 0.05f, Gain = 0.55f },
        new() { Name = "Bateria", Drums = true, Gain = 0.9f },
    ];

    public static Instrument Preset(string name) => Presets.FirstOrDefault(p => p.Name == name) ?? Presets[0];
}

/// <summary>A track: an instrument and its mixer strip.</summary>
public sealed record MusicTrack
{
    public string Name { get; init; } = "Faixa";
    public Instrument Instrument { get; init; } = Instrument.Presets[0];
    public float Volume { get; init; } = 0.8f;
    /// <summary>-1 left … +1 right.</summary>
    public float Pan { get; init; }
    public bool Muted { get; init; }
    public bool Solo { get; init; }
    /// <summary>Echo send 0–1 and its time in steps.</summary>
    public float Echo { get; init; }
    public int EchoSteps { get; init; } = 3;
    /// <summary>Room reverb send 0–1.</summary>
    public float Reverb { get; init; }
}

/// <summary>A pattern: a loop of steps with notes for any track. The song plays patterns in <see cref="MusicSong.Order"/>.</summary>
public sealed class MusicPattern
{
    public string Name { get; set; } = "A";
    public int Steps { get; set; } = 16;
    public List<MusicNote> Notes { get; set; } = [];
}

/// <summary>
/// A song of the Music Studio (LUNET-500), saved as <c>*.song.json</c> next to the exported WAV: tracks with synthesized
/// instruments, patterns and an arrangement. The JSON is the editable source; the WAV is derived (like sprite sheets).
/// </summary>
public sealed class MusicSong
{
    public const int MaxTracks = 16;
    public const int MaxPatterns = 64;
    public const int MaxSteps = 256;
    public const int MaxOrder = 256;
    public const int MaxNotes = 20_000;

    public int Version { get; set; } = 1;
    public string Name { get; set; } = "Música";
    public float Bpm { get; set; } = 120;
    /// <summary>Steps per beat: 4 = sixteenth notes.</summary>
    public int StepsPerBeat { get; set; } = 4;
    /// <summary>0–0.5: delays every second step for a groove.</summary>
    public float Swing { get; set; }
    public int Root { get; set; } = 60;
    public string Scale { get; set; } = "major";
    public List<MusicTrack> Tracks { get; set; } = [];
    public List<MusicPattern> Patterns { get; set; } = [];
    public List<int> Order { get; set; } = [];
    /// <summary>Loop the whole song when the game plays it (written as a WAV loop marker).</summary>
    public bool Loop { get; set; } = true;
    public float MasterVolume { get; set; } = 0.9f;

    [JsonIgnore] public double SecondsPerStep => 60.0 / Bpm / StepsPerBeat;
    [JsonIgnore] public int TotalSteps => Order.Sum(i => Patterns[i].Steps);
    [JsonIgnore] public double DurationSeconds => TotalSteps * SecondsPerStep;

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public string ToJson() => JsonSerializer.Serialize(this, Json);

    public static MusicSong Parse(string json)
    {
        var song = JsonSerializer.Deserialize<MusicSong>(json, Json) ?? throw new InvalidDataException("Música vazia.");
        song.Validate();
        return song;
    }

    /// <summary>Rejects songs the renderer cannot play safely (limits keep phones responsive).</summary>
    public void Validate()
    {
        if (Version != 1) throw new InvalidDataException("Versão de música não suportada: " + Version);
        if (!float.IsFinite(Bpm) || Bpm is < 30 or > 300) throw new InvalidDataException("Andamento entre 30 e 300 BPM.");
        if (StepsPerBeat is < 1 or > 8) throw new InvalidDataException("Passos por tempo entre 1 e 8.");
        if (!float.IsFinite(Swing) || Swing is < 0 or > 0.5f) throw new InvalidDataException("Swing entre 0 e 0,5.");
        Tracks ??= []; Patterns ??= []; Order ??= [];
        if (Tracks.Count is 0 or > MaxTracks) throw new InvalidDataException($"De 1 a {MaxTracks} faixas.");
        if (Patterns.Count is 0 or > MaxPatterns) throw new InvalidDataException($"De 1 a {MaxPatterns} padrões.");
        if (Order.Count > MaxOrder) throw new InvalidDataException($"No máximo {MaxOrder} padrões no arranjo.");
        if (Order.Any(i => i < 0 || i >= Patterns.Count)) throw new InvalidDataException("O arranjo cita um padrão que não existe.");
        foreach (var pattern in Patterns)
        {
            pattern.Notes ??= [];
            if (pattern.Steps is < 1 or > MaxSteps) throw new InvalidDataException($"Padrão {pattern.Name}: de 1 a {MaxSteps} passos.");
            foreach (var n in pattern.Notes)
                if (n.Track < 0 || n.Track >= Tracks.Count || n.Step < 0 || n.Step >= pattern.Steps || n.Pitch is < 0 or > 127 ||
                    n.Length < 1 || n.Length > MaxSteps || !float.IsFinite(n.Velocity) || n.Velocity is < 0 or > 1)
                    throw new InvalidDataException($"Nota inválida no padrão {pattern.Name}.");
        }
        if (Patterns.Sum(p => p.Notes.Count) > MaxNotes) throw new InvalidDataException("Notas demais na música.");
        if (DurationSeconds > 600) throw new InvalidDataException("A música passa de 10 minutos; divida em partes.");
    }

    /// <summary>A starter song: lead, chords, bass and drums over a progression, already sounding like music.</summary>
    public static MusicSong Starter(string name, string progressionId = "pop", string scaleId = "major", int root = 60, float bpm = 118, int seed = 1)
    {
        var scale = MusicTheory.Scale(scaleId);
        var progression = MusicTheory.Progressions.FirstOrDefault(p => p.Id == progressionId);
        var degrees = progression.Degrees ?? [1, 5, 6, 4];
        const int stepsPerChord = 16;
        var song = new MusicSong
        {
            Name = name, Bpm = bpm, Root = root, Scale = scale.Id,
            Tracks =
            [
                new() { Name = "Melodia", Instrument = Instrument.Preset("Lead pulso"), Volume = 0.7f, Echo = 0.25f, Reverb = 0.2f },
                new() { Name = "Acordes", Instrument = Instrument.Preset("Pad"), Volume = 0.45f, Pan = -0.2f, Reverb = 0.35f },
                new() { Name = "Baixo", Instrument = Instrument.Preset("Baixo"), Volume = 0.8f },
                new() { Name = "Bateria", Instrument = Instrument.Preset("Bateria"), Volume = 0.8f, Reverb = 0.1f },
            ],
        };
        var intro = new MusicPattern { Name = "Intro", Steps = stepsPerChord * degrees.Length };
        intro.Notes.AddRange(MusicTheory.ChordTrack(root, scale, degrees, stepsPerChord, arpeggio: false).Select(n => n with { Track = 1 }));
        intro.Notes.AddRange(MusicTheory.BassTrack(root, scale, degrees, stepsPerChord).Select(n => n with { Track = 2 }));
        var main = new MusicPattern { Name = "Tema", Steps = stepsPerChord * degrees.Length };
        main.Notes.AddRange(MusicTheory.Melody(seed, root, scale, degrees, stepsPerChord).Select(n => n with { Track = 0 }));
        main.Notes.AddRange(MusicTheory.ChordTrack(root, scale, degrees, stepsPerChord, arpeggio: false).Select(n => n with { Track = 1 }));
        main.Notes.AddRange(MusicTheory.BassTrack(root, scale, degrees, stepsPerChord).Select(n => n with { Track = 2 }));
        main.Notes.AddRange(MusicTheory.Groove("rock", main.Steps).Select(n => n with { Track = 3 }));
        song.Patterns.Add(intro);
        song.Patterns.Add(main);
        song.Order.AddRange([0, 1, 1]);
        song.Validate();
        return song;
    }

    /// <summary>An empty song with one lead track and one 16-step pattern.</summary>
    public static MusicSong Empty(string name) => new()
    {
        Name = name,
        Tracks = [new() { Name = "Melodia", Instrument = Instrument.Presets[0] }],
        Patterns = [new MusicPattern { Name = "A", Steps = 16 }],
        Order = [0],
    };
}
