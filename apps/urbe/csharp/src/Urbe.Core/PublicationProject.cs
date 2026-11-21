using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Urbe.Core;

/// <summary>A layout placement references one real vault document. It never owns Markdown.</summary>
public sealed record PublicationPlacement
{
    public string Id { get; init; } = "piece_" + Guid.NewGuid().ToString("N");
    public PublicationSourceReference Source { get; init; } = new("");
    /// <summary>"section", or "cover" (CST-159): the note shown as the publication's cover, first and out of the contents.</summary>
    public string Role { get; init; } = "section";

    public const string CoverRole = "cover";
    public bool IsCover => Role == CoverRole;
}

/// <summary>Editorial styling stays separate from authored notes.</summary>
public sealed record PublicationDesign
{
    public string Theme { get; init; } = "paper";
    public string Layout { get; init; } = "article";
    /// <summary>CST-161: the person's own accent ("#RRGGBB") over the theme's; null keeps the theme's.</summary>
    public string? Accent { get; init; }
    /// <summary>CST-161: "sans", "serif" or "mono"; null = the layout's (a book is serif, the rest sans).</summary>
    public string? Font { get; init; }
}

/// <summary>
/// CST-161: the identity tokens every output reads (HTML, slides, course, PDF): one place for the colours and the type,
/// instead of each exporter keeping its own copy of the theme.
/// </summary>
public static partial class PublicationTheme
{
    public sealed record Tokens(string Accent, string Ink, string Paper, string Font, string CssFont, double LineHeight);

    public static readonly IReadOnlyList<(string Id, string Name, string Accent)> Themes =
        [("paper", "Papel", "#6b4f2a"), ("ocean", "Oceano", "#075985"), ("ink", "Tinta", "#312e81")];

    public static Tokens Of(PublicationDesign design)
    {
        var accent = design.Accent is { } own && Hex().IsMatch(own) ? own.ToLowerInvariant()
            : Themes.FirstOrDefault(t => t.Id == design.Theme).Accent ?? Themes[0].Accent;
        var font = design.Font is "sans" or "serif" or "mono" ? design.Font : design.Layout == "book" ? "serif" : "sans";
        var css = font switch { "serif" => "Georgia,'Times New Roman',serif", "mono" => "ui-monospace,Menlo,Consolas,monospace", _ => "system-ui,sans-serif" };
        return new(accent, "#25272c", "#ffffff", font, css, font == "serif" ? 1.7 : 1.65);
    }

    public static bool ValidAccent(string? value) => value is not null && Hex().IsMatch(value);

    [System.Text.RegularExpressions.GeneratedRegex("^#[0-9A-Fa-f]{6}$")]
    private static partial System.Text.RegularExpressions.Regex Hex();
}

/// <summary>
/// CST-160: a dynamic source query. Kind is "tag", "folder" or "search"; notes that match later are offered for review,
/// never added by themselves, and a note the person declined stays out (Declined, by id).
/// </summary>
public sealed record PublicationQuery(string Kind, string Value)
{
    public List<string> Declined { get; init; } = [];
    /// <summary>Notes kept on purpose although they no longer match (not asked about again), by id.</summary>
    public List<string> Kept { get; init; } = [];

    private bool Matches(UrbeDocument note) => Kind switch
    {
        "tag" => note.Tags.Any(t => string.Equals(t.TrimStart('#'), Value.TrimStart('#'), StringComparison.OrdinalIgnoreCase)),
        "folder" => Value.Trim('/').Length == 0 || note.Path.StartsWith(Value.Trim('/') + "/", StringComparison.Ordinal),
        _ => false
    };

    /// <summary>
    /// CST-160: the notes the publication uses that no longer match (the tag taken off, moved out of the folder, gone from
    /// the search) — told for editorial review, never taken out by themselves.
    /// </summary>
    public IReadOnlyList<UrbeDocument> NoLongerMatching(PublicationProject project, Func<string, UrbeDocument?> note, Func<string, IReadOnlyList<UrbeDocument>> search)
    {
        var found = Kind == "search" ? search(Value).Select(d => d.Id).ToHashSet(StringComparer.Ordinal) : null;
        return project.Sections.Where(s => !s.IsCover).Select(s => note(s.Source.DocumentId)).OfType<UrbeDocument>()
            .Where(n => !Kept.Contains(n.Id) && !(found?.Contains(n.Id) ?? Matches(n))).DistinctBy(n => n.Id).ToList();
    }

