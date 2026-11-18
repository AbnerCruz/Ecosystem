using System.Globalization;
using System.Text.Json.Nodes;

namespace Urbe.Core.Assistant;

/// <summary>#956: the renderer the preview and the MP4 use, lent to the agent so it sees what it made.</summary>
public interface IAssistantVideoFrames
{
    /// <summary>How a text element fits its box when drawn at <paramref name="width"/>×<paramref name="height"/>.</summary>
    VideoTextFit Fit(VideoElementState element, int width, int height);
    /// <summary>The frames at <paramref name="times"/> side by side in one PNG, each labelled with its number.</summary>
    Task<byte[]> RenderSheetAsync(VideoTimeline timeline, IReadOnlyList<double> times, CancellationToken cancel = default);
}

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
            Name = "edit_video_scene", Access = "write",
            Description = "Muda só uma cena de uma publicação de vídeo, pelo id: duration, background, transition e elementos. Cada elemento de elements com id existente recebe só os campos enviados; sem id ou com id novo, entra na cena. " +
                          "remove_elements tira elementos. As outras cenas e as notas não mudam. Requer expected_revision (a revisão que review_publication ou look_at_video informam).",
            Parameters = JsonNode.Parse("""{"type":"object","properties":{"path":{"type":"string"},"expected_revision":{"type":"integer"},"scene_id":{"type":"string"},"duration":{"type":"number"},"background":{"type":"string"},"transition":{"type":"object","additionalProperties":true},"elements":{"type":"array","items":{"type":"object","additionalProperties":true}},"remove_elements":{"type":"array","items":{"type":"string"}}},"required":["path","expected_revision","scene_id"],"additionalProperties":false}""")!.AsObject(),
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
            e.Id + " " + e.Kind + " (x " + N(e.X) + ", y " + N(e.Y) + ", " + N(e.Width) + "×" + N(e.Height) + ")")))));

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
}
