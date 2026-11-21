using System.Diagnostics;
using SkiaSharp;
using Urbe.Client;
using Urbe.Core;

namespace Urbe.Client.Tests;

/// <summary>
/// #1004 slice 6: the three productions of the issue — a math explanation, a software spot with kinetic type, and
/// particles in orbit with a moving camera — composed by hand with the engine's vocabulary, rendered at 720p and
/// measured. They show what the engine can draw; they say nothing yet about what a model writes with it.
/// With URBE_EVIDENCE set, each leaves a contact sheet, an MP4 (when ffmpeg exists) and its numbers.
/// </summary>
[CollectionDefinition(nameof(VideoBenchmarkTests), DisableParallelization = true)]
public sealed class VideoBenchmarkCollection;

// heavy rendering: run alone, so it neither measures the other tests' load nor slows the UI tests' timed waits
[Collection(nameof(VideoBenchmarkTests))]
public sealed class VideoBenchmarkTests
{
    private static (PublicationSourceCatalog, PublicationProject) Video(string title, double seconds, string background, VideoCamera? camera, Dictionary<string, List<VideoElement>>? components, params VideoElement[] elements)
    {
        var store = new DocumentStore();
        var a = store.Upsert(new DocumentInput { Path = title + ".md", Content = "# " + title + "\n\nRoteiro.\n" });
        var catalog = new PublicationSourceCatalog(store);
        var project = VideoComposition.Synced(PublicationProject.Create(title, a.Id) with { Design = new PublicationDesign { Layout = "video" } });
        var scene = project.Video!.Scenes[0] with { Duration = seconds, Background = background, Transition = new VideoTransition { Kind = "cut" }, Camera = camera, Elements = elements.ToList() };
        return (catalog, project with { Video = project.Video with { Resolution = 720, Fps = 30, Subtitles = false, Components = components, Scenes = [scene] } });
    }

    private static VideoKeyframe K(string p, double t, double v, string easing = "easeInOut") => new() { Property = p, Time = t, Value = v, Easing = easing };
    private static VideoMotion M(string p, string e, string mode = "add") => new() { Property = p, Expression = e, Mode = mode };

    public static TheoryData<string> Productions => ["matematica", "software", "particulas", "codigo"];