    /// <summary>The notes that match now and the publication does not use yet (nor were declined).</summary>
    public IReadOnlyList<UrbeDocument> NewMatches(PublicationProject project, IEnumerable<UrbeDocument> notes, Func<string, IReadOnlyList<UrbeDocument>> search)
    {
        var used = project.Sources.Select(s => s.DocumentId).ToHashSet(StringComparer.Ordinal);
        var found = Kind == "search" ? search(Value).Select(d => d.Id).ToHashSet(StringComparer.Ordinal) : null;
        return notes.Where(n => ArtifactModel.IsNote(n.Path) && !used.Contains(n.Id) && !Declined.Contains(n.Id) &&
                (found?.Contains(n.Id) ?? Matches(n)))
            .OrderBy(n => n.Path, StringComparer.OrdinalIgnoreCase).ToList();
    }
}

/// <summary>
/// CST-153: an export's version: when, in which format, and a short hash of each source's text then. Comparing it with
/// the notes now says which sources changed or left since, without keeping any copy of their text.
/// </summary>
public sealed record PublicationExportRecord(string Format, DateTimeOffset At, Dictionary<string, string> Sources)
{
    /// <summary>What kind of change a source had since the export, the most serious first.</summary>
    public enum ChangeKind { Removed, Structure, Text, Visual }

