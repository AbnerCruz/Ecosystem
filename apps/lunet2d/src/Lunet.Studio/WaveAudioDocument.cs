using System.Buffers.Binary;

namespace Lunet.Studio;

/// <summary>Formas simples de onda para efeitos gerados sem ferramentas externas.</summary>
public enum WaveShape { Sine, Square, Triangle, Noise }

/// <summary>Pico mínimo e máximo de uma coluna de forma de onda normalizada [-1,1].</summary>
public readonly record struct WavePeak(float Minimum, float Maximum);

/// <summary>
/// Documento PCM carregado em memória para o Audio Studio. A origem permanece imutável:
/// Render cria um novo WAV PCM16. Suporta RIFF/WAVE PCM 8/16 bits mono/estéreo;
/// formatos comprimidos continuam no player, não neste editor.
/// </summary>
public sealed class WaveAudioDocument
{
    private const int MaxSourceBytes = 24 * 1024 * 1024;
    private const int MaxSamples = 8_000_000;
    private readonly short[] _samples;

    private WaveAudioDocument(int sampleRate, int channels, short[] samples)
    {
        SampleRate = sampleRate;
        Channels = channels;
        _samples = samples;
    }

    public int SampleRate { get; }
    public int Channels { get; }
    public int FrameCount => _samples.Length / Channels;
    public double DurationSeconds => (double)FrameCount / SampleRate;

    /// <summary>Lê PCM WAV e rejeita arquivos truncados, inválidos, grandes ou codificados.</summary>
    public static WaveAudioDocument Parse(ReadOnlySpan<byte> wav)
    {
        if (wav.Length is < 44 or > MaxSourceBytes)
            throw new InvalidDataException("WAV inválido ou grande demais para o editor (máximo 24 MiB).");
        if (!wav[..4].SequenceEqual("RIFF"u8) || !wav.Slice(8, 4).SequenceEqual("WAVE"u8))
            throw new InvalidDataException("Esperado arquivo RIFF/WAVE.");
        long end = 8L + BinaryPrimitives.ReadUInt32LittleEndian(wav.Slice(4, 4));
        if (end > wav.Length || end < 44)
            throw new InvalidDataException("RIFF truncado ou tamanho inválido.");

        int rate = 0, channels = 0, bits = 0, blockAlign = 0;
        uint bytesPerSecond = 0;
        int dataOffset = -1, dataLength = 0;
        bool foundFmt = false;

        for (long pos = 12; pos < end;)
        {
            if (end - pos < 8) throw new InvalidDataException("Chunk WAV incompleto.");
            var chunk = wav.Slice((int)pos, 8);
            uint length = BinaryPrimitives.ReadUInt32LittleEndian(chunk[4..]);
            long start = pos + 8, next = start + length + (length & 1u);
            if (next > end) throw new InvalidDataException("Chunk WAV truncado.");
            if (chunk[..4].SequenceEqual("fmt "u8))
            {
                if (foundFmt || length < 16) throw new InvalidDataException("Formato WAV inválido.");
                foundFmt = true;
                var f = wav.Slice((int)start, 16);
                if (BinaryPrimitives.ReadUInt16LittleEndian(f) != 1)
                    throw new NotSupportedException("Este editor suporta apenas PCM WAV, não áudio comprimido/float.");
                channels = BinaryPrimitives.ReadUInt16LittleEndian(f.Slice(2, 2));
                rate = (int)BinaryPrimitives.ReadUInt32LittleEndian(f.Slice(4, 4));
                bytesPerSecond = BinaryPrimitives.ReadUInt32LittleEndian(f.Slice(8, 4));
                blockAlign = BinaryPrimitives.ReadUInt16LittleEndian(f.Slice(12, 2));
                bits = BinaryPrimitives.ReadUInt16LittleEndian(f.Slice(14, 2));
            }
            else if (chunk[..4].SequenceEqual("data"u8))
            {
                if (dataOffset >= 0) throw new InvalidDataException("WAV com múltiplos chunks data.");
                dataOffset = (int)start;
                dataLength = (int)length;
            }
            pos = next;
        }
        if (!foundFmt || dataOffset < 0)
            throw new InvalidDataException("WAV sem formato ou áudio.");
        if (rate is < 4000 or > 192000 || channels is not (1 or 2) || bits is not (8 or 16))
            throw new NotSupportedException("A edição suporta PCM de 8/16 bits, mono ou estéreo (4–192 kHz).");
        int frameBytes = channels * bits / 8;
        if (blockAlign != frameBytes || bytesPerSecond != (long)rate * frameBytes ||
            dataLength == 0 || dataLength % frameBytes != 0)
            throw new InvalidDataException("Cabeçalho PCM desalinhado.");

        long sampleCount = (long)(dataLength / frameBytes) * channels;
        if (sampleCount > MaxSamples) throw new InvalidDataException("WAV longo demais para edição em memória.");
        var samples = new short[(int)sampleCount];
        if (bits == 16)
        {
            for (int i = 0; i < samples.Length; i++)
                samples[i] = BinaryPrimitives.ReadInt16LittleEndian(wav.Slice(dataOffset + i * 2, 2));
        }
        else
        {
            for (int i = 0; i < samples.Length; i++)
                samples[i] = (short)((wav[dataOffset + i] - 128) << 8);
        }
        return new WaveAudioDocument(rate, channels, samples);
    }

