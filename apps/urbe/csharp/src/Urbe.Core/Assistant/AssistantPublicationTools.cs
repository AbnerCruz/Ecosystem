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
        "Em documentId use o caminho da nota (como list_notes/search_notes mostram), o título ou o id; o Numa guarda o id. sections pode faltar: uma seção por fonte, na ordem. Fragmentos são títulos únicos ou ^id existente; ausência ou ambiguidade é erro. " +
        "Escreva/adapte conteúdo em notas Markdown reais pelas ferramentas de notas; reordenação/layout nunca modifica notas. write_publication requer expected_revision " +
        "para substituir um projeto existente, mantendo publicationId. " +
        "Layouts: article e book (capítulos, sumário); slides (cada nota/#/##/--- inicia slide; :::notas vira notas do apresentador); course (cada nota um módulo, " +
        "cada ## uma aula, blocos ```quiz viram questões); video (cada seção é uma cena; o texto da nota é o roteiro e a legenda). " +
        "Vídeo: campo video {aspect:16:9|9:16|1:1, resolution:144–2160, fps:1–60, subtitles:bool, theme:noite|quadro|papel|neon|sobrio (escolha sempre um: dá fundo em gradiente, " +
        "cor de texto, duas cores de diagrama e legendas combinando; elementos sem color e cenas sem background seguem o tema), scenes:[{id, placementId (= id da seção), duration (s), " +
        "transition:{kind:cut|fade|slide|wipe|zoom (slide entra da direita, wipe revela da esquerda, zoom aproxima ao surgir), seconds}, background:#rrggbb, camera? ({keyframes, motions} sobre x, y (ponto do quadro no centro, 0.5 é o meio), zoom (1 é o quadro inteiro, 2 o dobro de perto) e rotation; o fundo fica, os elementos se movem sob a câmera), elements:[{id, kind:title|text|script|shape|image|plot|math|path|group|use|code (code: text é um programa JavaScript seu que desenha a caixa do elemento a cada quadro; use coloca um componente: component, params {chave: valor} preenchem {{chave}} nos textos; x, y, scale, rotation, opacity, keyframes e motions movem o componente inteiro), text? (rótulo curto, até " +
        "160 caracteres; o roteiro é a nota; em plot a função, em math o TeX, em path os dados de path SVG \"M… L… C… Z\" ajustados à caixa), source? (imagem do vault), " +
        "x,y,width,height (0–1 do quadro), color, fontSize (em path, a espessura do traço), start, end?, parent? (id de outro elemento da cena: x,y viram deslocamento " +
        "a partir do centro dele, e escala, rotação e opacidade dele valem para este; group não desenha nada e serve de pai), " +
        "keyframes:[{property:x|y|scale|opacity|rotation|progress|width|height|glow|shadow|blur, time, value, easing:linear|easeIn|easeOut|easeInOut|step|back|cubic-bezier(x1,y1,x2,y2)}] " +
        "(progress de 0 a 1 desenha path e plot ao longo do tempo; back passa do alvo e volta), motions:[{property, expression (fórmula de t), mode:add|set (add soma ao valor; set vira o valor)}], repeat? (1–2000: o elemento vira N instâncias; as fórmulas veem i (0 a n−1), n e u = i/(n−1); um elemento repetido não é pai), split? (letters|words, só em title e text: uma instância por letra ou palavra, cada uma no seu lugar do texto, com as mesmas fórmulas), glow?, shadow?, blur? (0–0.2 da altura do quadro; animáveis por keyframes e motions), glowColor? e gradient? (#rrggbb: pinta de color, no alto, até esta cor, embaixo), clip? (id de uma forma rect/ellipse/circle da cena: o elemento só aparece dentro dela)}]}], " +
        // #1004: procedural motion — the model invents particles, orbits, grids and staggers as formulas instead of lists of elements
        "Fórmulas: + - * / ^, pi, tau, e; sin cos tan atan sqrt abs exp ln floor ceil round fract sat smooth (0–1 suave) rand(v) (0–1 determinístico por v) noise(v) (ruído suave); " +
        "min(a,b) max(a,b) mod(a,b) pow(a,b) atan2(y,x) step(borda,v) mix(a,b,u) clamp(v,lo,hi) — dentro delas a vírgula separa argumentos e o decimal é ponto. " +
        "CÓDIGO LIVRE (kind code): quando a cena pede algo que os elementos prontos não fazem — simulação, fractal, campo de partículas com rastro, visualização de dados, geometria gerada, tipografia desenhada, transições inventadas — escreva um programa. " + VideoCode.Help + " " +
        "Para ocupar o quadro inteiro use x 0.5, y 0.5, width 1, height 1. Combine com elementos comuns (título, fórmula) por cima. Se o programa falhar, review_publication diz a linha e o motivo. " +
        "Ex.: {\"id\":\"campo\",\"kind\":\"code\",\"x\":0.5,\"y\":0.5,\"width\":1,\"height\":1,\"text\":\"function draw(ctx,t){ for(var i=0;i<200;i++){ var r=30+Math.random()*200, a=Math.random()*6.28+t*0.5; ctx.fill('#7dd3fc'); ctx.circle(ctx.width/2+r*Math.cos(a), ctx.height/2+r*Math.sin(a)*0.6, 2); } }\"}. " +
        "Movimento procedural: partículas, órbitas, grades, ondas e entradas escalonadas são UM elemento com repeat e fórmulas, nunca dezenas de elementos; " +
        "ex. 200 estrelas: {\"id\":\"estrelas\",\"kind\":\"shape\",\"text\":\"circle\",\"width\":0.004,\"height\":0.007,\"repeat\":200,\"motions\":[{\"property\":\"x\",\"mode\":\"set\",\"expression\":\"rand(i)\"},{\"property\":\"y\",\"mode\":\"set\",\"expression\":\"rand(i+0.5)\"},{\"property\":\"opacity\",\"mode\":\"set\",\"expression\":\"0.3+0.7*noise(i+2t)\"}]}; " +
        "escalonar: opacity set sat((t - 0.1*i)/0.4). Tipografia cinética: title com split letters e motions opacity set sat((t-0.05*i)/0.3), y add 0.05*(1-smooth((t-0.05*i)/0.3)) (cada letra sobe e aparece em sequência); words para revelar palavra a palavra com a fala. Componentes: o que se repete com conteúdo diferente (um rótulo com caixa, um cartão de dado, um ícone com legenda) é um componente usado várias vezes, não cópias escritas à mão. Revelar com clip: uma forma rect invisível (opacity 0) com progress 0→1 revela o texto da esquerda para a direita; um circle que cresce (scale) abre uma imagem a partir do centro. Câmera com propósito: aproximar devagar (zoom 1→1.15 na cena toda) dá vida a uma cena parada; pan de um ponto a outro conduz o olhar entre partes de um diagrama. Efeitos com propósito: glow em pontos de luz, partículas e destaques; shadow para separar cartões do fundo; blur em planos de fundo ou para entrar desfocado (blur keyframes 0.03→0); gradient em formas grandes. Formas repetidas podem passar da borda; texto repetido é revisado como texto. " +
        "components? ({nome: [elementos]}: grupos feitos uma vez; dentro deles x e y são deslocamentos do centro, 0,0 é o centro), audio:[{id, kind:voice|music|effect, path (arquivo do vault), start, trimStart, length?, volume}]}. IDs de cenas e elementos são estáveis: preserve-os ao editar. " +
        "Saídas que a pessoa exporta no estúdio: HTML, EPUB, PDF editorial, DOCX, ODT, PPTX (slides) e MP4 verificado (vídeo) com legendas SRT. " +
        "Entregue a publicação editável e informe seu caminho. O Numa abre a publicação nova no Estúdio, onde a pessoa decide quando narrar ou exportar em MP4. Use .publication.json. " +
        // #956 (beta.57, conversa real): the model spent most of the run building test publications to learn the engine
        "Tudo o que o motor de vídeo aceita está descrito aqui: não crie publicações ou notas de teste para descobrir. " +
        // beta.58: the whole video in one call was cut by the model's output limit and arrived without its spec
        "Vídeo novo: create_video numa chamada só (título, tema e, por cena, a fala, o layout e os textos curtos da tela), depois revise com review_publication e, quando disponível, look_at_video. " +
        "Para mudar um vídeo existente, ou montar a partir de notas que já existem: 1) write_publication só com sources e design {layout:video} (o app cria uma cena por fonte e devolve os ids); " +
        "2) edit_video_scene para cada cena, uma por chamada, de preferência com layout e slots (abertura, topico, comparacao, numero, formula, grafico, citacao: " +
        "composições prontas com entradas animadas), acrescentando elementos só quando o layout não basta; 3) review_publication e look_at_video; 4) entregue a publicação editável. Nunca mande os elementos detalhados de todas as cenas num write_publication só (a resposta é cortada pelo limite de saída); o roteiro compacto do create_video cabe. " +
        "Exemplo de cena completa: {\"id\":\"cena_relogio\",\"placementId\":\"<id da seção>\",\"duration\":7,\"transition\":{\"kind\":\"fade\",\"seconds\":0.5},\"elements\":[" +
        "{\"id\":\"t\",\"kind\":\"title\",\"x\":0.5,\"y\":0.16,\"width\":0.8,\"height\":0.14,\"fontSize\":0.07,\"keyframes\":[{\"property\":\"opacity\",\"time\":0,\"value\":0},{\"property\":\"opacity\",\"time\":0.6,\"value\":1,\"easing\":\"easeOut\"}]}," +
        "{\"id\":\"espelho1\",\"kind\":\"shape\",\"text\":\"rect\",\"x\":0.3,\"y\":0.35,\"width\":0.2,\"height\":0.02}," +
        "{\"id\":\"pulso\",\"kind\":\"shape\",\"text\":\"circle\",\"x\":0.3,\"y\":0.6,\"width\":0.03,\"height\":0.053,\"motions\":[{\"property\":\"y\",\"expression\":\"0.12*sin(2*pi*t)\"}]}," +
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
        "look_at_video mostra os quadros reais; edit_video_scene corrige só a cena com problema; repita até a revisão limpa; por fim, informe o caminho da publicação editável. " +
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
        // rewriting an existing publication without naming its id keeps the id it has (asking for it made models loop)
        if (spec["publicationId"] is null && host.Documents.Get(path) is { } prior)
            try { spec["publicationId"] = PublicationProjectCodec.Parse(prior.Content).PublicationId; }
            catch (InvalidDataException) { /* an unreadable file is replaced only with a valid revision, below */ }
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
            var existingId = PublicationProjectCodec.Parse(previous.Content).PublicationId;
            if (!int.TryParse(input["expected_revision"]?.ToJsonString(), out var expected) || expected != previous.Revision)
                throw new AiToolException(path + " já existe, na revisão " + previous.Revision + ". Para substituí-la envie expected_revision: " + previous.Revision +
                    " e publicationId: \"" + existingId + "\"; para mudar uma cena só, use edit_video_scene.");
            if (existingId != project.PublicationId)
                throw new AiToolException("Preserve o publicationId do projeto existente: \"" + existingId + "\".");
        }
        return (path, PublicationProjectCodec.Serialize(project), previous);
    }
}
