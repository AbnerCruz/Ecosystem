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

    /// <summary>CST-057: the process killed while recording left a WAV whose header says 0 bytes; the retry still hears it.</summary>
    [Fact]
    public async Task ARecordingCutByAKilledProcessIsStillTranscribedAndSaved()
    {
        var dir = Path.Combine(Path.GetTempPath(), "urbe-voice-cut-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var queue = new VoiceNoteQueueFile(Path.Combine(dir, "fila.json"));
            using (var wav = File.Create(Path.Combine(dir, "cortada.wav")))
            {
                VideoAudio.WriteWavHeader(wav, 1, 16000, 0);   // the final header was never written
                wav.Write(new byte[16000 * 2 * 3]);
            }
            queue.Add(new VoiceNoteEntry("cortada", new DateTimeOffset(2026, 11, 16, 9, 0, 0, TimeSpan.FromHours(-3)), "cortada.wav", null));
            // a transcriber that, like a real one, reads only what the header says
            Task<string?> Transcribe(string path)
            {
                var bytes = File.ReadAllBytes(path);
                return Task.FromResult<string?>(BitConverter.ToInt32(bytes, 40) > 0 ? "comprar pão" : null);
            }
            var written = new List<string>();
            var (saved, left) = await new VoiceNoteQueueBacklog(queue, Transcribe).RetryAsync((text, _) => { written.Add(text); return Task.FromResult(true); });
            Assert.Equal((1, 0), (saved, left));
            Assert.Equal(["comprar pão"], written);
            Assert.Empty(queue.Load());
        }
        finally { Directory.Delete(dir, true); }
    }
}
