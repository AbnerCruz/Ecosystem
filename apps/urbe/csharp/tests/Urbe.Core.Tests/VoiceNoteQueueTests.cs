using Urbe.Core;

namespace Urbe.Core.Tests;

/// <summary>UC-66: the queue of voice notes kept until their line is in the vault.</summary>
public sealed class VoiceNoteQueueTests
{
    private static readonly DateTimeOffset At = new(2026, 10, 21, 22, 41, 7, 250, TimeSpan.FromHours(-3));

    [Fact]
    public void TheFileFormatIsStableAndRoundTrips()
    {
        var entries = new[]
        {
            new VoiceNoteEntry("a1", At, "a1.wav", null),
            new VoiceNoteEntry("b2", At.AddMinutes(3), null, "comprar \"pão\" e café", 2, "sem rede")
        };
        var json = VoiceNoteQueue.Serialize(entries);
        Assert.Equal("""
            {
              "version": 1,
              "notes": [
                {
                  "id": "a1",
                  "created": "2026-10-21T22:41:07.250-03:00",
                  "audio": "a1.wav",
                  "text": null,
                  "attempts": 0,
                  "error": null
                },
                {
                  "id": "b2",
                  "created": "2026-10-21T22:44:07.250-03:00",
                  "audio": null,
                  "text": "comprar \"pão\" e café",
                  "attempts": 2,
                  "error": "sem rede"
                }
              ]
            }

            """.ReplaceLineEndings("\n"), json.ReplaceLineEndings("\n"));
        Assert.Equal(entries, VoiceNoteQueue.Parse(json));
        Assert.Empty(VoiceNoteQueue.Parse(null));
        Assert.Empty(VoiceNoteQueue.Parse("  "));
    }

    [Theory]
    [InlineData("{")]
    [InlineData("[]")]
    [InlineData("{\"version\":1}")]
    [InlineData("{\"version\":1,\"notes\":[{\"id\":\"x\"}]}")]
    [InlineData("{\"version\":2,\"notes\":[]}")]
    public void AnUnreadableQueueIsRefusedNotEmptied(string json) =>
        Assert.Throws<InvalidDataException>(() => VoiceNoteQueue.Parse(json));

    [Fact]
    public void TheLineUsesTheTimeItWasSaid()
    {
        var entry = new VoiceNoteEntry("a", At, null, "  ligar\npara a Ana ");
        var made = entry.AppendTo(null)!.Value;
        Assert.Equal("Notas rápidas/2026-10.md", made.Path);
        Assert.Equal("# Notas rápidas — outubro de 2026\n\n## 21/10 — quarta-feira\n\n- 22:41 — ligar para a Ana\n", made.Content);
        Assert.Equal(made.Content + "- 22:41 — ligar para a Ana\n", entry.AppendTo(made.Content)!.Value.Content);
        Assert.Null((entry with { Text = null }).AppendTo(null));
    }

    [Fact]
    public void RecordingsWithoutAnEntryAreAdoptedBack()
    {
        var entries = new[] { new VoiceNoteEntry("a", At, "a.wav", null) };
        var adopted = VoiceNoteQueue.AdoptOrphans(entries, [("a.wav", At), ("z.wav", At.AddHours(1)), ("b.wav", At.AddMinutes(1))]);
        Assert.Equal(["a", "b", "z"], adopted.Select(e => e.Id));
        Assert.Equal(At.AddMinutes(1), adopted[1].Created);
        Assert.Null(adopted[1].Text);
    }

    [Fact]
    public void TheFileKeepsAudioUntilRemovedAndSetsAsideWhatItCannotRead()
    {
        var dir = Path.Combine(Path.GetTempPath(), "urbe-voice-" + Guid.NewGuid().ToString("N"));
        try
        {
            var queue = new VoiceNoteQueueFile(Path.Combine(dir, "fila.json"));
            Assert.Empty(queue.Load());
            Directory.CreateDirectory(dir);
            File.WriteAllBytes(Path.Combine(dir, "a.wav"), [1, 2, 3]);
            queue.Add(new VoiceNoteEntry("a", At, "a.wav", null));
            queue.Replace(new VoiceNoteEntry("a", At, "a.wav", "texto", 1));
            Assert.Equal("texto", Assert.Single(queue.Load()).Text);
            Assert.False(File.Exists(queue.Path + ".tmp"));

            // a recording the queue lost is found again
            File.WriteAllBytes(Path.Combine(dir, "b.wav"), [4]);
            Assert.Equal(["a", "b"], queue.LoadAdoptingOrphans().Select(e => e.Id));
            Assert.Equal(2, queue.Load().Count);

            queue.Remove("a");
            Assert.False(File.Exists(Path.Combine(dir, "a.wav")));
            Assert.True(File.Exists(Path.Combine(dir, "b.wav")));
            Assert.Equal("b", Assert.Single(queue.Load()).Id);

            // garbage is moved aside, the recordings stay and come back
            File.WriteAllText(queue.Path, "{ não é json");
            Assert.Empty(queue.Load());
            Assert.Single(Directory.GetFiles(dir, "fila.json.ilegivel-*"));
            Assert.Equal("b", Assert.Single(queue.LoadAdoptingOrphans()).Id);
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
    }
}
