using Urbe.Core;

namespace Urbe.Core.Tests;

/// <summary>#956 P2: a scene edited by touch — tap selects, drag moves, the playhead's keyframe toggles.</summary>
public sealed class VideoEditingTests
{
    private static (VideoTimeline Timeline, PublicationProject Project) Build(params VideoElement[] elements)
    {
        var project = VideoComposition.Synced(PublicationProject.Create("V", "n") with { Design = new PublicationDesign { Layout = "video" } });
        var scene = project.Video!.Scenes[0] with { Duration = 4, Transition = new VideoTransition { Kind = "cut" }, Elements = [.. elements] };
        project = project with { Video = project.Video.WithScene(scene) };
        return (VideoTimeline.Build(project, _ => ("n.md", "# T")), project);
    }

    [Fact]
    public void ATapFindsTheTopmostObjectCountingScaleAndRotation()
    {
        var (timeline, _) = Build(
            new VideoElement { Id = "fundo", Kind = "shape", Text = "rect", X = 0.5, Y = 0.5, Width = 1, Height = 1 },
            new VideoElement { Id = "titulo", Kind = "text", Text = "Oi", X = 0.3, Y = 0.3, Width = 0.2, Height = 0.1 },
            new VideoElement { Id = "girado", Kind = "text", Text = "45", X = 0.7, Y = 0.7, Width = 0.4, Height = 0.02, Keyframes = [new VideoKeyframe { Property = "rotation", Time = 0, Value = 90 }] });
        var frame = timeline.Evaluate(1);
        Assert.Equal("titulo", VideoEditing.HitTest(frame, 0.32, 0.31, timeline.Width, timeline.Height)!.Value.Element.Id);
        Assert.Equal("fundo", VideoEditing.HitTest(frame, 0.1, 0.9, timeline.Width, timeline.Height)!.Value.Element.Id);
        // turned 90°: a thin horizontal bar becomes a vertical one
        Assert.Equal("girado", VideoEditing.HitTest(frame, 0.7, 0.7 + 0.3 * timeline.Width / timeline.Height / 2 * 0.9, timeline.Width, timeline.Height)!.Value.Element.Id);
    }

    [Fact]
    public void MovingAStillObjectMovesItAndAnAnimatedOneGetsAKeyframeThere()
    {
        var still = new VideoElement { Id = "a", Kind = "text", Text = "A", X = 0.2, Y = 0.2 };
        var animated = new VideoElement
        {
            Id = "b", Kind = "text", Text = "B", Start = 1,
            Keyframes = [new VideoKeyframe { Property = "x", Time = 1, Value = 0.1 }, new VideoKeyframe { Property = "x", Time = 3, Value = 0.9 }]
        };
        var (timeline, project) = Build(still, animated);
        var scene = project.Video!.Scenes[0];
        scene = VideoEditing.Move(scene, "a", 2, 0.6, 0.4);
        Assert.Equal((0.6, 0.4), (scene.Elements[0].X, scene.Elements[0].Y));
        Assert.Empty(scene.Elements[0].Keyframes);
        scene = VideoEditing.Move(scene, "b", 2, 0.5, 0.8);
        var b = scene.Elements[1];
        Assert.Equal([1.0, 2.0, 3.0], b.Keyframes.Where(k => k.Property == "x").Select(k => k.Time));
        Assert.Equal(0.8, b.Keyframes.Single(k => k.Property == "y").Value);
        // a playhead 0.05 s from a keyframe changes it, not a second one beside it (seen in the app: 0,6 s and 0,65 s)
        scene = VideoEditing.Move(scene, "b", 2.05, 0.55, 0.8);
        Assert.Equal(3, scene.Elements[1].Keyframes.Count(k => k.Property == "x"));
        // the timeline shows it there at that time, the keyframes around it unchanged
        var moved = VideoTimeline.Build(project with { Video = project.Video.WithScene(scene) }, _ => ("n.md", "# T"));
        var state = moved.Evaluate(2).Layers[0].Elements.Single(e => e.Id == "b");
        Assert.Equal((0.55, 0.8), (state.X, state.Y));
        Assert.Equal(0.9, moved.Evaluate(3.5).Layers[0].Elements.Single(e => e.Id == "b").X, 6);
    }

    [Fact]
    public void ThePlayheadKeyframeTogglesAndKeepsTheLookOfThatInstant()
    {
        var (timeline, project) = Build(new VideoElement { Id = "a", Kind = "title", X = 0.5, Y = 0.4, Width = 0.5, Height = 0.2 });
        var scene = project.Video!.Scenes[0];
        var now = timeline.Evaluate(1.5).Layers[0].Elements[0];
        scene = VideoEditing.ToggleKeyframe(scene, "a", 1.5, now);
        Assert.True(VideoEditing.HasKeyframe(scene.Elements[0], 1.5));
        Assert.Equal(["opacity", "scale", "x", "y"], scene.Elements[0].Keyframes.Select(k => k.Property));
        Assert.All(scene.Elements[0].Keyframes, k => Assert.Equal("easeInOut", k.Easing));   // not robotic linear by default
        scene = VideoEditing.ToggleKeyframe(scene, "a", 1.5 + 0.005, now);
        Assert.Empty(scene.Elements[0].Keyframes);
        scene = VideoEditing.Resize(scene, "a", 1.2);
        Assert.Equal((0.6, 0.24), (Math.Round(scene.Elements[0].Width, 6), Math.Round(scene.Elements[0].Height, 6)));
        Assert.Empty(VideoEditing.Remove(scene, "a").Elements);
    }
}
