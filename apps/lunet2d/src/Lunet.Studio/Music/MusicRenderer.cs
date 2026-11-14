namespace Lunet.Studio.Music;

/// <summary>PCM16 WAV encoding shared by the Music Studio and the sound designer.</summary>
public static class WavEncoder
{
    /// <summary>Interleaved float samples in [-1,1] to a RIFF/WAVE PCM16 file, optionally with a SMPL loop over the whole sound.</summary>
    public static byte[] Encode(ReadOnlySpan<float> interleaved, int channels, int sampleRate, bool loop = false)
    {
        if (channels is not (1 or 2)) throw new ArgumentOutOfRangeException(nameof(channels));
        int frames = interleaved.Length / channels;
        int dataSize = checked(frames * channels * 2);
        bool marker = loop && frames > 2;
        using var stream = new MemoryStream(checked(44 + dataSize + (marker ? 68 : 0)));
        using var writer = new BinaryWriter(stream);
        writer.Write("RIFF"u8); writer.Write(checked(36 + dataSize + (marker ? 68 : 0)));
        writer.Write("WAVEfmt "u8); writer.Write(16); writer.Write((short)1);
        writer.Write((short)channels); writer.Write(sampleRate);
        writer.Write(sampleRate * channels * 2); writer.Write((short)(channels * 2)); writer.Write((short)16);
        if (marker)
        {
            writer.Write("smpl"u8); writer.Write(60);
            writer.Write(0); writer.Write(0); writer.Write((uint)Math.Round(1_000_000_000.0 / sampleRate));
            writer.Write(60); writer.Write(0); writer.Write(0); writer.Write(0); writer.Write(1); writer.Write(0);
            writer.Write(0); writer.Write(0); writer.Write(0); writer.Write(frames - 1); writer.Write(0); writer.Write(0);
        }
        writer.Write("data"u8); writer.Write(dataSize);
        foreach (var sample in interleaved)
            writer.Write((short)Math.Clamp((int)Math.Round(sample * 32767f), short.MinValue, short.MaxValue));
        return stream.ToArray();
    }
}

/// <summary>
/// Offline renderer of a <see cref="MusicSong"/> (LUNET-500): band-limited oscillators (polyBLEP), FM, ADSR, resonant
/// low-pass with envelope, vibrato and slides, synthesized drum kit, per-track pan/volume, echo and room reverb sends,
/// swing and a soft limiter. Deterministic: the same song always renders the same WAV.
/// </summary>
public sealed class MusicRenderer
{
    public const int DefaultSampleRate = 44_100;
    private readonly int _rate;

    public MusicRenderer(int sampleRate = DefaultSampleRate)
    {
        if (sampleRate is < 8000 or > 96_000) throw new ArgumentOutOfRangeException(nameof(sampleRate));
        _rate = sampleRate;
    }

