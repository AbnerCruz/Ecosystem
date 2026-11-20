using Urbe.Core;

namespace Urbe.Core.Tests;

/// <summary>
/// #1004 (motion graphics at the level of code-made ones), slice 1: procedural elements. One element with repeat N is N
/// instances whose motions are formulas of t, i, n and u — particles, orbits, grids and staggered entrances without
/// writing hundreds of elements, deterministic on every machine.
/// </summary>
public sealed class VideoProceduralTests
{
    private static (PublicationSourceCatalog Catalog, PublicationProject Project) Scene(params VideoElement[] elements)
    {
        var store = new DocumentStore();
        var a = store.Upsert(new DocumentInput { Path = "Roteiro/Cena.md", Content = "# Cena\n\nTexto.\n" });
        var catalog = new PublicationSourceCatalog(store);
        var project = VideoComposition.Synced(PublicationProject.Create("Vídeo", a.Id) with { Design = new PublicationDesign { Layout = "video" } });
        var scene = project.Video!.Scenes[0] with { Duration = 4, Transition = new VideoTransition(), Elements = elements.ToList() };
        return (catalog, project with { Video = project.Video.WithScene(scene) });
    }

    private static List<VideoElementState> At(VideoTimeline timeline, string id, double t) =>
        timeline.Evaluate(t).Layers.Single().Elements.Where(e => e.Id == id).ToList();

    [Fact]
    public void ARepeatedElementIsOneInstancePerIndexPlacedByItsFormulas()
    {
        // twelve dots on a circle that turns: x and y set by the angle of each one, plus the time
        var (catalog, project) = Scene(new VideoElement
        {
            Id = "orbita", Kind = "shape", Text = "circle", Width = 0.02, Height = 0.035, Repeat = 12,
            Motions =
            [
                new() { Property = "x", Mode = "set", Expression = "0.5 + 0.3*cos(tau*i/n + t)" },
                new() { Property = "y", Mode = "set", Expression = "0.5 + 0.3*sin(tau*i/n + t)" },
            ]
        });
        using var _ = catalog;
        PublicationProjectCodec.Validate(project);
        var timeline = VideoTimeline.Build(project, catalog);
        var dots = At(timeline, "orbita", 0);
        Assert.Equal(12, dots.Count);
        Assert.Equal(Enumerable.Range(0, 12), dots.Select(d => d.Instance));
        for (int i = 0; i < 12; i++)
        {
            Assert.Equal(0.5 + 0.3 * Math.Cos(Math.Tau * i / 12), dots[i].X, 9);
            Assert.Equal(0.5 + 0.3 * Math.Sin(Math.Tau * i / 12), dots[i].Y, 9);
        }
        // it turns with t
        Assert.Equal(0.5 + 0.3 * Math.Cos(1), At(timeline, "orbita", 1)[0].X, 9);
    }

    [Fact]
    public void AStaggeredEntranceIsOneFormula()
    {
        // each bar fades in 0.1 s after the previous one; u spreads them across the frame
        var (catalog, project) = Scene(new VideoElement
        {
            Id = "barras", Kind = "shape", Text = "rect", Repeat = 5, Width = 0.05, Height = 0.3,
            Motions =
            [
                new() { Property = "x", Mode = "set", Expression = "0.2 + 0.6*u" },
                new() { Property = "opacity", Mode = "set", Expression = "smooth((t - 0.1*i)/0.4)" },
            ]
        });
        using var _ = catalog;
        var timeline = VideoTimeline.Build(project, catalog);
        var at = At(timeline, "barras", 0.25);
        Assert.Equal([0.2, 0.35, 0.5, 0.65, 0.8], at.Select(b => Math.Round(b.X, 9)));
        // the first is half way in, the last has not started
        Assert.True(at[0].Opacity > at[1].Opacity && at[1].Opacity > at[2].Opacity);
        Assert.Equal(0, at[4].Opacity, 9);
        Assert.All(At(timeline, "barras", 1), b => Assert.Equal(1, b.Opacity, 9));
    }

