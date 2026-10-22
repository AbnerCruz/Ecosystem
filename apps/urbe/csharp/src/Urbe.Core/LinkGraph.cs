namespace Urbe.Core;

/// <summary>How a neighbour is linked to the note at the centre of <see cref="LinkGraph"/>.</summary>
public enum LinkRelation
{
    /// <summary>Each links to the other.</summary>
    Mutual,
    /// <summary>The centre links to it.</summary>
    Cites,
    /// <summary>It links to the centre (a backlink).</summary>
    CitedBy
}

/// <summary>A note linked to the centre: where it sits (X, Y in [-1, 1], the centre at 0, 0) and how linked it is.</summary>
public sealed record LinkNeighbour(string Id, string Path, string Title, string Folder, LinkRelation Relation, int Degree, double X, double Y);

/// <summary>The local graph of one note: every neighbour (for the list) and the ones placed on the drawing.</summary>
public sealed record LinkGraphView(string Id, string Path, string Title, string Folder, IReadOnlyList<LinkNeighbour> Neighbours, IReadOnlyList<LinkNeighbour> Placed)
{
    /// <summary>Neighbours left off the drawing (they stay in the list).</summary>
    public int Hidden => Neighbours.Count - Placed.Count;
}

/// <summary>
/// UC-71 (ADD-0029): the connections of one note — the notes it links to, the ones linking to it and the mutual ones.
/// Mutual neighbours sit on an inner ring, the others on an outer ring grouped by relation; the order inside a ring
/// comes from a stable hash of each note's id, so a neighbour keeps its place while the links stay the same, whatever
/// the order the notes were read. Only the <c>max</c> most connected are placed; all are listed.
/// </summary>
public static class LinkGraph
{
    public const double InnerRing = .42;
    public const double OuterRing = .82;

    public static LinkGraphView? Around(DocumentStore store, KnowledgeIndex index, string? idOrPath, int max = 24)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(index);
        if (store.Get(idOrPath) is not { } centre) return null;
        var cites = index.Links(centre.Id).Where(d => d.Id != centre.Id).ToDictionary(d => d.Id);
        var citedBy = index.Backlinks(centre.Id).Where(d => d.Id != centre.Id).ToDictionary(d => d.Id);
        int Degree(UrbeDocument d) => index.Links(d.Id).Select(x => x.Id).Union(index.Backlinks(d.Id).Select(x => x.Id)).Count();
        var all = cites.Values.Concat(citedBy.Values.Where(d => !cites.ContainsKey(d.Id)))
            .Select(d => (Doc: d, Relation: cites.ContainsKey(d.Id) && citedBy.ContainsKey(d.Id) ? LinkRelation.Mutual
                : cites.ContainsKey(d.Id) ? LinkRelation.Cites : LinkRelation.CitedBy, Degree: Degree(d)))
            .OrderBy(n => n.Relation).ThenBy(n => n.Doc.Title, StringComparer.CurrentCultureIgnoreCase).ThenBy(n => n.Doc.Path, StringComparer.Ordinal)
            .ToList();
        var placedIds = all.OrderByDescending(n => n.Degree).ThenBy(n => n.Relation).ThenBy(n => Hash(n.Doc.Id))
            .Take(Math.Max(0, max)).Select(n => n.Doc.Id).ToHashSet(StringComparer.Ordinal);

        var spots = new Dictionary<string, (double X, double Y)>(StringComparer.Ordinal);
        double offset = Hash(centre.Id) / (double)uint.MaxValue * Math.PI * 2;
        void Ring(IEnumerable<(UrbeDocument Doc, LinkRelation Relation, int Degree)> ring, double radius, double turn)
        {
            var list = ring.OrderBy(n => n.Relation).ThenBy(n => Hash(n.Doc.Id)).ThenBy(n => n.Doc.Id, StringComparer.Ordinal).ToList();
            for (int i = 0; i < list.Count; i++)
            {
                double a = offset + turn + i * Math.PI * 2 / list.Count;
                spots[list[i].Doc.Id] = (Math.Round(Math.Cos(a) * radius, 6), Math.Round(Math.Sin(a) * radius, 6));
            }
        }
        var placed = all.Where(n => placedIds.Contains(n.Doc.Id)).ToList();
        // the outer ring turned by the golden angle, so its points never line up behind the inner ones
        Ring(placed.Where(n => n.Relation == LinkRelation.Mutual), InnerRing, 0);
        Ring(placed.Where(n => n.Relation != LinkRelation.Mutual), OuterRing, Math.PI * (3 - Math.Sqrt(5)));

        LinkNeighbour Neighbour((UrbeDocument Doc, LinkRelation Relation, int Degree) n)
        {
            var (x, y) = spots.GetValueOrDefault(n.Doc.Id);
            return new LinkNeighbour(n.Doc.Id, n.Doc.Path, n.Doc.Title, NoteLibrary.FolderOf(n.Doc.Path), n.Relation, n.Degree, x, y);
        }
        var neighbours = all.Select(Neighbour).ToArray();
        return new LinkGraphView(centre.Id, centre.Path, centre.Title, NoteLibrary.FolderOf(centre.Path), neighbours,
            neighbours.Where(n => placedIds.Contains(n.Id)).ToArray());
    }

    /// <summary>FNV-1a of the id: the same on every run and device (string.GetHashCode is not).</summary>
    public static uint Hash(string text)
    {
        uint h = 2166136261;
        foreach (var c in text) { h ^= c; h *= 16777619; }
        return h;
    }
}
