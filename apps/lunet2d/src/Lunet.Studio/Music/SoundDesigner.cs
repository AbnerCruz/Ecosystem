namespace Lunet.Studio.Music;

/// <summary>
/// Parameters of a designed sound effect (LUNET-500): a classic game-SFX voice with frequency slides, arpeggio jump,
/// vibrato, duty sweep, punchy envelope, filters, repeat and bit crush. Saved as <c>*.sfx.json</c> beside the WAV.
/// </summary>
public sealed record SoundEffect
{
    public string Name { get; init; } = "Efeito";
    public Waveform Wave { get; init; } = Waveform.Square;
    public float StartHz { get; init; } = 440;
    /// <summary>Semitones per second of pitch slide (negative = falling) and its change over time.</summary>
    public float Slide { get; init; }
    public float SlideAcceleration { get; init; }
    public float MinHz { get; init; } = 30;
    /// <summary>Pitch jump (semitones) after <see cref="ArpeggioTime"/> seconds: coins and power-ups.</summary>
    public float ArpeggioSemitones { get; init; }
    public float ArpeggioTime { get; init; } = 0.08f;
    public float VibratoHz { get; init; } = 8;
    public float VibratoDepth { get; init; }
    /// <summary>Pulse width 0.05–0.5 and its change per second.</summary>
    public float Duty { get; init; } = 0.5f;
    public float DutySweep { get; init; }
    public float Attack { get; init; }
    public float Sustain { get; init; } = 0.12f;
    /// <summary>Extra loudness at the start of the sustain (0–1).</summary>
    public float Punch { get; init; }
    public float Decay { get; init; } = 0.2f;
    public float LowPass { get; init; } = 20000;
    public float HighPass { get; init; }
    /// <summary>Restarts the pitch every N seconds (0 = off): alarms, engines.</summary>
    public float Repeat { get; init; }
    /// <summary>Reduces resolution for crunchy retro sounds (0 = clean, 1 = very crushed).</summary>
    public float Crush { get; init; }
    public float Volume { get; init; } = 0.7f;

    public double Duration => Attack + Sustain + Decay;
}

/// <summary>Renders <see cref="SoundEffect"/>s, offers presets per category and seeded variations.</summary>
public static class SoundDesigner
{
    public const int SampleRate = 44_100;

    public static IReadOnlyList<(string Id, string Name)> Categories { get; } =
    [
        ("coin", "Moeda / item"), ("laser", "Tiro / laser"), ("explosion", "Explosão"), ("powerup", "Power-up"),
        ("hit", "Dano / impacto"), ("jump", "Pulo"), ("blip", "Menu / clique"), ("magic", "Magia"), ("alarm", "Alarme"),
    ];

