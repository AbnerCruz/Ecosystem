using System.Globalization;
using System.Text.Json.Nodes;

namespace Urbe.Core.Assistant;

/// <summary>#956: the renderer the preview and the MP4 use, lent to the agent so it sees what it made.</summary>
public interface IAssistantVideoFrames
{
    string? NarrationVoice => null;
    IReadOnlyList<string> AvailableNarrationVoices => [];
    /// <summary>How a text element fits its box when drawn at <paramref name="width"/>×<paramref name="height"/>.</summary>
    VideoTextFit Fit(VideoElementState element, int width, int height);
    /// <summary>The frames at <paramref name="times"/> side by side in one PNG, each labelled with its number.</summary>
    Task<byte[]> RenderSheetAsync(VideoTimeline timeline, IReadOnlyList<double> times, CancellationToken cancel = default);
    /// <summary>The text spoken by the device's voice, as a WAV; null when this device has no voice.</summary>
    Task<byte[]?> SpeakAsync(string text, CancellationToken cancel = default) => Task.FromResult<byte[]?>(null);
    /// <summary>The MP4 of the timeline (its images and sound read from the vault), encoded and verified as the Estúdio's export.</summary>
    Task<VideoRenderOutput> EncodeAsync(VideoTimeline timeline, CancellationToken cancel = default) =>
        throw new AiToolException("Este app não gera MP4 aqui. A pessoa exporta pelo Estúdio.");
    /// <summary>Writes a new file of the vault (never replaces one).</summary>
    Task SaveFileAsync(string path, byte[] bytes) => throw new AiToolException("Este app não grava arquivos do vídeo.");
    bool FileExists(string path) => false;
}

/// <summary>A rendered MP4 and what its check found (frames, size, seconds, sound), and the sound files it could not read.</summary>
public sealed record VideoRenderOutput(byte[] Mp4, string Checked, IReadOnlyList<string> SkippedAudio);

/// <summary>
/// #956: the loop of a motion graphics director — look at the real frames (look_at_video), find what is wrong
/// (review_publication measures it), change only the scene that needs it (edit_video_scene) and look again. The
/// edit goes through the same validation, approval, revision check and undo as write_publication.
/// </summary>
public static class AssistantVideoDirectionTools
{
    public const int MaxFrames = 12;

