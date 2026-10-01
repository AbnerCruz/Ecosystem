namespace Lunet.Core;

/// <summary>Gera pequenos WAVs (PCM 16 bits mono) para os modelos de projeto.</summary>
public static class WavGenerator
{
    public const int SampleRate = 22050;

    /// <summary>Bipe senoidal com decaimento linear.</summary>
    public static byte[] Beep(double frequencyHz, double seconds)
    {
        var samples = (int)(SampleRate * seconds);
        using var stream = new MemoryStream(44 + samples * 2);
        using var w = new BinaryWriter(stream);
        w.Write("RIFF"u8);
        w.Write(36 + samples * 2);
        w.Write("WAVEfmt "u8);
        w.Write(16);
        w.Write((short)1);          // PCM
        w.Write((short)1);          // mono
        w.Write(SampleRate);
        w.Write(SampleRate * 2);    // bytes/s
        w.Write((short)2);          // alinhamento
        w.Write((short)16);         // bits
        w.Write("data"u8);
        w.Write(samples * 2);
        for (var i = 0; i < samples; i++)
        {
            var envelope = 1.0 - (double)i / samples;
            w.Write((short)(Math.Sin(2 * Math.PI * frequencyHz * i / SampleRate) * envelope * 0.6 * short.MaxValue));
        }
        return stream.ToArray();
    }

    /// <summary>Arpejo curto em laço (~3,2 s): serve de música de teste.</summary>
    public static byte[] ArpeggioLoop()
    {
        double[] notes = [261.63, 329.63, 392.00, 523.25, 392.00, 329.63, 293.66, 349.23, 440.00, 587.33, 440.00, 349.23];
        const double noteSeconds = 0.27;
        var samplesPerNote = (int)(SampleRate * noteSeconds);
        var total = samplesPerNote * notes.Length;
        using var stream = new MemoryStream(44 + total * 2);
        using var w = new BinaryWriter(stream);
        w.Write("RIFF"u8);
        w.Write(36 + total * 2);
        w.Write("WAVEfmt "u8);
        w.Write(16);
        w.Write((short)1);
        w.Write((short)1);
        w.Write(SampleRate);
        w.Write(SampleRate * 2);
        w.Write((short)2);
        w.Write((short)16);
        w.Write("data"u8);
        w.Write(total * 2);
        foreach (var frequency in notes)
        {
            for (var i = 0; i < samplesPerNote; i++)
            {
                var attack = Math.Min(1.0, i / 200.0);
                var release = Math.Min(1.0, (samplesPerNote - i) / 400.0); // sem estalo entre notas
                w.Write((short)(Math.Sin(2 * Math.PI * frequency * i / SampleRate) * attack * release * 0.35 * short.MaxValue));
            }
        }
        return stream.ToArray();
    }
}
