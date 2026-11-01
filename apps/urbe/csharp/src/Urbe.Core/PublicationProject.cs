using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Urbe.Core;

/// <summary>A layout placement references one real vault document. It never owns Markdown.</summary>
public sealed record PublicationPlacement
{
    public string Id { get; init; } = "piece_" + Guid.NewGuid().ToString("N");
    public PublicationSourceReference Source { get; init; } = new("");
    public string Role { get; init; } = "section";
}

/// <summary>Editorial styling stays separate from authored notes.</summary>
public sealed record PublicationDesign
{
    public string Theme { get; init; } = "paper";
    public string Layout { get; init; } = "article";
}

/// <summary>
/// CST-150: versioned composition with stable source identity, presentation metadata and
/// no authored content. The same notes stay the only source for editor, agents and publications.
/// </summary>
public sealed record PublicationProject
{
    public int SchemaVersion { get; init; } = PublicationProjectCodec.CurrentVersion;
    public string PublicationId { get; init; } = "pub_" + Guid.NewGuid().ToString("N");
    public string Title { get; init; } = "Nova publicação";
    public string Objective { get; init; } = "";
    public string Audience { get; init; } = "";
    public List<PublicationSourceReference> Sources { get; init; } = [];
    public List<PublicationPlacement> Sections { get; init; } = [];
    public PublicationDesign Design { get; init; } = new();
    public List<string> Formats { get; init; } = ["html"];

    // Do not drop future non-authoring metadata on a load/edit/save round trip.
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; init; }

    public static PublicationProject Create(string title, string? documentId = null)
    {
        var result = new PublicationProject { Title = title };
        return string.IsNullOrEmpty(documentId) ? result : result.AddSource(documentId);
    }

    /// <summary>Idempotent source insertion and a corresponding ordered layout placement.</summary>
    public PublicationProject AddSource(string documentId)
    {
        if (string.IsNullOrWhiteSpace(documentId)) throw new ArgumentException("ID da fonte obrigatório.", nameof(documentId));
        if (Sources.Any(s => s.DocumentId == documentId && string.IsNullOrEmpty(s.Fragment))) return this;
        var source = new PublicationSourceReference(documentId);
        return this with
        {
            Sources = [.. Sources, source],
            Sections = [.. Sections, new PublicationPlacement { Source = source }]
        };
    }

    public PublicationProject RemoveSource(string documentId) => this with
    {
        Sources = Sources.Where(s => s.DocumentId != documentId).ToList(),
        Sections = Sections.Where(s => s.Source.DocumentId != documentId).ToList()
    };

    /// <summary>Reorder the visible composition without modifying the note or its revisions.</summary>
    public PublicationProject MoveSource(string documentId, int index)
    {
        if (!Sources.Any(s => s.DocumentId == documentId))
            throw new InvalidOperationException("Fonte inexistente na publicação.");
        var sources = Sources.ToList();
        var selected = sources.First(s => s.DocumentId == documentId);
        sources.Remove(selected);
        sources.Insert(Math.Clamp(index, 0, sources.Count), selected);
        var sections = Sections.ToList();
        var piece = sections.FirstOrDefault(s => s.Source.DocumentId == documentId);
        if (piece is not null)
        {
            sections.Remove(piece);
            sections.Insert(Math.Clamp(index, 0, sections.Count), piece);
        }
        return this with { Sources = sources, Sections = sections };
    }
}

/// <summary>Persistence contract for .publication.json; validation is non-destructive.</summary>
public static class PublicationProjectCodec
{
    public const int CurrentVersion = 1;
    public const int MaxFileBytes = 2_000_000;
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        MaxDepth = 32
    };

    public static PublicationProject Parse(string? json)
    {
        if (json is null || Encoding.UTF8.GetByteCount(json) > MaxFileBytes)
            throw new InvalidDataException("Projeto de publicação ausente ou grande demais.");
        PublicationProject result;
        try { result = JsonSerializer.Deserialize<PublicationProject>(json, Options) ?? throw new InvalidDataException("Projeto de publicação vazio."); }
        catch (JsonException e) { throw new InvalidDataException("JSON de Publicações inválido.", e); }
        Validate(result);
        return result;
    }

    public static string Serialize(PublicationProject project)
    {
        Validate(project);
        var json = JsonSerializer.Serialize(project, Options) + "\n";
        if (Encoding.UTF8.GetByteCount(json) > MaxFileBytes)
            throw new InvalidDataException("Projeto de publicação excede o limite de metadados.");
        return json;
    }

    public static void Validate(PublicationProject project)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (project.SchemaVersion != CurrentVersion)
            throw new NotSupportedException("Versão de publicação não suportada: " + project.SchemaVersion);
        if (string.IsNullOrWhiteSpace(project.PublicationId) || string.IsNullOrWhiteSpace(project.Title))
            throw new InvalidDataException("Publicação sem identidade ou título.");
        if (project.Sources is null || project.Sections is null || project.Formats is null || project.Design is null ||
            project.Sections.Count > 4096 || project.Sources.Count > 4096)
            throw new InvalidDataException("Estrutura de publicação inválida ou excessiva.");
        if (project.Sources.Any(s => s is null || string.IsNullOrWhiteSpace(s.DocumentId)) ||
            project.Sources.GroupBy(s => (s.DocumentId, s.Fragment)).Any(g => g.Count() != 1))
            throw new InvalidDataException("Fonte ausente ou repetida.");
        var refs = project.Sources.Select(s => (s.DocumentId, s.Fragment)).ToHashSet();
        if (project.Sections.Any(s => s is null || string.IsNullOrWhiteSpace(s.Id) ||
            s.Source is null || !refs.Contains((s.Source.DocumentId, s.Source.Fragment))) ||
            project.Sections.GroupBy(s => s.Id).Any(g => g.Count() != 1))
            throw new InvalidDataException("Seções sem identidade ou fonte cadastrada.");
        if (project.Formats.Count == 0 || project.Formats.Any(s => string.IsNullOrWhiteSpace(s)))
            throw new InvalidDataException("A publicação precisa declarar os formatos de destino.");
    }
}