    public static IReadOnlyList<AiTool> All { get; } =
    [
        new AiTool
        {
            Name = "look_at_video", Access = "network",
            Description = "Desenha quadros reais de uma publicação de vídeo (o mesmo desenho do MP4) e os mostra ao modelo visual, com consentimento da pessoa, para ver texto cortado, sobreposições e objetos fora do quadro. " +
                          "Sem times, um quadro por cena, depois das animações de entrada. Devolve também a revisão objetiva medida. Use depois de montar ou corrigir e antes de entregar.",
            Parameters = JsonNode.Parse("""{"type":"object","properties":{"path":{"type":"string"},"times":{"type":"array","items":{"type":"number","minimum":0},"maxItems":12}},"required":["path"],"additionalProperties":false}""")!.AsObject(),
            Label = i => "Olhando o vídeo " + ((string?)i["path"] ?? ""),
            Run = LookAsync
        },
        new AiTool
        {
            Name = "create_video", Access = "write",
            Description = "Cria uma publicação de vídeo editável. Cada cena tem uma nota de narração e um programa JavaScript que desenha o quadro inteiro. " +
                          "Escreva draw(ctx, t, info) para cada cena. Use componentes de notas que a pessoa escolheu como referência, quando houver. " +
                          "Depois confira com review_publication e, quando disponível, look_at_video. Entregue a publicação para edição no Estúdio. " +
                          VideoCode.Help + " Temas: " + string.Join(", ", VideoThemes.All.Select(t => t.Id)) + ".",
            Parameters = JsonNode.Parse("""
                {"type":"object","properties":{"title":{"type":"string"},"theme":{"type":"string"},"aspect":{"type":"string","enum":["16:9","9:16","1:1"]},"folder":{"type":"string"},"voice":{"type":"string"},
                 "scenes":{"type":"array","minItems":1,"items":{"type":"object","properties":{"title":{"type":"string"},"narration":{"type":"string"},"code":{"type":"string"}},"required":["title","narration","code"]}}},"required":["title","scenes"],"additionalProperties":false}
                """)!.AsObject(),
            Label = i => "Criando o vídeo " + ((string?)i["title"] ?? ""),
            Run = CreateVideoAsync
        },
        new AiTool
        {
            Name = "render_video", Access = "write",
            Description = "Entrega o vídeo pronto: narra o roteiro de cada cena com a voz do aparelho (narrate, padrão sim quando o vídeo não tem voz), " +
                          "alonga as cenas que ficariam curtas para a fala, gera o MP4 H.264 verificado e as legendas SRT dentro do vault, ao lado da publicação, " +
                          "e devolve os caminhos. Use por último, depois de review_publication e look_at_video sem problemas. Nunca substitui arquivos: cria um nome novo.",
            Parameters = JsonNode.Parse("""{"type":"object","properties":{"path":{"type":"string"},"narrate":{"type":"boolean"}},"required":["path"],"additionalProperties":false}""")!.AsObject(),
            Label = i => "Gerando o MP4 de " + ((string?)i["path"] ?? ""),
            Run = RenderAsync
        },
        new AiTool
        {
            Name = "edit_video_scene", Access = "write",
            Description = "Muda só uma cena de uma publicação de vídeo, pelo id: duration, background, transition e elementos. " +
                          "layout + slots monta a cena inteira já composta (posições, tamanhos e entradas animadas): " + string.Join("; ", VideoLayouts.All.Select(l => l.Id + " (" + string.Join(", ", l.Required) + (l.Optional.Length > 0 ? "; opcional " + string.Join(", ", l.Optional) : "") + ")")) +
                          ". Para cenas programadas, edite o elemento kind code pelo id. Use layout apenas ao manter uma publicação existente que já usa layout. Textos na tela curtos (o que é falado fica na nota). Cada elemento de elements com id existente recebe só os campos enviados; sem id ou com id novo, entra na cena. " +
                          "remove_elements tira elementos. As outras cenas e as notas não mudam. Requer expected_revision (a revisão que review_publication ou look_at_video informam).",
            Parameters = JsonNode.Parse("""{"type":"object","properties":{"path":{"type":"string"},"expected_revision":{"type":"integer"},"scene_id":{"type":"string"},"layout":{"type":"string"},"slots":{"type":"object","additionalProperties":{"type":"string"}},"duration":{"type":"number"},"background":{"type":"string"},"transition":{"type":"object","additionalProperties":true},"elements":{"type":"array","items":{"type":"object","additionalProperties":true}},"remove_elements":{"type":"array","items":{"type":"string"}}},"required":["path","expected_revision","scene_id"],"additionalProperties":false}""")!.AsObject(),
            Label = i => "Ajustando a cena " + ((string?)i["scene_id"] ?? ""),
            Preview = (input, host) =>
            {
                var (doc, text, note) = Edit(input, host);
                return new AiPreview(doc.Path, doc.Content, text, note);
            },
            Run = async (input, host) =>
            {
                var (doc, text, note) = Edit(input, host);
                var saved = await host.SaveNoteAsync(doc, text);
                return new AiToolOutput(note + " Revisão atual: " + saved.Revision + ". Olhe de novo com look_at_video.",
                    [new AiChange(saved.Id, new AiNoteSnap(doc.Path, doc.Content), new AiNoteSnap(saved.Path, saved.Content))]);
            }
        }
    ];

