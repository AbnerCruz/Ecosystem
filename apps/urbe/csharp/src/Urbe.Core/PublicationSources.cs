namespace Urbe.Core;

/// <summary>
/// A stable note identity, not a path or a pasted copy of its Markdown.
/// A fragment names a unique heading or an existing ^block marker; missing,
/// ambiguous and unsupported selectors never silently resolve to the whole note.
/// </summary>
public sealed record PublicationSourceReference(string DocumentId, string? Fragment = null);

/// <summary>
/// A disposable read projection of a note at one revision. Never persist this
/// snapshot in a publication manifest: reopen by DocumentId for current content.
/// </summary>
public sealed record PublicationSourceView(
    string DocumentId, string Path, int Revision, string Markdown, string? Fragment = null);

public enum PublicationSourceChangeKind
{
    Created,
    Updated,
    Removed,
    Reset
}

public sealed class PublicationSourceChange : EventArgs
{
    public PublicationSourceChange(
        string? documentId, PublicationSourceChangeKind kind, long storeRevision)
    {
        DocumentId = documentId;
        Kind = kind;
        StoreRevision = storeRevision;
    }

    /// <summary>Null for a full vault reset, which invalidates all projections.</summary>
    public string? DocumentId { get; }
    public PublicationSourceChangeKind Kind { get; }
    public long StoreRevision { get; }
}

/// <summary>
/// CST-151/153: the first native Publicações source adapter. A Publication
/// references the same DocumentStore as the editor and the agent tools: reads
/// resolve on demand and writes continue through the existing verified vault
/// edit contract. Nothing here saves, duplicates or synchronizes note text.
/// Existing Markdown ^id markers provide stable block identity; line offsets are rejected.
/// </summary>
public sealed class PublicationSourceCatalog : IDisposable
{
    private readonly DocumentStore _documents;
    private bool _disposed;

    public PublicationSourceCatalog(DocumentStore documents)
    {
        _documents = documents ?? throw new ArgumentNullException(nameof(documents));
        _documents.Changed += OnDocumentsChanged;
    }

    public event EventHandler<PublicationSourceChange>? SourceChanged;

    public PublicationSourceView? Read(PublicationSourceReference source)
    {
        ArgumentNullException.ThrowIfNull(source);
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (string.IsNullOrWhiteSpace(source.DocumentId))
            throw new ArgumentException("A fonte exige o ID persistente de uma nota.", nameof(source));
        var document = _documents.Get(source.DocumentId);
        // DocumentStore.Get also accepts paths. Never allow path-based identity
        // through the PublicationSourceReference contract.
        if (document is null || document.Id != source.DocumentId || !ArtifactModel.IsNote(document.Path))
            return null;

        var text = document.Content;
        if (!string.IsNullOrEmpty(source.Fragment))
        {
            var fragment = source.Fragment.Trim().TrimStart('#');
            if (fragment.StartsWith('^'))
            {
                if (EmbedFragments.BlockIds(text).Count(id => id == fragment[1..]) != 1) return null;
            }
            else
            {
                // Line offsets are not durable source selectors. Named headings are explicit:
                // removing/renaming them breaks the link instead of resolving the whole note.
                if (System.Text.RegularExpressions.Regex.IsMatch(fragment, @"^L\d", System.Text.RegularExpressions.RegexOptions.CultureInvariant))
                    throw new NotSupportedException("Referencie um título ou marcador ^id; números de linha não são identidade persistente.");
                if (EmbedFragments.Headings(text).Count(h => Assistant.AssistantTools.Fold(h) == Assistant.AssistantTools.Fold(fragment)) != 1) return null;
            }
            var part = EmbedFragments.Extract(text, fragment);
            if (part.Error is not null || part.Text is null) return null;
            text = part.Text;
        }
        return new PublicationSourceView(document.Id, document.Path, document.Revision, text, source.Fragment);
    }

    private void OnDocumentsChanged(object? sender, DocumentStoreChangedEventArgs args)
    {
        if (_disposed) return;
        if (args.Kind == DocumentStoreChangeKind.Reset)
        {
            SourceChanged?.Invoke(this, new PublicationSourceChange(null, PublicationSourceChangeKind.Reset, args.StoreRevision));
            return;
        }

        var document = args.Document;
        if (document is null || !ArtifactModel.IsNote(document.Path)) return;
        var kind = args.Kind switch
        {
            DocumentStoreChangeKind.Created => PublicationSourceChangeKind.Created,
            DocumentStoreChangeKind.Updated => PublicationSourceChangeKind.Updated,
            DocumentStoreChangeKind.Removed => PublicationSourceChangeKind.Removed,
            _ => throw new InvalidOperationException("Estado de fonte desconhecido.")
        };
        SourceChanged?.Invoke(this, new PublicationSourceChange(document.Id, kind, args.StoreRevision));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _documents.Changed -= OnDocumentsChanged;
        SourceChanged = null;
    }
}
