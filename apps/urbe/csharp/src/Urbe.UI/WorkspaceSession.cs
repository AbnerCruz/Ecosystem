using System.Collections.ObjectModel;
using Urbe.Core;

namespace Urbe.UI;

public enum EditorSurfaceMode
{
    Visual,
    Source
}

public enum EditorOpenOriginKind
{
    Explorer,
    Search,
    Link,
    City,
    Ai,
    Direct
}

public sealed record EditorOpenOrigin(EditorOpenOriginKind Kind, string Href);

public sealed record WorkspaceExplorerEntry(
    string Path,
    string Name,
    bool IsFolder,
    ArtifactType? Type,
    string Extension,
    bool Editable);

/// <summary>
/// Host-neutral session shared by the Explorer and Editor surfaces.
/// It owns no filesystem API: hosts load a VaultSnapshot and later persist
/// planned changes through the canonical persistence boundary.
/// </summary>
public sealed class WorkspaceSession : IDisposable
{
    private readonly HashSet<string> _paths =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Stack<EditorOpenOrigin> _origins = new();

    public WorkspaceSession()
    {
        Knowledge = new KnowledgeIndex(Documents);
    }

    public DocumentStore Documents { get; } = new();

    public KnowledgeIndex Knowledge { get; }

    public UrbeDocument? CurrentDocument { get; private set; }

    public EditorSurfaceMode Mode { get; private set; } = EditorSurfaceMode.Visual;

    public bool IsReadOnly { get; private set; }

    public long Revision { get; private set; }

    public string CurrentFolder { get; private set; } = string.Empty;

    public IReadOnlyCollection<string> Paths =>
        new ReadOnlyCollection<string>(
            _paths.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray());

    public string PreviewHtml =>
        MarkdownEngine.Render(CurrentDocument?.Content ?? string.Empty);

    public void Dispose()
    {
        Knowledge.Dispose();
        GC.SuppressFinalize(this);
    }

    public void Load(VaultSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        _paths.Clear();
        foreach (var path in snapshot.Files.Keys)
            _paths.Add(DocumentModel.NormalizePath(path));

        Documents.ReplaceFromVault(snapshot, "workspace-load");
        IsReadOnly = snapshot.IsReadOnly;
        CurrentDocument = null;
        CurrentFolder = string.Empty;
        _origins.Clear();
        Revision++;
    }

    /// <summary>
    /// Deterministic in-memory bootstrap used by tests and future host adapters
    /// after they have converted their source into canonical documents.
    /// </summary>
    public void LoadDocuments(IEnumerable<DocumentInput> documents, bool readOnly = false)
    {
        ArgumentNullException.ThrowIfNull(documents);

        var materialized = documents.ToArray();
        Documents.ReplaceAll(materialized, "workspace-load-documents");

        _paths.Clear();
        foreach (var document in Documents.List())
            _paths.Add(document.Path);

        IsReadOnly = readOnly;
        CurrentDocument = null;
        CurrentFolder = string.Empty;
        _origins.Clear();
        Revision++;
    }

    public IReadOnlyList<WorkspaceExplorerEntry> ExplorerEntries(
        string? folder = null,
        string? query = null,
        bool includeSystem = false)
    {
        var normalizedFolder = DocumentModel.NormalizePath(folder);
        var prefix = normalizedFolder.Length == 0
            ? string.Empty
            : normalizedFolder + "/";
        var normalizedQuery = (query ?? string.Empty).Trim();

        if (normalizedQuery.Length > 0)
        {
            return Array.AsReadOnly(
                _paths
                    .Where(path => includeSystem || !ArtifactModel.IsSystem(path))
                    .Where(path => path.Contains(
                        normalizedQuery,
                        StringComparison.OrdinalIgnoreCase))
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                    .Select(FileEntry)
                    .ToArray());
        }

        var folders = new Dictionary<string, WorkspaceExplorerEntry>(
            StringComparer.OrdinalIgnoreCase);
        var files = new List<WorkspaceExplorerEntry>();

        foreach (var path in _paths.OrderBy(
                     path => path,
                     StringComparer.OrdinalIgnoreCase))
        {
            if (!includeSystem && ArtifactModel.IsSystem(path))
                continue;
            if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                continue;

            var rest = path[prefix.Length..];
            if (rest.Length == 0)
                continue;

            var slash = rest.IndexOf('/');
            if (slash >= 0)
            {
                var name = rest[..slash];
                var childPath = prefix + name;
                folders.TryAdd(
                    childPath,
                    new WorkspaceExplorerEntry(
                        childPath,
                        name,
                        true,
                        null,
                        string.Empty,
                        false));
                continue;
            }

            files.Add(FileEntry(path));
        }

        return Array.AsReadOnly(
            folders.Values
                .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
                .Concat(
                    files.OrderBy(
                        entry => entry.Name,
                        StringComparer.OrdinalIgnoreCase))
                .ToArray());
    }

