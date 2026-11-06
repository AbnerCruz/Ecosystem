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
                return new AiToolOutput("Publicação salva: " + saved.Path + ". Abra com open_note; as fontes continuam sendo notas editáveis.",
                    [new AiChange(saved.Id, doc is null ? null : new AiNoteSnap(doc.Path, doc.Content), new AiNoteSnap(saved.Path, saved.Content))]);
            }
        }
    ];

    /// <summary>The longest on-screen label a video element may carry in the composition.</summary>
    public const int MaxOverlay = 160;

    /// <summary>CST-176: the same contract the Estúdio uses, said to agents (layouts, outputs, video).</summary>
    public const string Schema =
        "Publicações usam .publication.json, schemaVersion=1. Campos: publicationId (estável), title, objective, audience, sources [{documentId,fragment?}], " +
        "sections [{id,source:{documentId,fragment?},role}], design:{theme:paper|ocean|ink, layout:article|book|slides|course|video}, formats:[html,epub,…]. " +
        "Use IDs persistentes de read_note/search_notes, não caminhos, como documentId. Fragmentos são títulos únicos ou ^id existente; ausência ou ambiguidade é erro. " +
        "Escreva/adapte conteúdo em notas Markdown reais pelas ferramentas de notas; reordenação/layout nunca modifica notas. write_publication requer expected_revision " +
        "para substituir um projeto existente, mantendo publicationId. " +
        "Layouts: article e book (capítulos, sumário); slides (cada nota/#/##/--- inicia slide; :::notas vira notas do apresentador); course (cada nota um módulo, " +
        "cada ## uma aula, blocos ```quiz viram questões); video (cada seção é uma cena; o texto da nota é o roteiro e a legenda). " +
        "Vídeo: campo video {aspect:16:9|9:16|1:1, resolution:144–2160, fps:1–60, subtitles:bool, scenes:[{id, placementId (= id da seção), duration (s), " +
        "transition:{kind:cut|fade, seconds}, background:#rrggbb, elements:[{id, kind:title|text|script|shape|image|plot|math, text? (rótulo curto, até " +
        "160 caracteres; o roteiro é a nota), source? (imagem do vault, função do gráfico ou TeX), x,y,width,height (0–1 do quadro), color, fontSize, start, end?, " +
        "keyframes:[{property:x|y|scale|opacity|rotation|progress, time, value, easing:linear|easeIn|easeOut|easeInOut|step}], motions:[{property, expression (fórmula de t)}]}]}], " +
        "audio:[{id, kind:voice|music|effect, path (arquivo do vault), start, trimStart, length?, volume}]}. IDs de cenas e elementos são estáveis: preserve-os ao editar. " +
        "Saídas que a pessoa exporta no estúdio: HTML, EPUB, PDF editorial, DOCX, ODT, PPTX (slides) e MP4 verificado (vídeo) com legendas SRT; " +
        "a exportação e o salvamento do arquivo são sempre da pessoa. Não crie .page.json; esse editor foi substituído.";

    private static (string Path, string Text, UrbeDocument? Previous) Validate(JsonObject input, IAssistantHost host)
    {
        var path = (string?)input["path"] ?? "";
        if (!path.EndsWith(".publication.json", StringComparison.OrdinalIgnoreCase) || path.StartsWith('/') || path.Contains('\\') ||
            path.Split('/').Any(p => p is "" or "." or "..") || path.Any(c => char.IsControl(c) || ":*?\"<>|".Contains(c)))
            throw new AiToolException("Use um caminho relativo seguro terminado em .publication.json.");
        if (input["spec"] is not JsonObject spec) throw new AiToolException("Informe spec conforme publication_schema.");
        void CheckNoCopiedText(JsonNode? node, bool videoElement = false)
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
        CheckNoCopiedText(spec);
        PublicationProject project;
        try { project = PublicationProjectCodec.Parse(spec.ToJsonString()); }
        catch (Exception e) when (e is InvalidDataException or NotSupportedException or ArgumentException)
        { throw new AiToolException(e.Message); }
        using var catalog = new PublicationSourceCatalog(host.Documents);
        if (project.Sources.Any(source => catalog.Read(source) is null)) throw new AiToolException("Uma fonte não existe ou o trecho está indisponível.");
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
