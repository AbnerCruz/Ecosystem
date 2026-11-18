using System.Text.Json.Nodes;
using Urbe.Core;
using Urbe.Core.Assistant;

namespace Urbe.Core.Tests;

/// <summary>#956: the agent looks at the video it made, finds what is wrong and fixes only that scene.</summary>
public sealed class VideoDirectionTests
{
    private sealed class Host : IAssistantHost, IAssistantVideoFrames, IAssistantDocumentVision
    {
        public DocumentStore Documents { get; } = new();
        public KnowledgeIndex Knowledge { get; }
        public Host() => Knowledge = new(Documents);
        public int ReadChars => 2000;
        public (string? Path, string? Selection) EditorContext() => (null, null);
        public Task<UrbeDocument> CreateNoteAsync(string path, string content) => Task.FromResult(Documents.Upsert(new() { Path = path, Content = content }));
        public Task<UrbeDocument> SaveNoteAsync(UrbeDocument doc, string content) => Task.FromResult(Documents.Upsert(new() { Id = doc.Id, Path = doc.Path, Content = content }));
        public Task<UrbeDocument> RenameNoteAsync(UrbeDocument doc, string newPath) => throw new NotSupportedException();
        public Task TrashNoteAsync(UrbeDocument doc) => throw new NotSupportedException();
        public Task CreateFolderAsync(string path) => throw new NotSupportedException();
        public void OpenNote(string path) { }
        public void Remember(string fact) { }
        public void SetPlan(IReadOnlyList<AiPlanStep> steps) { }
        public IAssistantVideoFrames? VideoFrames => this;
        /// <summary>A stand-in for the renderer: text longer than 50 characters per unit of box width is cut.</summary>
        public VideoTextFit Fit(VideoElementState e, int width, int height) => new(1, e.Text.Length > 50 * e.Width);
        public List<IReadOnlyList<double>> Sheets { get; } = [];
        public Task<byte[]> RenderSheetAsync(VideoTimeline timeline, IReadOnlyList<double> times, CancellationToken cancel = default)
        { Sheets.Add(times); return Task.FromResult(new byte[] { 0x89, (byte)'P', (byte)'N', (byte)'G' }); }
        public string? Instruction { get; private set; }
        public Task<string> ExaminePageAsync(string reference, byte[] singlePagePdf, int page, CancellationToken c = default) => throw new NotSupportedException();
        public Task<string> ExamineImageAsync(string reference, byte[] image, string mimeType, string? instruction = null, CancellationToken c = default)
        { Instruction = instruction; return Task.FromResult("Quadro 1: o título sai pela direita."); }
    }

    private static VideoTimeline Timeline(params VideoElement[] elements)
    {
        var project = VideoComposition.Synced(PublicationProject.Create("V", "n1") with { Design = new PublicationDesign { Layout = "video" } });
        var scene = project.Video!.Scenes[0] with { Duration = 4, Transition = new VideoTransition { Kind = "cut" }, Elements = [.. elements] };
        project = project with { Video = project.Video.WithScene(scene) };
        return VideoTimeline.Build(project, _ => ("n1.md", "# Título\n\nUma frase curta para a legenda."));
    }

    [Fact]
    public void Inspection_FindsWhatIsOutOfTheFrameCutUnreadableOrUnderTheSubtitle()
    {
        var timeline = Timeline(
            new VideoElement { Id = "fora", Kind = "title", X = 0.9, Y = 0.3, Width = 0.5, Height = 0.15 },
            new VideoElement { Id = "escuro", Kind = "text", Text = "Sombra", Color = "#333333", X = 0.5, Y = 0.5, Width = 0.3, Height = 0.1 },
            new VideoElement { Id = "embaixo", Kind = "text", Text = "Rodapé", X = 0.5, Y = 0.88, Width = 0.4, Height = 0.08 },
            new VideoElement { Id = "longo", Kind = "text", Text = "Um rótulo comprido demais para a caixa estreita", X = 0.25, Y = 0.7, Width = 0.2, Height = 0.08 });
        var issues = VideoInspection.Run(timeline, e => new VideoTextFit(1, e.Text.Length > 40 * e.Width));
        Assert.Contains(issues, i => i.ElementId == "fora" && i.Severity == ReviewSeverity.Error && i.Message.Contains("sai do quadro"));
        Assert.Contains(issues, i => i.ElementId == "escuro" && i.Message.Contains("contraste"));
        Assert.Contains(issues, i => i.ElementId == "embaixo" && i.Message.Contains("legenda"));
        Assert.Contains(issues, i => i.ElementId == "longo" && i.Severity == ReviewSeverity.Error && i.Message.Contains("reticências"));
        Assert.Single(issues, i => i.ElementId == "fora" && i.Message.Contains("sai do quadro"));   // once, not once per instant
    }

