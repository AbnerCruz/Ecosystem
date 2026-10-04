using System.Collections.ObjectModel;
using System.Text;
using System.Text.RegularExpressions;

namespace Urbe.Core;

public sealed class DocumentInput
{
    public string? Id { get; init; }
    public required string Path { get; init; }
    public string? Title { get; init; }
    public string? Content { get; init; }
    public IReadOnlyDictionary<string, string>? Properties { get; init; }
    public IReadOnlyList<string>? Tags { get; init; }
    public IReadOnlyList<string>? Links { get; init; }
    public string? Created { get; init; }
    public string? Modified { get; init; }
    public int? Revision { get; init; }
}

public sealed record UrbeDocument(
    string Id,
    string Path,
    string Title,
    string Content,
    IReadOnlyDictionary<string, string> Properties,
    IReadOnlyList<string> Tags,
    IReadOnlyList<string> Links,
    string? Created,
    string? Modified,
    int Revision);

public enum DocumentStoreChangeKind
{
    Created,
    Updated,
    Removed,
    Reset
}

public sealed class DocumentStoreChangedEventArgs : EventArgs
{
    internal DocumentStoreChangedEventArgs(
        DocumentStoreChangeKind kind,
        UrbeDocument? document,
        UrbeDocument? previous,
        IReadOnlyList<UrbeDocument>? documents,
        IReadOnlyList<UrbeDocument>? previousDocuments,
        object? metadata,
        long storeRevision)
    {
        Kind = kind;
        Document = document;
        Previous = previous;
        Documents = documents;
        PreviousDocuments = previousDocuments;
        Metadata = metadata;
        StoreRevision = storeRevision;
    }

    public DocumentStoreChangeKind Kind { get; }
    public UrbeDocument? Document { get; }
    public UrbeDocument? Previous { get; }
    public IReadOnlyList<UrbeDocument>? Documents { get; }
    public IReadOnlyList<UrbeDocument>? PreviousDocuments { get; }
    public object? Metadata { get; }
    public long StoreRevision { get; }
}

public static partial class DocumentModel
{
    public static string NormalizePath(string? path)
    {
        var normalized = (path ?? string.Empty).Replace('\\', '/');
        normalized = normalized.Trim('/');
        return SlashRunsRegex().Replace(normalized, "/");
    }

    public static string CreateDocumentId() => "doc_" + Guid.NewGuid();

    public static IReadOnlyDictionary<string, string> ParseFrontmatter(string? content)
    {
        var text = content ?? string.Empty;
        var output = new Dictionary<string, string>(StringComparer.Ordinal);

        if (!text.StartsWith("---\n", StringComparison.Ordinal))
            return new ReadOnlyDictionary<string, string>(output);

        var end = text.IndexOf("\n---", 4, StringComparison.Ordinal);
        if (end < 0)
            return new ReadOnlyDictionary<string, string>(output);

        var body = text[4..end];
        foreach (var line in body.Split('\n'))
        {
            var colon = line.IndexOf(':');
            if (colon <= 0)
                continue;

            var key = line[..colon].Trim();
            var value = line[(colon + 1)..].Trim();
            if (key.Length > 0)
                output[key] = value;
        }

        return new ReadOnlyDictionary<string, string>(output);
    }

    public static IReadOnlyList<string> ParseLinks(string? content)
    {
        var output = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var text = WithoutCode(content);

        foreach (Match match in WikiLinkRegex().Matches(text))
        {
            var raw = match.Groups[1].Value;
            var target = raw.Split('|')[0].Split('#')[0].Trim();
            if (target.Length == 0)
                continue;

            var key = target.ToLowerInvariant();
            if (!seen.Add(key))
                continue;

            output.Add(target);
        }

        return output.AsReadOnly();
    }

