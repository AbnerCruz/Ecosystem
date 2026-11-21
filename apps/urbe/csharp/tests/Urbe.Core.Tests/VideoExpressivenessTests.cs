using Urbe.Core;

namespace Urbe.Core.Tests;

/// <summary>
/// #956 P2 (expressividade): timing curves as CSS writes them (cubic-bezier), a vector path that draws itself, and
/// groups — an element that follows another (its parent) in position, scale, rotation and opacity.
/// </summary>
public sealed class VideoExpressivenessTests
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

    private static VideoElementState At(VideoTimeline timeline, string id, double t) =>
        timeline.Evaluate(t).Layers.Single().Elements.Single(e => e.Id == id);

    [Fact]
    public void CubicBezierFollowsTheCssCurve()
    {
        // cubic-bezier(0,0,1,1) is the straight line; the endpoints are always 0 and 1
        foreach (var u in new[] { 0, 0.25, 0.5, 0.9, 1 })
            Assert.Equal(u, VideoTimeline.Ease("cubic-bezier(0, 0, 1, 1)", u), 3);
        Assert.Equal(0, VideoTimeline.Ease("cubic-bezier(.34,1.56,.64,1)", 0), 6);
        Assert.Equal(1, VideoTimeline.Ease("cubic-bezier(.34,1.56,.64,1)", 1), 6);
        // CSS "ease" at half time is ~0.8024 (the value browsers compute)
        Assert.Equal(0.8024, VideoTimeline.Ease("cubic-bezier(0.25, 0.1, 0.25, 1)", 0.5), 3);
        // a y above 1 goes past the target and comes back: the overshoot of a "back" curve
        Assert.True(Enumerable.Range(1, 99).Max(i => VideoTimeline.Ease("cubic-bezier(.34,1.56,.64,1)", i / 100.0)) > 1.05);
        // the named overshoot is that same curve
        Assert.Equal(VideoTimeline.Ease("cubic-bezier(.34,1.56,.64,1)", 0.4), VideoTimeline.Ease("back", 0.4), 6);
    }

    [Theory]
    [InlineData("cubic-bezier(1.2, 0, 1, 1)")]   // x outside 0–1 is not a function of time
    [InlineData("cubic-bezier(0, 0, 1)")]
    [InlineData("cubic-bezier(a, 0, 1, 1)")]
    [InlineData("bounce")]
    public void AnInvalidCurveIsRejected(string easing)
    {
        var (catalog, project) = Scene(new VideoElement { Id = "t", Kind = "text", Text = "x", Keyframes = [new() { Property = "x", Time = 1, Value = 0.2, Easing = easing }] });
        using var _ = catalog;
        Assert.False(VideoKeyframe.IsEasing(easing));
        Assert.Throws<InvalidDataException>(() => PublicationProjectCodec.Validate(project));
    }

    [Fact]
    public void APathIsValidatedAsSvgPathData()
    {
        var (catalog, ok) = Scene(new VideoElement { Id = "p", Kind = "path", Text = "M 0 0 C 20 40, 60 40, 80 0 L 100 50 Z" });
        using var _ = catalog;
        PublicationProjectCodec.Validate(ok);
        foreach (var bad in new[] { "", "L 10 10", "M 0 0 <script>", "M 0 0 " + new string('L', 9000) })
        {
            var scene = ok.Video!.Scenes[0];
            var broken = ok with { Video = ok.Video.WithScene(scene with { Elements = [scene.Elements[0] with { Text = bad }] }) };
            Assert.Throws<InvalidDataException>(() => PublicationProjectCodec.Validate(broken));
        }
    }

    [Fact]
    public void AChildFollowsItsParent()
    {
        var (catalog, project) = Scene(
            new VideoElement
            {
                Id = "grupo", Kind = "group", X = 0.5, Y = 0.5,
                Keyframes =
                [
                    new() { Property = "x", Time = 0, Value = 0.3, Easing = "linear" }, new() { Property = "x", Time = 2, Value = 0.7, Easing = "linear" },
                    new() { Property = "opacity", Time = 0, Value = 0.5 },
                    new() { Property = "scale", Time = 0, Value = 2 },
                ]
            },
            // the child's x/y are an offset from the parent's centre, in fractions of the frame
            new VideoElement { Id = "filho", Kind = "text", Text = "rótulo", Parent = "grupo", X = 0.1, Y = 0, Keyframes = [new() { Property = "opacity", Time = 0, Value = 0.8 }] });
        using var _ = catalog;
        PublicationProjectCodec.Validate(project);
        var timeline = VideoTimeline.Build(project, catalog);
        var child = At(timeline, "filho", 1);
        // parent at x 0.5 (half way), scale 2: the offset doubles
        Assert.Equal(0.5 + 0.2, child.X, 6);
        Assert.Equal(0.5, child.Y, 6);
        Assert.Equal(2, child.Scale, 6);
        Assert.Equal(0.4, child.Opacity, 6);
        // the parent moves and the child goes with it
        Assert.Equal(0.3 + 0.2, At(timeline, "filho", 0).X, 6);
        // a group itself draws nothing; it is still evaluated, so its children can follow it
        Assert.Equal("group", At(timeline, "grupo", 1).Kind);
    }

    [Fact]
    public void ARotatedParentTurnsTheChildAroundItsCentre()
    {
        var (catalog, project) = Scene(
            new VideoElement { Id = "g", Kind = "group", X = 0.5, Y = 0.5, Keyframes = [new() { Property = "rotation", Time = 0, Value = 90 }] },
            new VideoElement { Id = "c", Kind = "shape", Text = "rect", Parent = "g", X = 0.1, Y = 0, Keyframes = [new() { Property = "rotation", Time = 0, Value = 10 }] });
        using var _ = catalog;
        var timeline = VideoTimeline.Build(project, catalog);
        var child = At(timeline, "c", 1);
        // 0.1 of the width to the right (128 px at 1280×720), turned 90° clockwise, is 128 px down: 128/720 of the height
        Assert.Equal(0.5, child.X, 6);
        Assert.Equal(0.5 + 0.1 * timeline.Width / timeline.Height, child.Y, 6);
        Assert.Equal(100, child.Rotation, 6);
    }

    [Fact]
    public void AChildIsHiddenWhileItsParentIs()
    {
        var (catalog, project) = Scene(
            new VideoElement { Id = "g", Kind = "group", Start = 2 },
            new VideoElement { Id = "c", Kind = "text", Text = "x", Parent = "g" });
        using var _ = catalog;
        var timeline = VideoTimeline.Build(project, catalog);
        Assert.Empty(timeline.Evaluate(1).Layers.Single().Elements);
        Assert.Equal(2, timeline.Evaluate(3).Layers.Single().Elements.Count);
    }

    [Theory]
    [InlineData("nenhum")]   // no such element
    [InlineData("a")]        // itself
    [InlineData("b")]        // a → b → a
    public void ABrokenParentIsRejected(string parentOfA)
    {
        var (catalog, project) = Scene(
            new VideoElement { Id = "a", Kind = "group", Parent = parentOfA },
            new VideoElement { Id = "b", Kind = "group", Parent = "a" });
        using var _ = catalog;
        Assert.Throws<InvalidDataException>(() => PublicationProjectCodec.Validate(project));
    }

    [Fact]
    public void TheCurveAtThePlayheadIsChosenByName()
    {
        var scene = new VideoScene
        {
            PlacementId = "p",
            Elements = [new VideoElement { Id = "a", Keyframes = [new() { Property = "x", Time = 0, Value = 0 }, new() { Property = "x", Time = 1, Value = 1 }, new() { Property = "y", Time = 1.05, Value = 1 }] }]
        };
        var changed = VideoEditing.SetEasing(scene, "a", 1, "back");
        // the keyframes at that instant (x at 1 s and y at 1.05 s) take the curve; the other keeps its own
        Assert.Equal(["easeInOut", "back", "back"], changed.Elements[0].Keyframes.Select(k => k.Easing));
        Assert.Equal("back", VideoEditing.EasingAt(changed.Elements[0], 1));
        Assert.Null(VideoEditing.EasingAt(changed.Elements[0], 0.5));
        Assert.All(VideoEditing.Curves, c => Assert.True(VideoKeyframe.IsEasing(c.Easing)));
        Assert.Throws<ArgumentException>(() => VideoEditing.SetEasing(scene, "a", 1, "bounce"));
    }

    [Fact]
    public void AThemeColoursWhatKeepsTheDefaultsAndLeavesChosenColoursAlone()
    {
        var (catalog, project) = Scene(
            new VideoElement { Id = "t", Kind = "title" },
            new VideoElement { Id = "f", Kind = "shape", Text = "rect" },
            new VideoElement { Id = "s", Kind = "path", Text = "M 0 0 L 10 10" },
            new VideoElement { Id = "mao", Kind = "text", Text = "x", Color = "#ff0000" });
        using var _ = catalog;
        var themed = project with { Video = project.Video! with { Theme = "papel" } };
        var timeline = VideoTimeline.Build(themed, catalog);
        var papel = VideoThemes.Find("papel")!;
        Assert.Same(papel, timeline.Theme);
        Assert.Equal(papel.Text, At(timeline, "t", 1).Color);
        Assert.Equal(papel.Secondary, At(timeline, "f", 1).Color);
        Assert.Equal(papel.Accent, At(timeline, "s", 1).Color);
        Assert.Equal("#ff0000", At(timeline, "mao", 1).Color);
        // changing the theme redraws the scenes (their content key changes)
        Assert.NotEqual(VideoTimeline.Build(project, catalog).Scenes[0].ContentKey, timeline.Scenes[0].ContentKey);
        Assert.Throws<InvalidDataException>(() => PublicationProjectCodec.Validate(project with { Video = project.Video with { Theme = "arco-iris" } }));
    }
}
