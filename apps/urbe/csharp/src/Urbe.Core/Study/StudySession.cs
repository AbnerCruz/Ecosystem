using System.Text.Json;
using System.Text.Json.Serialization;

namespace Urbe.Core.Study;

/// <summary>
/// One source of a study session: a vault document by its stable id (the path is only a hint for reading), and the
/// version of it last studied (<see cref="PublicationExportRecord.Version"/>), so a change made since is told.
/// </summary>
public sealed record StudySource(string DocumentId, string PathHint, string? Seen = null);

/// <summary>
/// #956: a study session (the NotebookLM "notebook", adapted): the sources studied, by stable id, and the session's
/// objectives — metadata only, never a copy of a source. The same source can be in many sessions; a renamed or moved
/// source is still found by its id. Kept as <c>*.estudo.json</c> in the vault, a file the person can read.
/// </summary>
public sealed record StudySession
{
    public int Version { get; init; } = 1;
    public string Title { get; init; } = "Estudo";
    public List<string> Objectives { get; init; } = [];
    public List<StudySource> Sources { get; init; } = [];
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; init; }

    public const string Suffix = ".estudo.json";

    public StudySession Add(string documentId, string path, string? markdown = null) =>
        Sources.Any(s => s.DocumentId == documentId) ? this
            : this with { Sources = [.. Sources, new(documentId, path, markdown is null ? null : PublicationExportRecord.Version(markdown))] };

    /// <summary>
    /// #956 (one source, many sessions): the notes that changed since they were last studied here, and how (text,
    /// headings and links, look only). Missing notes are told by <see cref="Resolve"/>; files other than notes are not compared.
    /// </summary>
    public IReadOnlyList<(StudySource Source, PublicationExportRecord.ChangeKind Kind)> Changes(Func<string, string?> markdownOfId) =>
        [.. Sources.Where(s => s.Seen is not null).Select(s => (s, Now: markdownOfId(s.DocumentId)))
            .Where(x => x.Now is not null).Select(x => (x.s, Kind: PublicationExportRecord.Kind(x.s.Seen!, PublicationExportRecord.Version(x.Now!))))
            .Where(x => x.Kind is not null).Select(x => (x.s, x.Kind!.Value))];

    /// <summary>The notes as they are now become the versions studied (also the first baseline of an older session).</summary>
    public StudySession MarkSeen(Func<string, string?> markdownOfId) => this with
    {
        Sources = [.. Sources.Select(s => markdownOfId(s.DocumentId) is { } md ? s with { Seen = PublicationExportRecord.Version(md) } : s)]
    };

    public StudySession Remove(string documentId) => this with { Sources = Sources.Where(s => s.DocumentId != documentId).ToList() };

    /// <summary>The sources as they are now: their current paths by id, and the ids no longer in the vault.</summary>
    public (IReadOnlyList<(string Id, string Path)> Present, IReadOnlyList<StudySource> Missing) Resolve(Func<string, string?> pathOfId)
    {
        var present = new List<(string, string)>();
        var missing = new List<StudySource>();
        foreach (var s in Sources)
            if (pathOfId(s.DocumentId) is { } path) present.Add((s.DocumentId, path)); else missing.Add(s);
        return (present, missing);
    }

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public string Serialize() => JsonSerializer.Serialize(this, Json) + "\n";

    public static StudySession Parse(string json)
    {
        var s = JsonSerializer.Deserialize<StudySession>(json, Json) ?? throw new InvalidDataException("Sessão de estudo vazia.");
        if (s.Version != 1) throw new NotSupportedException("Versão de sessão de estudo não suportada: " + s.Version);
        return s;
    }
}
