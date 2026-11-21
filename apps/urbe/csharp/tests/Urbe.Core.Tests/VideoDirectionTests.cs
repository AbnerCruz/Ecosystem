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
        /// <summary>The device's voice: one second of silence for every five words.</summary>
        public bool HasVoice { get; set; } = true;
        public Task<byte[]?> SpeakAsync(string text, CancellationToken c = default) =>
            Task.FromResult(HasVoice ? Wav(text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length / 5.0) : null);
        public List<VideoTimeline> Encoded { get; } = [];
        public Task<VideoRenderOutput> EncodeAsync(VideoTimeline timeline, CancellationToken c = default)
        { Encoded.Add(timeline); return Task.FromResult(new VideoRenderOutput([0, 0, 0, 0x18, (byte)'f', (byte)'t', (byte)'y', (byte)'p'], "H.264 verificado", [])); }
        public Dictionary<string, byte[]> Files { get; } = [];
        public Task SaveFileAsync(string path, byte[] bytes) { if (!Files.TryAdd(path, bytes)) throw new InvalidOperationException("Já existe " + path); return Task.CompletedTask; }
        public bool FileExists(string path) => Files.ContainsKey(path);

        private static byte[] Wav(double seconds)
        {
            int rate = 8000, frames = (int)(seconds * rate);
            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms);
            w.Write("RIFF"u8); w.Write(36 + frames * 2); w.Write("WAVE"u8); w.Write("fmt "u8); w.Write(16); w.Write((short)1); w.Write((short)1);
            w.Write(rate); w.Write(rate * 2); w.Write((short)2); w.Write((short)16); w.Write("data"u8); w.Write(frames * 2); w.Write(new byte[frames * 2]);
            return ms.ToArray();
        }
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
        host.Documents.Upsert(new() { Path = "Roteiro/Abertura.md", Content = "# Abertura\n\nO Numa faz vídeos a partir das notas." });
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

    /// <summary>
    /// #986/#956 acceptance in the agent's own loop: "faça um motion graphics" → the script note → a video publication by
    /// the note's path → review (finds the provoked cut label, names the scene) → one scene fixed → looking at the frames
    /// → delivered. One write_publication, no tool error, nothing repeated, every write approved.
    /// </summary>
    [Fact]
    public async Task AMotionGraphicsRequestRunsWithoutLoopsFromScriptToALookedAtVideo()
    {
        var host = new Host();
        var tools = new[] { AssistantTools.Get("create_note")! }.Concat(AssistantPublicationTools.All).Concat(AssistantVideoDirectionTools.All).ToArray();
        var calls = new List<string>();
        int approvals = 0, step = 0;
        string Last(AiRequest request) => request.Messages.SelectMany(m => m.Content).LastOrDefault(b => b.Type == "tool_result")?.Content ?? "";
        static AiResult Use(string name, JsonObject input) => new([AiBlock.ToolUse("t" + Guid.NewGuid().ToString("N")[..6], name, input)], "", "tool_use", new(1, 1, null), "test");
        string? scene = null;
        List<AiChatMessage> messages = [new() { Id = "u", Role = "user", Content = [AiChatBlock.TextBlock("Faça um motion graphics curto mostrando que o Numa faz vídeos das notas.")] }];
        await AssistantAgent.RunAsync(new()
        {
            Host = host, Model = "test", Provider = new("p", "openai"), Policy = "ask", Tools = tools,
            Messages = messages,
            Approve = (_, _) => { approvals++; return Task.FromResult(AiApproval.Approve); },
            Stream = (_, request, _, _) =>
            {
                var last = Last(request);
                AiResult next = ++step switch
                {
                    1 => Use("create_note", new JsonObject { ["path"] = "Roteiros/Demo.md", ["content"] = "# Vídeos das suas notas\n\nO Numa transforma notas em vídeos editáveis." }),
                    2 => Use("write_publication", new JsonObject
                    {
                        ["path"] = "Publicações/Demo.publication.json",
                        ["spec"] = JsonNode.Parse("""{"schemaVersion":1,"title":"Demo","sources":[{"documentId":"Roteiros/Demo.md"}],"design":{"layout":"video"},"formats":["mp4"]}""")
                    }),
                    3 => Use("review_publication", new JsonObject { ["path"] = "Publicações/Demo.publication.json" }),
                    4 => Use("edit_video_scene", new JsonObject
                    {
                        ["path"] = "Publicações/Demo.publication.json", ["expected_revision"] = host.Documents.Get("Publicações/Demo.publication.json")!.Revision,
                        ["scene_id"] = scene = System.Text.RegularExpressions.Regex.Match(last, @"Cena 1 (\S+)").Groups[1].Value,
                        ["elements"] = new JsonArray(new JsonObject { ["id"] = "selo", ["kind"] = "text", ["text"] = "Feito no Numa com notas reais", ["x"] = 0.5, ["y"] = 0.72, ["width"] = 0.8, ["height"] = 0.1 })
                    }),
                    5 => Use("look_at_video", new JsonObject { ["path"] = "Publicações/Demo.publication.json" }),
                    _ => new([AiBlock.TextBlock("Pronto: o vídeo está em Publicações/Demo.publication.json. Revisei a geometria e olhei os quadros; exporte o MP4 no Estúdio.")], "", "end_turn", new(1, 1, null), "test")
                };
                if (next.Content.FirstOrDefault(b => b.Type == "tool_use") is { } use) calls.Add(use.Name!);
                if (step == 4) Assert.Contains("Revisão visual medida", last);   // the review measured the video and named its scene
                return Task.FromResult(next);
            }
        });
        Assert.Equal(["create_note", "write_publication", "review_publication", "edit_video_scene", "look_at_video"], calls);
        var results = messages.SelectMany(m => m.Content).Where(b => b.Type == "tool_result").ToList();
        Assert.Equal(5, results.Count);
        Assert.All(results, r => Assert.False(r.IsError, r.Content));
        Assert.True(approvals >= 3);   // the note, the publication and the scene's edit were each approved
        var saved = PublicationProjectCodec.Parse(host.Documents.Get("Publicações/Demo.publication.json")!.Content);
        Assert.Equal("video", saved.Design.Layout);
        Assert.Contains(saved.Video!.Scenes.Single(x => x.Id == scene).Elements, e => e.Id == "selo");
        Assert.Equal("# Vídeos das suas notas\n\nO Numa transforma notas em vídeos editáveis.", host.Documents.Get("Roteiros/Demo.md")!.Content);
        Assert.Single(host.Sheets);
    }

    // ---- Defeitos vistos numa conversa real da beta.57 (reprodução sem o conteúdo privado) ----

    private const string Sealed = "# Relógios {ia:\"modelo-x\"}\n\n## O relógio de luz {ia:\"modelo-x\"}\n\nUm pulso entre dois espelhos. {ia:\"modelo-x\"}\n\n## A conta {ia:\"modelo-x\"}\n\nO tempo se estica. {ia:\"modelo-x\"}\n";

    [Fact]
    public async Task ANoteWrittenByTheAssistantCanBeCutIntoScenesByItsHeadings()
    {
        // its headings carry the authorship seal; a fragment names the heading as the person reads it
        var host = new Host();
        host.Documents.Upsert(new() { Path = "Roteiro.md", Content = Sealed });
        var write = AssistantPublicationTools.All.Single(t => t.Name == "write_publication");
        var spec = JsonNode.Parse("""
            {"schemaVersion":1,"title":"V","design":{"layout":"video"},"sources":[{"documentId":"Roteiro.md"}],
             "sections":[{"id":"a","source":{"documentId":"Roteiro.md","fragment":"O relógio de luz"}},{"id":"b","source":{"documentId":"Roteiro.md","fragment":"A conta"}}]}
            """)!.AsObject();
        var result = await write.Run(new JsonObject { ["path"] = "V.publication.json", ["spec"] = spec }, host);
        Assert.Contains("Publicação salva", result.Content);
        // the scene ids come back, so the agent fills each scene with edit_video_scene in small calls
        Assert.Contains("Cenas (preencha cada uma com edit_video_scene): 1 scene_a = Roteiro.md", result.Content);
        var project = PublicationProjectCodec.Parse(host.Documents.Get("V.publication.json")!.Content);
        using var catalog = new PublicationSourceCatalog(host.Documents);
        var timeline = VideoTimeline.Build(project, catalog);
        // the seal never reaches the screen: not in the title, not in the subtitles
        Assert.Equal(["O relógio de luz", "A conta"], timeline.Scenes.Select(s => s.Title));
        Assert.DoesNotContain(timeline.Cues, c => c.Text.Contains("{ia:"));
        Assert.DoesNotContain(timeline.Scenes, s => s.Script.Contains("{ia:"));
    }

    [Fact]
    public async Task AMissingSourceIsNamedWithWhatExists()
    {
        var host = new Host();
        host.Documents.Upsert(new() { Path = "Roteiro.md", Content = Sealed });
        var write = AssistantPublicationTools.All.Single(t => t.Name == "write_publication");
        var spec = JsonNode.Parse("""
            {"schemaVersion":1,"title":"V","design":{"layout":"video"},"sources":[{"documentId":"Roteiro.md"}],
             "sections":[{"id":"a","source":{"documentId":"Roteiro.md","fragment":"Relógio de luz"}}]}
            """)!.AsObject();
        var error = await Assert.ThrowsAsync<AiToolException>(() => write.Run(new JsonObject { ["path"] = "V.publication.json", ["spec"] = spec }, host));
        Assert.Contains("Relógio de luz", error.Message);
        Assert.Contains("O relógio de luz", error.Message);   // the headings it has
        var missing = JsonNode.Parse("""{"schemaVersion":1,"title":"V","design":{"layout":"video"},"sources":[{"documentId":"Nao existe.md"}]}""")!.AsObject();
        error = await Assert.ThrowsAsync<AiToolException>(() => write.Run(new JsonObject { ["path"] = "V.publication.json", ["spec"] = missing }, host));
        Assert.Contains("Nao existe.md", error.Message);
    }

    [Fact]
    public async Task ScenesWrittenWithOtherPlacementIdsAreKeptInOrder()
    {
        // the model wrote its scenes but not with the section ids: they used to be replaced by default scenes, silently
        var host = new Host();
        host.Documents.Upsert(new() { Path = "A.md", Content = "# A\n\nUm." });
        host.Documents.Upsert(new() { Path = "B.md", Content = "# B\n\nDois." });
        var write = AssistantPublicationTools.All.Single(t => t.Name == "write_publication");
        var spec = JsonNode.Parse("""
            {"schemaVersion":1,"title":"V","design":{"layout":"video"},"sources":[{"documentId":"A.md"},{"documentId":"B.md"}],
             "video":{"scenes":[{"id":"s1","placementId":"cena1","duration":3,"elements":[{"id":"x1","kind":"text","text":"Um"}]},
                                {"id":"s2","placementId":"cena2","duration":5,"elements":[{"id":"x2","kind":"text","text":"Dois"}]}]}}
            """)!.AsObject();
        await write.Run(new JsonObject { ["path"] = "V.publication.json", ["spec"] = spec }, host);
        var project = PublicationProjectCodec.Parse(host.Documents.Get("V.publication.json")!.Content);
        Assert.Equal(["s1", "s2"], project.Video!.Scenes.Select(s => s.Id));
        Assert.Equal([3.0, 5.0], project.Video.Scenes.Select(s => s.Duration));
        Assert.Equal(project.Sections.Select(p => p.Id), project.Video.Scenes.Select(s => s.PlacementId));
    }

    [Fact]
    public void WhatModelsWriteNaturallyIsDrawn()
    {
        // a function or TeX in source (what the schema used to say), line and arrow shapes, and width animated
        var timeline = Timeline(
            new VideoElement { Id = "g", Kind = "plot", Source = "y = x^2" },
            new VideoElement { Id = "m", Kind = "math", Source = "E = mc^2" },
            new VideoElement { Id = "barra", Kind = "shape", Text = "rect", Width = 0.1,
                Keyframes = [new() { Property = "width", Time = 0, Value = 0.1 }, new() { Property = "width", Time = 2, Value = 0.5, Easing = "linear" }] });
        var at = timeline.Evaluate(1).Layers.Single().Elements;
        Assert.Equal("y = x^2", at.Single(e => e.Id == "g").Text);
        Assert.Equal("E = mc^2", at.Single(e => e.Id == "m").Text);
        Assert.Equal(0.3, at.Single(e => e.Id == "barra").Width, 6);
    }

    [Fact]
    public void AnInvalidKeyframeSaysWhichAndWhatIsAccepted()
    {
        var project = VideoComposition.Synced(PublicationProject.Create("V", "n1") with { Design = new PublicationDesign { Layout = "video" } });
        var scene = project.Video!.Scenes[0] with { Elements = [new VideoElement { Id = "bola", Keyframes = [new() { Property = "cor", Time = 0, Value = 1 }] }] };
        var error = Assert.Throws<InvalidDataException>(() => PublicationProjectCodec.Validate(project with { Video = project.Video.WithScene(scene) }));
        Assert.Contains("bola", error.Message);
        Assert.Contains("cor", error.Message);
        Assert.Contains("opacity", error.Message);
    }

    [Fact]
    public void TheSchemaExampleSceneIsAValidScene()
    {
        // the example the model copies must itself pass the engine's rules
        var schema = AssistantPublicationTools.Schema;
        int start = schema.IndexOf("Exemplo de cena completa: ", StringComparison.Ordinal) + "Exemplo de cena completa: ".Length;
        var json = schema[start..schema.IndexOf(". Formas:", start, StringComparison.Ordinal)].Replace("<id da seção>", "p");
        var scene = System.Text.Json.JsonSerializer.Deserialize<VideoScene>(json, new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase })!;
        var project = VideoComposition.Synced(PublicationProject.Create("V", "n1") with { Design = new PublicationDesign { Layout = "video" } });
        project = project with { Video = project.Video! with { Scenes = [scene with { PlacementId = project.Sections[0].Id }] } };
        PublicationProjectCodec.Validate(project);
        Assert.Contains(scene.Elements, e => e.Kind == "math" && e.Text!.StartsWith("\\gamma"));
        Assert.Contains(scene.Elements, e => e.Kind == "plot" && e.Text!.Contains('\n'));
    }

    [Fact]
    public async Task RenderVideoNarratesFitsTheScenesAndSavesAnMp4WithSubtitles()
    {
        var host = new Host();
        host.Documents.Upsert(new() { Path = "Vídeos/A.md", Content = "# A\n\num dois três quatro cinco seis sete oito nove dez onze doze treze quatorze quinze dezesseis dezessete dezoito dezenove vinte vinte-e-um vinte-e-dois vinte-e-três vinte-e-quatro vinte-e-cinco." });
        host.Documents.Upsert(new() { Path = "Vídeos/B.md", Content = "# B\n\nCurta." });
        var write = AssistantPublicationTools.All.Single(t => t.Name == "write_publication");
        await write.Run(new JsonObject { ["path"] = "Vídeos/Aula.publication.json", ["spec"] = JsonNode.Parse("""
            {"schemaVersion":1,"title":"Aula","design":{"layout":"video"},"sources":[{"documentId":"Vídeos/A.md"},{"documentId":"Vídeos/B.md"}],
             "video":{"scenes":[{"id":"s1","placementId":"x","duration":3},{"id":"s2","placementId":"y","duration":3}]}}
            """)!.AsObject() }, host);
        var render = AssistantVideoDirectionTools.All.Single(t => t.Name == "render_video");
        var result = await render.Run(new JsonObject { ["path"] = "Vídeos/Aula.publication.json" }, host);
        Assert.Contains("Vídeo pronto: Vídeos/Aula.mp4", result.Content);
        Assert.True(host.Files.ContainsKey("Vídeos/Aula.mp4"));
        Assert.Contains("00:00:00,000 --> ", System.Text.Encoding.UTF8.GetString(host.Files["Vídeos/Aula.srt"]));
        Assert.Equal(2, host.Files.Keys.Count(k => k.StartsWith("Narração/Aula/", StringComparison.Ordinal)));
        var project = PublicationProjectCodec.Parse(host.Documents.Get("Vídeos/Aula.publication.json")!.Content);
        // the 25-word scene speaks for 5 s: it grew from 3 s to hold the voice; the short one kept its 3 s
        Assert.Equal(5.4, project.Video!.Scenes[0].Duration, 6);
        Assert.Equal(3, project.Video.Scenes[1].Duration, 6);
        Assert.Equal(2, project.Video.Audio.Count(a => a.Kind == "voice"));
        Assert.NotNull(project.Video.Rendered);
        // the MP4 was made from the narrated timeline, and every change can be undone
        Assert.Equal(2, host.Encoded.Single().Audio.Count);
        Assert.Equal(2, result.Changes!.Count);

        // again: new files beside the first ones, nothing replaced, no second narration over the first
        var again = await render.Run(new JsonObject { ["path"] = "Vídeos/Aula.publication.json" }, host);
        Assert.Contains("Vídeos/Aula (2).mp4", again.Content);
        Assert.Equal(2, host.Files.Keys.Count(k => k.StartsWith("Narração/", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task RenderVideoWithoutAVoiceStillDeliversTheVideoAndSaysSo()
    {
        var host = new Host { HasVoice = false };
        host.Documents.Upsert(new() { Path = "A.md", Content = "# A\n\nTexto." });
        await AssistantPublicationTools.All.Single(t => t.Name == "write_publication").Run(new JsonObject { ["path"] = "V.publication.json", ["spec"] = JsonNode.Parse("""
            {"schemaVersion":1,"title":"V","design":{"layout":"video"},"sources":[{"documentId":"A.md"}]}
            """)!.AsObject() }, host);
        var result = await AssistantVideoDirectionTools.All.Single(t => t.Name == "render_video").Run(new JsonObject { ["path"] = "V.publication.json" }, host);
        Assert.Contains("Vídeo pronto: V.mp4", result.Content);
        Assert.Contains("sem narração", result.Content);
    }

    [Fact]
    public void ARepeatedIdSaysWhichAndWhere()
    {
        var project = VideoComposition.Synced(PublicationProject.Create("V", "n1") with { Design = new PublicationDesign { Layout = "video" } });
        var scene = project.Video!.Scenes[0] with { Elements = [new VideoElement { Id = "rotulo" }, new VideoElement { Id = "rotulo" }] };
        var error = Assert.Throws<InvalidDataException>(() => PublicationProjectCodec.Validate(project with { Video = project.Video.WithScene(scene) }));
        Assert.Contains("\"rotulo\" aparece mais de uma vez", error.Message);
        Assert.Contains(scene.Id, error.Message);
    }

    [Fact]
    public void ABrokenProjectSaysWhereItBreaks()
    {
        var wrongType = Assert.Throws<InvalidDataException>(() => PublicationProjectCodec.Parse("""{"schemaVersion":1,"title":"V","video":{"fps":"trinta"}}"""));
        Assert.Contains("video.fps", wrongType.Message);
        Assert.Contains("tipo errado", wrongType.Message);
        var cut = Assert.Throws<InvalidDataException>(() => PublicationProjectCodec.Parse("""{"schemaVersion":1,"title":"V","video":{"scenes":[{"id":"a" """));
        Assert.Contains("JSON de Publicações inválido", cut.Message);
    }

    [Fact]
    public async Task RewritingAPublicationSaysTheRevisionAndKeepsItsId()
    {
        var host = new Host();
        host.Documents.Upsert(new() { Path = "A.md", Content = "# A\n\nTexto." });
        var write = AssistantPublicationTools.All.Single(t => t.Name == "write_publication");
        JsonObject Spec() => JsonNode.Parse("""{"schemaVersion":1,"title":"V","design":{"layout":"video"},"sources":[{"documentId":"A.md"}]}""")!.AsObject();
        await write.Run(new JsonObject { ["path"] = "V.publication.json", ["spec"] = Spec() }, host);
        var id = PublicationProjectCodec.Parse(host.Documents.Get("V.publication.json")!.Content).PublicationId;
        var error = await Assert.ThrowsAsync<AiToolException>(() => write.Run(new JsonObject { ["path"] = "V.publication.json", ["spec"] = Spec() }, host));
        Assert.Contains("expected_revision: 1", error.Message);
        Assert.Contains(id, error.Message);
        // with the revision and no publicationId, the existing id is kept
        await write.Run(new JsonObject { ["path"] = "V.publication.json", ["spec"] = Spec(), ["expected_revision"] = 1 }, host);
        Assert.Equal(id, PublicationProjectCodec.Parse(host.Documents.Get("V.publication.json")!.Content).PublicationId);
    }

    [Fact]
    public async Task CreateVideoMakesTheNotesThePublicationAndProgrammedScenesInOneCall()
    {
        var host = new Host();
        var create = AssistantVideoDirectionTools.All.Single(t => t.Name == "create_video");
        var input = JsonNode.Parse("""
            {"title":"Relatividade do tempo","theme":"quadro","scenes":[
              {"title":"Abertura","narration":"O tempo não corre igual para todo mundo.","code":"function draw(ctx,t){ ctx.circle(400,250,70); }"},
              {"title":"O fator gama","narration":"O tempo se estica pelo fator gama, que cresce perto da luz.","code":"function draw(ctx,t){ ctx.circle(400,250,50+t); }"},
              {"title":"GPS","narration":"Sem corrigir 38 microssegundos por dia, o GPS erraria quilômetros.","code":"function draw(ctx,t){ ctx.circle(400,250,100+t); }"}]}
            """)!.AsObject();
        var result = await create.Run(input, host);
        Assert.Contains("Vídeo criado: Publicações/Relatividade do tempo.publication.json", result.Content);
        Assert.Contains("Revisão visual medida sem problemas", result.Content);
        // one note per scene, holding what is said; the publication with the theme and the composed scenes
        Assert.Equal("# GPS\n\nSem corrigir 38 microssegundos por dia, o GPS erraria quilômetros.\n", host.Documents.Get("Publicações/Relatividade do tempo/03 — GPS.md")!.Content);
        var project = PublicationProjectCodec.Parse(host.Documents.Get("Publicações/Relatividade do tempo.publication.json")!.Content);
        Assert.Equal("quadro", project.Video!.Theme);
        Assert.Equal(3, project.Video.Scenes.Count);
        Assert.All(project.Video.Scenes, scene => Assert.Equal("code", Assert.Single(scene.Elements).Kind));
        Assert.Equal(4, result.Changes!.Count);   // three notes and the publication, all undoable

        // a mistake is said before anything is written
        var before = host.Documents.List().Count;
        var bad = JsonNode.Parse("""{"title":"Outro","scenes":[{"title":"A","narration":"x","layout":"comparacao","slots":{"titulo":"T"}}]}""")!.AsObject();
        var error = await Assert.ThrowsAsync<AiToolException>(() => create.Run(bad, host));
        Assert.Contains("code", error.Message);
        Assert.Equal(before, host.Documents.List().Count);
        // and the video it made goes on to a verified MP4
        var render = await AssistantVideoDirectionTools.All.Single(t => t.Name == "render_video").Run(new JsonObject { ["path"] = "Publicações/Relatividade do tempo.publication.json" }, host);
        Assert.Contains("Vídeo pronto", render.Content);
    }

    /// <summary>#1004: create_video requires a program per scene and validates it before writing.</summary>
    [Fact]
    public async Task CreateVideoTakesAProgramPerScene()
    {
        var host = new Host();
        var create = AssistantVideoDirectionTools.All.Single(t => t.Name == "create_video");
        var input = JsonNode.Parse("""
            {"title":"Órbitas","theme":"noite","scenes":[
              {"title":"Campo","narration":"Partículas giram em torno de uma estrela.","code":"function draw(ctx,t){ for(var i=0;i<100;i++){ var a=Math.random()*6.28+t; ctx.fill('#7dd3fc'); ctx.circle(ctx.width/2+120*Math.cos(a), ctx.height/2+80*Math.sin(a), 2); } }"},
              {"title":"Lei","narration":"A gravidade mantém as órbitas.","code":"function draw(ctx,t){ ctx.fill('#1e293b'); ctx.circle(ctx.width/2, ctx.height/2, 40 + 10*Math.sin(t)); }"}]}
            """)!.AsObject();
        var result = await create.Run(input, host);
        Assert.Contains("Vídeo criado", result.Content);
        var project = PublicationProjectCodec.Parse(host.Documents.Get("Publicações/Órbitas.publication.json")!.Content);
        Assert.Equal(["code"], project.Video!.Scenes[0].Elements.Select(e => e.Kind));
        Assert.Equal("code", Assert.Single(project.Video.Scenes[1].Elements).Kind);

        var before = host.Documents.List().Count;
        var bad = JsonNode.Parse("""{"title":"Quebrado","scenes":[{"title":"A","narration":"x","code":"function draw(ctx){ nada(); }"}]}""")!.AsObject();
        var error = await Assert.ThrowsAsync<AiToolException>(() => create.Run(bad, host));
        Assert.Contains("nada", error.Message);
        Assert.Equal(before, host.Documents.List().Count);
        var none = JsonNode.Parse("""{"title":"Vazio","scenes":[{"title":"A","narration":"x"}]}""")!.AsObject();
        Assert.Contains("code", (await Assert.ThrowsAsync<AiToolException>(() => create.Run(none, host))).Message);
    }
}