    [Fact]
    public void Inspection_IsSilentOnAWellPlacedSceneAndSeesAnimationLeaveTheFrame()
    {
        var clean = Timeline(new VideoElement { Id = "ok", Kind = "title", X = 0.5, Y = 0.4, Width = 0.7, Height = 0.2 });
        Assert.Empty(VideoInspection.Run(clean, _ => new VideoTextFit(1, false)));
        // in place at the start, then slides off to the right at 3 s
        var sliding = Timeline(new VideoElement
        {
            Id = "sai", Kind = "title", X = 0.5, Y = 0.4, Width = 0.5, Height = 0.2,
            Keyframes = [new VideoKeyframe { Property = "x", Time = 0, Value = 0.5 }, new VideoKeyframe { Property = "x", Time = 3, Value = 1.1 }]
        });
        var issue = Assert.Single(VideoInspection.Run(sliding), i => i.Message.Contains("sai do quadro"));
        Assert.InRange(issue.Time, 1, 3);
    }

    /// <summary>#956 acceptance: a provoked cut title is found by the review, fixed in its scene only, and the review comes back clean.</summary>
    [Fact]
    public async Task TheAgentFindsACutTitleFixesOnlyThatSceneAndLooksAgain()
    {
        var host = new Host();
        host.Documents.Upsert(new() { Path = "Roteiro/Abertura.md", Content = "# Abertura\n\nO Urbe faz vídeos a partir das notas." });
        host.Documents.Upsert(new() { Path = "Roteiro/Fim.md", Content = "# Fim\n\nObrigado." });
        var write = AssistantPublicationTools.All.Single(t => t.Name == "write_publication");
        var spec = JsonNode.Parse("""{"schemaVersion":1,"title":"Demo","sources":[{"documentId":"Roteiro/Abertura.md"},{"documentId":"Roteiro/Fim.md"}],"design":{"layout":"video"},"formats":["mp4"]}""")!.AsObject();
        await write.Run(new JsonObject { ["path"] = "Demo.publication.json", ["spec"] = spec }, host);
        var saved = PublicationProjectCodec.Parse(host.Documents.Get("Demo.publication.json")!.Content);
        Assert.Equal(2, saved.Video!.Scenes.Count);   // the scenes are saved, so their ids stay
        var first = saved.Video.Scenes[0];
        var other = saved.Video.Scenes[1];

        var edit = AssistantVideoDirectionTools.All.Single(t => t.Name == "edit_video_scene");
        var review = AssistantPublicationTools.All.Single(t => t.Name == "review_publication");
        int Revision() => host.Documents.Get("Demo.publication.json")!.Revision;
        // the provoked problem: a long label in a narrow box, pushed off the right edge
        await edit.Run(new JsonObject
        {
            ["path"] = "Demo.publication.json", ["expected_revision"] = Revision(), ["scene_id"] = first.Id,
            ["elements"] = new JsonArray(new JsonObject { ["id"] = "rotulo", ["kind"] = "text", ["text"] = "Vídeos editáveis feitos das suas notas", ["x"] = 0.9, ["y"] = 0.3, ["width"] = 0.3, ["height"] = 0.1 })
        }, host);
        var before = (await review.Run(new JsonObject { ["path"] = "Demo.publication.json" }, host)).Content;
        Assert.Contains("rotulo", before);
        Assert.Contains("sai do quadro", before);
        Assert.Contains("reticências", before);
        Assert.Contains(first.Id, before);   // the scenes are listed by id for the edit

        // a stale revision is refused and says the current one
        var stale = await Assert.ThrowsAsync<AiToolException>(() => edit.Run(new JsonObject { ["path"] = "Demo.publication.json", ["expected_revision"] = 0, ["scene_id"] = first.Id, ["duration"] = 5 }, host));
        Assert.Contains(Revision().ToString(), stale.Message);

        var fixedResult = await edit.Run(new JsonObject
        {
            ["path"] = "Demo.publication.json", ["expected_revision"] = Revision(), ["scene_id"] = first.Id,
            ["elements"] = new JsonArray(new JsonObject { ["id"] = "rotulo", ["x"] = 0.5, ["y"] = 0.7, ["width"] = 0.8 })
        }, host);
        Assert.NotNull(fixedResult.Changes);   // undoable like any write
        var after = PublicationProjectCodec.Parse(host.Documents.Get("Demo.publication.json")!.Content);
        var label = after.Video!.Scenes[0].Elements.Single(e => e.Id == "rotulo");
        Assert.Equal((0.5, 0.8, 0.1, "Vídeos editáveis feitos das suas notas"), (label.X, label.Width, label.Height, label.Text));   // only the sent fields changed
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(other), System.Text.Json.JsonSerializer.Serialize(after.Video.Scenes[1]));
        Assert.Equal("# Fim\n\nObrigado.", host.Documents.Get("Roteiro/Fim.md")!.Content);
        var clean = (await review.Run(new JsonObject { ["path"] = "Demo.publication.json" }, host)).Content;
        Assert.True(clean.Contains("Revisão visual medida sem problemas"), clean);

