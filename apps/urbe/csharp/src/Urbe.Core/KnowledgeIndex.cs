using System.Collections.ObjectModel;
using System.Text.RegularExpressions;

namespace Urbe.Core;

public sealed record KnowledgeIndexStats(
    int Documents,
    int Links,
    int Tags,
    int Tokens);

public sealed record KnowledgeIndexedEventArgs(
    int Documents,
    int Links);

public sealed partial class KnowledgeIndex : IDisposable
{
    private readonly DocumentStore _store;
    private readonly Dictionary<string, List<string>> _byTitle =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<string>> _byTag =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, HashSet<string>> _outgoing =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<string>> _incoming =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<string>> _tokens =
        new(StringComparer.Ordinal);

    public KnowledgeIndex(DocumentStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        _store = store;
        _store.Changed += OnDocumentStoreChanged;
        Rebuild();
    }

    public event EventHandler<KnowledgeIndexedEventArgs>? Indexed;

    public void Dispose()
    {
        _store.Changed -= OnDocumentStoreChanged;
        GC.SuppressFinalize(this);
    }

    public void Rebuild()
    {
        _byTitle.Clear();
        _byTag.Clear();
        _outgoing.Clear();
        _incoming.Clear();
        _tokens.Clear();

        var all = _store.List();
        var aliases = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var document in all.Where(document => ArtifactModel.Linkable(document.Path)))
        {
            Add(_byTitle, document.Title, document.Id);
            aliases[Normalize(document.Title)] = document.Id;
            aliases[Normalize(ArtifactModel.WithoutNoteExtension(document.Path))] = document.Id;
            aliases[Normalize(document.Path)] = document.Id;
        }

        foreach (var document in all)
        {
            foreach (var tag in document.Tags)
                Add(_byTag, tag, document.Id);

            var outgoing = new HashSet<string>(StringComparer.Ordinal);
            foreach (var raw in document.Links)
            {
                if (!aliases.TryGetValue(Normalize(raw), out var target))
                    continue;

                outgoing.Add(target);
                Add(_incoming, target, document.Id);
            }

            _outgoing[document.Id] = outgoing;

            var text = (document.Title + " " + document.Path + " " + document.Content)
                .ToLowerInvariant();
            var seenWords = new HashSet<string>(StringComparer.Ordinal);
            foreach (Match match in TokenRegex().Matches(text))
            {
                var word = match.Value;
                if (seenWords.Add(word))
                    Add(_tokens, word, document.Id);
            }
        }

        Indexed?.Invoke(
            this,
            new KnowledgeIndexedEventArgs(
                all.Count,
                _outgoing.Values.Sum(set => set.Count)));
    }

    public IReadOnlyList<UrbeDocument> Backlinks(string? idOrPath)
    {
        var document = _store.Get(idOrPath);
        if (document is null)
            return Array.Empty<UrbeDocument>();

        return Resolve(_incoming.GetValueOrDefault(Normalize(document.Id)));
    }

    public IReadOnlyList<UrbeDocument> Links(string? idOrPath)
    {
        var document = _store.Get(idOrPath);
        if (document is null)
            return Array.Empty<UrbeDocument>();

        if (!_outgoing.TryGetValue(document.Id, out var ids))
            return Array.Empty<UrbeDocument>();

        return Array.AsReadOnly(
            ids.Select(id => _store.Get(id))
                .Where(document => document is not null)
                .Cast<UrbeDocument>()
                .ToArray());
    }

    public IReadOnlyList<UrbeDocument> Tagged(string? tag) =>
        Resolve(_byTag.GetValueOrDefault(Normalize(tag)));

    public IReadOnlyList<UrbeDocument> Search(string? query, int? limit = null)
    {
        var q = Normalize(query);
        var max = limit.GetValueOrDefault(50);
        if (max == 0)
            max = 50;

        var all = _store.List();
        if (q.Length == 0)
            return SliceLikeJavaScript(all, max);

        var terms = TokenRegex()
            .Matches(q)
            .Select(match => match.Value)
            .ToArray();

        var scores = new Dictionary<string, int>(StringComparer.Ordinal);
        var order = all
            .Select((document, index) => (document.Id, index))
            .ToDictionary(pair => pair.Id, pair => pair.index, StringComparer.Ordinal);

        foreach (var document in all)
        {
            var haystack = (document.Title + " " + document.Path).ToLowerInvariant();
            if (haystack.Contains(q, StringComparison.Ordinal))
                scores[document.Id] = scores.GetValueOrDefault(document.Id) + 10;
        }

        foreach (var term in terms)
        {
            if (!_tokens.TryGetValue(term, out var ids))
                continue;

            foreach (var id in ids)
                scores[id] = scores.GetValueOrDefault(id) + 1;
        }

        var ranked = scores
            .OrderByDescending(pair => pair.Value)
            .ThenBy(pair => order.GetValueOrDefault(pair.Key, int.MaxValue))
            .Select(pair => _store.Get(pair.Key))
            .Where(document => document is not null)
            .Cast<UrbeDocument>()
            .ToArray();

        return SliceLikeJavaScript(ranked, max);
    }

    public KnowledgeIndexStats Stats() =>
        new(
            _store.List().Count,
            _outgoing.Values.Sum(set => set.Count),
            _byTag.Count,
            _tokens.Count);

    public IReadOnlyDictionary<string, IReadOnlyList<string>> Titles =>
        Snapshot(_byTitle);

    public IReadOnlyDictionary<string, IReadOnlyList<string>> Tags =>
        Snapshot(_byTag);

    private void OnDocumentStoreChanged(
        object? sender,
        DocumentStoreChangedEventArgs args) =>
        Rebuild();

    private IReadOnlyList<UrbeDocument> Resolve(IEnumerable<string>? ids)
    {
        if (ids is null)
            return Array.Empty<UrbeDocument>();

        return Array.AsReadOnly(
            ids.Select(id => _store.Get(id))
                .Where(document => document is not null)
                .Cast<UrbeDocument>()
                .ToArray());
    }

    private static string Normalize(string? value) =>
        (value ?? string.Empty).Trim().ToLowerInvariant();

    private static void Add(
        IDictionary<string, List<string>> map,
        string? key,
        string value)
    {
        var normalized = Normalize(key);
        if (normalized.Length == 0)
            return;

        if (!map.TryGetValue(normalized, out var values))
        {
            values = [];
            map[normalized] = values;
        }

        if (!values.Contains(value, StringComparer.Ordinal))
            values.Add(value);
    }

    private static IReadOnlyList<UrbeDocument> SliceLikeJavaScript(
        IReadOnlyList<UrbeDocument> source,
        int end)
    {
        var count = end >= 0
            ? Math.Min(end, source.Count)
            : Math.Max(0, source.Count + end);

        return Array.AsReadOnly(source.Take(count).ToArray());
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> Snapshot(
        IReadOnlyDictionary<string, List<string>> source)
    {
        var copy = source.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<string>)Array.AsReadOnly(pair.Value.ToArray()),
            StringComparer.Ordinal);
        return new ReadOnlyDictionary<string, IReadOnlyList<string>>(copy);
    }

    [GeneratedRegex(
        @"[\p{L}\p{N}_-]{2,}",
        RegexOptions.CultureInvariant)]
    private static partial Regex TokenRegex();
}