    private static (PublicationSourceCatalog, PublicationProject) Make(string name) => name switch
    {
        "matematica" => Video("Ondas", 7, "#0d1117",
            new VideoCamera { Keyframes = [K("zoom", 0, 1), K("zoom", 7, 1.12, "linear"), K("x", 0, 0.5), K("x", 7, 0.55, "linear")] }, null,
            new VideoElement { Id = "titulo", Kind = "title", Text = "Uma onda leva energia", Split = "letters", X = 0.5, Y = 0.14, Width = 0.8, Height = 0.12, FontSize = 0.07, Color = "#e6edf3",
                Motions = [M("opacity", "sat((t - 0.03*i)/0.3)", "set"), M("y", "0.04*(1 - smooth((t - 0.03*i)/0.4))")] },
            new VideoElement { Id = "eixo", Kind = "shape", Text = "arrow", X = 0.42, Y = 0.55, Width = 0.62, Height = 0.006, Color = "#6e7681",
                Keyframes = [K("progress", 0.6, 0), K("progress", 1.4, 1)] },
            new VideoElement { Id = "onda", Kind = "plot", Text = "y = sin(x)\nx: 0..12.6\ny: -1.3..1.3", X = 0.42, Y = 0.55, Width = 0.62, Height = 0.42, Color = "#58a6ff", Glow = 0.012,
                Keyframes = [K("progress", 1.2, 0), K("progress", 4.2, 1, "linear")] },
            new VideoElement { Id = "ponto", Kind = "shape", Text = "circle", Width = 0.022, Height = 0.039, Color = "#ffa657", Glow = 0.02, Start = 4.2,
                Motions = [M("x", "0.11 + 0.62*fract((t-4.2)/2.8)", "set"), M("y", "0.55 - 0.42/2.6*sin(12.6*fract((t-4.2)/2.8))", "set")] },
            new VideoElement { Id = "formula", Kind = "math", Text = "y = A\\,\\sin(kx - \\omega t)", X = 0.84, Y = 0.3, Width = 0.24, Height = 0.12, Color = "#e6edf3", Start = 2.5,
                Keyframes = [K("opacity", 2.5, 0), K("opacity", 3.3, 1), K("blur", 2.5, 0.02), K("blur", 3.3, 0)] }),
        "software" => Video("Numa", 6, "#0b0b14", null,
            new()
            {
                ["recurso"] =
                [
                    new VideoElement { Id = "caixa", Kind = "shape", Text = "rect", X = 0, Y = 0, Width = 0.26, Height = 0.2, Color = "#7c3aed", Gradient = "#2563eb", Shadow = 0.03 },
                    new VideoElement { Id = "nome", Kind = "text", Text = "{{nome}}", X = 0, Y = 0, Width = 0.24, Height = 0.1, FontSize = 0.045, Color = "#f8fafc" },
                ]
            },
            new VideoElement { Id = "chamada", Kind = "title", Text = "Escreva. Estude. Crie.", Split = "words", X = 0.5, Y = 0.3, Width = 0.9, Height = 0.16, FontSize = 0.1, Color = "#f8fafc",
                Motions = [M("opacity", "sat((t - 0.35*i)/0.25)", "set"), M("scale", "1.4 - 0.4*smooth((t - 0.35*i)/0.3)", "set")] },
            new VideoElement { Id = "faixa", Kind = "shape", Text = "rect", X = 0.5, Y = 0.43, Width = 0.5, Height = 0.008, Color = "#a78bfa", Glow = 0.01,
                Keyframes = [K("progress", 1.3, 0), K("progress", 2, 1)] },
            new VideoElement { Id = "r1", Kind = "use", Component = "recurso", X = 0.2, Y = 0.68, Start = 2.2, Params = new() { ["nome"] = "Notas" },
                Keyframes = [K("opacity", 2.2, 0), K("opacity", 2.6, 1), K("y", 2.2, 0.76), K("y", 2.7, 0.68, "back")] },
            new VideoElement { Id = "r2", Kind = "use", Component = "recurso", X = 0.5, Y = 0.68, Start = 2.5, Params = new() { ["nome"] = "Flashcards" },
                Keyframes = [K("opacity", 2.5, 0), K("opacity", 2.9, 1), K("y", 2.5, 0.76), K("y", 3, 0.68, "back")] },
            new VideoElement { Id = "r3", Kind = "use", Component = "recurso", X = 0.8, Y = 0.68, Start = 2.8, Params = new() { ["nome"] = "Vídeos" },
                Keyframes = [K("opacity", 2.8, 0), K("opacity", 3.2, 1), K("y", 2.8, 0.76), K("y", 3.3, 0.68, "back")] }),
        "codigo" => Video("Esfera", 6, "#05060a", null, null,
            new VideoElement { Id = "esfera", Kind = "code", X = 0.5, Y = 0.5, Width = 1, Height = 1, Text = """
                function draw(ctx, t, info) {
                  var cx = ctx.width / 2, cy = ctx.height / 2 - 20, R = ctx.height * 0.32, n = 1500, golden = Math.PI * (3 - Math.sqrt(5));
                  ctx.blend('add');
                  for (var i = 0; i < n; i++) {
                    var y = 1 - 2 * (i + 0.5) / n, r = Math.sqrt(1 - y * y), a = golden * i;
                    var p = rotateX(rotateY({ x: Math.cos(a) * r * R, y: y * R, z: Math.sin(a) * r * R }, t * 25), 18);
                    var q = project(p.x, p.y, p.z, 900);
                    ctx.fill(hsl(200 + 80 * y + 30 * Math.sin(t), 0.9, 0.6, 0.35 + 0.65 * (1 - (p.z + R) / (2 * R))));
                    ctx.circle(cx + q.x, cy + q.y, 2.2 * q.scale);
                  }
                  ctx.blend('normal');
                  var title = 'ESFERA DE FIBONACCI', size = 34, x = cx - ctx.measure(title, size, true) / 2;
                  for (var k = 0; k < title.length; k++) {
                    var ch = title[k], w = ctx.measure(ch, size, true), u = ease((t - 0.8 - k * 0.05) / 0.4);
                    ctx.alpha(u); ctx.fill('#f8fafc');
                    ctx.text(ch, x + w / 2, ctx.height - 60 + 20 * (1 - u), size, 'center', true);
                    x += w;
                  }
                  ctx.alpha(1);
                }
                """ }),
        _ => Video("Órbitas", 6, "#05060a",
            new VideoCamera { Keyframes = [K("zoom", 0, 0.85), K("zoom", 6, 1.25)], Motions = [M("rotation", "4*t")] }, null,
            new VideoElement { Id = "estrelas", Kind = "shape", Text = "circle", Width = 0.003, Height = 0.005, Color = "#cbd5e1", Repeat = 300,
                Motions = [M("x", "-0.2 + 1.4*rand(i)", "set"), M("y", "-0.2 + 1.4*rand(i + 0.37)", "set"), M("opacity", "0.25 + 0.6*noise(i + 1.5*t)", "set")] },
            new VideoElement { Id = "nucleo", Kind = "shape", Text = "circle", Width = 0.07, Height = 0.124, Color = "#fde68a", Glow = 0.05, GlowColor = "#f59e0b",
                Motions = [M("scale", "1 + 0.05*sin(3*t)", "set")] },
            new VideoElement { Id = "particulas", Kind = "shape", Text = "circle", Width = 0.006, Height = 0.0107, Color = "#7dd3fc", Glow = 0.006, Repeat = 600,
                Motions = [M("x", "0.5 + (0.1 + 0.3*rand(i))*cos(tau*rand(i + 0.5) + t*(0.6 - 0.4*rand(i)))", "set"),
                           M("y", "0.5 + (0.1 + 0.3*rand(i))*sin(tau*rand(i + 0.5) + t*(0.6 - 0.4*rand(i)))*1.78", "set"),
                           M("opacity", "sat(t*2 - rand(i + 0.9))*(0.4 + 0.6*rand(i + 0.2))", "set")] },
            new VideoElement { Id = "titulo", Kind = "title", Text = "Órbitas", Split = "letters", X = 0.5, Y = 0.88, Width = 0.5, Height = 0.1, FontSize = 0.06, Color = "#f8fafc", Glow = 0.008,
                Motions = [M("opacity", "sat((t - 1 - 0.08*i)/0.3)", "set")] }),
    };