    public static string Hash(string markdown) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(markdown.Replace("\r\n", "\n"))))[..16].ToLowerInvariant();

    /// <summary>"full:plain:structure" — the whole text, the words without formatting, and the headings and links.</summary>
    public static string Version(string markdown)
    {
        var body = markdown.Replace("\r\n", "\n");
        var plain = System.Text.RegularExpressions.Regex.Replace(System.Text.RegularExpressions.Regex.Replace(body, @"[*_`~=>#\[\]()!|-]+", ""), @"\s+", " ").Trim();
        var structure = string.Join("\n", EmbedFragments.Headings(body).Concat(System.Text.RegularExpressions.Regex.Matches(body, @"\[\[[^\]]+\]\]|\]\([^)]+\)").Select(m => m.Value)));
        return Hash(body) + ":" + Hash(plain) + ":" + Hash(structure);
    }

    public static PublicationExportRecord Of(PublicationProject project, string format, Func<string, string?> markdown, DateTimeOffset at) =>
        new(format, at, project.Sources.Select(s => s.DocumentId).Distinct(StringComparer.Ordinal)
            .ToDictionary(id => id, id => markdown(id) is { } md ? Version(md) : "", StringComparer.Ordinal));

    /// <summary>How a source changed between two <see cref="Version"/>s, or null when it did not.</summary>
    public static ChangeKind? Kind(string then, string now)
    {
        if (now == then) return null;
        var a = then.Split(':'); var b = now.Split(':');
        return a.Length < 3 || b.Length < 3 ? ChangeKind.Text : a[2] != b[2] ? ChangeKind.Structure : a[1] != b[1] ? ChangeKind.Text : ChangeKind.Visual;
    }

    /// <summary>The sources that changed since (with the kind of change) and the ones added since.</summary>
    public (IReadOnlyList<(string Id, ChangeKind Kind)> Changed, IReadOnlyList<string> Added) Since(PublicationProject project, Func<string, string?> markdown)
    {
        var changed = new List<(string, ChangeKind)>();
        foreach (var (id, then) in Sources)
        {
            if (markdown(id) is not { } md) { changed.Add((id, ChangeKind.Removed)); continue; }
            if (Kind(then, Version(md)) is { } kind) changed.Add((id, kind));
        }
        var added = project.Sources.Select(s => s.DocumentId).Distinct(StringComparer.Ordinal).Where(id => !Sources.ContainsKey(id)).ToList();
        return (changed.OrderBy(c => c.Item2).ToList(), added);
    }
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
    /// <summary>CST-170: the timeline of a "video" layout (scenes, objects, keyframes, audio). Null for other layouts.</summary>
    public VideoComposition? Video { get; init; }
    /// <summary>CST-160: the query that chose the sources (a tag, folder or search), kept to offer the notes that match later.</summary>
    public PublicationQuery? Query { get; init; }
    /// <summary>CST-153: the version of each source the last export was made from (by format), to say what changed since.</summary>
    public PublicationExportRecord? LastExport { get; init; }

    // Do not drop future non-authoring metadata on a load/edit/save round trip.
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; init; }

    public static PublicationProject Create(string title, string? documentId = null)
    {
        var result = new PublicationProject { Title = title };
        return string.IsNullOrEmpty(documentId) ? result : result.AddSource(documentId);
    }

    /// <summary>
    /// CST-161: a new publication in the style of this one — layout, theme, accent, type, audience, objective and the
    /// video's format — with none of its notes, scenes or exports (a model, never a copy of the content).
    /// </summary>
    public PublicationProject StyleFor(string title) => new()
    {
        Title = title, Design = Design, Audience = Audience, Objective = Objective, Formats = [.. Formats],
        Video = Video is { } v ? v with { Scenes = [], Audio = [] } : null
    };

    /// <summary>Idempotent source insertion and a corresponding ordered layout placement.</summary>
    public PublicationProject AddSource(string documentId)
        => AddSource(new PublicationSourceReference(documentId));

    public PublicationProject AddSource(PublicationSourceReference source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (string.IsNullOrWhiteSpace(source.DocumentId)) throw new ArgumentException("ID da fonte obrigatório.", nameof(source));
        if (Sources.Contains(source)) return this;
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

    public PublicationProject RemovePlacement(string placementId)
    {
        var sections = Sections.Where(s => s.Id != placementId).ToList();
        return this with { Sections = sections, Sources = Sources.Where(s => sections.Any(p => p.Source == s)).ToList() };
    }

    /// <summary>CST-159: the piece shown as the cover, if any.</summary>
    [JsonIgnore]
    public PublicationPlacement? Cover => Sections.FirstOrDefault(s => s.IsCover);

    /// <summary>
    /// CST-159: this piece becomes the cover (the only one, moved to the start); null takes the cover away and the piece
    /// stays where it is as an ordinary section.
    /// </summary>
    public PublicationProject WithCover(string? placementId)
    {
        if (placementId is not null && Sections.All(s => s.Id != placementId)) throw new InvalidOperationException("Peça inexistente.");
        var sections = Sections.Select(s => s with { Role = s.Id == placementId ? PublicationPlacement.CoverRole : s.IsCover ? "section" : s.Role }).ToList();
        if (placementId is not null) sections = [.. sections.Where(s => s.Id == placementId), .. sections.Where(s => s.Id != placementId)];
        return this with { Sections = sections };
    }

    /// <summary>CST-155: a piece now shows another real note (an adaptation of its source); its place and role stay.</summary>
    public PublicationProject ReplacePlacementSource(string placementId, PublicationSourceReference source)
    {
        var piece = Sections.FirstOrDefault(s => s.Id == placementId) ?? throw new InvalidOperationException("Peça inexistente.");
        var sections = Sections.Select(s => s.Id == placementId ? s with { Source = source } : s).ToList();
        var sources = sections.Select(s => s.Source).Distinct().ToList();
        return this with { Sections = sections, Sources = sources };
    }

    public PublicationProject MovePlacement(string placementId, int index)
    {
        var sections = Sections.ToList();
        var piece = sections.FirstOrDefault(s => s.Id == placementId) ?? throw new InvalidOperationException("Peça inexistente.");
        sections.Remove(piece);
        sections.Insert(Math.Clamp(index, 0, sections.Count), piece);
        return this with { Sections = CoverFirst(sections) };
    }

    /// <summary>
    /// CST-160: the sections in another order — "titulo" (alphabetical), "data" (oldest change first, a diary's order)
    /// or "pasta" (grouped by folder, then by name). The cover stays first; equal keys keep their order; notes untouched.
    /// </summary>
    public PublicationProject SortedBy(string by, Func<string, UrbeDocument?> note)
    {
        ArgumentNullException.ThrowIfNull(note);
        var culture = StringComparer.Create(System.Globalization.CultureInfo.GetCultureInfo("pt-BR"), System.Globalization.CompareOptions.IgnoreCase);
        IOrderedEnumerable<PublicationPlacement> order = by switch
        {
            "titulo" => Sections.OrderBy(s => note(s.Source.DocumentId)?.Title ?? "", culture),
            // ISO dates sort as text; a note without a date goes last
            "data" => Sections.OrderBy(s => note(s.Source.DocumentId)?.Modified ?? "\uffff", StringComparer.Ordinal),
            "pasta" => Sections.OrderBy(s => Path.GetDirectoryName(note(s.Source.DocumentId)?.Path ?? "") ?? "", culture)
                               .ThenBy(s => note(s.Source.DocumentId)?.Title ?? "", culture),
            _ => throw new ArgumentException("Ordem desconhecida: " + by, nameof(by))
        };
        var sections = CoverFirst(order.ToList());
        return this with { Sections = sections, Sources = sections.Select(s => s.Source).Distinct().ToList() };
    }

    // the cover never leaves the start: moving another piece before it, or moving it, keeps it there
    private static List<PublicationPlacement> CoverFirst(List<PublicationPlacement> sections) =>
        sections.FindIndex(s => s.IsCover) is > 0 and var at ? [sections[at], .. sections.Where((_, i) => i != at)] : sections;

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
        return this with { Sources = sources, Sections = CoverFirst(sections) };
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
        catch (JsonException e)
        {
            // where it went wrong, so a person or the Assistente can fix that one field (a bare "inválido" made the model guess)
            var where = string.IsNullOrEmpty(e.Path) || e.Path == "$" ? "" : " no campo " + e.Path.TrimStart('$', '.');
            var line = e.LineNumber is { } l ? " (linha " + (l + 1) + ")" : "";
            var why = e.Message.Contains("could not be converted", StringComparison.Ordinal) ? ": valor do tipo errado (número, texto, lista ou objeto)"
                : e.Message.Contains("end of data", StringComparison.OrdinalIgnoreCase) || e.Message.Contains("incomplete", StringComparison.OrdinalIgnoreCase) ? ": o JSON termina antes da hora (cortado?)"
                : ": sintaxe de JSON inválida";
            throw new InvalidDataException("JSON de Publicações inválido" + where + line + why + ".", e);
        }
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
        if (project.Sections.Count(s => s.IsCover) > 1 || project.Sections.Skip(1).Any(s => s.IsCover))
            throw new InvalidDataException("A capa é uma só e vem antes das outras peças.");
        if (project.Formats.Count == 0 || project.Formats.Any(s => string.IsNullOrWhiteSpace(s)))
            throw new InvalidDataException("A publicação precisa declarar os formatos de destino.");
        if (project.Video is { } video) VideoCompositionRules.Validate(video);
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
    /// <param name="data">CST-207: the text of a ```dados source (preloaded by the app); null leaves those blocks unavailable.</param>
    public static PublicationHtmlResult Export(PublicationProject project, PublicationSourceCatalog catalog, Func<string, string?>? data = null)
    {
        PublicationProjectCodec.Validate(project);
        ArgumentNullException.ThrowIfNull(catalog);
        // CST-167: slides are a presentation of their own, not a page of scrolled articles
        if (project.Design.Layout == "slides") return PublicationSlides.Export(project, catalog, RenderMath, data);
        // CST-174: a course is a navigable product with activities and progress
        if (project.Design.Layout == "course") return PublicationCourse.Export(project, catalog, data);
        var title = MarkdownEngine.EscapeHtml(project.Title);
        var slides = false;
        var book = project.Design.Layout == "book";
        var tokens = PublicationTheme.Of(project.Design);
        var accent = tokens.Accent;
        var sb = new StringBuilder();
        sb.Append("<!doctype html>\n<html lang=\"pt-BR\"><head><meta charset=\"utf-8\">")
          .Append("<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">")
          .Append("<meta http-equiv=\"Content-Security-Policy\" content=\"default-src 'none'; img-src data:; style-src 'unsafe-inline'; base-uri 'none'; form-action 'none'\">")
          .Append("<title>").Append(title).Append("</title><style>")
          .Append(":root{color-scheme:light}*{box-sizing:border-box}body{margin:0;color:").Append(tokens.Ink).Append(";background:").Append(tokens.Paper).Append(";font-family:").Append(tokens.CssFont).Append(";line-height:").Append(tokens.LineHeight.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append("}")
          .Append("main{max-width:760px;padding:clamp(20px,5vw,64px);margin:auto}h1,h2,h3{line-height:1.24}article{margin:2.5rem 0}")
          .Append("img,video,svg{max-width:100%;height:auto}pre{overflow:auto}code{white-space:pre-wrap;overflow-wrap:anywhere}")
          .Append("blockquote{border-left:3px solid #999;padding-left:1rem}table{display:block;overflow-x:auto}")
          .Append(".umath{display:inline-block;max-width:100%;vertical-align:middle}.umath svg{vertical-align:middle}.umath-block{display:block;overflow-x:auto;text-align:center;margin:1.2rem 0}")
          .Append(".urbe-data{border-collapse:collapse;margin:1rem 0;display:block;overflow-x:auto}.urbe-data th,.urbe-data td{border:1px solid #d8d2c6;padding:.3rem .6rem}.urbe-data th{background:#f3f1ec}.urbe-data-source{font-size:.8em;color:#777;margin-top:-.6rem}")
          .Append(QuizBlock.Style)
          .Append(".urbe-calc{border-collapse:collapse;margin:1rem 0}.urbe-calc td{padding:.25rem .8rem;border-bottom:1px solid #e3ded4}.urbe-calc td:last-child{font-weight:600}.urbe-calc-error{color:#a3352f;font-weight:400!important}")
          .Append(".urbe-plot{margin:1.2rem 0;color:#25272c}.urbe-plot svg{max-width:100%;height:auto}")
          .Append(".ubox{border-left:4px solid #9c7b45;background:#f6f3ee;padding:.6rem 1rem;margin:1rem 0;border-radius:6px}.ubox-title{margin:0 0 .4rem}")
          .Append(".missing{border:1px dashed #a35;padding:1rem}@page{size:A4;margin:18mm}")
          .Append("@media print{main{max-width:none;padding:0}article{break-before:page}article:first-of-type{break-before:auto}h1,h2,h3{break-after:avoid}p{widows:3;orphans:3}pre,table,img{break-inside:avoid}}")
          .Append("h1,h2,h3,a{color:").Append(accent).Append("}nav a{display:block;padding:.35em 0}progress{width:100%;accent-color:").Append(accent).Append("}");
        if (book) sb.Append("p{text-align:start}article{counter-increment:chapter}nav{margin:2rem 0}");
        if (slides) sb.Append("main{max-width:none}article{min-height:80vh;scroll-snap-align:start;padding:clamp(16px,4vw,64px);border:1px solid #ddd;border-radius:12px}html{scroll-snap-type:y proximity}@media print{@page{size:320mm 180mm;margin:12mm}header,nav{display:none}article{min-height:0;border:0;padding:0;break-before:page}article:first-of-type{break-before:auto}}");
        // CST-159: a cover is a page of its own, centred, with the publication's title in its note (not repeated above it)
        sb.Append(".cover{min-height:85vh;display:flex;flex-direction:column;justify-content:center;text-align:center}.cover img{max-height:55vh;margin:1rem auto;display:block}@media print{.cover{min-height:250mm;break-after:page}}");
        sb.Append("</style></head><body><main>");
        if (project.Cover is null) sb.Append("<header><h1>").Append(title).Append("</h1></header>");
        if (book || slides)
        {
            sb.Append("<nav aria-label=\"Sumário\">");
            foreach (var piece in project.Sections.Where(p => !p.IsCover))
            {
                var source = catalog.Read(piece.Source);
                var label = source is null ? "Fonte indisponível" : System.IO.Path.GetFileNameWithoutExtension(source.Path);
                sb.Append("<a href=\"#").Append(MarkdownEngine.EscapeHtml(piece.Id)).Append("\">").Append(MarkdownEngine.EscapeHtml(label)).Append("</a>");
            }
            sb.Append("</nav>");
        }
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
            var body = PublicationAssets.WikiImagesToMarkdown(NoteMarks.StripComments(MarkdownEngine.SplitFrontmatter(source.Markdown).Body));
            var rendered = QuizBlock.RenderHtml(Data.DataBlock.RenderHtml(Calculus.Calculator.RenderHtml(FunctionPlot.RenderHtml(NoteContainers.RenderHtml(body, md => MarkdownEngine.Render(md, RenderMath)))), data ?? (_ => null)), interactive: false);
            var element = placement.IsCover ? "section" : "article";
            sb.Append('<').Append(element).Append(" id=\"").Append(MarkdownEngine.EscapeHtml(placement.Id)).Append(placement.IsCover ? "\" class=\"cover\" aria-label=\"Capa\">" : "\" tabindex=\"0\">")
              .Append(NoteProgress.RenderHtml(rendered, source.Markdown, id => catalog.Read(new PublicationSourceReference(id))?.Markdown)).Append("</").Append(element).Append('>');
        }
        sb.Append("</main></body></html>");
        return new PublicationHtmlResult(sb.ToString(), missing.AsReadOnly());
    }

    internal static string RenderMath(string tex, bool display)
    {
        var result = MathRenderer.RenderSvg(tex, display, 18);
        var label = MarkdownEngine.EscapeHtml(tex);
        if (result.Success)
            return "<span role=\"math\" aria-label=\"" + label + "\">" + result.Svg + "</span>";
        return "<code class=\"umath-raw\" title=\"" + MarkdownEngine.EscapeHtml(result.Diagnostic ?? "Fórmula não renderizada") + "\">" + label + "</code>";
    }
}