    [Fact]
    public void RandomAndNoiseAreTheSameEveryTimeAndDifferPerInstance()
    {
        var (catalog, project) = Scene(new VideoElement
        {
            Id = "estrelas", Kind = "shape", Text = "circle", Repeat = 300, Width = 0.004, Height = 0.007,
            Motions =
            [
                new() { Property = "x", Mode = "set", Expression = "rand(i)" },
                new() { Property = "y", Mode = "set", Expression = "rand(i + 1000)" },
                new() { Property = "opacity", Mode = "set", Expression = "0.4 + 0.6*noise(2*t + 13*i)" },
            ]
        });
        using var _ = catalog;
        var one = At(VideoTimeline.Build(project, catalog), "estrelas", 1.3);
        var two = At(VideoTimeline.Build(project, catalog), "estrelas", 1.3);
        Assert.Equal(one, two);   // deterministic: preview and export, here and on the phone
        Assert.True(one.Select(s => Math.Round(s.X, 6)).Distinct().Count() > 290);
        Assert.All(one, s => Assert.InRange(s.X, 0, 1));
        // spread over the frame, not clumped
        Assert.InRange(one.Average(s => s.X), 0.4, 0.6);
        Assert.InRange(one.Average(s => s.Y), 0.4, 0.6);
        Assert.All(one, s => Assert.InRange(s.Opacity, 0.4, 1));
        // noise is smooth: a frame later it has barely moved
        var next = At(VideoTimeline.Build(project, catalog), "estrelas", 1.3 + 1 / 30.0);
        Assert.All(one.Zip(next), p => Assert.True(Math.Abs(p.First.Opacity - p.Second.Opacity) < 0.15));
    }

    [Fact]
    public void AddKeepsTheKeyframesAndSetReplacesThem()
    {
        var (catalog, project) = Scene(new VideoElement
        {
            Id = "a", Kind = "text", Text = "x", Repeat = 3, X = 0.3,
            Keyframes = [new() { Property = "x", Time = 0, Value = 0.4 }],
            Motions = [new() { Property = "x", Expression = "0.1*i" }, new() { Property = "y", Mode = "set", Expression = "0.2" }]
        });
        using var _ = catalog;
        var at = At(VideoTimeline.Build(project, catalog), "a", 1);
        Assert.Equal([0.4, 0.5, 0.6], at.Select(s => Math.Round(s.X, 9)));
        Assert.All(at, s => Assert.Equal(0.2, s.Y, 9));
    }

    [Fact]
    public void ARepeatedChildFollowsItsParent()
    {
        var (catalog, project) = Scene(
            new VideoElement { Id = "g", Kind = "group", X = 0.5, Y = 0.5, Keyframes = [new() { Property = "x", Time = 0, Value = 0.6 }] },
            new VideoElement { Id = "raios", Kind = "shape", Text = "line", Parent = "g", Repeat = 4, X = 0, Y = 0,
                Motions = [new() { Property = "rotation", Mode = "set", Expression = "90*i" }] });
        using var _ = catalog;
        var at = At(VideoTimeline.Build(project, catalog), "raios", 1);
        Assert.Equal(4, at.Count);
        Assert.All(at, s => Assert.Equal(0.6, s.X, 9));
        Assert.Equal([0.0, 90, 180, 270], at.Select(s => s.Rotation));
    }

    [Theory]
    [InlineData(0, "x", "add", "0.1")]
    [InlineData(VideoProcedural.MaxRepeat + 1, "x", "add", "0.1")]
    [InlineData(3, "x", "multiply", "0.1")]
    [InlineData(3, "x", "add", "rand(")]
    [InlineData(3, "x", "add", "k*i")]   // an unknown name
    public void ABadProceduralElementIsRefusedSayingWhy(int repeat, string property, string mode, string expression)
    {
        var (catalog, project) = Scene(new VideoElement { Id = "p", Kind = "shape", Text = "circle", Repeat = repeat,
            Motions = [new() { Property = property, Mode = mode, Expression = expression }] });
        using var _ = catalog;
        var error = Assert.Throws<InvalidDataException>(() => PublicationProjectCodec.Validate(project));
        Assert.Contains("p", error.Message);
    }

    [Fact]
    public void ARepeatedElementIsNotAParent()
    {
        var (catalog, project) = Scene(
            new VideoElement { Id = "muitos", Kind = "group", Repeat = 3 },
            new VideoElement { Id = "filho", Kind = "text", Text = "x", Parent = "muitos" });
        using var _ = catalog;
        Assert.Contains("repeat", Assert.Throws<InvalidDataException>(() => PublicationProjectCodec.Validate(project)).Message);
    }

    [Fact]
    public void ARepeatedElementSurvivesSavingAndReading()
    {
        var (catalog, project) = Scene(new VideoElement { Id = "p", Kind = "shape", Text = "circle", Repeat = 40,
            Motions = [new() { Property = "x", Mode = "set", Expression = "u" }] });
        using var _ = catalog;
        var back = PublicationProjectCodec.Parse(PublicationProjectCodec.Serialize(project));
        var e = back.Video!.Scenes[0].Elements[0];
        Assert.Equal(40, e.Repeat);
        Assert.Equal("set", e.Motions[0].Mode);
    }

