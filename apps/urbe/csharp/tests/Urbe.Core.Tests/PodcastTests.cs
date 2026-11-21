using System.Buffers.Binary;
using Urbe.Core.Study;

namespace Urbe.Core.Tests;

public sealed class PodcastTests
{
    [Fact]
    public void TurnsAlternateVoicesBySpeaker()
    {
        var turns = Podcast.Turns("# Podcast\n\n**Ana:** Olá. {ia:\"modelo\"}\n\n**Beto:** Oi, Ana.\n\nTexto solto.\n\n**ana:** De novo.\n\n**Beto:**\n");
        Assert.Equal(["Olá.", "Oi, Ana.", "De novo."], turns.Select(t => t.Text));
        Assert.Equal([0, 1, 0], turns.Select(t => t.Voice));
    }

    [Fact]
    public void TheDraftAsksTheAgentInsideTheNote()
    {
        var draft = Podcast.Draft(new StudySession { Title = "Física" }, ["Newton.md"]);
        Assert.Single(Urbe.Core.NoteMarks.AgentCalls(draft));
        Assert.Contains("[[Newton]]", draft);
    }

    [Fact]
    public void JoinedWavHasEveryPartAndAPause()
    {
        var dir = Directory.CreateTempSubdirectory().FullName;
        try
        {
            string Part(string name, int samples)
            {
                var path = Path.Combine(dir, name);
                File.WriteAllBytes(path, Wav(samples));
                return path;
            }
            var target = Path.Combine(dir, "out.wav");
            Podcast.JoinWav([Part("a.wav", 100), Part("b.wav", 50)], target, pauseSeconds: 0.01);
            var bytes = File.ReadAllBytes(target);
            int data = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(40));
            Assert.Equal((100 + 50 + 80) * 2, data); // 8 kHz mono 16-bit: a 10 ms pause is 80 samples
            Assert.Equal(44 + data, bytes.Length);
            Assert.Equal(36 + data, BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(4)));
        }
        finally { Directory.Delete(dir, true); }
    }

    private static byte[] Wav(int samples)
    {
        var b = new byte[44 + samples * 2];
        "RIFF"u8.CopyTo(b); BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(4), 36 + samples * 2);
        "WAVEfmt "u8.CopyTo(b.AsSpan(8)); BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(16), 16);
        BinaryPrimitives.WriteInt16LittleEndian(b.AsSpan(20), 1); BinaryPrimitives.WriteInt16LittleEndian(b.AsSpan(22), 1);
        BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(24), 8000); BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(28), 16000);
        BinaryPrimitives.WriteInt16LittleEndian(b.AsSpan(32), 2); BinaryPrimitives.WriteInt16LittleEndian(b.AsSpan(34), 16);
        "data"u8.CopyTo(b.AsSpan(36)); BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(40), samples * 2);
        for (int i = 44; i < b.Length; i++) b[i] = 7;
        return b;
    }
}
