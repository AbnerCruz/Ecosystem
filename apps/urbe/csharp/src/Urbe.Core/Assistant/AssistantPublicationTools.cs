using System.Text.Json.Nodes;

namespace Urbe.Core.Assistant;

/// <summary>CST-149/150/177: live publication tools use the existing permission/preview/writer/undo pipeline.</summary>
public static class AssistantPublicationTools
{
    public static IReadOnlyList<AiTool> All { get; } =
    [
        new AiTool
        {
            Name = "publication_schema", Access = "read",
            Description = "Consulta o contrato do Estúdio de Publicações. O conteúdo pertence às notas reais, nunca ao JSON de composição.",
            Parameters = JsonNode.Parse("""{"type":"object","properties":{},"additionalProperties":false}""")!.AsObject(),
            Label = _ => "Formato de Publicações",
            Run = (_, _) => Task.FromResult(new AiToolOutput(Schema))
        },
        new AiTool
        {
            Name = "write_publication", Access = "write",
            Description = "Cria ou atualiza composição .publication.json com referências a notas reais. Valida fontes e revisão antes da prévia e escrita; não copia texto nem altera fontes.",
            Parameters = JsonNode.Parse("""{"type":"object","properties":{"path":{"type":"string"},"spec":{"type":"object","additionalProperties":true},"expected_revision":{"type":"integer"}},"required":["path","spec"],"additionalProperties":false}""")!.AsObject(),
            Label = i => "Publicação: " + ((string?)i["path"] ?? ""),
            Preview = (input, host) =>
            {
                var (path, text, doc) = Validate(input, host);
                return new AiPreview(path, doc?.Content, text, "Composição com referências vivas; as notas não serão alteradas.");
            },
            Run = async (input, host) =>
            {
                var (path, text, doc) = Validate(input, host);
                var saved = doc is null ? await host.CreateNoteAsync(path, text) : await host.SaveNoteAsync(doc, text);
                var ids = string.Join(", ", PublicationProjectCodec.Parse(text).Sources.Select(x => (host.Documents.Get(x.DocumentId)?.Path ?? "?") + " = " + x.DocumentId));
                var project = PublicationProjectCodec.Parse(text);
                var scenes = project.Video is { Scenes.Count: > 0 } video
                    ? " Revisão " + saved.Revision + ". Cenas (preencha cada uma com edit_video_scene): " + string.Join("; ", video.Scenes.Select((sc, i) =>
                        (i + 1) + " " + sc.Id + " = " + (host.Documents.Get(project.Sections[i].Source.DocumentId)?.Path ?? "?") + " (" + sc.Elements.Count + " elementos)")) + "."
                    : "";
                return new AiToolOutput("Publicação salva: " + saved.Path + " (fontes: " + ids + ")." + scenes + " Abra com open_note; as fontes continuam sendo notas editáveis.",
                    [new AiChange(saved.Id, doc is null ? null : new AiNoteSnap(doc.Path, doc.Content), new AiNoteSnap(saved.Path, saved.Content))]);
            }
        },
        new AiTool
        {
            Name = "review_publication", Access = "read",
            Description = "Revisa uma publicação como o Estúdio faz antes de exportar: fontes ausentes, títulos que pulam nível, imagens sem descrição, slides cheios, legendas rápidas, links quebrados. Use antes de entregar e corrija nas notas.",
            Parameters = JsonNode.Parse("""{"type":"object","properties":{"path":{"type":"string"}},"required":["path"],"additionalProperties":false}""")!.AsObject(),
            Label = i => "Revisando " + ((string?)i["path"] ?? "a publicação"),
            Run = (input, host) => Task.FromResult(new AiToolOutput(Review((string?)input["path"] ?? "", host)))
        }
    ];