    [Theory]
    [InlineData("min(0.2, 0.7)", 0.2)]
    [InlineData("max(0.2,0.7)", 0.7)]
    [InlineData("mod(-1, 3)", 2)]
    [InlineData("pow(2, 10)", 1024)]
    [InlineData("step(0.5, 0.6)", 1)]
    [InlineData("mix(10, 20, 0.25)", 12.5)]
    [InlineData("clamp(5, 0, 1)", 1)]
    [InlineData("0,5 + min(1, 2)", 1.5)]
    public void FunctionsOfSeveralArgumentsUseTheCommaAsSeparator(string expression, double expected) =>
        Assert.Equal(expected, VideoProcedural.Evaluate(expression, 0), 9);

    [Theory]
    [InlineData("min(1)")]
    [InlineData("min(1, 2, 3)")]
    [InlineData("mix(1, 2")]
    public void AWrongNumberOfArgumentsIsRefusedSayingWhy(string expression) =>
        Assert.Throws<FormatException>(() => VideoProcedural.Check(expression));

    [Fact]
    public void ParticlesRunningPastTheEdgeAreNotAnErrorButARepeatedTextIs()
    {
        var (catalog, project) = Scene(
            new VideoElement { Id = "chuva", Kind = "shape", Text = "circle", Width = 0.01, Height = 0.02, Repeat = 50,
                Motions = [new() { Property = "x", Mode = "set", Expression = "u" }, new() { Property = "y", Mode = "set", Expression = "fract(rand(i) + t) * 1.2 - 0.1" }] },
            new VideoElement { Id = "rotulos", Kind = "text", Text = "a", Width = 0.1, Height = 0.06, Repeat = 3,
                Motions = [new() { Property = "x", Mode = "set", Expression = "-0.2 + 0.1*i" }] });
        using var _ = catalog;
        var issues = VideoInspection.Run(VideoTimeline.Build(project, catalog));
        Assert.DoesNotContain(issues, i => i.ElementId == "chuva");
        Assert.Contains(issues, i => i.ElementId == "rotulos" && i.Severity == ReviewSeverity.Error);
    }

    [Fact]
    public void ASplitTextIsOneInstancePerLetterOrWordWithoutSpaces()
    {
        Assert.Equal([(0, 1), (1, 1), (3, 1)], VideoProcedural.Units("ab c", "letters"));
        Assert.Equal([(0, 3), (5, 2)], VideoProcedural.Units("Olá  mã", "words"));
        Assert.Equal(2, VideoProcedural.Units("e\u0301x", "letters").Count);   // é written as e + accent is one letter

        var (catalog, project) = Scene(new VideoElement
        {
            Id = "titulo", Kind = "text", Text = "Olá mundo", Split = "letters", Width = 0.8, Height = 0.2,
            Motions = [new() { Property = "opacity", Mode = "set", Expression = "sat((t - 0.1*i)/0.2)" }]
        });
        using var _ = catalog;
        PublicationProjectCodec.Validate(project);
        var timeline = VideoTimeline.Build(project, catalog);
        var letters = At(timeline, "titulo", 0.35);
        Assert.Equal(8, letters.Count);
        Assert.All(letters, l => Assert.Equal("letters", l.Split));
        Assert.Equal(1, letters[0].Opacity, 9);
        Assert.Equal(0, letters[7].Opacity, 9);
        // the review measures the text once and never calls its own letters overlapping
        Assert.DoesNotContain(VideoInspection.Run(timeline), i => i.ElementId?.Contains("titulo+titulo") == true);
        var back = PublicationProjectCodec.Parse(PublicationProjectCodec.Serialize(project));
        Assert.Equal("letters", back.Video!.Scenes[0].Elements[0].Split);
    }

    [Theory]
    [InlineData("shape", "letters", 1)]
    [InlineData("text", "sílabas", 1)]
    [InlineData("text", "words", 3)]
    public void ABadSplitIsRefusedSayingWhy(string kind, string split, int repeat)
    {
        var (catalog, project) = Scene(new VideoElement { Id = "t", Kind = kind, Text = kind == "shape" ? "rect" : "abc", Split = split, Repeat = repeat });
        using var _ = catalog;
        Assert.Contains("split", Assert.Throws<InvalidDataException>(() => PublicationProjectCodec.Validate(project)).Message);
    }

    [Fact]
    public void EffectsAreAnimatableAndCheckedAndChangeTheFramesKey()
    {
        var (catalog, project) = Scene(new VideoElement
        {
            Id = "sol", Kind = "shape", Text = "circle", Width = 0.1, Height = 0.18, Glow = 0.02, GlowColor = "#ffcc00", Gradient = "#ff0000",
            Motions = [new() { Property = "glow", Expression = "0.02*sin(2*pi*t)" }]
        });
        using var _ = catalog;
        PublicationProjectCodec.Validate(project);
        var timeline = VideoTimeline.Build(project, catalog);
        var a = At(timeline, "sol", 0)[0].Effects!;
        var b = At(timeline, "sol", 0.25)[0].Effects!;
        Assert.Equal((0.02, "#ffcc00", "#ff0000"), (a.Glow, a.GlowColor, a.Gradient));
        Assert.Equal(0.04, b.Glow, 9);
        Assert.NotEqual(timeline.Evaluate(0).Layers[0].StateKey, timeline.Evaluate(0.25).Layers[0].StateKey);
        var back = PublicationProjectCodec.Parse(PublicationProjectCodec.Serialize(project)).Video!.Scenes[0].Elements[0];
        Assert.Equal((0.02, "#ffcc00", "#ff0000"), (back.Glow, back.GlowColor, back.Gradient));
    }

