using System.Collections.ObjectModel;
using Urbe.Core;

namespace Urbe.UI;

public enum EditorSurfaceMode
{
    Visual,
    Source,
    Split
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

public sealed record EditorWorkspaceTab(
    string Id,
    string Title,
    string Path,
    bool Pinned,
    bool Active);

public sealed record EditorHistoryStatus(bool CanUndo, bool CanRedo);

public enum WorkspaceMutationKind
{
    CreateFolder,
    Move
}

public sealed record WorkspaceMutation(
    WorkspaceMutationKind Kind,
    string? SourcePath,
    string TargetPath);

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
    private readonly List<EditorTabState> _tabs = [];
    private readonly Dictionary<string, EditorHistoryState> _history =
        new(StringComparer.Ordinal);
    private readonly HashSet<string> _explicitFolders =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly List<WorkspaceMutation> _pendingMutations = [];
    private const int MaxTabs = 12;
    private const int HistoryLimit = 100;

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

    public IReadOnlyCollection<string> Folders =>
        new ReadOnlyCollection<string>(
            EnumerateFolders()
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray());

    public IReadOnlyList<WorkspaceMutation> PendingMutations =>
        new ReadOnlyCollection<WorkspaceMutation>(_pendingMutations.ToArray());

    public string PreviewHtml =>
        MarkdownEngine.Render(CurrentDocument?.Content ?? string.Empty);

    public VisualDocumentModel VisualModel =>
        VisualDocumentEditor.Parse(CurrentDocument?.Content ?? string.Empty);

    /// <summary>
    /// Structural editing is disabled when a parse/save cycle would rewrite
    /// any existing Markdown. The Source editor remains unrestricted.
    /// </summary>
    public bool CanEditVisualBlocks =>
        !IsReadOnly &&
        CurrentDocument is not null &&
        VisualDocumentEditor.IsLosslessRoundTrip(CurrentDocument.Content);

    /// <summary>
    /// HTML visual editing must not normalize any untouched Markdown.
    /// Independent of CanEditVisualBlocks: the HTML converter has its own
    /// fidelity boundary and only the Source mode is always lossless.
    /// </summary>
    public bool CanEditVisualHtml =>
        !IsReadOnly &&
        CurrentDocument is not null &&
        VisualMarkdown.IsLosslessEditorRoundTrip(CurrentDocument.Content);

    public IReadOnlyList<EditorWorkspaceTab> Tabs =>
        Array.AsReadOnly(
            _tabs
                .Select(
                    tab =>
                    {
                        var document = Documents.Get(tab.Id);
                        return document is null
                            ? null
                            : new EditorWorkspaceTab(
                                document.Id,
                                document.Title,
                                document.Path,
                                tab.Pinned,
                                string.Equals(
                                    CurrentDocument?.Id,
                                    document.Id,
                                    StringComparison.Ordinal));
                    })
                .Where(tab => tab is not null)
                .Cast<EditorWorkspaceTab>()
                .ToArray());

    public EditorHistoryStatus HistoryStatus
    {
        get
        {
            if (CurrentDocument is null ||
                !_history.TryGetValue(CurrentDocument.Id, out var history))
                return new EditorHistoryStatus(false, false);

            return new EditorHistoryStatus(
                history.Past.Count > 0,
                history.Future.Count > 0);
        }
    }

    public void Dispose()
    {
        Knowledge.Dispose();
        GC.SuppressFinalize(this);
    }

