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

    /// <summary>#956 acceptance 5: after one word changes in the middle scene, the frames of the other scenes come out pixel for pixel the same.</summary>
    [Fact]
    public void EditingOneSceneLeavesTheOtherScenesFramesIdentical()
    {
        var store = new DocumentStore();
        var docs = new[] { "Começo", "Meio", "Fim" }.Select(n => store.Upsert(new DocumentInput { Path = n + ".md", Content = "# " + n + "\n\nA cena " + n.ToLowerInvariant() + " explica uma ideia.\n" })).ToList();
        using var catalog = new PublicationSourceCatalog(store);
        var project = VideoComposition.Synced(PublicationProject.Create("Aula", docs[0].Id).AddSource(docs[1].Id).AddSource(docs[2].Id) with { Design = new PublicationDesign { Layout = "video" } });
        project = project with { Video = project.Video! with { Resolution = 144, Scenes = project.Video.Scenes.Select(s => s with { Elements = [.. s.Elements, new VideoElement { Kind = "script", Y = 0.75 }] }).ToList() } };
        byte[][] Frames(VideoTimeline t)
        {
            using var renderer = new VideoFrameRenderer(t);
            return t.Scenes.Select(s => { using var b = renderer.Render(s.Start + s.Duration * 0.6); return b.Bytes; }).ToArray();
        }
        var before = Frames(VideoTimeline.Build(project, catalog));
        store.Upsert(new DocumentInput { Id = docs[1].Id, Path = docs[1].Path, Content = "# Meio\n\nA cena meio explica outra ideia.\n" });
        var after = Frames(VideoTimeline.Build(project, catalog));
        Assert.Equal(before[0], after[0]);
        Assert.NotEqual(before[1], after[1]);
        Assert.Equal(before[2], after[2]);
        var timeline = VideoTimeline.Build(project, catalog);
        var rendered = timeline.SceneKeys();
        rendered[project.Video!.Scenes[1].Id] = "antes";
        Assert.Equal(" Mudou só a cena 2 (" + PublicationVideoPanel.Clock(timeline.Scenes[1].Start) + "–" + PublicationVideoPanel.Clock(timeline.Scenes[1].End) +
            "); as outras saíram iguais ao último MP4.", MainView.WhatChangedSinceLastMp4(timeline, rendered));
    }

    /// <summary>#956: every composed layout passes the measured review (real text measure) in every theme: nothing cut,
    /// out of the frame or under the subtitles, and its text readable on the theme's background.</summary>
    [Fact]
    public void EveryLayoutPassesTheMeasuredReview()
    {
        var (catalog, project) = Project();
        using var _c = catalog;
        var slots = new Dictionary<string, string>
        {
            ["titulo"] = "Relatividade do tempo", ["subtitulo"] = "Por que relógios discordam", ["texto"] = "Em movimento, o pulso de luz percorre um caminho mais longo.",
            ["esquerda"] = "Parado: 1 tique por segundo", ["direita"] = "A 87% de c: 1 tique a cada 2 s", ["numero"] = "38 µs", ["legenda"] = "de diferença por dia no GPS",
            ["formula"] = "\\gamma = \\frac{1}{\\sqrt{1 - v^2/c^2}}", ["nota"] = "cresce sem limite perto de c", ["grafico"] = "y = 1/sqrt(1-x^2)\nx: 0..0.95\ny: 0..4",
            ["autor"] = "Albert Einstein"
        };
        var sheets = new List<(string Name, byte[] Png)>();
        foreach (var theme in VideoThemes.All)
            foreach (var layout in VideoLayouts.All)
            {
                var scene = project.Video!.Scenes[0] with { Background = VideoThemes.DefaultBackground, Duration = 6, Elements = VideoLayouts.Build(layout.Id, slots, "c") };
                var built = project with { Video = project.Video with { Theme = theme.Id, Resolution = 360, Subtitles = true, Scenes = [scene] } };
                PublicationProjectCodec.Validate(built);
                var timeline = VideoTimeline.Build(built, catalog);
                var issues = VideoInspection.Run(timeline, e => VideoFrameRenderer.Fit(e, timeline.Width, timeline.Height));
                Assert.True(issues.Count == 0, theme.Id + "/" + layout.Id + ": " + string.Join(" | ", issues.Select(i => i.ElementId + " " + i.Message)));
                if (theme.Id == "noite") { using var r = new VideoFrameRenderer(timeline); sheets.Add((layout.Id, r.RenderSheet([3.5]))); }
            }
        if (Environment.GetEnvironmentVariable("URBE_EVIDENCE") is { Length: > 0 } dir)
        {
            Directory.CreateDirectory(dir);
            foreach (var (name, png) in sheets) File.WriteAllBytes(Path.Combine(dir, "layout-" + name + ".png"), png);
        }
    }

    /// <summary>#956: a theme paints the default background as its gradient and the default text in its colour.</summary>
    [Fact]
    public void AThemePaintsTheBackgroundAndTheText()
    {
        var (catalog, project) = Project();
        using var _c = catalog;
        var scene = project.Video!.Scenes[0] with { Background = VideoThemes.DefaultBackground,
            Elements = [new VideoElement { Kind = "shape", Text = "rect", X = 0.5, Y = 0.5, Width = 0.3, Height = 0.3 }] };
        project = project with { Video = project.Video with { Theme = "papel", Scenes = [scene] } };
        var timeline = VideoTimeline.Build(project, catalog);
        using var renderer = new VideoFrameRenderer(timeline);
        using var frame = renderer.Render(1);
        var corner = frame.GetPixel(4, 4);
        Assert.True(corner.Red > 220 && corner.Green > 215 && corner.Blue > 200, corner.ToString());   // the paper's light top
        var middle = frame.GetPixel(160, 90);
        Assert.Equal(SKColor.Parse(VideoThemes.Find("papel")!.Secondary), middle);                    // the shape in the theme's colour
        if (Environment.GetEnvironmentVariable("URBE_EVIDENCE") is { Length: > 0 } dir)
        {
            // the same scene in every theme, for the owner to compare
            Directory.CreateDirectory(dir);
            var sample = scene with
            {
                Elements =
                [
                    new VideoElement { Kind = "title", Y = 0.2, Height = 0.16, FontSize = 0.08 },
                    new VideoElement { Kind = "shape", Text = "rect", X = 0.25, Y = 0.55, Width = 0.22, Height = 0.3 },
                    new VideoElement { Kind = "path", Text = "M 0 50 L 90 50 M 66 26 L 90 50 L 66 74", X = 0.5, Y = 0.55, Width = 0.12, Height = 0.12, FontSize = 0.05 },
                    new VideoElement { Kind = "math", Text = "E = mc^2", X = 0.75, Y = 0.55, Width = 0.3, Height = 0.14 }
                ]
            };
            foreach (var theme in VideoThemes.All)
            {
                var themed = project with { Video = project.Video with { Theme = theme.Id, Resolution = 360, Scenes = [sample] } };
                var t = VideoTimeline.Build(themed, catalog);
                using var r = new VideoFrameRenderer(t);
                File.WriteAllBytes(Path.Combine(dir, "tema-" + theme.Id + ".png"), r.RenderSheet([1.5]));
            }
        }
    }

    /// <summary>#956 P2: a path draws itself along its length; a group draws nothing and carries its child with it.</summary>
    [Fact]
    public void APathDrawsOnAndAGroupCarriesItsChild()
    {
        var (catalog, project) = Project();
        using var _c = catalog;
        var scene = project.Video!.Scenes[0] with
        {
            Elements =
            [
                // a horizontal line across the left half, drawn from left to right in the first second
                new VideoElement { Id = "linha", Kind = "path", Text = "M 0 0 L 100 0", X = 0.25, Y = 0.3, Width = 0.4, Height = 0.1, Color = "#ff0000", FontSize = 0.1,
                    Keyframes = [new() { Property = "progress", Time = 0, Value = 0 }, new() { Property = "progress", Time = 1, Value = 1, Easing = "linear" }] },
                new VideoElement { Id = "grupo", Kind = "group", X = 0.25, Y = 0.75, Width = 0.3, Height = 0.3,
                    Keyframes = [new() { Property = "x", Time = 0, Value = 0.25 }, new() { Property = "x", Time = 2, Value = 0.75, Easing = "linear" }] },
                new VideoElement { Id = "filho", Kind = "shape", Text = "rect", Parent = "grupo", X = 0, Y = 0, Width = 0.1, Height = 0.1, Color = "#ff0000" }
            ]
        };
        project = project with { Video = project.Video with { Scenes = [scene] } };
        var timeline = VideoTimeline.Build(project, catalog);
        using var renderer = new VideoFrameRenderer(timeline);
        int Red(SKBitmap b, int y) => Enumerable.Range(0, b.Width).Count(x => Reddish(b.GetPixel(x, y)));
        using (var half = renderer.Render(0.5))
        using (var whole = renderer.Render(1.5))
        {
            int y = (int)(0.3 * 180);
            // half the progress draws about half the line, from its start
            Assert.InRange(Red(half, y) / (double)Red(whole, y), 0.4, 0.6);
            Assert.True(Reddish(half.GetPixel(40, y)));
            Assert.False(Reddish(half.GetPixel(110, y)));
            // the child sits at its parent's centre and moves with it; the group itself left no mark
            Assert.True(Reddish(half.GetPixel((int)(0.375 * 320), 135)));
        }
        using var end = renderer.Render(2.5);
        Assert.True(Reddish(end.GetPixel(240, 135)));
        Assert.False(Reddish(end.GetPixel(80, 135)));
    }

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

    /// <summary>
    /// #956: the agent's review measures text with the renderer that draws it — a long title in a small box is told as
    /// cut (it ends in "…" on screen), and after widening the box it fits; the contact sheet is a real PNG of the frames.
    /// </summary>
    [Fact]
    public void TheReviewMeasuresTextAsDrawnAndTheSheetShowsTheRealFrames()
    {
        var store = new DocumentStore();
        var note = store.Upsert(new DocumentInput { Path = "Demo.md", Content = "# Vídeos editáveis feitos das suas notas\n\nCada cena vem de uma nota.\n" });
        using var catalog = new PublicationSourceCatalog(store);
        var project = VideoComposition.Synced(PublicationProject.Create("Demo", note.Id) with { Design = new PublicationDesign { Layout = "video" } });
        VideoTimeline With(double width, double x, double height = 0.14)
        {
            var scene = project.Video!.Scenes[0] with
            {
                Duration = 3, Transition = new VideoTransition { Kind = "cut" },
                Elements = [new VideoElement { Id = "titulo", Kind = "title", X = x, Y = 0.4, Width = width, Height = height, FontSize = 0.1 }]
            };
            return VideoTimeline.Build(project with { Video = project.Video with { Resolution = 360, Scenes = [scene] } }, catalog);
        }
        IReadOnlyList<VideoIssue> Review(VideoTimeline t) => VideoInspection.Run(t, e => VideoFrameRenderer.Fit(e, t.Width, t.Height));
        var broken = With(0.16, 0.95, 0.06);
        var issues = Review(broken);
        Assert.Contains(issues, i => i.ElementId == "titulo" && i.Message.Contains("reticências"));
        Assert.Contains(issues, i => i.ElementId == "titulo" && i.Message.Contains("sai do quadro"));
        Assert.Contains(issues, i => With(0.2, 0.5) is var small && VideoInspection.Run(small, e => VideoFrameRenderer.Fit(e, small.Width, small.Height)).Any(x => x.Severity == ReviewSeverity.Error && x.Message.Contains("pequeno demais")));
        var fixedTimeline = With(0.86, 0.5, 0.3);   // two lines in any font (CI draws with a wider one)
        Assert.Empty(Review(fixedTimeline));

        byte[] Sheet(VideoTimeline t) { using var r = new VideoFrameRenderer(t); return r.RenderSheet([0.5, 2.5]); }
        var before = Sheet(broken);
        var after = Sheet(fixedTimeline);
        using var decoded = SKBitmap.Decode(after);
        Assert.Equal((2 * 640 + 3 * 8, 360 + 2 * 8), (decoded.Width, decoded.Height));
        Assert.NotEqual(before, after);
        if (Environment.GetEnvironmentVariable("URBE_EVIDENCE") is { Length: > 0 } dir)
        {
            Directory.CreateDirectory(dir);
            File.WriteAllBytes(Path.Combine(dir, "video-antes.png"), before);
            File.WriteAllBytes(Path.Combine(dir, "video-depois.png"), after);
        }
    }

    /// <summary>#956 P2: the cost of drawing every frame of a real 12 s video at 720p (headless measure, not a device claim).</summary>
    [Fact]
    public void RenderingTheExampleVideoAt720pIsMeasured()
    {
        var vault = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "fixtures", "e2e-vault");
        var store = new DocumentStore();
        foreach (var file in Directory.GetFiles(Path.Combine(vault, "Roteiro")))
            store.Upsert(new DocumentInput { Id = "doc_e2e_" + Path.GetFileNameWithoutExtension(file).ToLowerInvariant(), Path = "Roteiro/" + Path.GetFileName(file), Content = File.ReadAllText(file) });
        using var catalog = new PublicationSourceCatalog(store);
        var project = PublicationProjectCodec.Parse(File.ReadAllText(Path.Combine(vault, "Publicações", "Demo.publication.json")));
        var timeline = VideoTimeline.Build(project, catalog);
        Assert.True(timeline.IsComplete);
        using var renderer = new VideoFrameRenderer(timeline);
        using var frame = new SKBitmap(timeline.Width, timeline.Height);
        var clock = Stopwatch.StartNew();
        for (int i = 0; i < timeline.FrameCount; i++) renderer.RenderInto(timeline.FrameTime(i), frame);
        clock.Stop();
        double perFrame = clock.Elapsed.TotalMilliseconds / timeline.FrameCount;
        TestContext.Current.SendDiagnosticMessage("video.render720p " + timeline.FrameCount + " quadros: " + perFrame.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) +
            " ms/quadro, " + renderer.LayersDrawn + " camadas desenhadas (o resto veio do cache)");
        Console.WriteLine("video.render720p " + timeline.FrameCount + " frames: " + perFrame.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " ms/frame, layers drawn " + renderer.LayersDrawn);
        Assert.Equal(360, timeline.FrameCount);
        Assert.True(renderer.LayersDrawn < timeline.FrameCount, "still frames reuse their layer");
        // the example uses the new vocabulary (an arrow drawn on, a group turning its child, a "back" curve) and
        // still passes the measured review: nothing out of the frame, cut or over the subtitles
        Assert.Contains(project.Video!.Scenes.SelectMany(s => s.Elements), e => e.Kind == "path");
        Assert.Contains(project.Video.Scenes.SelectMany(s => s.Elements), e => e.Parent is not null);
        Assert.DoesNotContain(VideoInspection.Run(timeline, e => VideoFrameRenderer.Fit(e, timeline.Width, timeline.Height)), i => i.Severity == ReviewSeverity.Error);
        if (Environment.GetEnvironmentVariable("URBE_EVIDENCE") is { Length: > 0 } dir)
        {
            Directory.CreateDirectory(dir);
            File.WriteAllBytes(Path.Combine(dir, "video-expressividade.png"), renderer.RenderSheet([5.2, 7.0, 7.6, 8.1, 9.0, 10.5]));
        }
    }

    /// <summary>#1004: 2000 instances of one element (the most allowed) at 720p, drawn and measured.</summary>
    [Fact]
    public void TwoThousandParticlesAt720pAreDrawnAndMeasured()
    {
        var (catalog, project) = Project(resolution: 720, seconds: 2);
        using var _c = catalog;
        var scene = project.Video!.Scenes[0] with
        {
            Elements =
            [
                new VideoElement { Id = "galaxia", Kind = "shape", Text = "circle", Width = 0.004, Height = 0.007, Color = "#ff0000", Repeat = VideoProcedural.MaxRepeat,
                    Motions =
                    [
                        new() { Property = "x", Mode = "set", Expression = "0.5 + (0.05 + 0.35*rand(i))*cos(tau*rand(i + 0.5) + t*(0.3 + rand(i)))" },
                        new() { Property = "y", Mode = "set", Expression = "0.5 + (0.05 + 0.35*rand(i))*sin(tau*rand(i + 0.5) + t*(0.3 + rand(i)))*1.6" },
                        new() { Property = "opacity", Mode = "set", Expression = "0.4 + 0.6*noise(i + 3t)" },
                    ] }
            ]
        };
        project = project with { Video = project.Video with { Scenes = [scene] } };
        var timeline = VideoTimeline.Build(project, catalog);
        using var renderer = new VideoFrameRenderer(timeline);
        using var frame = new SKBitmap(timeline.Width, timeline.Height);
        var clock = Stopwatch.StartNew();
        const int frames = 20;
        for (int i = 0; i < frames; i++) renderer.RenderInto(i / 30.0, frame);
        double perFrame = clock.Elapsed.TotalMilliseconds / frames;
        Console.WriteLine("video.particles720p " + VideoProcedural.MaxRepeat + " instances: " + perFrame.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " ms/frame");
        int red = 0;
        for (int y = 0; y < frame.Height; y += 2) for (int x = 0; x < frame.Width; x += 2) if (frame.GetPixel(x, y).Red > 120 && frame.GetPixel(x, y).Blue < 120) red++;
        Assert.True(red > 300, "the particles are drawn: " + red);
        Assert.True(perFrame < 1000, "a frame of 2000 instances took " + perFrame + " ms");
        var clock2 = Stopwatch.StartNew();
        VideoInspection.Run(timeline);
        Assert.True(clock2.Elapsed.TotalSeconds < 20, "the review of 2000 instances took " + clock2.Elapsed);
    }

    /// <summary>#1004 slice 2: a title split into letters enters one letter after another, each in its place.</summary>
    [Fact]
    public void ASplitTitleEntersLetterByLetterInItsPlace()
    {
        var (catalog, project) = Project(resolution: 360, seconds: 3);
        using var _c = catalog;
        var scene = project.Video!.Scenes[0] with
        {
            Elements =
            [
                new VideoElement { Id = "t", Kind = "title", Text = "MMMMMMMMMM", Split = "letters", X = 0.5, Y = 0.5, Width = 0.9, Height = 0.3, FontSize = 0.12, Color = "#ff0000",
                    Motions = [new() { Property = "opacity", Mode = "set", Expression = "step(0.1*i, t)" }, new() { Property = "y", Expression = "0.1*(1 - step(0.1*i, t))" }] }
            ]
        };
        project = project with { Video = project.Video with { Scenes = [scene] } };
        var timeline = VideoTimeline.Build(project, catalog);
        using var renderer = new VideoFrameRenderer(timeline);
        int Red(SKBitmap b, int from, int to)
        {
            int n = 0;
            for (int x = from; x < to; x++) for (int y = 0; y < b.Height; y++) if (Reddish(b.GetPixel(x, y))) n++;
            return n;
        }
        int w = timeline.Width;
        using var whole = renderer.Render(2.0);
        using var half = renderer.Render(0.45);
        int left = Red(whole, 0, w / 2), right = Red(whole, w / 2, w);
        Assert.True(left > 50 && right > 50, "the whole title is drawn across its box: " + left + " / " + right);
        Assert.InRange(left / (double)right, 0.7, 1.4);
        // halfway through the entrance only the first letters are there, on the left, where they will stay
        Assert.True(Red(half, 0, w / 2) > left * 0.6, "first letters drawn");
        Assert.True(Red(half, w / 2, w) < right * 0.3, "last letters not yet");
    }

    /// <summary>#1004 slice 3: glow lights around the element, blur spreads it, a shadow falls below it, a gradient paints it.</summary>
    [Fact]
    public void GlowShadowBlurAndGradientAreDrawn()
    {
        var (catalog, project) = Project(resolution: 360, seconds: 1);
        using var _c = catalog;
        VideoTimeline Build(VideoElement e)
        {
            var scene = project.Video!.Scenes[0] with { Background = "#000000", Elements = [e] };
            return VideoTimeline.Build(project with { Video = project.Video with { Scenes = [scene] } }, catalog);
        }
        var plain = new VideoElement { Id = "c", Kind = "shape", Text = "rect", X = 0.5, Y = 0.5, Width = 0.2, Height = 0.3, Color = "#fefefe" };
        SKColor At(VideoElement e, double fx, double fy)
        {
            using var r = new VideoFrameRenderer(Build(e));
            using var b = r.Render(0.5);
            return b.GetPixel((int)(fx * b.Width), (int)(fy * b.Height));
        }
        // just outside the right edge of the box: dark without effects, lit by the glow, grey with the blur
        double edge = 0.5 + 0.1 + 0.02;
        Assert.True(At(plain, edge, 0.5).Red < 20);
        Assert.True(At(plain with { Glow = 0.05, GlowColor = "#00ff00" }, edge, 0.5).Green > 60);
        Assert.True(At(plain with { Blur = 0.03 }, 0.5 + 0.1 + 0.005, 0.5).Red > 20);
        // below the box and to the right, the shadow is darker than the background only over a light background
        var light = plain with { Shadow = 0.06 };
        var mid = At(light, 0.5, 0.5); Assert.True(mid.Red > 240, mid.ToString());
        // a gradient from white (top) to red (bottom)
        var g = plain with { Gradient = "#ff0000" };
        var top = At(g, 0.5, 0.37);
        var bottom = At(g, 0.5, 0.63);
        Assert.True(top.Green > 180 && bottom.Green < 80 && bottom.Red > 200, top + " → " + bottom);
        if (Environment.GetEnvironmentVariable("URBE_EVIDENCE") is { Length: > 0 } dir)
        {
            Directory.CreateDirectory(dir);
            var scene = project.Video!.Scenes[0] with
            {
                Background = "#101018",
                Elements =
                [
                    plain with { Id = "g", X = 0.2, Glow = 0.04, GlowColor = "#59c3ff", Color = "#bfe8ff" },
                    plain with { Id = "b", X = 0.4, Blur = 0.02, Color = "#ffb347" },
                    plain with { Id = "s", X = 0.6, Shadow = 0.05, Color = "#f5f5f5" },
                    plain with { Id = "d", X = 0.8, Gradient = "#8a2be2", Color = "#ff6ec7" },
                ]
            };
            using var r = new VideoFrameRenderer(VideoTimeline.Build(project with { Video = project.Video with { Scenes = [scene] } }, catalog));
            using var frame = r.Render(0.5);
            using var data = frame.Encode(SKEncodedImageFormat.Png, 100);
            File.WriteAllBytes(Path.Combine(dir, "video-efeitos.png"), data.ToArray());
        }
    }
}