    /// <summary>Cria um efeito mono sintetizado determinístico, com ataque e decaimento para evitar clicks.</summary>
    public static WaveAudioDocument CreateEffect(double frequencyHz, double durationSeconds, WaveShape shape)
    {
        if (!double.IsFinite(frequencyHz) || frequencyHz is < 40 or > 5000 ||
            !double.IsFinite(durationSeconds) || durationSeconds is < 0.05 or > 5)
            throw new ArgumentOutOfRangeException(nameof(frequencyHz), "Frequência 40–5000 Hz e duração 0,05–5 s.");
        const int rate = 22050;
        int frames = (int)Math.Round(rate * durationSeconds);
        var samples = new short[frames];
        uint noise = 0x12345678;
        for (int i = 0; i < frames; i++)
        {
            double t = (double)i / rate;
            double cycle = t * frequencyHz - Math.Floor(t * frequencyHz);
            noise = noise * 1664525 + 1013904223;
            double wave = shape switch
            {
                WaveShape.Sine => Math.Sin(2 * Math.PI * cycle),
                WaveShape.Square => cycle < 0.5 ? 1 : -1,
                WaveShape.Triangle => 1 - 4 * Math.Abs(cycle - 0.5),
                WaveShape.Noise => (noise / (double)uint.MaxValue) * 2 - 1,
                _ => throw new ArgumentOutOfRangeException(nameof(shape))
            };
            double attack = Math.Min(1, t / 0.006);
            double release = Math.Min(1, (frames - 1 - i) / (rate * 0.025));
            double decay = Math.Exp(-2.3 * t / durationSeconds);
            samples[i] = (short)Math.Clamp(Math.Round(wave * attack * Math.Max(0, release) * decay * 0.72 * short.MaxValue),
                short.MinValue, short.MaxValue);
        }
        return new WaveAudioDocument(rate, 1, samples);
    }

    /// <summary>Envelope de picos por coluna para uma prévia eficiente de ondas longas.</summary>
    public WavePeak[] GetPeaks(int columns)
    {
        if (columns is < 1 or > 4096) throw new ArgumentOutOfRangeException(nameof(columns));
        var lo = new float[columns];
        var hi = new float[columns];
        for (int i = 0; i < FrameCount; i++)
        {
            int bin = (int)((long)i * columns / FrameCount);
            for (int channel = 0; channel < Channels; channel++)
            {
                float sample = _samples[i * Channels + channel] / 32768f;
                lo[bin] = Math.Min(lo[bin], sample);
                hi[bin] = Math.Max(hi[bin], sample);
            }
        }
        var result = new WavePeak[columns];
        for (int i = 0; i < columns; i++) result[i] = new WavePeak(lo[i], hi[i]);
        return result;
    }