    /// <summary>The video of a publication note as the Estúdio builds it (missing scenes made, notes read).</summary>
    public static (UrbeDocument Doc, PublicationProject Project, VideoTimeline Timeline) Load(string path, IAssistantHost host, int? resolution = null)
    {
        var doc = host.Documents.Get(path) ?? throw new AiToolException("Publicação não encontrada: " + path + ". Use o caminho .publication.json que write_publication informou.");
        PublicationProject project;
        try { project = PublicationProjectCodec.Parse(doc.Content); }
        catch (Exception e) when (e is InvalidDataException or NotSupportedException or System.Text.Json.JsonException) { throw new AiToolException("Não é uma publicação válida: " + e.Message); }
        if (project.Design.Layout != "video") throw new AiToolException("A publicação usa layout " + project.Design.Layout + ", não video. Para um vídeo, mude design.layout para video com write_publication.");
        if (project.Sections.Count == 0) throw new AiToolException("A publicação não tem seções: cada seção (uma nota) é uma cena.");
        project = VideoComposition.Synced(project);
        using var catalog = new PublicationSourceCatalog(host.Documents);
        try { return (doc, project, VideoTimeline.Build(project, catalog, resolution)); }
        catch (Exception e) when (e is InvalidDataException or NotSupportedException) { throw new AiToolException(e.Message); }
    }

    /// <summary>The objective checks with the renderer's real text measure, when the app lends it.</summary>
    public static IReadOnlyList<VideoIssue> Inspect(VideoTimeline timeline, IAssistantHost host)
    {
        var files = host.OtherFiles;
        Func<VideoElementState, VideoTextFit>? fit = host.VideoFrames is { } frames ? e => frames.Fit(e, timeline.Width, timeline.Height) : null;
        return VideoInspection.Run(timeline, fit, f => files.Contains(f));
    }

    /// <summary>The scenes and their elements by id, for the agent to aim an edit.</summary>
    public static string Outline(VideoTimeline timeline) => string.Join("\n", timeline.Scenes.Select((s, i) =>
        "Cena " + (i + 1) + " " + s.Scene.Id + " (" + N(s.Start) + "–" + N(s.End) + " s, " + (s.Title ?? "sem título") + "): " +
        (s.Scene.Elements.Count == 0 ? "sem elementos" : string.Join("; ", s.Scene.Elements.Select(e =>
            e.Id + " " + e.Kind + " (x " + N(e.X) + ", y " + N(e.Y) + ", " + N(e.Width) + "×" + N(e.Height) + (e.Repeat > 1 ? ", " + e.Repeat + " instâncias" : "") + ")")))));

    public static string Issues(IReadOnlyList<VideoIssue> issues, bool measured) =>
        (issues.Count == 0 ? "Revisão visual medida sem problemas." :
            issues.Count + " problema(s) visuais medidos:\n" + string.Join("\n", issues.Select(i =>
                "- " + (i.Severity == ReviewSeverity.Error ? "[erro] " : "[aviso] ") + i.Place + ": " + i.Message + " → " + i.Fix))) +
        (measured ? "" : "\nO texto não foi medido aqui (sem o desenho do app): cortes de texto não conferidos.");

    private static async Task<AiToolOutput> LookAsync(JsonObject input, IAssistantHost host)
    {
        var path = (string?)input["path"] ?? "";
        var frames = host.VideoFrames ?? throw new AiToolException("Este app não desenha quadros de vídeo aqui. Use review_publication, que mede a geometria.");
        var (doc, _, timeline) = Load(path, host, 360);
        var times = (input["times"] as JsonArray)?.Select(t => t?.GetValue<double>() ?? 0).Select(t => Math.Clamp(t, 0, Math.Max(0, timeline.Duration - 0.01))).Take(MaxFrames).ToList();
        if (times is not { Count: > 0 }) times = timeline.Scenes.Select(s => s.Settled).Take(MaxFrames).ToList();
        string Where(double t)
        {
            var i = timeline.Scenes.ToList().FindLastIndex(s => t >= s.Start);
            return i < 0 ? "" : "cena " + (i + 1) + " " + timeline.Scenes[i].Scene.Id + " aos " + N(t - timeline.Scenes[i].Start) + " s da cena";
        }
        var legend = string.Join("\n", times.Select((t, i) => "Quadro " + (i + 1) + ": " + N(t) + " s do vídeo, " + Where(t)));
        var sheet = await frames.RenderSheetAsync(timeline, times);
        string seen;
        if (host.DocumentVision is not { } vision) seen = "Sem modelo visual neste app: confie na revisão medida abaixo.";
        else
        {
            try
            {
                seen = await vision.ExamineImageAsync(path, sheet, "image/png",
                    "Esta imagem junta quadros numerados de um vídeo motion graphics que você mesmo montou:\n" + legend +
                    "\nPara cada quadro, diga o que se vê e aponte só problemas visíveis: texto cortado ou fora do quadro, elementos sobrepostos, " +
                    "texto ilegível (pequeno, pouco contraste), legenda cobrindo algo, quadro vazio ou desequilibrado. Não invente o que não aparece.");
            }
            catch (AiToolException e) { seen = "Os quadros não foram vistos: " + e.Message; }
        }
        var issues = Inspect(Load(path, host).Timeline, host);
        return new AiToolOutput(path + " · revisão " + doc.Revision + " · " + timeline.Scenes.Count + " cenas, " + N(timeline.Duration) + " s\n" + legend +
            "\n\nO que o modelo visual viu:\n" + seen + "\n\n" + Issues(issues, true) + "\n\n" + Outline(timeline));
    }