    /// <summary>
    /// CST-178: the Estúdio's own review, for the agent to check its work before handing it over; it says what it does
    /// not check (facts, the drawn result), so a clean review is never told as "ficou bom".
    /// </summary>
    private static string Review(string path, IAssistantHost host)
    {
        var doc = host.Documents.Get(path) ?? throw new AiToolException("Publicação não encontrada: " + path + ".");
        PublicationProject project;
        try { project = PublicationProjectCodec.Parse(doc.Content); }
        catch (Exception e) when (e is InvalidDataException or NotSupportedException or System.Text.Json.JsonException) { throw new AiToolException("Não é uma publicação válida: " + e.Message); }
        using var catalog = new PublicationSourceCatalog(host.Documents);
        var files = host.OtherFiles;
        UrbeDocument? Named(string n) => host.Documents.Get(n) ?? host.Documents.Get(n + ".md") ??
            host.Documents.List().FirstOrDefault(d => string.Equals(System.IO.Path.GetFileNameWithoutExtension(d.Path), System.IO.Path.GetFileName(n), StringComparison.OrdinalIgnoreCase));
        var issues = PublicationReview.Run(project, catalog, f => files.Contains(f) || host.Documents.Get(f) is not null, n => Named(n) is not null, readNote: n => Named(n)?.Content);
        // #956: a video is also looked at — the frames measured, and the scenes listed by id for edit_video_scene
        string video = "";
        if (project.Design.Layout == "video" && project.Sections.Count > 0)
        {
            var (_, _, timeline) = AssistantVideoDirectionTools.Load(path, host);
            video = "\n\n" + AssistantVideoDirectionTools.Issues(AssistantVideoDirectionTools.Inspect(timeline, host), host.VideoFrames is not null) +
                    "\n\nRevisão " + doc.Revision + ". " + AssistantVideoDirectionTools.Outline(timeline);
        }
        string Unchecked = "\nNão conferido por esta revisão: se os fatos estão certos (confira nas fontes) e " +
            (project.Design.Layout == "video" ? "o gosto e o ritmo do vídeo (veja os quadros com look_at_video)." : "a aparência final de cada formato.");
        if (issues.Count == 0) return "Revisão automática sem problemas em " + project.Sections.Count + (project.Sections.Count == 1 ? " seção." : " seções.") + video + Unchecked;
        return issues.Count + (issues.Count == 1 ? " ponto" : " pontos") + " a corrigir:\n" +
               string.Join("\n", issues.Select(i => "- " + (i.Severity == ReviewSeverity.Error ? "[erro] " : "[aviso] ") + i.Place + ": " + i.Message + " → " + i.Fix)) + video + Unchecked;
    }

    /// <summary>The longest on-screen label a video element may carry in the composition.</summary>
    public const int MaxOverlay = 160;