    /// <summary>A sound of the category; <paramref name="seed"/> varies it (same seed, same sound).</summary>
    public static SoundEffect Generate(string category, int seed)
    {
        var r = new Random(seed);
        float F(float min, float max) => (float)(min + r.NextDouble() * (max - min));
        Waveform Pick(params Waveform[] options) => options[r.Next(options.Length)];
        return category switch
        {
            "coin" => new SoundEffect { Name = "Moeda", Wave = Pick(Waveform.Square, Waveform.Pulse25, Waveform.Triangle), StartHz = F(700, 1400),
                ArpeggioSemitones = r.Next(2) == 0 ? 5 : 7, ArpeggioTime = F(0.04f, 0.09f), Sustain = F(0.05f, 0.12f),
                Punch = F(0.2f, 0.5f), Decay = F(0.15f, 0.35f), Volume = 0.6f },
            "laser" => new SoundEffect { Name = "Laser", Wave = Pick(Waveform.Saw, Waveform.Square, Waveform.Pulse12), StartHz = F(900, 2200),
                Slide = F(-60, -25), MinHz = F(80, 250), Duty = F(0.2f, 0.5f), DutySweep = F(-1, 1), Sustain = F(0.05f, 0.15f), Decay = F(0.05f, 0.2f),
                Punch = F(0, 0.3f), Volume = 0.55f },
            "explosion" => new SoundEffect { Name = "Explosão", Wave = Waveform.Noise, StartHz = F(60, 200), Slide = F(-12, -3), Sustain = F(0.1f, 0.3f),
                Punch = F(0.4f, 0.8f), Decay = F(0.4f, 0.9f), LowPass = F(1500, 5000), Crush = F(0, 0.4f), Volume = 0.8f },
            "powerup" => new SoundEffect { Name = "Power-up", Wave = Pick(Waveform.Square, Waveform.Triangle, Waveform.Saw), StartHz = F(250, 500),
                Slide = F(10, 30), VibratoDepth = F(0, 0.3f), VibratoHz = F(6, 14), Repeat = r.NextDouble() < 0.5 ? F(0.08f, 0.16f) : 0,
                Sustain = F(0.25f, 0.45f), Decay = F(0.1f, 0.3f), Volume = 0.55f },
            "hit" => new SoundEffect { Name = "Impacto", Wave = Pick(Waveform.Noise, Waveform.Square, Waveform.Saw), StartHz = F(150, 500),
                Slide = F(-40, -15), Sustain = F(0.02f, 0.06f), Punch = F(0.3f, 0.7f), Decay = F(0.08f, 0.2f), LowPass = F(2000, 8000), Volume = 0.7f },
            "jump" => new SoundEffect { Name = "Pulo", Wave = Pick(Waveform.Square, Waveform.Pulse25), StartHz = F(250, 450), Slide = F(12, 28),
                Duty = F(0.25f, 0.5f), Sustain = F(0.08f, 0.15f), Decay = F(0.08f, 0.2f), Volume = 0.55f },
            "blip" => new SoundEffect { Name = "Clique", Wave = Pick(Waveform.Square, Waveform.Sine, Waveform.Triangle), StartHz = F(500, 1500),
                Sustain = F(0.02f, 0.05f), Decay = F(0.02f, 0.08f), HighPass = F(0, 200), Volume = 0.5f },
            "magic" => new SoundEffect { Name = "Magia", Wave = Pick(Waveform.Sine, Waveform.Triangle, Waveform.Fm), StartHz = F(500, 900), Slide = F(-6, 10),
                VibratoDepth = F(0.3f, 0.8f), VibratoHz = F(10, 22), ArpeggioSemitones = F(3, 12), ArpeggioTime = F(0.1f, 0.25f), Attack = F(0.02f, 0.1f),
                Sustain = F(0.25f, 0.5f), Decay = F(0.3f, 0.6f), Volume = 0.55f },
            "alarm" => new SoundEffect { Name = "Alarme", Wave = Pick(Waveform.Square, Waveform.Saw), StartHz = F(500, 900), Slide = F(-20, 20),
                Repeat = F(0.12f, 0.25f), Sustain = F(0.6f, 1.0f), Decay = F(0.05f, 0.15f), Volume = 0.5f },
            _ => throw new ArgumentException("Categoria desconhecida: " + category),
        };
    }

    /// <summary>A nearby variation: same character, slightly different (for "give me another one like this").</summary>
    public static SoundEffect Mutate(SoundEffect effect, int seed, float amount = 0.15f)
    {
        var r = new Random(seed);
        float M(float v, float min, float max) => Math.Clamp(v * (1 + (float)(r.NextDouble() * 2 - 1) * amount) +
                                                             (float)(r.NextDouble() * 2 - 1) * amount * 0.01f * (max - min), min, max);
        return effect with
        {
            StartHz = M(effect.StartHz, 20, 8000), Slide = M(effect.Slide, -200, 200), Sustain = M(effect.Sustain, 0, 3),
            Decay = M(effect.Decay, 0, 3), Punch = M(effect.Punch, 0, 1), Duty = M(effect.Duty, 0.05f, 0.5f),
            VibratoDepth = M(effect.VibratoDepth, 0, 1), LowPass = M(effect.LowPass, 100, 20000),
        };
    }

    /// <summary>Validates ranges (the file is editable by hand and by the AI).</summary>
    public static void Validate(SoundEffect e)
    {
        static bool Ok(float v, float min, float max) => float.IsFinite(v) && v >= min && v <= max;
        if (!Ok(e.StartHz, 20, 8000) || !Ok(e.MinHz, 10, 8000) || !Ok(e.Slide, -400, 400) || !Ok(e.SlideAcceleration, -400, 400) ||
            !Ok(e.ArpeggioSemitones, -24, 24) || !Ok(e.ArpeggioTime, 0, 3) || !Ok(e.VibratoHz, 0, 60) || !Ok(e.VibratoDepth, 0, 1) ||
            !Ok(e.Duty, 0.05f, 0.5f) || !Ok(e.DutySweep, -2, 2) || !Ok(e.Attack, 0, 3) || !Ok(e.Sustain, 0, 3) || !Ok(e.Punch, 0, 1) ||
            !Ok(e.Decay, 0, 3) || !Ok(e.LowPass, 50, 20000) || !Ok(e.HighPass, 0, 8000) || !Ok(e.Repeat, 0, 3) || !Ok(e.Crush, 0, 1) ||
            !Ok(e.Volume, 0, 1) || e.Duration is < 0.01 or > 8)
            throw new InvalidDataException("Parâmetros do efeito fora dos limites.");
    }

