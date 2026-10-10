namespace Urbe.Core;

public sealed record EditorReference(
    string Id,
    string DocumentId,
    string Title,
    string Path,
    string Markdown,
    bool IsExcerpt);

/// <summary>
/// Read-only reference snapshots, owned by the current workspace session.
/// These are not documents, never participate in history, and are not persisted.
/// </summary>
public sealed class EditorReferenceSession
{
    private readonly List<EditorReference> _entries = [];

    public IReadOnlyList<EditorReference> Entries => Array.AsReadOnly(_entries.ToArray());

    public EditorReference PinDocument(UrbeDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return Pin(document, document.Content, false);
    }

    public EditorReference? PinBlock(UrbeDocument document, int index)
    {
        ArgumentNullException.ThrowIfNull(document);
        var blocks = VisualDocumentEditor.Parse(document.Content).Blocks;
        if (index < 0 || index >= blocks.Count)
            return null;

        // The projection is only for reading; it never writes back to the source.
        var markdown = VisualDocumentEditor.ToMarkdown(string.Empty, [blocks[index]]);
        return Pin(document, markdown, true);
    }

    public bool Remove(string id) => _entries.RemoveAll(entry => entry.Id == id) > 0;

    public void Clear() => _entries.Clear();

    private EditorReference Pin(UrbeDocument document, string markdown, bool excerpt)
    {
        var existing = _entries.Find(entry =>
            entry.DocumentId == document.Id &&
            entry.Markdown == markdown &&
            entry.IsExcerpt == excerpt);
        if (existing is not null)
            return existing;

        var reference = new EditorReference(
            Guid.NewGuid().ToString("N"), document.Id, document.Title,
            document.Path, markdown, excerpt);
        _entries.Add(reference);
        return reference;
    }
}