    /// <summary>CST-176: the same contract the Estúdio uses, said to agents (layouts, outputs, video).</summary>
    public const string Schema =
        "Publicações usam .publication.json, schemaVersion=1. Campos: publicationId (estável), title, objective, audience, sources [{documentId,fragment?}], " +
        "sections [{id,source:{documentId,fragment?},role}], design:{theme:paper|ocean|ink, layout:article|book|slides|course|video}, formats:[html,epub,…]. " +
        "Em documentId use o caminho da nota (como list_notes/search_notes mostram), o título ou o id; o Urbe guarda o id. sections pode faltar: uma seção por fonte, na ordem. Fragmentos são títulos únicos ou ^id existente; ausência ou ambiguidade é erro. " +
        "Escreva/adapte conteúdo em notas Markdown reais pelas ferramentas de notas; reordenação/layout nunca modifica notas. write_publication requer expected_revision " +
        "para substituir um projeto existente, mantendo publicationId. " +
        "Layouts: article e book (capítulos, sumário); slides (cada nota/#/##/--- inicia slide; :::notas vira notas do apresentador); course (cada nota um módulo, " +
        "cada ## uma aula, blocos ```quiz viram questões); video (cada seção é uma cena; o texto da nota é o roteiro e a legenda). " +
        "Vídeo: campo video {aspect:16:9|9:16|1:1, resolution:144–2160, fps:1–60, subtitles:bool, scenes:[{id, placementId (= id da seção), duration (s), " +
        "transition:{kind:cut|fade, seconds}, background:#rrggbb, elements:[{id, kind:title|text|script|shape|image|plot|math|path|group, text? (rótulo curto, até " +
        "160 caracteres; o roteiro é a nota; em plot a função, em math o TeX, em path os dados de path SVG \"M… L… C… Z\" ajustados à caixa), source? (imagem do vault), " +
        "x,y,width,height (0–1 do quadro), color, fontSize (em path, a espessura do traço), start, end?, parent? (id de outro elemento da cena: x,y viram deslocamento " +
        "a partir do centro dele, e escala, rotação e opacidade dele valem para este; group não desenha nada e serve de pai), " +
        "keyframes:[{property:x|y|scale|opacity|rotation|progress, time, value, easing:linear|easeIn|easeOut|easeInOut|step|back|cubic-bezier(x1,y1,x2,y2)}] " +
        "(progress de 0 a 1 desenha path e plot ao longo do tempo; back passa do alvo e volta), motions:[{property, expression (fórmula de t)}]}]}], " +
        "audio:[{id, kind:voice|music|effect, path (arquivo do vault), start, trimStart, length?, volume}]}. IDs de cenas e elementos são estáveis: preserve-os ao editar. " +
        "Saídas que a pessoa exporta no estúdio: HTML, EPUB, PDF editorial, DOCX, ODT, PPTX (slides) e MP4 verificado (vídeo) com legendas SRT. " +
        "Um vídeo pedido a você termina em render_video, que narra, gera o MP4 e as legendas no vault. Não crie .page.json; esse editor foi substituído. " +
        // #956 (beta.57, conversa real): the model spent most of the run building test publications to learn the engine
        "Tudo o que o motor de vídeo aceita está descrito aqui: não crie publicações ou notas de teste para descobrir. " +
        // beta.58: the whole video in one call was cut by the model's output limit and arrived without its spec
        "Monte um vídeo em partes: 1) write_publication só com sources e design {layout:video} (o Urbe cria uma cena por fonte e devolve os ids); " +
        "2) edit_video_scene para cada cena, uma por chamada, com os elementos dela; 3) review_publication e look_at_video; 4) render_video. Nunca mande todas as cenas numa chamada só. " +
        "Exemplo de cena completa: {\"id\":\"cena_relogio\",\"placementId\":\"<id da seção>\",\"duration\":7,\"background\":\"#0b1d33\",\"transition\":{\"kind\":\"fade\",\"seconds\":0.5},\"elements\":[" +
        "{\"id\":\"t\",\"kind\":\"title\",\"x\":0.5,\"y\":0.16,\"width\":0.8,\"height\":0.14,\"fontSize\":0.07,\"keyframes\":[{\"property\":\"opacity\",\"time\":0,\"value\":0},{\"property\":\"opacity\",\"time\":0.6,\"value\":1,\"easing\":\"easeOut\"}]}," +
        "{\"id\":\"espelho1\",\"kind\":\"shape\",\"text\":\"rect\",\"x\":0.3,\"y\":0.35,\"width\":0.2,\"height\":0.02,\"color\":\"#9ad1ff\"}," +
        "{\"id\":\"pulso\",\"kind\":\"shape\",\"text\":\"circle\",\"x\":0.3,\"y\":0.6,\"width\":0.03,\"height\":0.053,\"color\":\"#ffd166\",\"motions\":[{\"property\":\"y\",\"expression\":\"0.12*sin(2*pi*t)\"}]}," +
        "{\"id\":\"seta\",\"kind\":\"shape\",\"text\":\"arrow\",\"x\":0.55,\"y\":0.5,\"width\":0.15,\"height\":0.015,\"keyframes\":[{\"property\":\"progress\",\"time\":1,\"value\":0},{\"property\":\"progress\",\"time\":2,\"value\":1}]}," +
        "{\"id\":\"g\",\"kind\":\"plot\",\"text\":\"y = 1/sqrt(1-x^2)\\nx: 0..0.95\\ny: 0..4\",\"x\":0.78,\"y\":0.5,\"width\":0.34,\"height\":0.36,\"keyframes\":[{\"property\":\"progress\",\"time\":1,\"value\":0},{\"property\":\"progress\",\"time\":4,\"value\":1,\"easing\":\"linear\"}]}," +
        "{\"id\":\"f\",\"kind\":\"math\",\"text\":\"\\\\gamma=\\\\frac{1}{\\\\sqrt{1-v^2/c^2}}\",\"x\":0.78,\"y\":0.82,\"width\":0.3,\"height\":0.1}]}. " +
        "Formas: rect, ellipse/circle, line, arrow (progress desenha line e arrow). " +
        "Qualidade: uma ideia por cena; a mesma paleta (2–3 cores sobre um fundo escuro) e as mesmas posições de título em todas as cenas; texto na tela curto " +
        "(o roteiro vai na narração e na legenda); elementos dentro de 5–95% do quadro e acima de y 0,8, onde fica a legenda; cada elemento entra animado " +
        "(opacity, y, scale ou progress) em vez de aparecer pronto; diagramas feitos de formas, setas, gráficos e fórmulas que se constroem com a fala. " +
        "Cada nota de cena tem só a fala daquela cena (12 a 30 palavras para 5 a 10 s). Para várias cenas de uma nota, use seções com fragment = título exato da nota. " +
        "Texto fiel à fonte é editado na própria nota. Quando a publicação precisa de outra redação (resumo, roteiro, fala, tradução), crie uma nota ao lado da " +
        "fonte com as propriedades adaptacao_de: \"[[Nome da fonte]]\", adaptacao_id (id da fonte), adaptacao_tipo e adaptacao_origem (deixe vazio: o app avisa que a fonte mudou até alguém revisar) " +
        "e troque a fonte da seção por essa nota; nunca esconda texto no .publication.json. " +
        // CST-178: the editorial method, in the order a person would follow it in the Estúdio
        "Método: 1) briefing — objective e audience da publicação (pergunte à pessoa se o pedido não diz); 2) fontes — notas reais lidas por inteiro, sem inventar " +
        "referências; 3) roteiro — a ordem das seções e o que cada uma diz, antes de escrever; 4) composição — texto nas notas, layout no projeto; " +
        "5) revisão — review_publication e correção nas notas; 6) entrega — diga o que foi revisado e o que não foi. Decisões de rumo (público, tom, cortes) são da pessoa. " +
        "Vídeo, depois de montar: review_publication mede cada cena (fora do quadro, texto cortado, sobreposição, contraste) e lista os ids; " +
        "look_at_video mostra os quadros reais; edit_video_scene corrige só a cena com problema; repita até a revisão limpa; por fim render_video. " +
        "Um especialista (consult_agent) só quando ele sabe algo que você não sabe; não divida em vários agentes o que um faz bem.";