    /// <summary>
    /// #956 (owner: "demorou muito, muitos passos"): the whole video in one call — the notes with what each scene says,
    /// the publication with its theme, and a drawing program for every scene. Everything is checked before anything is
    /// written, so a mistake costs one corrected call, not a half-made video.
    /// </summary>
    private static async Task<AiToolOutput> CreateVideoAsync(JsonObject input, IAssistantHost host)
    {
        var title = ((string?)input["title"] ?? "").Trim();
        if (title.Length == 0) throw new AiToolException("Dê um título ao vídeo.");
        var theme = (string?)input["theme"] ?? "noite";
        if (VideoThemes.Find(theme) is null) throw new AiToolException("Tema " + theme + " não existe; temas: " + string.Join(", ", VideoThemes.All.Select(t => t.Id)) + ".");
        var aspect = (string?)input["aspect"] ?? "16:9";
        if (!VideoComposition.Aspects.Contains(aspect)) throw new AiToolException("Proporção " + aspect + "; use 16:9, 9:16 ou 1:1.");
        var frames = host.VideoFrames;
        var voice = frames?.NarrationVoice;
        if (voice is null && (string?)input["voice"] is { Length: > 0 } requestedVoice &&
            frames?.AvailableNarrationVoices.Contains(requestedVoice, StringComparer.Ordinal) == true)
            voice = requestedVoice;
        var name = ArtifactModel.SafeName(title);
        var folder = ((string?)input["folder"] ?? "Publicações/" + name).Trim().Trim('/');
        if (!VideoScript.IsVaultPath(folder + "/x.md")) throw new AiToolException("Pasta inválida: " + folder + ".");
        var pubPath = folder + ".publication.json";
        if (host.Documents.Get(pubPath) is not null) throw new AiToolException(pubPath + " já existe: para mudá-lo use edit_video_scene, ou dê outro título.");
        var scenes = (input["scenes"] as JsonArray ?? []).OfType<JsonObject>().ToList();
        if (scenes.Count == 0) throw new AiToolException("Envie ao menos uma cena.");

        // Validate every program before creating any scene notes.
        var plans = new List<(string Title, string Narration, string Code)>();
        for (int i = 0; i < scenes.Count; i++)
        {
            var scene = scenes[i];
            var sceneTitle = ((string?)scene["title"] ?? "").Trim();
            var narration = ((string?)scene["narration"] ?? "").Trim();
            var code = ((string?)scene["code"] ?? "").Trim();
            if (sceneTitle.Length == 0 || narration.Length == 0 || code.Length == 0)
                throw new AiToolException("Cena " + (i + 1) + ": title, narration e code são obrigatórios.");
            if (scene.ContainsKey("layout") || scene.ContainsKey("slots"))
                throw new AiToolException("Cena " + (i + 1) + ": desenhe o quadro no programa code.");
            try { VideoCode.Check(code); VideoCode.Run(code, 0, 1280, 720); }
            catch (VideoCodeException e) { throw new AiToolException("Cena " + (i + 1) + ", code: " + e.Message); }
            plans.Add((sceneTitle, narration, code));
        }

        var changes = new List<AiChange>();
        var docs = new List<UrbeDocument>();
        for (int i = 0; i < plans.Count; i++)
        {
            var notePath = folder + "/" + (i + 1).ToString("00", CultureInfo.InvariantCulture) + " — " + ArtifactModel.SafeName(plans[i].Title) + ".md";
            for (int n = 2; host.Documents.Get(notePath) is not null; n++) notePath = notePath[..^3] + " (" + n + ").md";
            var doc = await host.CreateNoteAsync(notePath, "# " + plans[i].Title + "\n\n" + plans[i].Narration + "\n");
            docs.Add(doc);
            changes.Add(new AiChange(doc.Id, null, new AiNoteSnap(doc.Path, doc.Content)));
        }
        var project = PublicationProject.Create(title, docs[0].Id) with { Design = new PublicationDesign { Layout = "video" } };
        foreach (var d in docs.Skip(1)) project = project.AddSource(d.Id);
        project = VideoComposition.Synced(project, r => host.Documents.Get(r.DocumentId)?.Content);
        var video = project.Video! with
        {
            Theme = theme, Aspect = aspect, NarrationVoice = voice,
            Scenes = project.Video!.Scenes.Select((scene, i) => scene with
            {
                Background = VideoThemes.DefaultBackground,
                Elements = [new VideoElement
                {
                    Id = scene.Id + "_codigo", Kind = "code", Text = plans[i].Code,
                    X = 0.5, Y = 0.5, Width = 1, Height = 1
                }]
            }).ToList()
        };
        project = project with { Video = video };
        PublicationProjectCodec.Validate(project);
        var saved = await host.CreateNoteAsync(pubPath, PublicationProjectCodec.Serialize(project));
        changes.Add(new AiChange(saved.Id, null, new AiNoteSnap(saved.Path, saved.Content)));
        var (_, _, timeline) = Load(pubPath, host);
        var issues = Inspect(timeline, host);
        return new AiToolOutput("Vídeo criado: " + pubPath + " · revisão " + saved.Revision + " · " + timeline.Scenes.Count + " cenas, " + N(timeline.Duration) + " s, tema " + theme +
            ". Falas nas notas de " + folder + "/.\n" + Issues(issues, host.VideoFrames is not null) + "\n" + Outline(timeline) +
            "\nPróximo: confira a revisão e entregue a publicação editável. A pessoa decide no Estúdio se quer narrar ou exportar.", changes);
    }

