namespace Lunet.Studio.Music;

/// <summary>A named scale as semitone offsets from the root.</summary>
public sealed record MusicScale(string Id, string Name, int[] Intervals);

/// <summary>A named chord quality as semitone offsets from the root.</summary>
public sealed record ChordQuality(string Id, string Symbol, int[] Intervals);

/// <summary>
/// Music theory for the Music Studio (LUNET-500): note names, scales, chords, progressions in Roman numerals, a seeded
/// melody generator and drum grooves, so a person without music training can compose something that sounds right.
/// </summary>
public static class MusicTheory
{
    public static readonly string[] NoteNames = ["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"];
    public static readonly string[] NoteNamesPt = ["Dó", "Dó#", "Ré", "Ré#", "Mi", "Fá", "Fá#", "Sol", "Sol#", "Lá", "Lá#", "Si"];

    public static IReadOnlyList<MusicScale> Scales { get; } =
    [
        new("major", "Maior (alegre)", [0, 2, 4, 5, 7, 9, 11]),
        new("minor", "Menor natural (triste, épica)", [0, 2, 3, 5, 7, 8, 10]),
        new("harmonic-minor", "Menor harmônica (mistério)", [0, 2, 3, 5, 7, 8, 11]),
        new("dorian", "Dórico (aventura)", [0, 2, 3, 5, 7, 9, 10]),
        new("mixolydian", "Mixolídio (heroico)", [0, 2, 4, 5, 7, 9, 10]),
        new("phrygian", "Frígio (tenso, deserto)", [0, 1, 3, 5, 7, 8, 10]),
        new("lydian", "Lídio (sonho, magia)", [0, 2, 4, 6, 7, 9, 11]),
        new("major-pentatonic", "Pentatônica maior (fácil, sem notas erradas)", [0, 2, 4, 7, 9]),
        new("minor-pentatonic", "Pentatônica menor (rock, blues)", [0, 3, 5, 7, 10]),
        new("blues", "Blues", [0, 3, 5, 6, 7, 10]),
        new("chromatic", "Cromática (todas as notas)", [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11]),
    ];

    public static IReadOnlyList<ChordQuality> Chords { get; } =
    [
        new("maj", "", [0, 4, 7]), new("min", "m", [0, 3, 7]), new("dim", "°", [0, 3, 6]), new("aug", "+", [0, 4, 8]),
        new("sus2", "sus2", [0, 2, 7]), new("sus4", "sus4", [0, 5, 7]), new("maj7", "maj7", [0, 4, 7, 11]),
        new("min7", "m7", [0, 3, 7, 10]), new("dom7", "7", [0, 4, 7, 10]), new("power", "5", [0, 7, 12]),
    ];

    /// <summary>Well-known progressions written in scale degrees (1-based), with a mood.</summary>
    public static IReadOnlyList<(string Id, string Name, int[] Degrees)> Progressions { get; } =
    [
        ("pop", "I–V–vi–IV (pop, otimista)", [1, 5, 6, 4]),
        ("epic", "vi–IV–I–V (épico)", [6, 4, 1, 5]),
        ("fifties", "I–vi–IV–V (clássico)", [1, 6, 4, 5]),
        ("sad", "i–VI–III–VII (menor, aventura)", [1, 6, 3, 7]),
        ("tension", "i–iv–V–i (tensão e resolução)", [1, 4, 5, 1]),
        ("jazz", "ii–V–I (jazz)", [2, 5, 1, 1]),
        ("loop", "I–IV (loop calmo)", [1, 4, 1, 4]),
    ];

    public static MusicScale Scale(string id) => Scales.FirstOrDefault(s => s.Id == id) ?? Scales[0];

    public static double Frequency(int midi) => 440.0 * Math.Pow(2, (midi - 69) / 12.0);

    /// <summary>"C4" is MIDI 60.</summary>
    public static string Name(int midi, bool portuguese = false) =>
        (portuguese ? NoteNamesPt : NoteNames)[((midi % 12) + 12) % 12] + (midi / 12 - 1);

    public static int? Parse(string text)
    {
        var t = (text ?? "").Trim();
        if (t.Length < 2) return null;
        int length = t.Length > 2 && t[1] == '#' ? 2 : 1;
        var index = Array.IndexOf(NoteNames, t[..length].ToUpperInvariant());
        if (index < 0 || !int.TryParse(t[length..], out var octave) || octave is < -1 or > 9) return null;
        return (octave + 1) * 12 + index;
    }