    public static float[] Render(SoundEffect e)
    {
        Validate(e);
        int frames = (int)Math.Ceiling(e.Duration * SampleRate);
        var output = new float[frames];
        double phase = 0, low = 0, highState = 0, lastInput = 0, held = 0;
        uint noise = 0x6C8E9CF5u;
        double noiseValue = 0;
        double lowCoefficient = 1 - Math.Exp(-2 * Math.PI * e.LowPass / SampleRate);
        double highCoefficient = e.HighPass <= 0 ? 1 : Math.Exp(-2 * Math.PI * e.HighPass / SampleRate);
        int crushStep = e.Crush <= 0 ? 1 : 1 + (int)(e.Crush * 15);
        double crushLevels = e.Crush <= 0 ? 0 : Math.Pow(2, 16 - e.Crush * 12);
        for (int i = 0; i < frames; i++)
        {
            double t = i / (double)SampleRate;
            double local = e.Repeat > 0 ? t % e.Repeat : t;
            double semitones = e.Slide * local + 0.5 * e.SlideAcceleration * local * local;
            if (e.ArpeggioSemitones != 0 && local >= e.ArpeggioTime) semitones += e.ArpeggioSemitones;
            if (e.VibratoDepth > 0) semitones += e.VibratoDepth * 2 * Math.Sin(2 * Math.PI * e.VibratoHz * t);
            double frequency = Math.Max(e.MinHz, e.StartHz * Math.Pow(2, semitones / 12));
            if (frequency > 12_000) frequency = 12_000;
            double dt = frequency / SampleRate;
            double previous = phase;
            phase = (phase + dt) % 1;
            double sample;
            switch (e.Wave)
            {
                case Waveform.Noise:
                    if (phase < previous) { noise ^= noise << 13; noise ^= noise >> 17; noise ^= noise << 5; noiseValue = noise / (double)uint.MaxValue * 2 - 1; }
                    sample = noiseValue;
                    break;
                case Waveform.Sine: sample = Math.Sin(2 * Math.PI * phase); break;
                case Waveform.Triangle: sample = 1 - 4 * Math.Abs(phase - 0.5); break;
                case Waveform.Saw: sample = 2 * phase - 1; break;
                case Waveform.Fm: sample = Math.Sin(2 * Math.PI * phase + 2 * Math.Sin(2 * Math.PI * phase * 3)); break;
                default:
                    double duty = Math.Clamp(e.Duty + e.DutySweep * t, 0.05, 0.5);
                    if (e.Wave == Waveform.Pulse25) duty = Math.Min(duty, 0.25);
                    if (e.Wave == Waveform.Pulse12) duty = Math.Min(duty, 0.125);
                    sample = phase < duty ? 0.8 : -0.8;
                    break;
            }
            double envelope = t < e.Attack ? t / Math.Max(1e-4, e.Attack)
                : t < e.Attack + e.Sustain ? 1 + e.Punch * (1 - (t - e.Attack) / Math.Max(1e-4, e.Sustain))
                : Math.Max(0, 1 - (t - e.Attack - e.Sustain) / Math.Max(1e-4, e.Decay));
            low += lowCoefficient * (sample - low);
            sample = low;
            if (e.HighPass > 0) { highState = highCoefficient * (highState + sample - lastInput); lastInput = sample; sample = highState; }
            if (crushStep > 1) { if (i % crushStep == 0) held = sample; sample = held; }
            if (crushLevels > 0) sample = Math.Round(sample * crushLevels) / crushLevels;
            output[i] = (float)Math.Clamp(sample * envelope * e.Volume, -1, 1);
        }
        // Tiny fade-out avoids a click at the end.
        int fade = Math.Min(frames, SampleRate / 200);
        for (int i = 0; i < fade; i++) output[frames - 1 - i] *= i / (float)fade;
        return output;
    }

    public static byte[] RenderWav(SoundEffect e) => WavEncoder.Encode(Render(e), 1, SampleRate);

    private static readonly System.Text.Json.JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };

    public static string ToJson(SoundEffect e) => System.Text.Json.JsonSerializer.Serialize(e, Json);

    public static SoundEffect Parse(string json)
    {
        var effect = System.Text.Json.JsonSerializer.Deserialize<SoundEffect>(json, Json) ?? throw new InvalidDataException("Efeito vazio.");
        Validate(effect);
        return effect;
    }
}