    public static IReadOnlyList<string> ParseTags(
        string? content,
        IReadOnlyDictionary<string, string>? properties = null)
    {
        var output = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var text = WithoutCode(content);

        void Add(string value)
        {
            if (seen.Add(value))
                output.Add(value);
        }

        foreach (Match match in TagRegex().Matches(text))
            Add(match.Groups[2].Value);

        properties ??= ParseFrontmatter(content);
        string raw = string.Empty;
        if (properties.TryGetValue("tags", out var tags) && tags.Length > 0)
            raw = tags;
        else if (properties.TryGetValue("tag", out var tag) && tag.Length > 0)
            raw = tag;

        raw = BracketEdgesRegex().Replace(raw, string.Empty);
        foreach (var item in raw.Split(','))
        {
            var tag = item.Trim();
            if (tag.StartsWith('#'))
                tag = tag[1..];
            if (tag.Length > 0)
                Add(tag);
        }

        return output.AsReadOnly();
    }

    private static string WithoutCode(string? content)
    {
        var text = content ?? string.Empty;
        text = FencedCodeRegex().Replace(text, " ");
        return InlineCodeRegex().Replace(text, " ");
    }

    [GeneratedRegex(@"/+", RegexOptions.CultureInvariant)]
    private static partial Regex SlashRunsRegex();

    [GeneratedRegex(
        @"^(?<fence>```|~~~)[^\n]*\n[\s\S]*?^\k<fence>[ \t]*$",
        RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex FencedCodeRegex();

    [GeneratedRegex(@"`[^`\n]*`", RegexOptions.CultureInvariant)]
    private static partial Regex InlineCodeRegex();

    [GeneratedRegex(@"\[\[([^\]\n]+)\]\]", RegexOptions.CultureInvariant)]
    private static partial Regex WikiLinkRegex();

    [GeneratedRegex(
        @"(^|\s)#([\p{L}\p{N}_/-]+)",
        RegexOptions.CultureInvariant)]
    private static partial Regex TagRegex();

    [GeneratedRegex(@"^\[|\]$", RegexOptions.CultureInvariant)]
    private static partial Regex BracketEdgesRegex();
}

public sealed class DocumentStore
{
    private readonly Dictionary<string, UrbeDocument> _documents =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _pathIndex =
        new(StringComparer.OrdinalIgnoreCase);

    public event EventHandler<DocumentStoreChangedEventArgs>? Changed;

    public long Revision { get; private set; }

    public UrbeDocument Make(DocumentInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var path = DocumentModel.NormalizePath(input.Path);
        var content = input.Content ?? string.Empty;
        var properties = input.Properties ?? DocumentModel.ParseFrontmatter(content);
        var propertyCopy = new ReadOnlyDictionary<string, string>(
            properties.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal));
        var tags = input.Tags ?? DocumentModel.ParseTags(content, propertyCopy);
        var links = input.Links ?? DocumentModel.ParseLinks(content);

        return new UrbeDocument(
            string.IsNullOrEmpty(input.Id)
                ? DocumentModel.CreateDocumentId()
                : input.Id,
            path,
            string.IsNullOrEmpty(input.Title)
                ? ArtifactModel.TitleFromPath(path)
                : input.Title,
            content,
            propertyCopy,
            Array.AsReadOnly(tags.ToArray()),
            Array.AsReadOnly(links.ToArray()),
            string.IsNullOrEmpty(input.Created) ? null : input.Created,
            string.IsNullOrEmpty(input.Modified) ? null : input.Modified,
            input.Revision ?? 0);
    }

    public UrbeDocument Upsert(DocumentInput input, object? metadata = null)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (string.IsNullOrEmpty(input.Path))
            throw new ArgumentException("document path is required", nameof(input));

        var path = DocumentModel.NormalizePath(input.Path);
        _pathIndex.TryGetValue(path, out var existingId);
        var id = !string.IsNullOrEmpty(input.Id)
            ? input.Id
            : existingId ?? DocumentModel.CreateDocumentId();
        _documents.TryGetValue(id, out var previous);

        IReadOnlyDictionary<string, string>? properties = input.Properties;
        IReadOnlyList<string>? tags = input.Tags;
        IReadOnlyList<string>? links = input.Links;
        var inputContent = input.Content ?? string.Empty;

        if (previous is not null &&
            !string.Equals(inputContent, previous.Content, StringComparison.Ordinal))
        {
            properties = null;
            tags = null;
            links = null;
        }

        var next = Make(
            new DocumentInput
            {
                Id = id,
                Path = path,
                Title = input.Title,
                Content = input.Content,
                Properties = properties,
                Tags = tags,
                Links = links,
                Created = input.Created,
                Modified = input.Modified,
                Revision = (previous?.Revision ?? 0) + 1
            });

        if (previous is not null &&
            !string.Equals(previous.Path, path, StringComparison.OrdinalIgnoreCase))
            _pathIndex.Remove(previous.Path);

        _documents[id] = next;
        _pathIndex[path] = id;
        Revision++;

        Changed?.Invoke(
            this,
            new DocumentStoreChangedEventArgs(
                previous is null
                    ? DocumentStoreChangeKind.Created
                    : DocumentStoreChangeKind.Updated,
                next,
                previous,
                null,
                null,
                metadata,
                Revision));

        return next;
    }