    /// <summary>Renders the song (or one pattern looped once when <paramref name="onlyPattern"/> is set) to interleaved stereo floats.</summary>
    public float[] Render(MusicSong song, int? onlyPattern = null, CancellationToken cancellationToken = default)
    {
        song.Validate();
        var order = onlyPattern is { } p ? [p] : song.Order.ToArray();
        if (order.Length == 0) return [];
        double stepSeconds = song.SecondsPerStep;
        int totalSteps = order.Sum(i => song.Patterns[i].Steps);
        double tail = 2.5; // releases, echoes and reverb ring out
        int frames = (int)Math.Ceiling((totalSteps * stepSeconds + tail) * _rate);
        var left = new float[frames];
        var right = new float[frames];
        var reverbBus = new float[frames];
        bool anySolo = song.Tracks.Any(t => t.Solo);

        for (int t = 0; t < song.Tracks.Count; t++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var track = song.Tracks[t];
            if (track.Muted || anySolo && !track.Solo) continue;
            var dry = new float[frames];
            int offset = 0;
            foreach (var index in order)
            {
                var pattern = song.Patterns[index];
                foreach (var note in pattern.Notes.Where(n => n.Track == t))
                {
                    int step = offset + note.Step;
                    double start = step * stepSeconds + (step % 2 == 1 ? song.Swing * stepSeconds : 0);
                    double gate = Math.Max(0.02, note.Length * stepSeconds - 0.004);
                    if (track.Instrument.Drums) Drum(dry, (int)(start * _rate), note.Pitch, note.Velocity);
                    else Voice(dry, (int)(start * _rate), gate, note.Pitch, note.Velocity, track.Instrument);
                }
                offset += pattern.Steps;
            }
            // Mixer strip: equal-power pan, echo send (ping-pong-free feedback delay), reverb send.
            float gain = track.Volume * track.Instrument.Gain;
            double angle = (Math.Clamp(track.Pan, -1, 1) + 1) * Math.PI / 4;
            float gl = (float)Math.Cos(angle) * gain, gr = (float)Math.Sin(angle) * gain;
            if (track.Echo > 0) ApplyEcho(dry, (int)(Math.Max(1, track.EchoSteps) * stepSeconds * _rate), Math.Clamp(track.Echo, 0, 1));
            float send = Math.Clamp(track.Reverb, 0, 1) * gain;
            for (int i = 0; i < frames; i++)
            {
                var s = dry[i];
                if (s == 0) continue;
                left[i] += s * gl;
                right[i] += s * gr;
                reverbBus[i] += s * send;
            }
        }

        if (reverbBus.Any(v => v != 0))
        {
            var (wetL, wetR) = Reverb(reverbBus);
            for (int i = 0; i < frames; i++) { left[i] += wetL[i]; right[i] += wetR[i]; }
        }

        // Trim silence after the tail, then soft-limit.
        int last = frames - 1;
        while (last > 0 && Math.Abs(left[last]) < 1e-4f && Math.Abs(right[last]) < 1e-4f) last--;
        int length = song.Loop && onlyPattern is null ? (int)Math.Round(totalSteps * stepSeconds * _rate) : last + 1;
        if (song.Loop && onlyPattern is null)
        {
            // A looping track wraps its tail onto the beginning, so the loop point is seamless.
            for (int i = length; i <= last; i++) { left[i - length] += left[i]; right[i - length] += right[i]; }
        }
        length = Math.Clamp(length, 1, frames);
        var output = new float[length * 2];
        float master = Math.Clamp(song.MasterVolume, 0, 1.5f);
        for (int i = 0; i < length; i++)
        {
            output[2 * i] = SoftClip(left[i] * master);
            output[2 * i + 1] = SoftClip(right[i] * master);
        }
        return output;
    }

    public byte[] RenderWav(MusicSong song, int? onlyPattern = null, CancellationToken cancellationToken = default) =>
        WavEncoder.Encode(Render(song, onlyPattern, cancellationToken), 2, _rate, loop: song.Loop && onlyPattern is null);

    /// <summary>Linear below 0.6, then compresses smoothly toward ±1 (never clips hard).</summary>
    private static float SoftClip(float x)
    {
        if (!float.IsFinite(x)) return 0;
        if (x is > -0.6f and < 0.6f) return x;
        float magnitude = 0.6f + 0.4f * MathF.Tanh((Math.Abs(x) - 0.6f) / 0.4f);
        return Math.Sign(x) * magnitude;
    }

    // ---------- Oscillators ----------

    private static double PolyBlep(double t, double dt)
    {
        if (t < dt) { t /= dt; return t + t - t * t - 1; }
        if (t > 1 - dt) { t = (t - 1) / dt; return t * t + t + t + 1; }
        return 0;
    }