    /// <summary>
    /// #986: the agent names a source the way it knows it — the note's id, path or title (the note tools show paths,
    /// not ids). Each one becomes the canonical id; missing sections are made from the sources, one per source, in order.
    /// Every name that matches nothing is listed with what to do, so the agent stops instead of trying invented ids.
    /// </summary>
    private static void Canonicalize(JsonObject spec, IAssistantHost host)
    {
        var unknown = new List<string>();
        string? Resolve(JsonNode? reference)
        {
            if (reference is not JsonObject r || r["documentId"] is not JsonValue v || !v.TryGetValue<string>(out var name) || name.Trim().Length == 0) return null;
            var doc = host.Documents.Get(name.Trim()) ?? AssistantTools.ResolveDoc(host, name.Trim(), must: false);
            if (doc is null) { if (!unknown.Contains(name)) unknown.Add(name); return null; }
            r["documentId"] = doc.Id;
            return doc.Id;
        }
        foreach (var source in spec["sources"] as JsonArray ?? []) Resolve(source);
        if (spec["sections"] is not JsonArray { Count: > 0 } sections)
            spec["sections"] = new JsonArray((spec["sources"] as JsonArray ?? []).Select(s => (JsonNode)new JsonObject
                { ["id"] = "piece_" + Guid.NewGuid().ToString("N"), ["source"] = s!.DeepClone(), ["role"] = "section" }).ToArray());
        else
            foreach (var section in sections.OfType<JsonObject>())
            {
                if (section["id"] is null) section["id"] = "piece_" + Guid.NewGuid().ToString("N");
                Resolve(section["source"]);
            }
        // a section's note that is not among the sources joins them (one list of sources, kept in sync)
        var sources = spec["sources"] as JsonArray ?? (JsonArray)(spec["sources"] = new JsonArray());
        foreach (var section in (spec["sections"] as JsonArray)!.OfType<JsonObject>())
            if (section["source"] is JsonObject src && !sources.OfType<JsonObject>().Any(x => (string?)x["documentId"] == (string?)src["documentId"] && (string?)x["fragment"] == (string?)src["fragment"]))
                sources.Add(src.DeepClone());
        if (unknown.Count > 0)
            throw new AiToolException("Nenhuma nota com " + string.Join(", ", unknown.Select(u => "\"" + u + "\"")) +
                ". Use o caminho que list_notes ou search_notes mostra (por exemplo Pasta/Nota.md); não invente ids. Se a nota ainda não existe, crie-a com create_note antes.");
    }

