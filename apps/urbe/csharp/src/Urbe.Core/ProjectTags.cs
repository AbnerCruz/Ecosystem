namespace Urbe.Core;

/// <summary>A tag a note gets from the folder it is in: the tag, the folder note that gives it and that folder.</summary>
public sealed record InheritedTag(string Tag, string FolderNote, string Folder);

/// <summary>
/// Tags herdadas por projeto (DEC-0058 / ADR-0046, alternativa A). The tags in the frontmatter of a folder's own note — the
/// note named like the folder, inside it (<c>Droerni/Droerni.md</c>) — are worth for everything in that folder and below
/// it. Nothing is written in the other notes: the inheritance is worked out from where a note is, so moving a note changes
/// what it inherits at once. Subfolders add their own folder notes' tags. A note refuses all of it with
/// <c>herdar: false</c>, or some tags with <c>sem-tags: [worldbuilding]</c>. A tag the note already has is its own, not
/// inherited. The folder note is the only authority (NN-001); everything else is a projection of it.
/// </summary>
public sealed class ProjectTags
{
    private readonly Dictionary<string, (string Note, IReadOnlyList<string> Tags)> _byFolder;

    private ProjectTags(Dictionary<string, (string Note, IReadOnlyList<string> Tags)> byFolder) => _byFolder = byFolder;

    public static ProjectTags Empty { get; } = new(new(StringComparer.OrdinalIgnoreCase));

    /// <summary>The folders whose folder note gives tags.</summary>
    public IReadOnlyCollection<string> Folders => _byFolder.Keys;

    /// <summary>The folder notes of a vault and their tags, read once.</summary>
    public static ProjectTags Of(IEnumerable<UrbeDocument> documents)
    {
        var byFolder = new Dictionary<string, (string, IReadOnlyList<string>)>(StringComparer.OrdinalIgnoreCase);
        foreach (var document in documents)
        {
            if (FolderOf(document.Path) is not { } folder) continue;
            if (!ArtifactModel.IsNote(document.Path)) continue;
            if (!string.Equals(ArtifactModel.WithoutNoteExtension(document.Path), folder + "/" + LastSegment(folder), StringComparison.OrdinalIgnoreCase)) continue;
            if (!document.Content.StartsWith("---", StringComparison.Ordinal)) continue;
            var tags = Clean(NoteProperties.Parse(document.Content).Tags);
            if (tags.Count == 0) continue;
            // two notes named like the folder (Droerni.md and Droerni.txt…): the shorter path, as a link resolves it
            if (byFolder.TryGetValue(folder, out var existing) && existing.Item1.Length <= document.Path.Length) continue;
            byFolder[folder] = (document.Path, tags);
        }
        return new ProjectTags(byFolder);
    }

    /// <summary>
    /// What a note at <paramref name="path"/> inherits, from the outer folder in: each tag once, with the folder note that
    /// gives it. <paramref name="content"/> is the note's text (for <c>herdar</c>, <c>sem-tags</c> and its own tags).
    /// </summary>
    public IReadOnlyList<InheritedTag> For(string path, string? content, IReadOnlyList<string>? own = null)
    {
        if (_byFolder.Count == 0 || FolderOf(path) is not { } folder) return [];
        var parts = folder.Split('/');
        var givers = new List<(string Folder, string Note, IReadOnlyList<string> Tags)>();
        for (int i = 1; i <= parts.Length; i++)
        {
            var at = string.Join('/', parts.Take(i));
            // the folder note's own tags are its own, not inherited from itself
            if (_byFolder.TryGetValue(at, out var given) && !string.Equals(given.Note, path, StringComparison.OrdinalIgnoreCase))
                givers.Add((at, given.Note, given.Tags));
        }
        if (givers.Count == 0) return [];
        var props = content is not null && content.StartsWith("---", StringComparison.Ordinal) ? NoteProperties.Parse(content) : null;
        if (props?.Get("herdar") is { } inherit && inherit.Value.Trim().Equals("false", StringComparison.OrdinalIgnoreCase)) return [];
        var refused = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (props?.Get("sem-tags") is { } without)
            foreach (var t in without.Items.Count > 0 ? without.Items : without.Value.Split([',', ' '], StringSplitOptions.RemoveEmptyEntries))
                refused.Add(t.Trim().TrimStart('#'));
        var have = new HashSet<string>(own ?? DocumentModel.ParseTags(content), StringComparer.OrdinalIgnoreCase);
        var output = new List<InheritedTag>();
        foreach (var (at, note, tags) in givers)
            foreach (var tag in tags)
                if (!refused.Contains(tag) && have.Add(tag))
                    output.Add(new InheritedTag(tag, note, at));
        return output;
    }

    /// <summary>A note's tags with the inherited ones after its own (for search, filters and queries).</summary>
    public IReadOnlyList<string> Effective(UrbeDocument document)
    {
        var inherited = For(document.Path, document.Content, document.Tags);
        return inherited.Count == 0 ? document.Tags : [.. document.Tags, .. inherited.Select(t => t.Tag)];
    }

    /// <summary>"#worldbuilding · de Droerni" — how an inherited tag is shown.</summary>
    public static string Describe(InheritedTag tag) => "#" + tag.Tag + " · de " + FolderName(tag);

    /// <summary>The name of the folder an inherited tag comes from ("Droerni").</summary>
    public static string FolderName(InheritedTag tag) => LastSegment(tag.Folder);

    private static IReadOnlyList<string> Clean(IReadOnlyList<string> tags) =>
        [.. tags.Select(t => t.Trim().TrimStart('#')).Where(t => t.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase)];

    private static string? FolderOf(string? path)
    {
        var p = (path ?? "").Replace('\\', '/').Trim('/');
        int slash = p.LastIndexOf('/');
        return slash > 0 ? p[..slash] : null;
    }

    private static string LastSegment(string folder)
    {
        int slash = folder.LastIndexOf('/');
        return slash >= 0 ? folder[(slash + 1)..] : folder;
    }
}
