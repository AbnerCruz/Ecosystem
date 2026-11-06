using System.Diagnostics;
using SkiaSharp;
using Urbe.Client;
using Urbe.Core;

namespace Urbe.Client.Tests;

/// <summary>CST-209: frames drawn natively, the export pipeline, cancellation and a real H.264 MP4 checked by ffprobe.</summary>
public sealed class VideoRenderTests
{
    private static (PublicationSourceCatalog Catalog, PublicationProject Project) Project(int resolution = 180, int fps = 30, double seconds = 3)
    {
        var store = new DocumentStore();
        var a = store.Upsert(new DocumentInput { Path = "Aula.md", Content = "# Ondas\n\nUma onda leva energia. Veja o gráfico.\n" });
        var catalog = new PublicationSourceCatalog(store);
        var project = VideoComposition.Synced(PublicationProject.Create("Aula", a.Id) with { Design = new PublicationDesign { Layout = "video" } });
        var scene = project.Video!.Scenes[0] with
        {
            Duration = seconds, Background = "#0000ff", Transition = new VideoTransition { Kind = "cut" },
            Elements =
            [
                new VideoElement { Kind = "shape", Text = "rect", X = 0.25, Y = 0.5, Width = 0.2, Height = 0.3, Color = "#ff0000",
                    Keyframes = [new VideoKeyframe { Property = "x", Time = 0, Value = 0.25 }, new VideoKeyframe { Property = "x", Time = 2, Value = 0.75, Easing = "linear" }] },
                new VideoElement { Kind = "plot", Text = "y = sin(x)", X = 0.5, Y = 0.2, Width = 0.5, Height = 0.25 },
                new VideoElement { Kind = "math", Text = "\\frac{a}{b}", X = 0.85, Y = 0.2, Width = 0.2, Height = 0.2 },
                new VideoElement { Kind = "title", Y = 0.8, Height = 0.15 }
            ]
        };
        project = project with { Video = project.Video with { Resolution = resolution, Fps = fps, Scenes = [scene] } };
        return (catalog, project);
    }

    private static bool Reddish(SKColor c) => c.Red > 200 && c.Green < 60 && c.Blue < 60;

    [Fact]
    public void FramesShowTheAnimatedStateAtTheirTimeAndStillFramesComeFromTheCache()
    {
        var (catalog, project) = Project();
        using var _c = catalog;
        var timeline = VideoTimeline.Build(project, catalog);
        Assert.Equal((320, 180), (timeline.Width, timeline.Height));
        using var renderer = new VideoFrameRenderer(timeline);
        using (var start = renderer.Render(0))
        {
            Assert.True(Reddish(start.GetPixel(80, 90)));          // the square at x = 0.25
            Assert.False(Reddish(start.GetPixel(240, 90)));
            Assert.Equal(new SKColor(0, 0, 255), start.GetPixel(5, 175));   // the scene's background
        }
        using (var later = renderer.Render(2))
        {
            Assert.True(Reddish(later.GetPixel(240, 90)));         // moved to x = 0.75
            Assert.False(Reddish(later.GetPixel(80, 90)));
        }
        var drawn = renderer.LayersDrawn;
        using (renderer.Render(2.5)) { }                            // same look as at 2 s: no new drawing
        Assert.Equal(drawn, renderer.LayersDrawn);
        Assert.NotNull(VideoFrameRenderer.MathPath("\\frac{a}{b}"));
    }

    private sealed class FakeEncoder(Action<int>? onFrame = null) : IVideoEncoder
    {
        public string? Output;
        public int Frames;
        public bool Disposed;
        public VideoEncodeSettings? Settings;
        public string Name => "fake";
        public Task StartAsync(VideoEncodeSettings settings, string outputPath, CancellationToken cancel)
        {
            Settings = settings; Output = outputPath;
            File.WriteAllBytes(outputPath, [0, 0, 0, 8, (byte)'f', (byte)'r', (byte)'e', (byte)'e']);
            return Task.CompletedTask;
        }
        public Task WriteFrameAsync(ReadOnlyMemory<byte> rgba, CancellationToken cancel)
        {
            Assert.Equal(Settings!.Width * Settings.Height * 4, rgba.Length);
            Frames++;
            onFrame?.Invoke(Frames);
            return Task.CompletedTask;
        }
        public Task FinishAsync(CancellationToken cancel) => Task.CompletedTask;
        public ValueTask DisposeAsync() { Disposed = true; return ValueTask.CompletedTask; }
    }