/// <summary>Preview/export result: missing source IDs are surfaced, never quietly treated as success.</summary>
public sealed record PublicationHtmlResult(string Html, IReadOnlyList<string> MissingDocumentIds)
{
    public bool IsComplete => MissingDocumentIds.Count == 0;
}

/// <summary>
/// CST-156/162 first target: static accessible offline HTML produced from live note references.
/// HTML is an output, not a canonical project model. Media packaging and other formats follow.
/// </summary>
public static class PublicationHtml
{
    public static PublicationHtmlResult Export(PublicationProject project, PublicationSourceCatalog catalog)
    {
        PublicationProjectCodec.Validate(project);
        ArgumentNullException.ThrowIfNull(catalog);
        var title = MarkdownEngine.EscapeHtml(project.Title);
        var sb = new StringBuilder();
        sb.Append("<!doctype html>\n<html lang=\"pt-BR\"><head><meta charset=\"utf-8\">")
          .Append("<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">")
          .Append("<meta http-equiv=\"Content-Security-Policy\" content=\"default-src 'none'; img-src data:; style-src 'unsafe-inline'; base-uri 'none'; form-action 'none'\">")
          .Append("<title>").Append(title).Append("</title><style>")
          .Append(":root{color-scheme:light}*{box-sizing:border-box}body{margin:0;color:#25272c;background:#fff;font-family:system-ui,sans-serif;line-height:1.65}")
          .Append("main{max-width:760px;padding:clamp(20px,5vw,64px);margin:auto}h1,h2,h3{line-height:1.24}article{margin:2.5rem 0}")
          .Append("img,video,svg{max-width:100%;height:auto}pre{overflow:auto}code{white-space:pre-wrap;overflow-wrap:anywhere}")
          .Append("blockquote{border-left:3px solid #999;padding-left:1rem}table{display:block;overflow-x:auto}")
          .Append(".umath{display:inline-block;max-width:100%;vertical-align:middle}.umath svg{vertical-align:middle}.umath-block{display:block;overflow-x:auto;text-align:center;margin:1.2rem 0}")
          .Append(".missing{border:1px dashed #a35;padding:1rem}@page{size:A4;margin:18mm}")
          .Append("@media print{main{max-width:none;padding:0}article{break-before:page}article:first-of-type{break-before:auto}h1,h2,h3{break-after:avoid}p{widows:3;orphans:3}pre,table,img{break-inside:avoid}}")
          .Append("</style></head><body><main><header><h1>").Append(title).Append("</h1></header>");
        var missing = new List<string>();
        foreach (var placement in project.Sections)
        {
            var source = catalog.Read(placement.Source);
            if (source is null)
            {
                if (!missing.Contains(placement.Source.DocumentId, StringComparer.Ordinal))
                    missing.Add(placement.Source.DocumentId);
                sb.Append("<section class=\"missing\" role=\"status\">Fonte indisponível para esta publicação.</section>");
                continue;
            }
            var body = NoteMarks.StripComments(MarkdownEngine.SplitFrontmatter(source.Markdown).Body);
            sb.Append("<article>").Append(MarkdownEngine.Render(body, RenderMath)).Append("</article>");
        }
        sb.Append("</main></body></html>");
        return new PublicationHtmlResult(sb.ToString(), missing.AsReadOnly());
    }

    private static string RenderMath(string tex, bool display)
    {
        var result = MathRenderer.RenderSvg(tex, display, 18);
        var label = MarkdownEngine.EscapeHtml(tex);
        if (result.Success)
            return "<span role=\"math\" aria-label=\"" + label + "\">" + result.Svg + "</span>";
        return "<code class=\"umath-raw\" title=\"" + MarkdownEngine.EscapeHtml(result.Diagnostic ?? "Fórmula não renderizada") + "\">" + label + "</code>";
    }
}