    [Theory]
    [MemberData(nameof(Productions))]
    public async Task AProductionRendersAt720pAndIsMeasured(string name)
    {
        var (catalog, project) = Make(name);
        using var _c = catalog;
        PublicationProjectCodec.Validate(project);
        var timeline = VideoTimeline.Build(project, catalog);
        Assert.True(timeline.IsComplete);
        using var renderer = new VideoFrameRenderer(timeline);
        using var frame = new SKBitmap(timeline.Width, timeline.Height);
        long before = GC.GetTotalMemory(false);
        var clock = Stopwatch.StartNew();
        var issuesFirst = VideoInspection.Run(timeline, e => VideoFrameRenderer.Fit(e, timeline.Width, timeline.Height));
        Assert.False(issuesFirst.Any(i => i.Severity == ReviewSeverity.Error), string.Join("\n", issuesFirst.Where(i => i.Severity == ReviewSeverity.Error).Select(i => i.Place + ": " + i.Message)));
        for (int i = 0; i < timeline.FrameCount; i += 3) renderer.RenderInto(timeline.FrameTime(i), frame);
        clock.Stop();
        double perFrame = clock.Elapsed.TotalMilliseconds / Math.Ceiling(timeline.FrameCount / 3.0);
        var issues = VideoInspection.Run(timeline, e => VideoFrameRenderer.Fit(e, timeline.Width, timeline.Height));
        Assert.False(issues.Any(i => i.Severity == ReviewSeverity.Error), string.Join("\n", issues.Where(i => i.Severity == ReviewSeverity.Error).Select(i => i.Place + ": " + i.Message)));
        var line = "video.bench " + name + ": " + timeline.Width + "x" + timeline.Height + ", " + timeline.FrameCount + " quadros, " +
                   perFrame.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " ms/quadro (amostra 1 em 3, CPU deste computador, sem GPU), " +
                   "memória gerenciada +" + (Math.Max(0, GC.GetTotalMemory(false) - before) >> 20) + " MB, " + issues.Count + " avisos da revisão";
        TestContext.Current.SendDiagnosticMessage(line);
        if (Environment.GetEnvironmentVariable("URBE_EVIDENCE") is { Length: > 0 } dir)
        {
            Directory.CreateDirectory(dir);
            File.WriteAllBytes(Path.Combine(dir, "bench-" + name + ".png"), renderer.RenderSheet([0.4, 1.2, 2.4, 3.6, timeline.Duration * 0.75, timeline.Duration - 0.2]));
            File.AppendAllText(Path.Combine(dir, "bench.txt"), line + "\n");
            if (FfmpegVideoEncoder.Find() is { } ffmpeg)
            {
                using var export = new VideoFrameRenderer(timeline);
                var result = await VideoExport.RunAsync(export, new FfmpegVideoEncoder(ffmpeg), Path.Combine(dir, "bench-" + name + ".mp4"), null, null, TestContext.Current.CancellationToken);
                File.AppendAllText(Path.Combine(dir, "bench.txt"), "  mp4 " + name + ": " + result.Info.Video!.Codec + " " + result.Info.Video.Width + "x" + result.Info.Video.Height + ", " + result.Info.Seconds.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " s\n");
            }
        }
    }
}