    [Fact]
    public async Task ExportFeedsEveryFrameReportsProgressAndNeverClaimsAnUnverifiedFile()
    {
        var (catalog, project) = Project(seconds: 1);
        using var _c = catalog;
        using var renderer = new VideoFrameRenderer(VideoTimeline.Build(project, catalog));
        var dir = Directory.CreateTempSubdirectory("urbe-video-").FullName;
        var output = Path.Combine(dir, "aula.mp4");
        var encoder = new FakeEncoder();
        var reports = new List<VideoExportProgress>();
        var progress = new Progress<VideoExportProgress>(reports.Add);
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => VideoExport.RunAsync(renderer, encoder, output, null, progress, TestContext.Current.CancellationToken));
        Assert.Contains("verificação", error.Message);
        Assert.Equal(30, encoder.Frames);
        Assert.False(File.Exists(output));
        Assert.False(File.Exists(encoder.Output));
        Assert.True(encoder.Disposed);
        Directory.Delete(dir, true);
    }

    [Fact]
    public async Task CancellingStopsRenderingAndDeletesThePartialFile()
    {
        var (catalog, project) = Project(seconds: 3);
        using var _c = catalog;
        using var renderer = new VideoFrameRenderer(VideoTimeline.Build(project, catalog));
        var dir = Directory.CreateTempSubdirectory("urbe-video-").FullName;
        using var cancel = new CancellationTokenSource();
        var encoder = new FakeEncoder(n => { if (n == 10) cancel.Cancel(); });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => VideoExport.RunAsync(renderer, encoder, Path.Combine(dir, "x.mp4"), null, null, cancel.Token));
        Assert.Equal(10, encoder.Frames);
        Assert.Empty(Directory.GetFiles(dir));
        Directory.Delete(dir, true);
    }

    /// <summary>The real thing where ffmpeg exists: H.264 at 30 fps, 90 frames, 3 s, with AAC sound — checked by ffprobe too.</summary>
    [Fact]
    public async Task ARealMp4IsEncodedVerifiedAndPlayableWhenFfmpegExists()
    {
        if (FfmpegVideoEncoder.Find() is not { } ffmpeg) { Assert.Skip("ffmpeg ausente neste ambiente"); return; }
        var (catalog, project) = Project(resolution: 240, seconds: 3);
        using var _c = catalog;
        var dir = Directory.CreateTempSubdirectory("urbe-video-").FullName;
        try
        {
            var tone = Path.Combine(dir, "voz.wav");
            await using (var s = File.Create(tone))
            {
                VideoAudio.WriteWavHeader(s, 1, 16000, 16000 * 2 * 2);
                for (int i = 0; i < 32000; i++) s.Write(BitConverter.GetBytes((short)(8000 * Math.Sin(i * 2 * Math.PI * 440 / 16000))));
            }
            var mixed = Path.Combine(dir, "mix.wav");
            await using (var wav = File.OpenRead(tone))
            await using (var output = File.Create(mixed))
                VideoAudio.Mix([(new VideoAudioClip { Path = "voz.wav", Start = 0.5 }, wav)], 3, output, TestContext.Current.CancellationToken);
            using var renderer = new VideoFrameRenderer(VideoTimeline.Build(project, catalog));
            var target = Path.Combine(dir, "aula.mp4");
            var result = await VideoExport.RunAsync(renderer, new FfmpegVideoEncoder(ffmpeg), target, mixed, null, TestContext.Current.CancellationToken);
            Assert.True(File.Exists(target));
            Assert.Equal("avc1", result.Info.Video!.Codec);
            Assert.Equal(90, result.Info.Video.SampleCount);
            Assert.Equal((426, 240), (result.Info.Video.Width, result.Info.Video.Height));
            Assert.NotNull(result.Info.Audio);
            Assert.InRange(result.Info.Seconds, 2.9, 3.1);
            Assert.Empty(Directory.GetFiles(dir, "*.part.mp4"));

            var probe = Path.Combine(Path.GetDirectoryName(ffmpeg)!, OperatingSystem.IsWindows() ? "ffprobe.exe" : "ffprobe");
            if (File.Exists(probe))
            {
                var info = new ProcessStartInfo(probe) { RedirectStandardOutput = true, RedirectStandardError = true };
                foreach (var a in new[] { "-v", "error", "-count_frames", "-show_entries", "stream=codec_name,r_frame_rate,nb_read_frames,pix_fmt", "-of", "csv=p=0", target })
                    info.ArgumentList.Add(a);
                using var p = Process.Start(info)!;
                var text = await p.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
                await p.WaitForExitAsync(TestContext.Current.CancellationToken);
                Assert.Equal(0, p.ExitCode);
                Assert.Contains("h264,yuv420p,30/1,90", text);
                Assert.Contains("aac", text);
            }
        }
        finally { Directory.Delete(dir, true); }
    }
}