    /// <summary>Authored text belongs to notes; only a video element may carry a short on-screen label.</summary>
    internal static void CheckNoCopiedText(JsonNode? node, bool videoElement = false)
    {
        if (node is JsonObject obj)
            foreach (var (key, value) in obj)
            {
                string name = key.ToLowerInvariant();
                // a video element may carry a short on-screen label ("Passo 1"); the script stays in the note
                if (name == "text" && videoElement && value is JsonValue label && label.TryGetValue<string>(out var t) && t.Length <= MaxOverlay) continue;
                if (name is "markdown" or "content" or "text" or "html" or "body")
                    throw new AiToolException(name == "text" && videoElement
                        ? "Rótulo de vídeo com mais de " + MaxOverlay + " caracteres: o roteiro pertence à nota da cena (elemento kind \"script\")."
                        : "Texto autoral pertence a notas reais, não à composição: " + key);
                CheckNoCopiedText(value, videoElement || name == "elements" && value is JsonArray);
            }
        else if (node is JsonArray array) foreach (var value in array) CheckNoCopiedText(value, videoElement);
    }

    /// <summary>Which source is missing and what there is instead: the note's headings, or notes with a similar name.</summary>
    private static string MissingSource(PublicationSourceReference source, IAssistantHost host)
    {
        var doc = host.Documents.Get(source.DocumentId);
        if (doc is null)
            return "A fonte " + source.DocumentId + " não existe. Use o caminho da nota como list_notes ou search_notes mostram" +
                   (host.Documents.List().Where(d => d.Path.Contains(System.IO.Path.GetFileNameWithoutExtension(source.DocumentId), StringComparison.OrdinalIgnoreCase)).Take(5).Select(d => d.Path).ToList() is { Count: > 0 } like
                       ? "; parecidas: " + string.Join(", ", like) : "") + ".";
        var headings = EmbedFragments.Headings(doc.Content);
        return "O trecho \"" + source.Fragment + "\" não existe em " + doc.Path + ". Use um título exato da nota" +
               (headings.Count > 0 ? ": " + string.Join(" | ", headings.Take(40)) : " (ela não tem títulos: use a nota inteira, sem fragment)") + ".";
    }

    private static (string Path, string Text, UrbeDocument? Previous) Validate(JsonObject input, IAssistantHost host)
    {
        var path = (string?)input["path"] ?? "";
        if (!path.EndsWith(".publication.json", StringComparison.OrdinalIgnoreCase) || path.StartsWith('/') || path.Contains('\\') ||
            path.Split('/').Any(p => p is "" or "." or "..") || path.Any(c => char.IsControl(c) || ":*?\"<>|".Contains(c)))
            throw new AiToolException("Use um caminho relativo seguro terminado em .publication.json.");
        if (input["spec"] is not JsonObject spec) throw new AiToolException("Informe spec conforme publication_schema.");
        CheckNoCopiedText(spec);
        Canonicalize(spec, host);
        PublicationProject project;
        try { project = PublicationProjectCodec.Parse(spec.ToJsonString()); }
        catch (Exception e) when (e is InvalidDataException or NotSupportedException or ArgumentException)
        { throw new AiToolException(e.Message); }
        using var catalog = new PublicationSourceCatalog(host.Documents);
        foreach (var source in project.Sources.Concat(project.Sections.Select(p => p.Source)))
            if (catalog.Read(source) is null) throw new AiToolException(MissingSource(source, host));
        // #956: a video is saved with its scenes, so their ids stay the same for edit_video_scene
        if (project.Design.Layout == "video") project = VideoComposition.Synced(project, r => catalog.Read(r)?.Markdown);
        var previous = host.Documents.Get(path);
        if (previous is not null)
        {
            if (!int.TryParse(input["expected_revision"]?.ToJsonString(), out var expected) || expected != previous.Revision)
                throw new AiToolException("Releia a publicação e informe expected_revision atual antes de substituir.");
            if (PublicationProjectCodec.Parse(previous.Content).PublicationId != project.PublicationId)
                throw new AiToolException("Preserve o publicationId do projeto existente.");
        }
        return (path, PublicationProjectCodec.Serialize(project), previous);
    }
}
