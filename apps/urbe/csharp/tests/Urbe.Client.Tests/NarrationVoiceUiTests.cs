using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Urbe.Client;

namespace Urbe.Client.Tests;

public sealed partial class HeadlessUiTests
{
    /// <summary>#1004: the narration voice is chosen from the device's voices, kept on the device and back after the app reopens.</summary>
    [Fact]
    public async Task Narration_TheChosenVoiceIsKeptOnTheDevice() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var dir = Path.Combine(Path.GetTempPath(), "urbe-voice-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var (voices, preferred) = (PlatformAudio.Voices, PlatformAudio.PreferredVoice);
        PlatformAudio.Voices = () => ["pt-br-x-afs-local", "pt-br-x-ptd-local"];
        PlatformAudio.PreferredVoice = null;
        try
        {
            var view = new MainView { VoiceSettingPath = Path.Combine(dir, "voice.txt") };
            var window = new Window { Width = 412, Height = 860, Content = view };
            window.Show();
            var choosing = view.ChooseNarrationVoiceAsync();
            for (int i = 0; i < 200 && !view.Dialogs.IsOpen; i++) { await Task.Delay(5); Dispatcher.UIThread.RunJobs(); }
            var option = view.Dialogs.GetLogicalDescendants().OfType<Button>()
                .First(b => b.GetLogicalDescendants().OfType<TextBlock>().Any(t => t.Text == "pt-br-x-ptd-local"));
            option.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            await choosing;
            Assert.Equal("pt-br-x-ptd-local", PlatformAudio.PreferredVoice);
            Assert.Equal("pt-br-x-ptd-local", File.ReadAllText(Path.Combine(dir, "voice.txt")));
            window.Close();

            PlatformAudio.PreferredVoice = null;
            var reopened = new MainView { VoiceSettingPath = Path.Combine(dir, "voice.txt") };
            reopened.ReloadNarrationVoice();
            Assert.Equal("pt-br-x-ptd-local", PlatformAudio.PreferredVoice);
            return true;
        }
        finally
        {
            (PlatformAudio.Voices, PlatformAudio.PreferredVoice) = (voices, preferred);
            try { Directory.Delete(dir, true); } catch (IOException) { }
        }
    }, CancellationToken.None));
}