    public static bool InScale(int midi, int root, MusicScale scale) =>
        scale.Intervals.Contains((((midi - root) % 12) + 12) % 12);

    /// <summary>The notes of the scale between two MIDI notes, inclusive, low to high.</summary>
    public static int[] ScaleNotes(int root, MusicScale scale, int low, int high) =>
        Enumerable.Range(low, Math.Max(0, high - low + 1)).Where(n => InScale(n, root, scale)).ToArray();

    /// <summary>Nearest note of the scale (ties go down), to snap a tapped pitch.</summary>
    public static int Snap(int midi, int root, MusicScale scale)
    {
        for (int d = 0; d < 12; d++)
        {
            if (InScale(midi - d, root, scale)) return midi - d;
            if (InScale(midi + d, root, scale)) return midi + d;
        }
        return midi;
    }

    /// <summary>The diatonic triad (or seventh) on a 1-based degree of the scale, as MIDI notes from <paramref name="octaveRoot"/>.</summary>
    public static int[] DiatonicChord(int octaveRoot, MusicScale scale, int degree, bool seventh = false)
    {
        // Chords are built in thirds of a seven-note scale; pentatonic, blues and chromatic borrow their parent scale.
        if (scale.Intervals.Length != 7)
            scale = Scale(scale.Id is "minor-pentatonic" or "blues" ? "minor" : "major");
        var count = scale.Intervals.Length;
        var notes = new List<int>();
        for (int k = 0; k < (seventh ? 4 : 3); k++)
        {
            int index = degree - 1 + 2 * k;
            notes.Add(octaveRoot + scale.Intervals[index % count] + 12 * (index / count));
        }
        return notes.ToArray();
    }

    public static int[] Chord(int root, ChordQuality quality) => quality.Intervals.Select(i => root + i).ToArray();

    /// <summary>Chord name like "Am" or "G7" for a set of notes built on a root.</summary>
    public static string ChordName(int[] notes)
    {
        if (notes.Length == 0) return "";
        var root = notes.Min();
        var intervals = notes.Select(n => n - root).OrderBy(i => i).ToArray();
        var quality = Chords.FirstOrDefault(c => c.Intervals.SequenceEqual(intervals));
        return NoteNames[((root % 12) + 12) % 12] + (quality?.Symbol ?? "?");
    }

    /// <summary>
    /// A melody over a progression: chord tones on strong beats, scale steps in between, mostly stepwise motion with a few
    /// leaps, phrases resolving to the root at the end. Deterministic for a seed.
    /// </summary>
    public static IReadOnlyList<MusicNote> Melody(int seed, int root, MusicScale scale, int[] degrees, int stepsPerChord,
        int low = 60, int high = 79, double density = 0.6)
    {
        var random = new Random(seed);
        var notes = new List<MusicNote>();
        var pool = ScaleNotes(root, scale, low, high);
        if (pool.Length == 0) return notes;
        int current = pool[pool.Length / 2];
        for (int c = 0; c < degrees.Length; c++)
        {
            var chord = DiatonicChord(root, scale, degrees[c]).Select(n => ((n % 12) + 12) % 12).ToHashSet();
            int start = c * stepsPerChord;
            int step = 0;
            while (step < stepsPerChord)
            {
                bool strong = step % 4 == 0;
                bool last = c == degrees.Length - 1 && step >= stepsPerChord - 4;
                if (!strong && random.NextDouble() > density) { step++; continue; }
                int target;
                if (last) target = pool.Where(n => ((n - root) % 12 + 12) % 12 == 0).OrderBy(n => Math.Abs(n - current)).First();
                else if (strong)
                {
                    var tones = pool.Where(n => chord.Contains(((n % 12) + 12) % 12)).ToArray();
                    target = tones.OrderBy(n => Math.Abs(n - current) + random.Next(0, 4)).First();
                }
                else
                {
                    int index = Array.IndexOf(pool, current);
                    if (index < 0) index = pool.Length / 2;
                    int move = random.NextDouble() < 0.8 ? (random.Next(2) == 0 ? -1 : 1) : random.Next(-3, 4);
                    target = pool[Math.Clamp(index + move, 0, pool.Length - 1)];
                }
                int length = last ? stepsPerChord - step : strong && random.NextDouble() < 0.4 ? 2 : 1;
                length = Math.Min(length, stepsPerChord - step);
                notes.Add(new MusicNote(0, start + step, target, length, strong ? 0.9f : 0.75f));
                current = target;
                step += length;
            }
        }
        return notes;
    }

