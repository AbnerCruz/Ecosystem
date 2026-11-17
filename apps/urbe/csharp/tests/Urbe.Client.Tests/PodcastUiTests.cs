using Avalonia.Controls;
using Urbe.Client;
using Urbe.Core.Study;

namespace Urbe.Client.Tests;

public sealed partial class HeadlessUiTests
{
    /// <summary>#956: a podcast script with two hosts becomes one WAV beside it, each turn in its host's voice.</summary>
    [Fact]
    public async Task Podcast_ScriptTurnsBecomeOneWavInTheVault() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var dir = Path.Combine(Path.GetTempPath(), "urbe-podcast-" + Guid.NewGuid().ToString("N"));
        var vault = Path.Combine(dir, "vault");
        Directory.CreateDirectory(Path.Combine(vault, "Estudos"));
        File.WriteAllText(Path.Combine(vault, "Estudos", "Física.podcast.md"), "# Podcast\n\n**Ana:** Olá.\n\n**Beto:** Oi.\n\n**Ana:** Tchau.\n");
        var spoken = new List<(string, int)>();
        var previous = PlatformAudio.SynthesizeToWav;
        PlatformAudio.SynthesizeToWav = (text, voice, target, _) =>
        {
            spoken.Add((text, voice));
            File.WriteAllBytes(target, Urbe.Core.LongAudio.Wav(new short[160]));
            return Task.FromResult(true);
        };
        var view = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "app", "sel.json")));
        var window = new Window { Width = 412, Height = 860, Content = view };
        window.Show();
        try
        {
            await view.OpenVaultAsync();
            await Settle(window, view);
            Assert.True(view.CanSpeakPodcast("Estudos/Física.podcast.md"));
            Assert.False(view.CanSpeakPodcast("Estudos/Física.md"));
            var made = await view.SpeakPodcastAsync("Estudos/Física.podcast.md");
            Assert.Equal("Estudos/Física.podcast.wav", made);
            Assert.Equal([("Olá.", 0), ("Oi.", 1), ("Tchau.", 0)], spoken);
            Assert.True(new FileInfo(Path.Combine(vault, "Estudos", "Física.podcast.wav")).Length > 44 + 3 * 320);
            return true;
        }
        finally
        {
            PlatformAudio.SynthesizeToWav = previous;
            window.Close();
            try { Directory.Delete(dir, true); } catch (IOException) { }
        }
    }, CancellationToken.None));
}
