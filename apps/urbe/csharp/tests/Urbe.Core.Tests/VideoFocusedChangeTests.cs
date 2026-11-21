using Urbe.Core;

namespace Urbe.Core.Tests;

/// <summary>#956 acceptance 5: one word edited in one scene changes that scene only, captions follow, and the record of the last MP4 reopens.</summary>
public sealed class VideoFocusedChangeTests
{
    [Fact]
    public void AWordEditedInOneSceneChangesThatSceneOnlyAndTheCaptionsFollow()
    {
        var store = new DocumentStore();
        var docs = new[] { "Começo", "Meio", "Fim" }.Select(n => store.Upsert(new DocumentInput { Path = n + ".md", Content = "# " + n + "\n\nA cena " + n.ToLowerInvariant() + " explica uma ideia.\n" })).ToList();
        using var catalog = new PublicationSourceCatalog(store);
        var project = VideoComposition.Synced(PublicationProject.Create("Aula", docs[0].Id).AddSource(docs[1].Id).AddSource(docs[2].Id) with { Design = new PublicationDesign { Layout = "video" } });
        var first = VideoTimeline.Build(project, catalog);
        Assert.Null(first.ChangedSince(null));   // no MP4 yet: nothing to compare
        Assert.Empty(first.ChangedSince(first.SceneKeys())!);

        // the MP4 was saved: its scenes go into the project file and come back when it is reopened
        project = PublicationProjectCodec.Parse(PublicationProjectCodec.Serialize(project with { Video = project.Video! with { Rendered = first.SceneKeys() } }));
        store.Upsert(new DocumentInput { Id = docs[1].Id, Path = docs[1].Path, Content = "# Meio\n\nA cena meio explica outra ideia.\n" });
        var second = VideoTimeline.Build(project, catalog);
        var changed = Assert.Single(second.ChangedSince(project.Video!.Rendered)!);
        Assert.Equal(project.Video.Scenes[1].Id, changed.Scene.Id);

        // the captions of the other scenes are the same, at the same times; the edited one says the new word inside its scene
        static string Of(VideoCue c) => c.Text + "@" + c.Start + "-" + c.End;
        Assert.Equal(first.Cues.Where(c => c.Start < first.Scenes[1].Start).Select(Of), second.Cues.Where(c => c.Start < second.Scenes[1].Start).Select(Of));
        var edited = second.Cues.Where(c => c.Start >= second.Scenes[1].Start && c.End <= second.Scenes[1].End).ToList();
        Assert.Contains(edited, c => c.Text.Contains("outra ideia"));
        Assert.DoesNotContain(second.Cues, c => c.Text.Contains("uma ideia") && c.Text.Contains("meio"));
        Assert.Equal(first.Duration, second.Duration);   // timing lives in the project: a word does not move the next scenes
    }
}