    [Theory]
    [InlineData(0.5, null)]
    [InlineData(-0.1, null)]
    [InlineData(0.01, "amarelo")]
    public void ABadEffectIsRefusedSayingWhy(double glow, string? gradient)
    {
        var (catalog, project) = Scene(new VideoElement { Id = "s", Kind = "shape", Text = "rect", Glow = glow, Gradient = gradient });
        using var _ = catalog;
        var error = Assert.Throws<InvalidDataException>(() => PublicationProjectCodec.Validate(project)).Message;
        Assert.True(error.Contains("glow") || error.Contains("gradient"), error);
    }

    [Fact]
    public void ASceneCameraPansZoomsAndTurnsAndIsChecked()
    {
        var (catalog, project) = Scene(new VideoElement { Id = "a", Kind = "shape", Text = "rect" });
        using var _ = catalog;
        var scene = project.Video!.Scenes[0] with
        {
            Camera = new VideoCamera
            {
                Keyframes = [new() { Property = "zoom", Time = 0, Value = 1 }, new() { Property = "zoom", Time = 2, Value = 2, Easing = "linear" }],
                Motions = [new() { Property = "x", Expression = "0.1*t" }]
            }
        };
        project = project with { Video = project.Video.WithScene(scene) };
        PublicationProjectCodec.Validate(project);
        var timeline = VideoTimeline.Build(project, catalog);
        Assert.Null(timeline.Evaluate(0).Layers[0].Camera);   // at the start it is the whole frame
        var cam = timeline.Evaluate(1).Layers[0].Camera!;
        Assert.Equal((0.6, 0.5, 1.5, 0.0), (cam.X, cam.Y, cam.Zoom, cam.Rotation));
        Assert.NotEqual(timeline.Evaluate(1).Layers[0].StateKey, timeline.Evaluate(1.5).Layers[0].StateKey);
        Assert.NotNull(PublicationProjectCodec.Parse(PublicationProjectCodec.Serialize(project)).Video!.Scenes[0].Camera);

        var bad = project with { Video = project.Video.WithScene(scene with { Camera = new VideoCamera { Keyframes = [new() { Property = "scale", Time = 0, Value = 2 }] } }) };
        Assert.Contains("câmera", Assert.Throws<InvalidDataException>(() => PublicationProjectCodec.Validate(bad)).Message);
        var zero = project with { Video = project.Video.WithScene(scene with { Camera = new VideoCamera { Keyframes = [new() { Property = "zoom", Time = 0, Value = 0 }] } }) };
        Assert.Contains("zoom", Assert.Throws<InvalidDataException>(() => PublicationProjectCodec.Validate(zero)).Message);
    }

    [Fact]
    public void TheReviewMeasuresWhatTheCameraShows()
    {
        // a text well inside the frame, then a camera zoomed 3× on a corner: on the screen it leaves the frame
        var (catalog, project) = Scene(new VideoElement { Id = "caixa", Kind = "text", Text = "Olá", X = 0.5, Y = 0.5, Width = 0.3, Height = 0.15 });
        using var _ = catalog;
        Assert.DoesNotContain(VideoInspection.Run(VideoTimeline.Build(project, catalog)), i => i.ElementId == "caixa");
        var scene = project.Video!.Scenes[0] with { Camera = new VideoCamera { Keyframes = [new() { Property = "zoom", Time = 0, Value = 3 }, new() { Property = "x", Time = 0, Value = 0.4 }] } };
        var zoomed = project with { Video = project.Video.WithScene(scene) };
        var tl = VideoTimeline.Build(zoomed, catalog);
        var found = VideoInspection.Run(tl);
        Assert.True(found.Any(i => i.ElementId == "caixa" && i.Severity == ReviewSeverity.Error), string.Join(" | ", found.Select(i => i.Place + ": " + i.Message)) + " cam=" + tl.Evaluate(1).Layers[0].Camera);
        var cam = new VideoCameraState(0.4, 0.5, 3, 0);
        var at = cam.Apply(new VideoElementState("x", "shape", "", null, 0.5, 0.5, 0.1, 0.1, 1, 1, 0, 1, "#fff", 0.05), 1000, 500);
        Assert.Equal((0.8, 0.5, 3.0), (Math.Round(at.X, 9), at.Y, at.Scale));
    }
}
