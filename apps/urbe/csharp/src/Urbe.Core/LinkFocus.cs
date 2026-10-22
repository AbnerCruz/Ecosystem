namespace Urbe.Core;

/// <summary>How a building relates to the focused one (UC-70).</summary>
public enum LinkGroup
{
    /// <summary>The focused building itself.</summary>
    Focus,
    /// <summary>The focused note links to it, not back ("cita").</summary>
    Outgoing,
    /// <summary>It links to the focused note, not back ("citada por").</summary>
    Incoming,
    /// <summary>Both link to each other ("mútua").</summary>
    Mutual
}

/// <summary>
/// The links of one building (UC-70): the notes it references, the ones that reference it and the mutual ones (the
/// three groups do not overlap), plus the street tiles joining it to each of them. Built from the [[links]] of the last
/// street rebuild (<see cref="LegacyCity.DirectedLinks"/>); nothing of the city changes.
/// </summary>
public sealed class LinkFocus
{
    private readonly Dictionary<LegacyCityBuilding, LinkGroup> _groups;
    private readonly Dictionary<(int X, int Y), LinkGroup> _roads;

    private LinkFocus(LegacyCityBuilding focus, List<LegacyCityBuilding> outgoing, List<LegacyCityBuilding> incoming,
        List<LegacyCityBuilding> mutual, Dictionary<(int X, int Y), LinkGroup> roads)
    {
        Focus = focus;
        Outgoing = outgoing;
        Incoming = incoming;
        Mutual = mutual;
        _roads = roads;
        _groups = new Dictionary<LegacyCityBuilding, LinkGroup> { [focus] = LinkGroup.Focus };
        foreach (var b in outgoing) _groups[b] = LinkGroup.Outgoing;
        foreach (var b in incoming) _groups[b] = LinkGroup.Incoming;
        foreach (var b in mutual) _groups[b] = LinkGroup.Mutual;
    }

    public LegacyCityBuilding Focus { get; }
    public IReadOnlyList<LegacyCityBuilding> Outgoing { get; }
    public IReadOnlyList<LegacyCityBuilding> Incoming { get; }
    public IReadOnlyList<LegacyCityBuilding> Mutual { get; }

    /// <summary>Street tiles on the way from the focus to its linked buildings, with the group of the way.</summary>
    public IReadOnlyDictionary<(int X, int Y), LinkGroup> Roads => _roads;

    /// <summary>The focus and every linked building.</summary>
    public IEnumerable<LegacyCityBuilding> Buildings => _groups.Keys;

    /// <summary>The group of a building, or null when it is not linked to the focus.</summary>
    public LinkGroup? GroupOf(LegacyCityBuilding b) => _groups.TryGetValue(b, out var g) ? g : null;

    /// <summary>The links of <paramref name="focus"/> in <paramref name="city"/>.</summary>
    public static LinkFocus Of(LegacyCity city, LegacyCityBuilding focus)
    {
        ArgumentNullException.ThrowIfNull(city);
        return Of(focus, city.Buildings, city.DirectedLinks, city.HasRoad);
    }

    /// <summary>The links of <paramref name="focus"/> among <paramref name="buildings"/>, given directed links by id.</summary>
    public static LinkFocus Of(LegacyCityBuilding focus, IReadOnlyList<LegacyCityBuilding> buildings,
        IEnumerable<(string From, string To)> directed, Func<int, int, bool> hasRoad)
    {
        ArgumentNullException.ThrowIfNull(focus);
        ArgumentNullException.ThrowIfNull(buildings);
        ArgumentNullException.ThrowIfNull(directed);
        ArgumentNullException.ThrowIfNull(hasRoad);
        var byId = new Dictionary<string, LegacyCityBuilding>(StringComparer.Ordinal);
        foreach (var b in buildings) byId.TryAdd(b.Id, b);
        var outs = new HashSet<string>(StringComparer.Ordinal);
        var ins = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (from, to) in directed)
        {
            if (from == to) continue;
            if (from == focus.Id) outs.Add(to);
            else if (to == focus.Id) ins.Add(from);
        }
        // city order, so the result does not depend on the order of the links
        List<LegacyCityBuilding> outgoing = [], incoming = [], mutual = [];
        foreach (var b in buildings)
        {
            if (b == focus || !byId.TryGetValue(b.Id, out var first) || first != b) continue;
            bool o = outs.Contains(b.Id), i = ins.Contains(b.Id);
            if (o && i) mutual.Add(b);
            else if (o) outgoing.Add(b);
            else if (i) incoming.Add(b);
        }
        var roads = new Dictionary<(int X, int Y), LinkGroup>();
        var start = RoadStart(focus);
        if (hasRoad(start.X, start.Y) && outgoing.Count + incoming.Count + mutual.Count > 0)
        {
            var came = Explore(start, hasRoad);
            // incoming first, mutual last: a shared tile takes the strongest group
            foreach (var (list, g) in new[] { (incoming, LinkGroup.Incoming), (outgoing, LinkGroup.Outgoing), (mutual, LinkGroup.Mutual) })
            foreach (var b in list)
            {
                var goal = RoadStart(b);
                if (!came.ContainsKey(goal)) continue;
                for (var p = goal; ; p = came[p])
                {
                    roads[p] = g;
                    if (p == start) break;
                }
            }
        }
        return new LinkFocus(focus, outgoing, incoming, mutual, roads);
    }

    /// <summary>The street tile under a building's door (LegacyCity.LowerRoadStart).</summary>
    public static (int X, int Y) RoadStart(LegacyCityBuilding b) =>
        ((int)LegacyJsMath.Round(b.X + (b.W - 1) / 2.0), b.Y + b.H + 1);

    // breadth-first over the street tiles: the shortest way from start to every reachable tile
    private static Dictionary<(int X, int Y), (int X, int Y)> Explore((int X, int Y) start, Func<int, int, bool> hasRoad)
    {
        var came = new Dictionary<(int X, int Y), (int X, int Y)> { [start] = start };
        var queue = new Queue<(int X, int Y)>();
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            var c = queue.Dequeue();
            foreach (var n in new[] { (c.X + 1, c.Y), (c.X - 1, c.Y), (c.X, c.Y + 1), (c.X, c.Y - 1) })
            {
                if (came.ContainsKey(n) || !hasRoad(n.Item1, n.Item2)) continue;
                came[n] = c;
                queue.Enqueue(n);
            }
        }
        return came;
    }
}