    /// <summary>
    /// #956: narration, MP4 and subtitles made by the agent, so a request for a video ends in a video to play. The voice
    /// is the device's (as the Estúdio's Narrar); files are new ones in the vault; the publication keeps the voice
    /// tracks and which scenes the MP4 holds, through the same save (and undo) as the other edits.
    /// </summary>
    private static async Task<AiToolOutput> RenderAsync(JsonObject input, IAssistantHost host)
    {
        var path = (string?)input["path"] ?? "";
        var frames = host.VideoFrames ?? throw new AiToolException("Este app não gera vídeo aqui. A pessoa exporta pelo Estúdio.");
        var (doc, project, timeline) = Load(path, host);
        if (!timeline.IsComplete) throw new AiToolException("Faltam notas das cenas: " + string.Join(", ", timeline.MissingDocumentIds) + ". Corrija as fontes com write_publication.");
        if (Inspect(timeline, host).Where(i => i.Severity == ReviewSeverity.Error).ToList() is { Count: > 0 } errors)
            throw new AiToolException("Corrija antes de gerar o MP4 (edit_video_scene):\n" + Issues(errors, host.VideoFrames is not null));
        var video = project.Video!;
        var changes = new List<AiChange>();
        var notes = new List<string>();
        string Unique(string wanted)
        {
            var dot = wanted.LastIndexOf('.');
            var candidate = wanted;
            for (int n = 2; frames.FileExists(candidate) || host.Documents.Get(candidate) is not null; n++) candidate = wanted[..dot] + " (" + n + ")" + wanted[dot..];
            return candidate;
        }
        var folder = path.Contains('/') ? path[..(path.LastIndexOf('/') + 1)] : "";
        var name = ArtifactModel.SafeName(project.Title);
        bool narrate = input["narrate"] is JsonValue v && v.TryGetValue<bool>(out var asked) ? asked : !video.Audio.Any(a => a.Kind == "voice");
        if (narrate)
        {
            var seconds = new Dictionary<string, double>(StringComparer.Ordinal);
            var wavs = new List<(VideoTimelineScene Scene, byte[] Wav, double Seconds)>();
            foreach (var scene in timeline.Scenes.Where(s => s.Script.Trim().Length > 0))
            {
                var wav = await frames.SpeakAsync(scene.Script.Trim());
                if (wav is null) { notes.Add("Sem voz neste aparelho: o vídeo saiu sem narração."); wavs.Clear(); break; }
                double length;
                using (var read = new MemoryStream(wav)) length = VideoAudio.ReadWavInfo(read)?.Seconds ?? 0;
                wavs.Add((scene, wav, length));
                if (length > 0) seconds[scene.Scene.Id] = length;
            }
            if (wavs.Count > 0)
            {
                var fitted = video.FitToVoices(seconds);
                var starts = VideoTimeline.Build(project with { Video = fitted }, _ => null).Scenes.ToDictionary(x => x.Scene.Id, x => x.Start);
                var clips = new List<VideoAudioClip>();
                for (int i = 0; i < wavs.Count; i++)
                {
                    var wavPath = Unique("Narração/" + name + "/Cena " + (i + 1) + ".wav");
                    await frames.SaveFileAsync(wavPath, wavs[i].Wav);
                    clips.Add(new VideoAudioClip { Kind = "voice", Path = wavPath, Start = starts[wavs[i].Scene.Scene.Id], Length = wavs[i].Seconds > 0 ? wavs[i].Seconds : null });
                }
                var grown = fitted.Scenes.Zip(video.Scenes).Count(p => p.First.Duration != p.Second.Duration);
                video = fitted with { Audio = [.. fitted.Audio.Where(a => a.Kind != "voice"), .. clips] };
                notes.Add("Narração de " + clips.Count + (clips.Count == 1 ? " cena" : " cenas") + (grown > 0 ? "; " + grown + " ficaram mais longas para caber a fala" : "") + ".");
                (doc, var change) = await Save(doc, project = project with { Video = video });
                changes.Add(change);
                using var catalog = new PublicationSourceCatalog(host.Documents);
                timeline = VideoTimeline.Build(project, catalog);
            }
        }
        var output = await frames.EncodeAsync(timeline);
        var mp4 = Unique(folder + name + ".mp4");
        await frames.SaveFileAsync(mp4, output.Mp4);
        var srt = Unique(folder + name + ".srt");
        await frames.SaveFileAsync(srt, System.Text.Encoding.UTF8.GetBytes(timeline.Srt()));
        if (output.SkippedAudio.Count > 0) notes.Add("Sem o áudio de: " + string.Join(", ", output.SkippedAudio) + " (formato que este aparelho não lê).");
        (doc, var rendered) = await Save(doc, project with { Video = video with { Rendered = timeline.SceneKeys() } });
        changes.Add(rendered);
        return new AiToolOutput("Vídeo pronto: " + mp4 + " (" + output.Checked + "). Legendas: " + srt + ". " + string.Join(" ", notes) +
            " Diga à pessoa onde está o vídeo; ela toca no arquivo para assistir.", changes);

        async Task<(UrbeDocument, AiChange)> Save(UrbeDocument current, PublicationProject changed)
        {
            var saved = await host.SaveNoteAsync(current, PublicationProjectCodec.Serialize(changed));
            return (saved, new AiChange(saved.Id, new AiNoteSnap(current.Path, current.Content), new AiNoteSnap(saved.Path, saved.Content)));
        }
    }