    private static double Osc(Waveform wave, double phase, double dt, ref uint noise)
    {
        switch (wave)
        {
            case Waveform.Sine: return Math.Sin(2 * Math.PI * phase);
            case Waveform.Triangle: return 1 - 4 * Math.Abs(phase - 0.5);
            case Waveform.Saw: return 2 * phase - 1 - PolyBlep(phase, dt);
            case Waveform.Noise:
                noise ^= noise << 13; noise ^= noise >> 17; noise ^= noise << 5;
                return noise / (double)uint.MaxValue * 2 - 1;
            default:
            {
                double width = wave switch { Waveform.Pulse25 => 0.25, Waveform.Pulse12 => 0.125, _ => 0.5 };
                double v = phase < width ? 1 : -1;
                v += PolyBlep(phase, dt);
                v -= PolyBlep((phase - width + 1) % 1, dt);
                return v * 0.8;
            }
        }
    }

    private void Voice(float[] buffer, int start, double gate, int pitch, float velocity, Instrument instrument)
    {
        double attack = Math.Max(0.001, instrument.Attack), decay = Math.Max(0.001, instrument.Decay);
        double sustain = Math.Clamp(instrument.Sustain, 0, 1), release = Math.Max(0.005, instrument.Release);
        int total = (int)((gate + release) * _rate);
        double baseFrequency = MusicTheory.Frequency(pitch);
        double phase1 = 0, phase2 = 0, fmPhase = 0;
        double ratio2 = Math.Pow(2, instrument.Octave2 + instrument.DetuneCents / 1200.0);
        uint noise = 0x9E3779B9u ^ (uint)(pitch * 7919 + start);
        double low = 0, band = 0;
        double resonance = Math.Clamp(instrument.Resonance, 0, 0.95);
        double releaseLevel = 0;
        for (int i = 0; i < total; i++)
        {
            int index = start + i;
            if (index < 0) continue;
            if (index >= buffer.Length) break;
            double t = i / (double)_rate;
            double env;
            if (t < gate)
            {
                env = t < attack ? t / attack : t < attack + decay ? 1 - (1 - sustain) * (t - attack) / decay : sustain;
                releaseLevel = env;
            }
            else env = releaseLevel * Math.Max(0, 1 - (t - gate) / release);
            if (env <= 0 && t >= gate) break;

            double slide = instrument.SlideSemitones != 0 && t < instrument.SlideTime
                ? instrument.SlideSemitones * (1 - t / Math.Max(0.001, instrument.SlideTime)) : 0;
            double vibrato = instrument.VibratoCents > 0 && t > 0.12
                ? instrument.VibratoCents / 100.0 * Math.Sin(2 * Math.PI * instrument.VibratoHz * t) * Math.Min(1, (t - 0.12) / 0.2) : 0;
            double frequency = baseFrequency * Math.Pow(2, (slide + vibrato) / 12.0);
            double dt = frequency / _rate;

            double sample;
            if (instrument.Wave == Waveform.Fm)
            {
                fmPhase = (fmPhase + dt * instrument.FmRatio) % 1;
                sample = Math.Sin(2 * Math.PI * phase1 + instrument.FmDepth * (0.3 + 0.7 * env) * Math.Sin(2 * Math.PI * fmPhase));
            }
            else sample = Osc(instrument.Wave, phase1, dt, ref noise);
            if (instrument.Mix2 > 0)
                sample = sample * (1 - instrument.Mix2 * 0.5) + Osc(instrument.Wave2, phase2, dt * ratio2, ref noise) * instrument.Mix2 * 0.5;
            phase1 = (phase1 + dt) % 1;
            phase2 = (phase2 + dt * ratio2) % 1;

            if (instrument.Cutoff < 19_000 || instrument.FilterEnvelope > 0)
            {
                double cutoff = instrument.Cutoff + instrument.FilterEnvelope * env * 8000;
                double f = 2 * Math.Sin(Math.PI * Math.Min(cutoff, _rate / 6.5) / _rate);
                double q = 1 - resonance;
                low += f * band;
                double high = sample - low - q * band;
                band += f * high;
                sample = low;
            }
            buffer[index] += (float)(sample * env * velocity);
        }
    }

    // ---------- Drums ----------