    public bool SetFolder(string? folder)
    {
        var normalized = DocumentModel.NormalizePath(folder);
        if (string.Equals(
                normalized,
                CurrentFolder,
                StringComparison.OrdinalIgnoreCase))
            return false;

        CurrentFolder = normalized;
        Revision++;
        return true;
    }

    public bool Open(string? idOrPath, EditorOpenOrigin origin)
    {
        var document = Documents.Get(idOrPath);
        if (document is null || !ArtifactModel.Classify(document.Path).Editable)
            return false;

        if (CurrentDocument is not null &&
            string.Equals(
                CurrentDocument.Id,
                document.Id,
                StringComparison.Ordinal))
        {
            CurrentDocument = document;
            return true;
        }

        _origins.Push(origin);
        CurrentDocument = document;
        Revision++;
        return true;
    }

    public bool OpenLinked(string? target, string returnHref)
    {
        if (CurrentDocument is null)
            return false;

        var resolved = Knowledge
            .Links(CurrentDocument.Id)
            .FirstOrDefault(
                document =>
                    string.Equals(
                        document.Title,
                        target,
                        StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(
                        ArtifactModel.WithoutNoteExtension(document.Path),
                        target,
                        StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(
                        document.Path,
                        target,
                        StringComparison.OrdinalIgnoreCase));

        return resolved is not null &&
               Open(
                   resolved.Id,
                   new EditorOpenOrigin(EditorOpenOriginKind.Link, returnHref));
    }

    public bool TryPopOrigin(out EditorOpenOrigin origin)
    {
        if (_origins.Count == 0)
        {
            origin = new EditorOpenOrigin(
                EditorOpenOriginKind.Explorer,
                "explorer");
            return false;
        }

        origin = _origins.Pop();
        Revision++;
        return true;
    }

    public void SetMode(EditorSurfaceMode mode)
    {
        if (Mode == mode)
            return;

        Mode = mode;
        Revision++;
    }

    public UrbeDocument? UpdateSource(string? content)
    {
        if (IsReadOnly || CurrentDocument is null)
            return null;

        var current = CurrentDocument;
        CurrentDocument = Documents.Upsert(
            new DocumentInput
            {
                Id = current.Id,
                Path = current.Path,
                Title = current.Title,
                Content = content ?? string.Empty,
                Created = current.Created,
                Modified = current.Modified
            },
            "editor-source");

        _paths.Add(CurrentDocument.Path);
        Revision++;
        return CurrentDocument;
    }

    public UrbeDocument? UpdateVisualHtml(string? html)
    {
        if (CurrentDocument is null)
            return null;

        var markdown = VisualMarkdown.FromHtmlUsingEditorSource(
            html,
            CurrentDocument.Content);
        return UpdateSource(markdown);
    }

    public UrbeDocument? CreateNote(
        string? title,
        string? folder = null,
        string? content = null)
    {
        if (IsReadOnly)
            return null;

        var safeTitle = ArtifactModel.SafeName(title);
        var normalizedFolder = DocumentModel.NormalizePath(folder);
        var path = normalizedFolder.Length == 0
            ? safeTitle + ".md"
            : normalizedFolder + "/" + safeTitle + ".md";

        var suffix = 2;
        var candidate = path;
        while (Documents.Get(candidate) is not null ||
               _paths.Contains(candidate))
        {
            var file = safeTitle + "-" + suffix + ".md";
            candidate = normalizedFolder.Length == 0
                ? file
                : normalizedFolder + "/" + file;
            suffix++;
        }

        var document = Documents.Upsert(
            new DocumentInput
            {
                Path = candidate,
                Content = content ?? string.Empty
            },
            "explorer-create");

        _paths.Add(document.Path);
        Revision++;
        return document;
    }

    public UrbeDocument? CreateLinkedNote(string? target)
    {
        if (CurrentDocument is null || IsReadOnly)
            return null;

        var slash = CurrentDocument.Path.LastIndexOf('/');
        var folder = slash >= 0
            ? CurrentDocument.Path[..slash]
            : string.Empty;

        return CreateNote(target, folder);
    }

    public IReadOnlyList<UrbeDocument> Search(string? query, int limit = 50) =>
        Knowledge.Search(query, limit);

    public IReadOnlyList<UrbeDocument> Links() =>
        CurrentDocument is null
            ? Array.Empty<UrbeDocument>()
            : Knowledge.Links(CurrentDocument.Id);

    public IReadOnlyList<UrbeDocument> Backlinks() =>
        CurrentDocument is null
            ? Array.Empty<UrbeDocument>()
            : Knowledge.Backlinks(CurrentDocument.Id);

    private static WorkspaceExplorerEntry FileEntry(string path)
    {
        var classification = ArtifactModel.Classify(path);
        var slash = path.LastIndexOf('/');
        var name = slash >= 0 ? path[(slash + 1)..] : path;

        return new WorkspaceExplorerEntry(
            path,
            name,
            false,
            classification.Type,
            classification.Extension,
            classification.Editable);
    }
}
