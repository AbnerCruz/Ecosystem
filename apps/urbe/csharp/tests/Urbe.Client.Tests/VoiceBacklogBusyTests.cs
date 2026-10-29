using Urbe.Client;
using Urbe.Core;

namespace Urbe.Client.Tests;

/// <summary>CST-055: with consecutive recordings, the service may be saving several notes; the app leaves all of them alone.</summary>
public sealed class VoiceBacklogBusyTests
{
    [Fact]
    public async Task EveryNoteTheServiceIsStillSavingIsLeftAlone()
    {
        var dir = Path.Combine(Path.GetTempPath(), "urbe-voice-busy-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var queue = new VoiceNoteQueueFile(Path.Combine(dir, "fila.json"));
            var said = new DateTimeOffset(2026, 10, 28, 9, 0, 0, TimeSpan.FromHours(-3));
            queue.Add(new VoiceNoteEntry("gravando", said, null, "primeira"));
            queue.Add(new VoiceNoteEntry("salvando", said.AddMinutes(1), null, "segunda"));
            queue.Add(new VoiceNoteEntry("solta", said.AddMinutes(2), null, "terceira"));
            var busy = new HashSet<string> { "gravando", "salvando" };
            var backlog = new VoiceNoteQueueBacklog(queue, _ => Task.FromResult<string?>(null), busy.Contains);
            Assert.Equal(1, backlog.Count);
            var written = new List<string>();
            var (saved, left) = await backlog.RetryAsync((text, _) => { written.Add(text); return Task.FromResult(true); });
            Assert.Equal((1, 0), (saved, left));
            Assert.Equal(["terceira"], written);
            Assert.Equal(["gravando", "salvando"], queue.Load().Select(e => e.Id).ToArray());
        }
        finally { Directory.Delete(dir, true); }
    }
}