    private void Drum(float[] buffer, int start, int pitch, float velocity)
    {
        uint noise = 0x2545F491u ^ (uint)(pitch * 31 + start);
        double phase = 0, hp = 0, lastNoise = 0;
        (double length, Func<double, double, double, double> sound) = pitch switch
        {
            36 or 35 => (0.45, (Func<double, double, double, double>)((t, n, ph) => Math.Sin(2 * Math.PI * ph) * Math.Exp(-t * 9) + n * 0.05 * Math.Exp(-t * 80))),
            38 or 40 => (0.25, (t, n, ph) => n * 0.7 * Math.Exp(-t * 18) + Math.Sin(2 * Math.PI * ph) * 0.35 * Math.Exp(-t * 25)),
            39 => (0.3, (t, n, _) => n * 0.8 * (Math.Exp(-((t % 0.012) * 300)) * (t < 0.036 ? 1 : 0) + Math.Exp(-(t - 0.036) * 20) * (t >= 0.036 ? 1 : 0))),
            42 or 44 => (0.07, (t, n, _) => n * 0.45 * Math.Exp(-t * 60)),
            46 => (0.4, (t, n, _) => n * 0.4 * Math.Exp(-t * 8)),
            49 or 57 => (1.4, (t, n, _) => n * 0.45 * Math.Exp(-t * 2.8)),
            _ => (0.35, (t, n, ph) => Math.Sin(2 * Math.PI * ph) * Math.Exp(-t * 10)),
        };
        int frames = (int)(length * _rate);
        bool highPassed = pitch is 42 or 44 or 46 or 49 or 57 or 38 or 40 or 39;
        for (int i = 0; i < frames; i++)
        {
            int index = start + i;
            if (index < 0) continue;
            if (index >= buffer.Length) break;
            double t = i / (double)_rate;
            double frequency = pitch switch
            {
                36 or 35 => 45 + 110 * Math.Exp(-t * 30),
                38 or 40 => 185,
                _ => 80 + 60 * Math.Exp(-t * 20),
            };
            phase = (phase + frequency / _rate) % 1;
            noise ^= noise << 13; noise ^= noise >> 17; noise ^= noise << 5;
            double n = noise / (double)uint.MaxValue * 2 - 1;
            if (highPassed) { hp = 0.92 * (hp + n - lastNoise); lastNoise = n; n = hp; }
            buffer[index] += (float)(sound(t, n, phase) * velocity);
        }
    }

    // ---------- Effects ----------

    private static void ApplyEcho(float[] buffer, int delay, float amount)
    {
        if (delay <= 0) return;
        float feedback = 0.35f + amount * 0.3f;
        for (int i = delay; i < buffer.Length; i++)
            buffer[i] += buffer[i - delay] * feedback * amount;
    }

    /// <summary>Small Schroeder room: four combs in parallel and two all-passes, slightly different per side.</summary>
    private (float[] Left, float[] Right) Reverb(float[] input)
    {
        float[] Side(double spread)
        {
            int[] combs = [1557, 1617, 1491, 1422];
            int[] allpasses = [225, 556];
            double scale = _rate / 44_100.0;
            var output = new float[input.Length];
            foreach (var c in combs)
            {
                int size = Math.Max(1, (int)((c + spread) * scale));
                var line = new float[size];
                int position = 0;
                float filter = 0;
                for (int i = 0; i < input.Length; i++)
                {
                    float delayed = line[position];
                    filter = delayed * 0.8f + filter * 0.2f;
                    line[position] = input[i] * 0.04f + filter * 0.84f;
                    output[i] += delayed;
                    position = (position + 1) % size;
                }
            }
            foreach (var a in allpasses)
            {
                int size = Math.Max(1, (int)((a + spread / 2) * scale));
                var line = new float[size];
                int position = 0;
                for (int i = 0; i < output.Length; i++)
                {
                    float delayed = line[position];
                    float value = output[i];
                    line[position] = value + delayed * 0.5f;
                    output[i] = delayed - value;
                    position = (position + 1) % size;
                }
            }
            return output;
        }
        return (Side(0), Side(23));
    }
}
