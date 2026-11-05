using Urbe.Core;

namespace Urbe.Core.Tests;

/// <summary>CST-170/209: the editable audiovisual document and its deterministic timeline.</summary>
public sealed class VideoProjectTests
{
    private static (DocumentStore Store, PublicationSourceCatalog Catalog, PublicationProject Project, UrbeDocument A, UrbeDocument B) TwoScenes()
    {
        var store = new DocumentStore();
        var a = store.Upsert(new DocumentInput { Path = "Roteiro/Abertura.md", Content = "# Abertura\n\nOlá. Este é o **primeiro** trecho do vídeo.\n" });
        var b = store.Upsert(new DocumentInput { Path = "Roteiro/Fim.md", Content = "# Fim\n\nObrigado por assistir.\n" });
        var catalog = new PublicationSourceCatalog(store);
        var project = VideoComposition.Synced(PublicationProject.Create("Vídeo", a.Id).AddSource(b.Id) with
        {
            Design = new PublicationDesign { Layout = "video" }
        });
        return (store, catalog, project, a, b);
    }

    [Fact]
    public void ScenesFollowPlacementsAndKeepStableIdsAcrossReopenAndReorder()
    {
        var (_, catalog, project, _, b) = TwoScenes();
        using var _c = catalog;
        Assert.Equal(2, project.Video!.Scenes.Count);
        Assert.Equal(project.Sections.Select(s => s.Id), project.Video.Scenes.Select(s => s.PlacementId));
        var json = PublicationProjectCodec.Serialize(project);
        Assert.DoesNotContain("primeiro trecho", json);
        var reopened = PublicationProjectCodec.Parse(json);
        Assert.Equal(project.Video.Scenes.Select(s => s.Id), reopened.Video!.Scenes.Select(s => s.Id));
        Assert.Equal(project.Video.Scenes.SelectMany(s => s.Elements).Select(e => e.Id), reopened.Video.Scenes.SelectMany(s => s.Elements).Select(e => e.Id));

        var moved = VideoComposition.Synced(reopened.MoveSource(b.Id, 0));
        Assert.Equal(new[] { project.Video.Scenes[1].Id, project.Video.Scenes[0].Id }, moved.Video!.Scenes.Select(s => s.Id));
        var removed = VideoComposition.Synced(moved.RemoveSource(b.Id));
        Assert.Equal(project.Video.Scenes[0].Id, Assert.Single(removed.Video!.Scenes).Id);
        Assert.Same(removed, VideoComposition.Synced(removed));
    }

    [Fact]
    public void EditingTheScriptChangesTheNoteAndTimingStaysInTheProject()
    {
        var (store, catalog, project, a, _) = TwoScenes();
        using var _c = catalog;
        var before = VideoTimeline.Build(project, catalog);
        Assert.Contains("primeiro trecho", before.Scenes[0].Script);
        Assert.DoesNotContain("**", before.Scenes[0].Script);
        Assert.Equal("Abertura", before.Scenes[0].Title);
        Assert.Contains(before.Cues, c => c.Text.Contains("Olá."));

        // the script is the note: edited there, the next build shows it; the project file is unchanged
        var json = PublicationProjectCodec.Serialize(project);
        store.Upsert(new DocumentInput { Id = a.Id, Path = a.Path, Content = "# Abertura nova\n\nTexto reescrito na nota.\n" });
        var after = VideoTimeline.Build(project, catalog);
        Assert.Equal("Abertura nova", after.Scenes[0].Title);
        Assert.Contains(after.Cues, c => c.Text.Contains("reescrito"));
        Assert.Equal(json, PublicationProjectCodec.Serialize(project));
        Assert.NotEqual(before.Scenes[0].ContentKey, after.Scenes[0].ContentKey);
        Assert.Equal(before.Scenes[1].ContentKey, after.Scenes[1].ContentKey);

        // timing is the project: changing it never writes the note
        var note = store.Get(a.Id)!.Content;
        var scene = project.Video!.Scenes[0] with { Duration = 9 };
        var timed = project with { Video = project.Video.WithScene(scene) };
        Assert.Equal(9, VideoTimeline.Build(timed, catalog).Scenes[0].Duration);
        Assert.Equal(note, store.Get(a.Id)!.Content);
        Assert.Contains("\"duration\": 9", PublicationProjectCodec.Serialize(timed));
    }

    [Fact]
    public void KeyframesEaseMotionsAddAndTransitionsCrossfade()
    {
        var (_, catalog, project, _, _) = TwoScenes();
        using var _c = catalog;
        var element = new VideoElement
        {
            Kind = "shape", Text = "rect", X = 0.5,
            Keyframes = [new VideoKeyframe { Property = "x", Time = 0, Value = 0 }, new VideoKeyframe { Property = "x", Time = 2, Value = 1, Easing = "linear" },
                         new VideoKeyframe { Property = "opacity", Time = 1, Value = 0 }, new VideoKeyframe { Property = "opacity", Time = 3, Value = 1, Easing = "easeIn" }],
            Motions = [new VideoMotion { Property = "y", Expression = "0.1*sin(pi*t/2)" }]
        };
        var first = project.Video!.Scenes[0] with { Duration = 4, Transition = new VideoTransition { Kind = "cut" }, Elements = [element] };
        var second = project.Video.Scenes[1] with { Duration = 3, Transition = new VideoTransition { Kind = "fade", Seconds = 1 } };
        project = project with { Video = project.Video.WithScene(first).WithScene(second) };
        var timeline = VideoTimeline.Build(project, catalog);
        Assert.Equal(6, timeline.Duration, 6);           // 4 + 3 − 1 s of crossfade
        Assert.Equal(180, timeline.FrameCount);
        Assert.Equal(3, timeline.Scenes[1].Start, 6);

        var s = timeline.Evaluate(1).Layers.Single().Elements.Single();
        Assert.Equal(0.5, s.X, 6);                       // linear halfway
        Assert.Equal(0.5 + 0.1, s.Y, 6);                 // 0.1·sin(pi/2) added
        Assert.Equal(0, s.Opacity, 6);
        Assert.Equal(0.125, timeline.Evaluate(2).Layers[0].Elements[0].Opacity, 6);   // easeIn: 0.5³
        Assert.Equal(1, timeline.Evaluate(3.9).Layers[0].Elements[0].X, 6);           // held after the last keyframe

        var cross = timeline.Evaluate(3.5);
        Assert.Equal(2, cross.Layers.Count);
        Assert.Equal(1, cross.Layers[0].Opacity);
        Assert.Equal(0.5, cross.Layers[1].Opacity, 6);
        // deterministic: the same time is the same frame
        Assert.Equal(cross.Layers[1].StateKey, VideoTimeline.Build(project, catalog).Evaluate(3.5).Layers[1].StateKey);
        Assert.Equal(1, VideoTimeline.Ease("easeOut", 1));
        Assert.Equal(0, VideoTimeline.Ease("step", 0.99));
    }