        // looking: one settled frame per scene, the frames named by scene, what the visual model saw
        var look = AssistantVideoDirectionTools.All.Single(t => t.Name == "look_at_video");
        var seen = (await look.Run(new JsonObject { ["path"] = "Demo.publication.json" }, host)).Content;
        Assert.Equal(2, host.Sheets[^1].Count);
        Assert.Contains("Quadro 2", seen);
        Assert.Contains(other.Id, seen);
        Assert.Contains("o título sai pela direita", seen);
        Assert.Contains("motion graphics", host.Instruction);
    }

    [Fact]
    public async Task EditVideoScene_RefusesAScriptHiddenInTheCompositionAndAnUnknownScene()
    {
        var host = new Host();
        host.Documents.Upsert(new() { Path = "A.md", Content = "# A\n\nTexto." });
        await AssistantPublicationTools.All.Single(t => t.Name == "write_publication").Run(new JsonObject
        {
            ["path"] = "A.publication.json",
            ["spec"] = JsonNode.Parse("""{"schemaVersion":1,"title":"A","sources":[{"documentId":"A.md"}],"design":{"layout":"video"},"formats":["mp4"]}""")
        }, host);
        var edit = AssistantVideoDirectionTools.All.Single(t => t.Name == "edit_video_scene");
        int rev = host.Documents.Get("A.publication.json")!.Revision;
        var unknown = await Assert.ThrowsAsync<AiToolException>(() => edit.Run(new JsonObject { ["path"] = "A.publication.json", ["expected_revision"] = rev, ["scene_id"] = "inventada", ["duration"] = 3 }, host));
        Assert.Contains("Cenas:", unknown.Message);
        var scene = PublicationProjectCodec.Parse(host.Documents.Get("A.publication.json")!.Content).Video!.Scenes[0].Id;
        await Assert.ThrowsAsync<AiToolException>(() => edit.Run(new JsonObject
        {
            ["path"] = "A.publication.json", ["expected_revision"] = rev, ["scene_id"] = scene,
            ["elements"] = new JsonArray(new JsonObject { ["kind"] = "text", ["text"] = new string('x', 400) })
        }, host));
    }

    /// <summary>#956: a narration longer than its scene grows the scene; the subtitles follow the voice, not the scene; a voice cut by the next scene is told.</summary>
    [Fact]
    public void Narration_GrowsTheSceneToHoldTheVoiceAndTheSubtitlesFollowIt()
    {
        var project = VideoComposition.Synced(PublicationProject.Create("V", "a").AddSource("b") with { Design = new PublicationDesign { Layout = "video" } });
        var video = project.Video! with { Scenes = project.Video.Scenes.Select(s => s with { Duration = 4, Transition = new VideoTransition { Kind = "cut" } }).ToList() };
        string first = video.Scenes[0].Id, second = video.Scenes[1].Id;
        var fitted = video.FitToVoices(new Dictionary<string, double> { [first] = 6.2, [second] = 1.5 });
        Assert.Equal(6.6, fitted.Scenes[0].Duration);   // 6.2 s of voice + 0.4 s of breath
        Assert.Equal(4, fitted.Scenes[1].Duration);   // longer than its voice: the person's choice stays

        (string, string)? Read(PublicationSourceReference r) => ("x.md", "# T\n\nPrimeira frase dita. Segunda frase dita.");
        var withVoice = project with { Video = fitted with { Audio = [new VideoAudioClip { Kind = "voice", Path = "N/2.wav", Start = 6.6, Length = 1.5 }] } };
        var cues = VideoTimeline.Build(withVoice, Read).Cues.Where(c => c.Start >= 6.6).ToList();
        Assert.Equal(8.1, cues[^1].End, 3);   // the subtitles end with the voice, not 2.5 s later at the scene's end

        var cut = project with { Video = video with { Audio = [new VideoAudioClip { Kind = "voice", Path = "N/1.wav", Start = 0, Length = 6.2 }] } };
        var issue = Assert.Single(VideoInspection.Run(VideoTimeline.Build(cut, Read)), i => i.Message.Contains("narração"));
        Assert.Contains("6,6 s", issue.Fix);
    }
}