    /// <summary>Block chords or arpeggios of a progression.</summary>
    public static IReadOnlyList<MusicNote> ChordTrack(int root, MusicScale scale, int[] degrees, int stepsPerChord, bool arpeggio, int octave = 4)
    {
        var notes = new List<MusicNote>();
        int baseNote = (octave + 1) * 12 + (((root % 12) + 12) % 12);
        for (int c = 0; c < degrees.Length; c++)
        {
            var chord = DiatonicChord(baseNote, scale, degrees[c]);
            int start = c * stepsPerChord;
            if (!arpeggio)
                foreach (var n in chord) notes.Add(new MusicNote(0, start, n, stepsPerChord, 0.6f));
            else
                for (int s = 0; s < stepsPerChord; s++)
                    notes.Add(new MusicNote(0, start + s, chord[s % chord.Length] + (s / chord.Length % 2 == 1 ? 12 : 0), 1, s % 4 == 0 ? 0.75f : 0.55f));
        }
        return notes;
    }

    /// <summary>Root notes of each chord in the bass register, on beats.</summary>
    public static IReadOnlyList<MusicNote> BassTrack(int root, MusicScale scale, int[] degrees, int stepsPerChord, bool walking = false)
    {
        var notes = new List<MusicNote>();
        int baseNote = 36 + (((root % 12) + 12) % 12);
        for (int c = 0; c < degrees.Length; c++)
        {
            var chord = DiatonicChord(baseNote, scale, degrees[c]);
            for (int s = 0; s < stepsPerChord; s += 4)
            {
                int note = walking ? chord[s / 4 % chord.Length] : s % 8 == 4 ? chord[0] + 12 : chord[0];
                notes.Add(new MusicNote(0, c * stepsPerChord + s, note, s % 8 == 0 ? 3 : 2, s == 0 ? 0.95f : 0.8f));
            }
        }
        return notes;
    }

    // ---------- Drums ----------

    /// <summary>Drum lanes of a drum track: the "pitch" of a drum note selects the sound.</summary>
    public static IReadOnlyList<(int Pitch, string Name)> DrumLanes { get; } =
    [
        (36, "Bumbo"), (38, "Caixa"), (39, "Palmas"), (42, "Chimbal fechado"), (46, "Chimbal aberto"), (49, "Prato"), (45, "Tom"),
    ];

    public static IReadOnlyList<(string Id, string Name, (int Pitch, string Pattern)[] Lanes)> Grooves { get; } =
    [
        ("rock", "Rock", [(36, "x.......x.x....."), (38, "....x.......x..."), (42, "x.x.x.x.x.x.x.x.")]),
        ("four", "Batida reta (dance)", [(36, "x...x...x...x..."), (39, "....x.......x..."), (46, "..x...x...x...x.")]),
        ("chip", "Chiptune", [(36, "x.....x...x....."), (38, "....x.......x..x"), (42, "x.xxx.xxx.xxx.xx")]),
        ("halftime", "Meio tempo (épico)", [(36, "x.........x....."), (38, "........x......."), (42, "x...x...x...x...")]),
        ("shuffle", "Suingado", [(36, "x.....x...x....."), (38, "....x.......x..."), (42, "x..x.xx..x.xx..x")]),
        ("march", "Marcha", [(36, "x.......x......."), (38, "x.xxx.x.x.xxx.x."), (49, "x...............")]),
    ];

    /// <summary>The groove repeated to <paramref name="steps"/> steps (16-step patterns tile).</summary>
    public static IReadOnlyList<MusicNote> Groove(string id, int steps)
    {
        var groove = Grooves.FirstOrDefault(g => g.Id == id);
        if (groove.Lanes is null) throw new ArgumentException("Batida desconhecida: " + id);
        var notes = new List<MusicNote>();
        foreach (var (pitch, pattern) in groove.Lanes)
            for (int s = 0; s < steps; s++)
                if (pattern[s % pattern.Length] == 'x')
                    notes.Add(new MusicNote(0, s, pitch, 1, s % 4 == 0 ? 0.95f : 0.7f));
        return notes;
    }
}