    [Fact]
    public void FrameCacheReusesStillFramesAndReRendersOnlyTheEditedScene()
    {
        var (_, catalog, project, _, _) = TwoScenes();
        using var _c = catalog;
        project = project with { Video = project.Video! with { Scenes = project.Video.Scenes.Select(s => s with { Duration = 2, Transition = new VideoTransition { Kind = "cut" } }).ToList() } };
        var renders = 0;
        using var cache = new VideoFrameCache<string>(1 << 20, _ => 10);
        void RenderAll(VideoTimeline t)
        {
            for (int i = 0; i < t.FrameCount; i++)
                foreach (var layer in t.Frame(i).Layers) cache.GetOrAdd(layer.StateKey, () => { renders++; return layer.StateKey; });
        }
        var timeline = VideoTimeline.Build(project, catalog);
        RenderAll(timeline);
        // the default title animates for 0.6 s (18 frames + its end state) and then holds still
        Assert.True(renders < timeline.FrameCount / 2, renders + " renders");
        var first = renders;
        renders = 0;
        RenderAll(VideoTimeline.Build(project, catalog));
        Assert.Equal(0, renders);

        var edited = project.Video!.Scenes[1] with { Background = "#334455" };
        RenderAll(VideoTimeline.Build(project with { Video = project.Video.WithScene(edited) }, catalog));
        Assert.Equal(first / 2, renders);   // only scene 2's looks again
    }

    [Fact]
    public void SubtitlesAndValidationRejectUnsafeValuesWithoutTouchingTheVault()
    {
        var (_, catalog, project, _, _) = TwoScenes();
        using var _c = catalog;
        var srt = VideoTimeline.Build(project, catalog).Srt();
        Assert.StartsWith("1\n00:00:00,", srt);
        Assert.Contains(" --> ", srt);
        Assert.StartsWith("WEBVTT", VideoTimeline.Build(project, catalog).Vtt());
        var bad = project with { Video = project.Video! with { Fps = 0 } };
        Assert.Throws<InvalidDataException>(() => PublicationProjectCodec.Serialize(bad));
        var escape = project with { Video = project.Video with { Audio = [new VideoAudioClip { Path = "../fora.wav" }] } };
        Assert.Throws<InvalidDataException>(() => PublicationProjectCodec.Serialize(escape));
        var motion = project.Video.Scenes[0] with { Elements = [new VideoElement { Motions = [new VideoMotion { Expression = "rm -rf" }] }] };
        Assert.Throws<InvalidDataException>(() => PublicationProjectCodec.Serialize(project with { Video = project.Video.WithScene(motion) }));
        Assert.Equal((1280, 720), project.Video.FrameSize);
        Assert.Equal((720, 1280), (project.Video with { Aspect = "9:16" }).FrameSize);
    }

    [Fact]
    public void AudioMixPlacesTrimsAndLimitsClipsAt44kStereo()
    {
        static MemoryStream Tone(int rate, int channels, double seconds, short value)
        {
            var s = new MemoryStream();
            long frames = (long)(rate * seconds);
            VideoAudio.WriteWavHeader(s, channels, rate, frames * channels * 2);
            for (long i = 0; i < frames * channels; i++) s.Write(BitConverter.GetBytes(value));
            return s;
        }
        var voice = Tone(16000, 1, 1, 20000);
        var music = Tone(48000, 2, 3, 20000);
        var output = new MemoryStream();
        VideoAudio.Mix([(new VideoAudioClip { Path = "v.wav", Start = 0.5 }, voice), (new VideoAudioClip { Path = "m.wav", Start = 0, TrimStart = 1, Volume = 0.75 }, music)], 3, output, TestContext.Current.CancellationToken);
        var info = VideoAudio.ReadWavInfo(output)!;
        Assert.Equal((2, 44100), (info.Channels, info.SampleRate));
        Assert.Equal(3, info.Seconds, 3);
        short At(double seconds)
        {
            output.Position = info.DataOffset + (long)(seconds * 44100) * 4;
            var b = new byte[2]; output.ReadExactly(b); return BitConverter.ToInt16(b);
        }
        Assert.InRange(At(0.2), 14800, 15200);          // music alone at 3/4 volume
        Assert.Equal(32767, At(1.0));                    // voice + music: limited, not wrapped
        Assert.Equal(0, At(2.5));                        // music ended (3 s − 1 s trimmed)
    }
}
