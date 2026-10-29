using System.Globalization;
using System.Text;

namespace Urbe.Core;

/// <summary>
/// CST-058: a long recording (a class, a meeting) becomes a note to review. The decoded audio is brought to what the
/// speech recognizer reads (16 kHz, mono, 16-bit PCM), cut in pauses about every 45 s — a part never splits a word
/// and none is too long for the recognizer — and each part's text goes into the note with the time it starts at. All
/// of it works on a stream of samples, so an hour of audio is never held in memory at once.
/// </summary>
public static class LongAudio
{
    public const int Rate = 16000;
    public const double TargetSeconds = 45, SearchSeconds = 7;

    /// <summary>A part of the recording, in samples at 16 kHz.</summary>
    public sealed record Part(long Start, int Length)
    {
        public TimeSpan At => TimeSpan.FromSeconds(Start / (double)Rate);
    }

    /// <summary>
    /// Decoded PCM (16-bit, interleaved) to 16 kHz mono, a chunk at a time: the channels are averaged, and a higher
    /// rate is averaged over each output step (a box filter, so what is above 8 kHz does not fold back as noise).
    /// </summary>
    public sealed class Mono16k(int channels, int sampleRate)
    {
        private readonly int _channels = Math.Max(1, channels);
        private readonly double _step = sampleRate / (double)Rate;
        private double _boundary = sampleRate / (double)Rate;
        private long _frame;
        private double _sum;
        private int _count;

        public short[] Push(ReadOnlySpan<short> interleaved)
        {
            int frames = interleaved.Length / _channels;
            var output = new List<short>((int)(frames / Math.Max(_step, .001)) + 2);
            for (int f = 0; f < frames; f++)
            {
                double value = 0;
                for (int c = 0; c < _channels; c++) value += interleaved[f * _channels + c];
                value /= _channels;
                if (_step <= 1)
                {
                    // a lower rate (8 kHz): each sample held for its share of the output
                    _boundary -= 1;
                    while (_boundary <= 0) { output.Add(Clamp(value)); _boundary += _step; }
                    continue;
                }
                _sum += value;
                _count++;
                _frame++;
                if (_frame >= _boundary)
                {
                    output.Add(Clamp(_sum / _count));
                    _sum = 0;
                    _count = 0;
                    _boundary += _step;
                }
            }
            return [.. output];
        }

        private static short Clamp(double v) => (short)Math.Clamp(Math.Round(v), short.MinValue, short.MaxValue);
    }

    public static short[] ToMono16k(ReadOnlySpan<short> interleaved, int channels, int sampleRate) =>
        new Mono16k(channels, sampleRate).Push(interleaved);

    /// <summary>
    /// The parts of <paramref name="total"/> samples: each cut is the quietest 200 ms within 7 s of the 45 s mark,
    /// the nearest to the mark among equally quiet ones. <paramref name="read"/> gives the samples of a window.
    /// </summary>
    public static IReadOnlyList<Part> Plan(long total, Func<long, int, short[]> read)
    {
        var parts = new List<Part>();
        long start = 0, target = (long)(TargetSeconds * Rate), search = (long)(SearchSeconds * Rate);
        int window = Rate / 5, hop = Rate / 20;
        while (total - start > target + search)
        {
            long ideal = start + target, from = ideal - search;
            var samples = read(from, (int)(2 * search));
            double best = double.MaxValue;
            long cut = ideal;
            for (int o = 0; o + window <= samples.Length; o += hop)
            {
                double energy = 0;
                for (int k = o; k < o + window; k++) energy += (double)samples[k] * samples[k];
                long at = from + o + window / 2;
                if (energy < best - 1e-9 || Math.Abs(energy - best) <= 1e-9 && Math.Abs(at - ideal) < Math.Abs(cut - ideal))
                {
                    best = energy;
                    cut = at;
                }
            }
            parts.Add(new Part(start, (int)(cut - start)));
            start = cut;
        }
        if (total > start) parts.Add(new Part(start, (int)(total - start)));
        return parts;
    }

    /// <summary>A WAV of 16 kHz mono 16-bit samples, with the 44-byte header the recognizer skips.</summary>
    public static byte[] Wav(ReadOnlySpan<short> samples)
    {
        var bytes = new byte[44 + samples.Length * 2];
        void Text(int at, string s) => Encoding.ASCII.GetBytes(s).CopyTo(bytes, at);
        void Int(int at, int v) => BitConverter.GetBytes(v).CopyTo(bytes, at);
        void Short(int at, short v) => BitConverter.GetBytes(v).CopyTo(bytes, at);
        Text(0, "RIFF"); Int(4, 36 + samples.Length * 2); Text(8, "WAVE");
        Text(12, "fmt "); Int(16, 16); Short(20, 1); Short(22, 1); Int(24, Rate); Int(28, Rate * 2); Short(32, 2); Short(34, 16);
        Text(36, "data"); Int(40, samples.Length * 2);
        for (int i = 0; i < samples.Length; i++) BitConverter.GetBytes(samples[i]).CopyTo(bytes, 44 + i * 2);
        return bytes;
    }

    public static string Time(TimeSpan at) => at.TotalHours >= 1
        ? ((int)at.TotalHours).ToString(CultureInfo.InvariantCulture) + ":" + at.Minutes.ToString("00", CultureInfo.InvariantCulture) + ":" + at.Seconds.ToString("00", CultureInfo.InvariantCulture)
        : ((int)at.TotalMinutes).ToString("00", CultureInfo.InvariantCulture) + ":" + at.Seconds.ToString("00", CultureInfo.InvariantCulture);

    /// <summary>The note: a link to the recording, a word that it is to be reviewed, and each part with its time.</summary>
    public static string Note(string audioPath, IReadOnlyList<(TimeSpan At, string? Text)> parts)
    {
        var name = Path.GetFileNameWithoutExtension(audioPath);
        var note = new StringBuilder();
        note.Append("# ").Append(name).Append(" — transcrição\n\n");
        note.Append("![[").Append(audioPath).Append("]]\n\n");
        note.Append("Transcrição automática, para revisar: confira nomes, números e termos ouvindo o áudio. Cada horário marca onde o trecho começa.\n\n");
        note.Append("## Transcrição\n\n");
        foreach (var (at, text) in parts)
            note.Append("**[").Append(Time(at)).Append("]** ").Append(string.IsNullOrWhiteSpace(text) ? "(trecho não reconhecido)" : text.Trim()).Append("\n\n");
        return note.ToString().TrimEnd('\n') + "\n";
    }

    /// <summary>"Nome — transcrição.md" next to the recording, numbered when taken.</summary>
    public static string NotePath(string audioPath, Func<string, bool> taken)
    {
        var folder = audioPath.Contains('/') ? audioPath[..(audioPath.LastIndexOf('/') + 1)] : "";
        var name = Path.GetFileNameWithoutExtension(audioPath) + " — transcrição";
        var path = folder + name + ".md";
        for (int n = 2; taken(path); n++) path = folder + name + " " + n + ".md";
        return path;
    }
}