    public void Load(VaultSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        _paths.Clear();
        _explicitFolders.Clear();
        _pendingMutations.Clear();
        foreach (var path in snapshot.Files.Keys)
            _paths.Add(DocumentModel.NormalizePath(path));

        Documents.ReplaceFromVault(snapshot, "workspace-load");
        IsReadOnly = snapshot.IsReadOnly;
        CurrentDocument = null;
        CurrentFolder = string.Empty;
        ResetEditorState();
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
        _explicitFolders.Clear();
        _pendingMutations.Clear();
        foreach (var document in Documents.List())
            _paths.Add(document.Path);

        IsReadOnly = readOnly;
        CurrentDocument = null;
        CurrentFolder = string.Empty;
        ResetEditorState();
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

        foreach (var folderPath in EnumerateFolders())
        {
            if (!includeSystem && ArtifactModel.IsSystem(folderPath))
                continue;
            if (!folderPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                continue;

            var rest = folderPath[prefix.Length..];
            if (rest.Length == 0 || rest.Contains('/'))
                continue;

            folders.TryAdd(
                folderPath,
                new WorkspaceExplorerEntry(
                    folderPath,
                    rest,
                    true,
                    null,
                    string.Empty,
                    false));
        }

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
        EnsureTab(document);
        EnsureHistory(document);
        Revision++;
        return true;
    }

    public bool Restore(string? idOrPath)
    {
        var document = Documents.Get(idOrPath);
        if (document is null || !ArtifactModel.Classify(document.Path).Editable)
            return false;

        CurrentDocument = document;
        EnsureTab(document);
        EnsureHistory(document);
        Revision++;
        return true;
    }

    public bool ActivateTab(string? id)
    {
        var document = Documents.Get(id);
        if (document is null ||
            !_tabs.Any(
                tab => string.Equals(tab.Id, document.Id, StringComparison.Ordinal)))
            return false;

        CurrentDocument = document;
        EnsureHistory(document);
        Revision++;
        return true;
    }

    public bool CloseTab(string? id)
    {
        var document = Documents.Get(id);
        var target = document?.Id ?? id ?? string.Empty;
        var index = _tabs.FindIndex(
            tab => string.Equals(tab.Id, target, StringComparison.Ordinal));

        if (index < 0)
            return false;

        var wasActive = string.Equals(
            CurrentDocument?.Id,
            target,
            StringComparison.Ordinal);

        _tabs.RemoveAt(index);

        if (wasActive)
        {
            var next = _tabs.Count == 0
                ? null
                : _tabs[Math.Min(index, _tabs.Count - 1)];
            CurrentDocument = next is null ? null : Documents.Get(next.Id);
        }

        Revision++;
        return true;
    }

    public bool PinTab(string? id, bool pinned = true)
    {
        var document = Documents.Get(id);
        if (document is null)
            return false;

        var tab = _tabs.FirstOrDefault(
            item => string.Equals(item.Id, document.Id, StringComparison.Ordinal));
        if (tab is null)
            return false;

        if (tab.Pinned == pinned)
            return true;

        tab.Pinned = pinned;
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

        var next = content ?? string.Empty;
        if (string.Equals(
                CurrentDocument.Content,
                next,
                StringComparison.Ordinal))
            return CurrentDocument;

        var history = EnsureHistory(CurrentDocument);
        history.Past.Add(history.Present);
        if (history.Past.Count > HistoryLimit)
            history.Past.RemoveAt(0);
        history.Present = next;
        history.Future.Clear();

        return ApplyCurrentContent(next, "editor-source");
    }

    public UrbeDocument? Undo()
    {
        if (IsReadOnly || CurrentDocument is null)
            return null;

        var history = EnsureHistory(CurrentDocument);
        if (history.Past.Count == 0)
            return CurrentDocument;

        history.Future.Add(history.Present);
        history.Present = history.Past[^1];
        history.Past.RemoveAt(history.Past.Count - 1);
        return ApplyCurrentContent(history.Present, "editor-undo");
    }

    public UrbeDocument? Redo()
    {
        if (IsReadOnly || CurrentDocument is null)
            return null;

        var history = EnsureHistory(CurrentDocument);
        if (history.Future.Count == 0)
            return CurrentDocument;

        history.Past.Add(history.Present);
        history.Present = history.Future[^1];
        history.Future.RemoveAt(history.Future.Count - 1);
        return ApplyCurrentContent(history.Present, "editor-redo");
    }

    public UrbeDocument? UpdateVisualHtml(string? html)
    {
        var current = CurrentDocument;
        if (current is null || !CanEditVisualHtml)
            return null;

        var markdown = VisualMarkdown.FromHtmlUsingEditorSource(
            html,
            current.Content);
        return UpdateSource(markdown);
    }

    public UrbeDocument? UpdateVisualBlocks(
        string? frontmatter,
        IEnumerable<VisualBlock>? blocks)
    {
        if (!CanEditVisualBlocks)
            return null;

        return UpdateSource(
            VisualDocumentEditor.ToMarkdown(frontmatter, blocks));
    }

    public string? CreateFolder(string? name, string? parent = null)
    {
        if (IsReadOnly)
            return null;

        if (string.IsNullOrWhiteSpace(name))
            return null;

        var safeName = ArtifactModel.SafeName(name);

        var normalizedParent = DocumentModel.NormalizePath(parent);
        if (normalizedParent.Length > 0 &&
            (!FolderExists(normalizedParent) ||
             ArtifactModel.IsSystem(normalizedParent) ||
             ContainsTraversal(normalizedParent)))
            return null;

        var candidate = normalizedParent.Length == 0
            ? safeName
            : normalizedParent + "/" + safeName;

        var suffix = 2;
        var baseCandidate = candidate;
        while (PathOrFolderExists(candidate))
            candidate = baseCandidate + "-" + suffix++;

        _explicitFolders.Add(candidate);
        _pendingMutations.Add(
            new WorkspaceMutation(
                WorkspaceMutationKind.CreateFolder,
                null,
                candidate));
        Revision++;
        return candidate;
    }

    public bool MoveItem(string? sourcePath, string? targetFolder)
    {
        if (IsReadOnly)
            return false;

        var source = DocumentModel.NormalizePath(sourcePath);
        var target = DocumentModel.NormalizePath(targetFolder);

        if (source.Length == 0 ||
            ContainsTraversal(source) ||
            ContainsTraversal(target) ||
            ArtifactModel.IsSystem(source) ||
            (target.Length > 0 &&
             (ArtifactModel.IsSystem(target) || !FolderExists(target))))
            return false;

        var sourceIsFile = _paths.Contains(source);
        var sourceIsFolder = FolderExists(source);
        if (!sourceIsFile && !sourceIsFolder)
            return false;

        if (sourceIsFolder &&
            (string.Equals(source, target, StringComparison.OrdinalIgnoreCase) ||
             target.StartsWith(source + "/", StringComparison.OrdinalIgnoreCase)))
            return false;

        var slash = source.LastIndexOf('/');
        var name = slash >= 0 ? source[(slash + 1)..] : source;
        var destination = target.Length == 0 ? name : target + "/" + name;

        if (string.Equals(source, destination, StringComparison.OrdinalIgnoreCase))
            return true;

        if (PathOrFolderExists(destination))
            return false;

        if (sourceIsFile)
            MoveSinglePath(source, destination);
        else
            MoveFolderTree(source, destination);

        _pendingMutations.Add(
            new WorkspaceMutation(
                WorkspaceMutationKind.Move,
                source,
                destination));
        Revision++;
        return true;
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
        if (normalizedFolder.Length > 0 &&
            (ArtifactModel.IsSystem(normalizedFolder) ||
             ContainsTraversal(normalizedFolder) ||
             !FolderExists(normalizedFolder)))
            return null;

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

    public IReadOnlyList<string> MissingLinks()
    {
        if (CurrentDocument is null)
            return Array.Empty<string>();

        var resolved = Knowledge.Links(CurrentDocument.Id);
        var missing = CurrentDocument.Links
            .Where(
                raw =>
                    !resolved.Any(
                        document =>
                            string.Equals(
                                document.Title,
                                raw,
                                StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(
                                ArtifactModel.WithoutNoteExtension(document.Path),
                                raw,
                                StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(
                                document.Path,
                                raw,
                                StringComparison.OrdinalIgnoreCase)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return Array.AsReadOnly(missing);
    }

    private IEnumerable<string> EnumerateFolders()
    {
        var folders = new HashSet<string>(
            _explicitFolders,
            StringComparer.OrdinalIgnoreCase);

        foreach (var path in _paths)
        {
            var slash = path.LastIndexOf('/');
            while (slash > 0)
            {
                var folder = path[..slash];
                folders.Add(folder);
                slash = folder.LastIndexOf('/');
            }
        }

        return folders;
    }

    private static bool ContainsTraversal(string path) =>
        path.Split('/').Any(segment => segment is "." or "..");

    private bool FolderExists(string path) =>
        _explicitFolders.Contains(path) ||
        _paths.Any(
            file =>
                file.StartsWith(
                    path + "/",
                    StringComparison.OrdinalIgnoreCase));

    private bool PathOrFolderExists(string path) =>
        _paths.Contains(path) || FolderExists(path);

    private void MoveSinglePath(string source, string destination)
    {
        _paths.Remove(source);
        _paths.Add(destination);
        MoveDocumentPath(source, destination);
    }

    private void MoveFolderTree(string source, string destination)
    {
        var fileMoves = _paths
            .Where(
                path =>
                    path.StartsWith(
                        source + "/",
                        StringComparison.OrdinalIgnoreCase))
            .Select(
                path => new
                {
                    Source = path,
                    Target = destination + path[source.Length..]
                })
            .ToArray();

        foreach (var move in fileMoves)
        {
            _paths.Remove(move.Source);
            _paths.Add(move.Target);
            MoveDocumentPath(move.Source, move.Target);
        }

        var folderMoves = _explicitFolders
            .Where(
                path =>
                    string.Equals(
                        path,
                        source,
                        StringComparison.OrdinalIgnoreCase) ||
                    path.StartsWith(
                        source + "/",
                        StringComparison.OrdinalIgnoreCase))
            .Select(
                path => new
                {
                    Source = path,
                    Target = destination + path[source.Length..]
                })
            .ToArray();

        foreach (var move in folderMoves)
        {
            _explicitFolders.Remove(move.Source);
            _explicitFolders.Add(move.Target);
        }

        if (folderMoves.Length == 0)
            _explicitFolders.Add(destination);

        if (string.Equals(
                CurrentFolder,
                source,
                StringComparison.OrdinalIgnoreCase) ||
            CurrentFolder.StartsWith(
                source + "/",
                StringComparison.OrdinalIgnoreCase))
            CurrentFolder = destination + CurrentFolder[source.Length..];
    }

    private void MoveDocumentPath(string source, string destination)
    {
        var document = Documents.Get(source);
        if (document is null)
            return;

        var wasCurrent = string.Equals(
            CurrentDocument?.Id,
            document.Id,
            StringComparison.Ordinal);

        Documents.Remove(source, "explorer-move");
        var moved = Documents.Upsert(
            new DocumentInput
            {
                Id = document.Id,
                Path = destination,
                Title = document.Title,
                Content = document.Content,
                Properties = document.Properties,
                Tags = document.Tags,
                Links = document.Links,
                Created = document.Created,
                Modified = document.Modified,
                Revision = document.Revision
            },
            "explorer-move");

        if (wasCurrent)
            CurrentDocument = moved;
    }

    private UrbeDocument? ApplyCurrentContent(string content, string metadata)
    {
        if (CurrentDocument is null)
            return null;

        var current = CurrentDocument;
        CurrentDocument = Documents.Upsert(
            new DocumentInput
            {
                Id = current.Id,
                Path = current.Path,
                Title = current.Title,
                Content = content,
                Created = current.Created,
                Modified = current.Modified
            },
            metadata);

        _paths.Add(CurrentDocument.Path);
        Revision++;
        return CurrentDocument;
    }

    private void EnsureTab(UrbeDocument document)
    {
        if (_tabs.Any(
                tab => string.Equals(
                    tab.Id,
                    document.Id,
                    StringComparison.Ordinal)))
            return;

        _tabs.Add(new EditorTabState(document.Id));

        if (_tabs.Count <= MaxTabs)
            return;

        var removeIndex = _tabs.FindIndex(
            tab =>
                !tab.Pinned &&
                !string.Equals(
                    tab.Id,
                    CurrentDocument?.Id,
                    StringComparison.Ordinal));

        if (removeIndex >= 0)
            _tabs.RemoveAt(removeIndex);
    }

    private EditorHistoryState EnsureHistory(UrbeDocument document)
    {
        if (_history.TryGetValue(document.Id, out var history))
        {
            if (history.Past.Count == 0 &&
                history.Future.Count == 0 &&
                !string.Equals(
                    history.Present,
                    document.Content,
                    StringComparison.Ordinal))
                history.Present = document.Content;
            return history;
        }

        history = new EditorHistoryState(document.Content);
        _history[document.Id] = history;
        return history;
    }

    private void ResetEditorState()
    {
        _origins.Clear();
        _tabs.Clear();
        _history.Clear();
    }

    private sealed class EditorTabState(string id)
    {
        public string Id { get; } = id;
        public bool Pinned { get; set; }
    }

    private sealed class EditorHistoryState(string present)
    {
        public List<string> Past { get; } = [];
        public string Present { get; set; } = present;
        public List<string> Future { get; } = [];
    }

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
