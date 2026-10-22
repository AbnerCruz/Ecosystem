using Avalonia.Threading;
using Urbe.Client;
using Urbe.Core;

namespace Urbe.Client.Tests;

/// <summary>UC-66: voice notes the widget kept are offered again when the app opens, and leave only once written.</summary>
public sealed partial class HeadlessUiTests
{
    [Fact]
    public async Task QuickVoiceBacklog_OffersAndRetriesTheWaitingVoiceNotes() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var (view, window, vault, dir) = await OpenCreateVault();
            try
            {
                var queue = new VoiceNoteQueueFile(Path.Combine(dir, "voz", "fila.json"));
                Directory.CreateDirectory(queue.Folder);
                var said = new DateTimeOffset(2026, 10, 21, 7, 30, 0, TimeSpan.FromHours(-3));
                // one already transcribed, one only recorded, one whose transcription keeps failing
                queue.Add(new VoiceNoteEntry("t", said, null, "comprar pão"));
                File.WriteAllBytes(Path.Combine(queue.Folder, "a.wav"), [0, 1]);
                queue.Add(new VoiceNoteEntry("a", said.AddMinutes(5), "a.wav", null));
                File.WriteAllBytes(Path.Combine(queue.Folder, "f.wav"), [0, 2]);
                queue.Add(new VoiceNoteEntry("f", said.AddMinutes(9), "f.wav", null));
                var transcribed = new List<string>();
                view.VoiceBacklog = new VoiceNoteQueueBacklog(queue, audio =>
                {
                    transcribed.Add(Path.GetFileName(audio));
                    return Task.FromResult(Path.GetFileName(audio) == "a.wav" ? "ligar para a Ana" : null);
                });

                view.OfferVoiceBacklog();
                Assert.Equal("Há 3 notas de voz para transcrever.", view.NoticeText);
                ClickButtonWithText(view, "Tentar");
                for (int i = 0; i < 100 && queue.Load().Count == 3; i++) { await Task.Delay(20); Dispatcher.UIThread.RunJobs(); }
                await Task.Delay(50);
                Dispatcher.UIThread.RunJobs();

                var file = Path.Combine(vault, QuickNotes.Folder, "2026-10.md");
                Assert.Equal("# Notas rápidas — outubro de 2026\n\n## 21/10 — quarta-feira\n\n- 07:30 — comprar pão\n- 07:35 — ligar para a Ana\n", File.ReadAllText(file));
                Assert.Equal(["a.wav", "f.wav"], transcribed);
                // the written ones and their audio are gone; the failing one stays, with its audio
                var left = Assert.Single(queue.Load());
                Assert.Equal("f", left.Id);
                Assert.Equal(1, left.Attempts);
                Assert.False(File.Exists(Path.Combine(queue.Folder, "a.wav")));
                Assert.True(File.Exists(Path.Combine(queue.Folder, "f.wav")));
                Assert.Equal("2 salva(s); 1 continua guardada para tentar depois.", view.Notices[^1]);
                Assert.NotNull(view.Documents!.Get(QuickNotes.Folder + "/2026-10.md"));

                // nothing waiting: no offer
                queue.Remove("f");
                int notices = view.Notices.Count;
                view.OfferVoiceBacklog();
                Assert.Equal(notices, view.Notices.Count);
                window.Close();
                return true;
            }
            finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
        }, CancellationToken.None));

    [Fact]
    public async Task QuickVoiceBacklog_WithoutAVaultKeepsEverything() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var dir = Path.Combine(Path.GetTempPath(), "urbe-voice-" + Guid.NewGuid().ToString("N"));
            try
            {
                var view = new MainView();
                var window = new Avalonia.Controls.Window { Width = 412, Height = 860, Content = view };
                window.Show();
                await view.LoadAsync(TutorialNotes.Load());
                await Settle(window, view);
                var queue = new VoiceNoteQueueFile(Path.Combine(dir, "fila.json"));
                queue.Add(new VoiceNoteEntry("t", DateTimeOffset.Now, null, "ideia"));
                bool asked = false;
                view.VoiceBacklog = new VoiceNoteQueueBacklog(queue, _ => { asked = true; return Task.FromResult<string?>("x"); });
                await view.RetryVoiceBacklogAsync();
                Assert.False(asked);
                Assert.Single(queue.Load());
                Assert.StartsWith("Abra a pasta do seu vault", view.Notices[^1]);
                window.Close();
                return true;
            }
            finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
        }, CancellationToken.None));
}
