using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Urbe.Client;
using Urbe.Core;

namespace Urbe.Client.Tests;

/// <summary>CST-170/209 in the studio: a video publication previewed, timed and exported as a verified MP4.</summary>
public sealed partial class HeadlessUiTests
{
    [Fact]
    public async Task PublicationVideo_PreviewTimingInProjectScriptInNoteAndVerifiedMp4() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var dir = Path.Combine(Path.GetTempPath(), "urbe-pubvideo-" + Guid.NewGuid().ToString("N"));
        var vault = Path.Combine(dir, "vault");
        Directory.CreateDirectory(vault);
        File.WriteAllText(Path.Combine(vault, "Cena.md"), "# Abertura\n\nUma frase do roteiro.\n");
        var view = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "app", "sel.json")));
        var window = new Window { Width = 412, Height = 860, Content = view };
        window.Show();
        async Task Pump() { for (int j = 0; j < 12; j++) { Dispatcher.UIThread.RunJobs(); await Task.Delay(15); } }
        Button Named(string name) => view.PublicationsStudio.GetLogicalDescendants().OfType<Button>().First(b => AutomationProperties.GetName(b) == name);
        void Click(Button b) => b.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        try
        {
            await view.OpenVaultAsync();
            await Settle(window, view);
            var create = view.CreatePublicationAsync("Cena.md");
            Dispatcher.UIThread.RunJobs();
            view.Dialogs.GetLogicalDescendants().OfType<TextBox>().First().Text = "Filme";
            ClickButtonWithText(view.Dialogs, "Criar");
            await create;
            await Pump();
            var studio = view.PublicationsStudio;
            studio.UpdateProject(studio.Project! with { Design = studio.Project.Design with { Layout = "video" } });
            await Pump();
            Assert.True(studio.IsVideo);
            var scene = Assert.Single(studio.Project!.Video!.Scenes);
            Assert.NotNull(studio.VideoPanel.Frame);
            Assert.Equal((640, 360), (studio.VideoPanel.Timeline!.Width, studio.VideoPanel.Timeline.Height));

            var target = Path.Combine(vault, "Publicações", "Filme.publication.json");
            Click(Named("Alongar cena Cena"));
            await Pump();
            for (int j = 0; j < 80 && !File.ReadAllText(target).Contains("\"duration\": " + (scene.Duration + 0.5).ToString(System.Globalization.CultureInfo.InvariantCulture)); j++) await Pump();
            var json = File.ReadAllText(target);
            Assert.Contains(scene.Id, json);
            Assert.DoesNotContain("frase do roteiro", json);
            Assert.Equal("# Abertura\n\nUma frase do roteiro.\n", File.ReadAllText(Path.Combine(vault, "Cena.md")));
            Assert.Equal(scene.Duration + 0.5, studio.VideoPanel.Timeline!.Duration, 6);

            if (FfmpegVideoEncoder.Find() is null) return true;   // the MP4 itself is covered where ffmpeg exists
            studio.UpdateProject(studio.Project! with { Video = studio.Project.Video! with { Resolution = 144 } });
            await Pump();
            (string Name, byte[] Bytes)? saved = null;
            view.SavePageFile = (name, bytes) => { saved = (name, bytes); return Task.FromResult<bool?>(true); };
            Click(Named("Exportar vídeo MP4"));
            for (int j = 0; j < 600 && saved is null; j++) await Pump();
            Assert.NotNull(saved);
            Assert.Equal("Filme.mp4", saved!.Value.Name);
            var info = Mp4Inspection.Read(new MemoryStream(saved.Value.Bytes))!;
            Assert.Equal("avc1", info.Video!.Codec);
            Assert.Equal((int)Math.Ceiling((scene.Duration + 0.5) * 30 - 1e-6), info.Video.SampleCount);
            Assert.Equal((256, 144), (info.Video.Width, info.Video.Height));
            return true;
        }
        finally { window.Close(); try { Directory.Delete(dir, true); } catch (IOException) { } }
    }, CancellationToken.None));
}