    /// <summary>
    /// Aplica recorte e ganho sem tocar nos samples originais. Loop start/end são tempos absolutos
    /// na fonte; na saída são convertidos em marcadores SMPL relativos ao trecho exportado.
    /// </summary>
    public byte[] Render(double startSeconds, double endSeconds, float gain = 1,
        double? loopStartSeconds = null, double? loopEndSeconds = null)
    {
        if (!double.IsFinite(startSeconds) || !double.IsFinite(endSeconds) ||
            startSeconds < 0 || endSeconds > DurationSeconds || endSeconds <= startSeconds)
            throw new ArgumentOutOfRangeException(nameof(startSeconds), "Recorte fora da duração do WAV.");
        if (!float.IsFinite(gain) || gain is < 0 or > 2)
            throw new ArgumentOutOfRangeException(nameof(gain), "Ganho permitido: 0–200%.");

        int start = (int)Math.Floor(startSeconds * SampleRate);
        int end = (int)Math.Ceiling(endSeconds * SampleRate);
        end = Math.Min(end, FrameCount);
        if (end <= start) throw new ArgumentOutOfRangeException(nameof(endSeconds), "Recorte vazio.");
        bool loop = loopStartSeconds.HasValue || loopEndSeconds.HasValue;
        int loopStart = 0, loopEnd = 0;
        if (loop)
        {
            if (!loopStartSeconds.HasValue || !loopEndSeconds.HasValue ||
                !double.IsFinite(loopStartSeconds.Value) || !double.IsFinite(loopEndSeconds.Value) ||
                loopStartSeconds.Value < startSeconds || loopEndSeconds.Value > endSeconds ||
                loopStartSeconds.Value >= loopEndSeconds.Value)
                throw new ArgumentOutOfRangeException(nameof(loopStartSeconds), "Pontos de loop fora do recorte.");
            loopStart = (int)Math.Floor(loopStartSeconds.Value * SampleRate) - start;
            loopEnd = (int)Math.Ceiling(loopEndSeconds.Value * SampleRate) - start - 1;
            if (loopStart >= loopEnd) throw new ArgumentOutOfRangeException(nameof(loopEndSeconds), "Loop precisa de pelo menos dois frames.");
        }

        int totalFrames = end - start;
        int dataSize = checked(totalFrames * Channels * 2);
        using var stream = new MemoryStream(checked(44 + dataSize + (loop ? 68 : 0)));
        using var writer = new BinaryWriter(stream);
        writer.Write("RIFF"u8); writer.Write(checked(36 + dataSize + (loop ? 68 : 0)));
        writer.Write("WAVEfmt "u8); writer.Write(16); writer.Write((short)1);
        writer.Write((short)Channels); writer.Write(SampleRate);
        writer.Write(SampleRate * Channels * 2); writer.Write((short)(Channels * 2)); writer.Write((short)16);

        if (loop)
        {
            writer.Write("smpl"u8); writer.Write(60);
            writer.Write(0); writer.Write(0); writer.Write((uint)Math.Round(1_000_000_000.0 / SampleRate));
            writer.Write(60); writer.Write(0); writer.Write(0); writer.Write(0); writer.Write(1); writer.Write(0);
            writer.Write(0); writer.Write(0); writer.Write(loopStart); writer.Write(loopEnd); writer.Write(0); writer.Write(0);
        }
        writer.Write("data"u8); writer.Write(dataSize);
        for (int i = start * Channels; i < end * Channels; i++)
        {
            int amplified = (int)Math.Round(_samples[i] * (double)gain);
            writer.Write((short)Math.Clamp(amplified, short.MinValue, short.MaxValue));
        }
        return stream.ToArray();
    }
}
