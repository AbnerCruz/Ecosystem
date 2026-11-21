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
    private sealed class PreviewClock : IPreviewAudio
    {
        public double Position { get; set; }
        public bool Ended { get; set; }
        public bool Failed => false;
        public bool Disposed { get; private set; }
        public void Dispose() => Disposed = true;
    }

    [Fact]
    public async Task PublicationVideo_PreviewFollowsTheAudioClockAndResumesAtThePlayhead() =>
        Assert.True(await Session.Dispatch(async () =>
        {
            var store = new DocumentStore();
            var note = store.Upsert(new DocumentInput { Path = "Cena.md", Content = "# Cena\n\nA fala." });
            using var catalog = new PublicationSourceCatalog(store);
            var project = VideoComposition.Synced(PublicationProject.Create("Cena", note.Id) with { Design = new PublicationDesign { Layout = "video" } });
            project = project with { Video = project.Video! with
            {
                Audio = [new VideoAudioClip { Kind = "voice", Path = "voz.wav", Start = 0 }]
            } };
            var panel = new PublicationVideoPanel();
            var timeline = VideoTimeline.Build(project, catalog, 144);
            var clock = new PreviewClock { Position = 1.5 };
            var starts = new List<double>();
            panel.StartAudioAsync = (_, start, _) =>
            {
                starts.Add(start);
                return Task.FromResult<IPreviewAudio?>(clock);
            };

            try
            {
                panel.Show(timeline, new Dictionary<string, byte[]>(), id => id);
                panel.TogglePlay();
                Assert.Equal(0, Assert.Single(starts));
                for (int i = 0; i < 50 && Math.Abs(panel.CurrentTime - 1.5) > 0.001; i++)
                {
                    await Task.Delay(10);
                    Dispatcher.UIThread.RunJobs();
                }
                Assert.Equal(1.5, panel.CurrentTime, 3);
                panel.TogglePlay();
                Assert.True(clock.Disposed);
                clock = new PreviewClock { Position = 1.5 };
                panel.TogglePlay();
                Assert.Equal(1.5, starts.Last(), 3);
                return true;
            }
            finally { panel.Release(); }
        }, CancellationToken.None));

    /// <summary>#956 (conversa da beta.57: "o vídeo não ficou pronto"): render_video speaks and encodes through the app, as the Estúdio exports.</summary>
    [Fact]
    public async Task PublicationVideo_TheAssistantGetsTheVoiceAndAVerifiedMp4() =>
        Assert.True(await Session.Dispatch(AssistantVideo, CancellationToken.None));

    private static async Task<bool> AssistantVideo()
    {
        var dir = Path.Combine(Path.GetTempPath(), "urbe-agentvideo-" + Guid.NewGuid().ToString("N"));
        var vault = Path.Combine(dir, "vault");
        Directory.CreateDirectory(vault);
        File.WriteAllText(Path.Combine(vault, "Cena.md"), "# Abertura\n\nUma frase do roteiro.\n");
        var view = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "app", "sel.json")));
        var window = new Window { Width = 412, Height = 860, Content = view };
        window.Show();
        try
        {
            await view.OpenVaultAsync();
            await Settle(window, view);
            IAssistantApp app = view;
            ConfigureSpeechProvider(view);
            var wav = await app.SpeakAsync("Uma frase.", CancellationToken.None);
            Assert.NotNull(wav);
            Assert.True(VideoAudio.ReadWavInfo(new MemoryStream(wav!)) is { Frames: 800 });
            view.Assistant.Config.AgentMedia.Remove("geral");
            Assert.Null(await app.SpeakAsync("Uma frase.", CancellationToken.None));
            if (FfmpegVideoEncoder.Find() is null) return true;   // the MP4 itself is checked where ffmpeg exists
            var store = new DocumentStore();
            var doc = store.Upsert(new DocumentInput { Path = "Cena.md", Content = File.ReadAllText(Path.Combine(vault, "Cena.md")) });
            using var catalog = new PublicationSourceCatalog(store);
            var project = VideoComposition.Synced(PublicationProject.Create("Filme", doc.Id) with { Design = new PublicationDesign { Layout = "video" } });
            project = project with { Video = project.Video! with { Resolution = 144, Scenes = [project.Video.Scenes[0] with { Duration = 1 }] } };
            var output = await app.RenderVideoAsync(VideoTimeline.Build(project, catalog), CancellationToken.None);
            var info = Mp4Inspection.Read(new MemoryStream(output.Mp4))!;
            Assert.Equal("avc1", info.Video!.Codec);
            Assert.Equal(30L, (long)info.Video.SampleCount);
            Assert.Contains("verificado", output.Checked);
            return true;
        }
        finally
        {
            window.Close();
            try { Directory.Delete(dir, true); } catch (IOException) { }
        }
    }

    [Fact]
    public async Task PublicationVideo_PreviewTimingInProjectScriptInNoteAndVerifiedMp4() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var dir = Path.Combine(Path.GetTempPath(), "urbe-pubvideo-" + Guid.NewGuid().ToString("N"));
        var vault = Path.Combine(dir, "vault");
        Directory.CreateDirectory(vault);
        using (var wav = File.Create(Path.Combine(vault, "trilha.wav")))
        {
            VideoAudio.WriteWavHeader(wav, 1, 8000, 8000L * 2);
            for (int k = 0; k < 8000; k++) wav.Write(BitConverter.GetBytes((short)(k < 4000 ? 30000 : 0)));
        }
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
            Assert.Contains(studio.VideoPanel.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text?.StartsWith("Vídeo sem áudio.") == true);
            Assert.NotNull(Named("Narrar roteiro"));
            Assert.NotNull(Named("Escolher voz"));
            Assert.NotNull(Named("Adicionar áudio"));

            var target = Path.Combine(vault, "Publicações", "Filme.publication.json");
            Click(Named("Alongar cena Cena"));
            await Pump();
            for (int j = 0; j < 80 && !File.ReadAllText(target).Contains("\"duration\": " + (scene.Duration + 0.5).ToString(System.Globalization.CultureInfo.InvariantCulture)); j++) await Pump();
            var json = File.ReadAllText(target);
            Assert.Contains(scene.Id, json);
            Assert.DoesNotContain("frase do roteiro", json);
            Assert.Equal("# Abertura\n\nUma frase do roteiro.\n", File.ReadAllText(Path.Combine(vault, "Cena.md")));
            Assert.Equal(scene.Duration + 0.5, studio.VideoPanel.Timeline!.Duration, 6);

            // the first look and the end of playback are the poster (the scene formed), never the black of the fade-in
            var poster = studio.VideoPanel.Timeline!.Scenes[0].Settled;
            Assert.True(poster > 0);
            studio.VideoPanel.Advance(studio.VideoPanel.Timeline.Duration + 1);
            Assert.Equal(poster, studio.VideoPanel.CurrentTime, 6);
            Assert.False(studio.VideoPanel.IsPlaying);
            // #956 P2: the timeline strip — a tap goes to that instant; the scene's right edge dragged makes it longer
            var strip = studio.VideoPanel.Strip;
            var before = studio.Project!.Video!.Scenes[0].Duration;
            Assert.True(strip.Bounds.Width > 100);
            Assert.False(strip.Press(strip.X(0.5)));   // a tap in the middle of the scene: no drag, a seek
            Assert.Equal(0.5, studio.VideoPanel.CurrentTime, 1);
            Assert.True(strip.Press(strip.X(studio.VideoPanel.Timeline!.Scenes[0].End) - 2));   // on the last scene's edge
            strip.Drag(strip.X(studio.VideoPanel.Timeline!.Scenes[0].End + 1.04));   // into the room after the end
            strip.Release();
            await Pump();
            Assert.Equal(before + 1, studio.Project!.Video!.Scenes[0].Duration, 6);
            Assert.True(strip.Press(strip.X(studio.VideoPanel.Timeline!.Scenes[0].End) + 3));   // and back, shorter
            strip.Drag(strip.X(studio.VideoPanel.Timeline!.Scenes[0].End - 1));
            strip.Release();
            await Pump();
            Assert.Equal(before, studio.Project!.Video!.Scenes[0].Duration, 6);
            // #956 P2: a tap on the title selects it; the inspector marks a keyframe at the playhead; a move goes to the project
            var panel = studio.VideoPanel;
            var title = studio.Project!.Video!.Scenes[0].Elements[0];
            panel.ShowAt(1);
            panel.TapAt(title.X, title.Y);
            Assert.Equal(title.Id, panel.Selected!.Value.Element);
            Click(Named("Marcar quadro-chave neste instante"));
            await Pump();
            Assert.True(VideoEditing.HasKeyframe(studio.Project!.Video!.Scenes[0].Elements[0], 1));
            Assert.NotNull(Named("Tirar o quadro-chave deste instante"));   // the button says what it does now
            panel.MoveSelected(0.5, 0.25);
            await Pump();
            Assert.Equal(0.25, studio.Project!.Video!.Scenes[0].Elements[0].Keyframes.Single(k => k.Property == "y" && Math.Abs(k.Time - 1) < 0.01).Value, 6);
            var width = studio.Project!.Video!.Scenes[0].Elements[0].Width;
            panel.PinchSelected(1.5);   // two fingers opened: the object grows by the pinch
            await Pump();
            Assert.Equal(width * 1.5, studio.Project!.Video!.Scenes[0].Elements[0].Width, 6);
            panel.PinchSelected(1.01);   // a wobble of the fingers changes nothing
            await Pump();
            Assert.Equal(width * 1.5, studio.Project!.Video!.Scenes[0].Elements[0].Width, 6);
            panel.TapAt(0.02, 0.98);   // nothing there: the selection ends
            Assert.Null(panel.Selected);

            // CST-172: the same captions as SRT and as WebVTT
            var captions = new Dictionary<string, string>();
            view.SavePageFile = (name, bytes) => { captions[name] = System.Text.Encoding.UTF8.GetString(bytes); return Task.FromResult<bool?>(true); };
            await view.ExportPublicationSubtitlesAsync("srt");
            await view.ExportPublicationSubtitlesAsync("vtt");
            Assert.StartsWith("1\n00:00:", captions["Filme.srt"]);
            Assert.StartsWith("WEBVTT\n\n00:00:", captions["Filme.vtt"]);
            Assert.Contains(" --> ", captions["Filme.vtt"]);
            // CST-172: a track's volume is adjusted on screen, and it can be taken out
            studio.UpdateProject(studio.Project! with { Video = studio.Project.Video! with { Audio = [new VideoAudioClip { Path = "trilha.wav", Kind = "music" }] } });
            await Pump();
            async Task Choose(string text) { for (int j = 0; j < 100 && !view.Dialogs.GetLogicalDescendants().OfType<TextBlock>().Any(t => t.Text == text); j++) await Pump(); ClickButtonWithText(view.Dialogs, text); }
            var previousPreview = PlatformAudio.StartPreview;
            var preview = new PreviewClock { Position = 0.25 };
            string? mixedPath = null;
            bool hasAudio = false;
            PlatformAudio.StartPreview = (path, _, _) =>
            {
                mixedPath = path;
                using var wav = File.OpenRead(path);
                var info = VideoAudio.ReadWavInfo(wav)!;
                wav.Position = info.DataOffset;
                using var pcm = new BinaryReader(wav);
                hasAudio = Enumerable.Range(0, 128).Any(_ => pcm.ReadInt16() != 0);
                return Task.FromResult<IPreviewAudio>(preview);
            };
            try
            {
                panel.ShowAt(0);
                panel.TogglePlay();
                for (int j = 0; j < 50 && !panel.IsPlaying; j++) await Pump();
                Assert.True(panel.IsPlaying);
                Assert.True(hasAudio);
                panel.TogglePlay();
                Assert.True(preview.Disposed);
                Assert.False(File.Exists(mixedPath));
            }
            finally { PlatformAudio.StartPreview = previousPreview; }
            var adjust = view.AdjustPublicationAudioAsync();
            await Choose("trilha.wav");
            for (int j = 0; j < 100 && !view.Dialogs.GetLogicalDescendants().OfType<TextBlock>().Any(t => t.Text?.Contains('█') == true); j++) await Pump();
            Assert.Contains(view.Dialogs.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text?.Contains("█▁") == true); // loud, then quiet
            await Choose("Volume");
            for (int j = 0; j < 100 && !view.Dialogs.GetLogicalDescendants().OfType<TextBox>().Any(); j++) await Pump();
            view.Dialogs.GetLogicalDescendants().OfType<TextBox>().First().Text = "50";
            ClickButtonWithText(view.Dialogs, "Aplicar");
            await adjust;
            Assert.Equal(0.5, studio.Project!.Video!.Audio.Single().Volume);
            var remove = view.AdjustPublicationAudioAsync();
            await Choose("trilha.wav");
            await Choose("Tirar do vídeo");
            await remove;
            Assert.Empty(studio.Project!.Video!.Audio);
            var speech = ConfigureSpeechProvider(view);
            await view.NarratePublicationVideoAsync();
            await Pump();
            Assert.Contains(speech.Requests, t => t.Text.Contains("Uma frase do roteiro."));
            var narration = Assert.Single(studio.Project!.Video!.Audio, a => a.Kind == "voice");
            Assert.Equal(("Narração/Filme/Cena 1.wav", 0.0), (narration.Path, narration.Start));
            Assert.True(File.Exists(Path.Combine(vault, "Narração", "Filme", "Cena 1.wav")));
            // CST-186: too little space is said before any frame is drawn, and nothing is saved
            var (encoder, free) = (PlatformVideo.CreateEncoder, MainView.FreeSpace);
            var started = false;
            PlatformVideo.CreateEncoder = () => new NeverStarted(() => started = true);
            MainView.FreeSpace = _ => 1000;
            (string, byte[])? none = null;
            view.SavePageFile = (name, bytes) => { none = (name, bytes); return Task.FromResult<bool?>(true); };
            try
            {
                var tight = view.ExportPublicationVideoAsync();
                for (int j = 0; j < 100 && !view.Dialogs.IsOpen; j++) await Pump();
                Assert.Contains(view.Dialogs.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text?.Contains("Libere espaço ou encurte o vídeo") == true);
                ClickButtonWithText(view.Dialogs, "Ok");
                await tight;
            }
            finally { (PlatformVideo.CreateEncoder, MainView.FreeSpace) = (encoder, free); }
            Assert.False(started);
            Assert.Null(none);
            if (FfmpegVideoEncoder.Find() is null) return true;   // the MP4 itself is covered where ffmpeg exists
            studio.UpdateProject(studio.Project! with { Video = studio.Project.Video! with { Resolution = 144 } });
            await Pump();
            (string Name, byte[] Bytes)? saved = null;
            view.SavePageFile = (name, bytes) => { saved = (name, bytes); return Task.FromResult<bool?>(true); };
            Click(Named("Exportar vídeo MP4"));
            for (int j = 0; j < 600 && saved is null; j++) await Pump();
            Assert.True(saved is not null, "dialog: " + string.Join(" | ", view.Dialogs.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text).Where(t => !string.IsNullOrEmpty(t)).Take(6)) + " / " + view.NoticeText);
            Assert.Equal("Filme.mp4", saved!.Value.Name);
            var info = Mp4Inspection.Read(new MemoryStream(saved.Value.Bytes))!;
            Assert.Equal("avc1", info.Video!.Codec);
            Assert.NotNull(info.Audio);   // the narration (a WAV track) is in the file: a .wav track used to fail the export
            Assert.Equal((int)Math.Ceiling((scene.Duration + 0.5) * 30 - 1e-6), info.Video.SampleCount);
            Assert.Equal((256, 144), (info.Video.Width, info.Video.Height));
            // #956 acceptance 5: the MP4 is recorded as the last export, with what each scene showed; made again, it says what changed
            for (int j = 0; j < 80 && !File.ReadAllText(target).Contains("\"rendered\""); j++) await Pump();
            Assert.Equal("mp4", studio.Project!.LastExport!.Format);
            Assert.Equal(studio.Project.Video!.Scenes.Single().Id, Assert.Single(studio.Project.Video.Rendered!).Key);
            saved = null;
            Click(Named("Exportar vídeo MP4"));
            for (int j = 0; j < 600 && saved is null; j++) await Pump();
            Assert.Contains("Nenhuma cena mudou desde o último MP4", view.NoticeText);
            return true;
        }
        finally { window.Close(); try { Directory.Delete(dir, true); } catch (IOException) { } }
    }, CancellationToken.None));

    private sealed class NeverStarted(Action started) : IVideoEncoder
    {
        public string Name => "teste";
        public Task StartAsync(VideoEncodeSettings settings, string outputPath, CancellationToken cancel) { started(); return Task.CompletedTask; }
        public Task WriteFrameAsync(ReadOnlyMemory<byte> rgba, CancellationToken cancel) => Task.CompletedTask;
        public Task FinishAsync(CancellationToken cancel) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