    private static (UrbeDocument Doc, string Text, string Note) Edit(JsonObject input, IAssistantHost host)
    {
        var path = (string?)input["path"] ?? "";
        var sceneId = (string?)input["scene_id"] ?? "";
        var (doc, project, _) = Load(path, host);
        if (!int.TryParse(input["expected_revision"]?.ToJsonString(), out var expected) || expected != doc.Revision)
            throw new AiToolException("A publicação está na revisão " + doc.Revision + ", não " + input["expected_revision"]?.ToJsonString() + ". Releia com review_publication e refaça só esta mudança.");
        var root = JsonNode.Parse(PublicationProjectCodec.Serialize(project))!.AsObject();
        var scenes = root["video"]!["scenes"]!.AsArray();
        var scene = scenes.OfType<JsonObject>().FirstOrDefault(s => (string?)s["id"] == sceneId)
            ?? throw new AiToolException("Nenhuma cena " + sceneId + ". Cenas: " + string.Join(", ", scenes.Select(s => (string?)s?["id"])) + ".");
        var changed = new List<string>();
        foreach (var field in new[] { "duration", "background", "transition" })
            if (input[field] is { } value) { scene[field] = value.DeepClone(); changed.Add(field); }
        // #956: a composed layout replaces the scene's elements (the narration stays in the note)
        if (input["layout"] is JsonValue lv && lv.TryGetValue<string>(out var layoutId))
        {
            var slots = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var (key, value) in input["slots"] as JsonObject ?? [])
            {
                var text = value is JsonValue v && v.TryGetValue<string>(out var str) ? str : value?.ToJsonString() ?? "";
                if (text.Length > AssistantPublicationTools.MaxOverlay)
                    throw new AiToolException("O texto de " + key + " tem " + text.Length + " caracteres; na tela vão até " + AssistantPublicationTools.MaxOverlay + ". O que é falado fica na nota da cena.");
                slots[key] = text;
            }
            List<VideoElement> built;
            try { built = VideoLayouts.Build(layoutId, slots, sceneId); }
            catch (ArgumentException e) { throw new AiToolException(e.Message); }
            scene["elements"] = JsonNode.Parse(System.Text.Json.JsonSerializer.Serialize(built, LayoutJson));
            changed.Add("layout " + layoutId);
        }
        var elements = scene["elements"] as JsonArray ?? (JsonArray)(scene["elements"] = new JsonArray());
        foreach (var id in (input["remove_elements"] as JsonArray ?? []).Select(n => (string?)n))
        {
            var gone = elements.OfType<JsonObject>().FirstOrDefault(e => (string?)e["id"] == id) ?? throw new AiToolException("A cena não tem o elemento " + id + ".");
            elements.Remove(gone);
            changed.Add("tirou " + id);
        }
        foreach (var patch in (input["elements"] as JsonArray ?? []).OfType<JsonObject>())
        {
            AssistantPublicationTools.CheckNoCopiedText(patch, true);
            var id = (string?)patch["id"];
            if (elements.OfType<JsonObject>().FirstOrDefault(e => id is not null && (string?)e["id"] == id) is { } target)
            {
                foreach (var (key, value) in patch) target[key] = value?.DeepClone();
                changed.Add(id!);
            }
            else
            {
                var added = (JsonObject)patch.DeepClone();
                added["id"] ??= "el_" + Guid.NewGuid().ToString("N");
                elements.Add(added);
                changed.Add("novo " + (string?)added["id"]);
            }
        }
        if (changed.Count == 0) throw new AiToolException("Nada a mudar: envie duration, background, transition, elements ou remove_elements.");
        PublicationProject edited;
        try { edited = PublicationProjectCodec.Parse(root.ToJsonString()); }
        catch (Exception e) when (e is InvalidDataException or NotSupportedException or ArgumentException or System.Text.Json.JsonException) { throw new AiToolException(e.Message); }
        return (doc, PublicationProjectCodec.Serialize(edited), "Cena " + sceneId + " mudou (" + string.Join(", ", changed) + "); as outras cenas e as notas não mudaram.");
    }

    private static string N(double v) => v.ToString("0.##", CultureInfo.GetCultureInfo("pt-BR"));

    private static readonly System.Text.Json.JsonSerializerOptions LayoutJson = new()
    {
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };
}