    public bool Remove(string? idOrPath, object? metadata = null)
    {
        var document = Get(idOrPath);
        if (document is null)
            return false;

        _documents.Remove(document.Id);
        _pathIndex.Remove(document.Path);
        Revision++;

        Changed?.Invoke(
            this,
            new DocumentStoreChangedEventArgs(
                DocumentStoreChangeKind.Removed,
                document,
                null,
                null,
                null,
                metadata,
                Revision));

        return true;
    }

    public UrbeDocument? Get(string? idOrPath)
    {
        var key = idOrPath ?? string.Empty;
        if (_documents.TryGetValue(key, out var byId))
            return byId;

        var path = DocumentModel.NormalizePath(key);
        return _pathIndex.TryGetValue(path, out var id) &&
               _documents.TryGetValue(id, out var byPath)
            ? byPath
            : null;
    }

    public IReadOnlyList<UrbeDocument> List() =>
        Array.AsReadOnly(_documents.Values.ToArray());

    public void Clear(object? metadata = null)
    {
        var previous = List();
        _documents.Clear();
        _pathIndex.Clear();
        Revision++;

        Changed?.Invoke(
            this,
            new DocumentStoreChangedEventArgs(
                DocumentStoreChangeKind.Reset,
                null,
                null,
                null,
                previous,
                metadata,
                Revision));
    }

    public IReadOnlyList<UrbeDocument> ReplaceAll(
        IEnumerable<DocumentInput>? items,
        object? metadata = null)
    {
        _documents.Clear();
        _pathIndex.Clear();

        foreach (var input in items ?? Array.Empty<DocumentInput>())
        {
            var document = Make(
                new DocumentInput
                {
                    Id = input.Id,
                    Path = input.Path,
                    Title = input.Title,
                    Content = input.Content,
                    Properties = input.Properties,
                    Tags = input.Tags,
                    Links = input.Links,
                    Created = input.Created,
                    Modified = input.Modified,
                    Revision = 1
                });

            _documents[document.Id] = document;
            _pathIndex[document.Path] = document.Id;
        }

        Revision++;
        var documents = List();

        Changed?.Invoke(
            this,
            new DocumentStoreChangedEventArgs(
                DocumentStoreChangeKind.Reset,
                null,
                null,
                documents,
                null,
                metadata,
                Revision));

        return documents;
    }

    public IReadOnlyList<UrbeDocument> ReplaceFromVault(
        VaultSnapshot snapshot,
        object? metadata = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return ReplaceAll(
            snapshot.Documents.Select(
                document =>
                    new DocumentInput
                    {
                        Id = document.Id,
                        Path = document.Path,
                        Content = document.Text ??
                                  Encoding.UTF8.GetString(document.Bytes.Span)
                    }),
            metadata);
    }
}
