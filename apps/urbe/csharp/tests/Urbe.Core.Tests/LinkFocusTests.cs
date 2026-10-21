using Urbe.Core;

namespace Urbe.Core.Tests;

/// <summary>UC-70: the links of a building — cita, citada por, mútua — and the streets between them.</summary>
public sealed class LinkFocusTests
{
    private static LegacyCity City()
    {
        var city = new LegacyCity(new FlatTerrain(), p => p + Guid.NewGuid().ToString("N")[..6]);
        city.OpenFirstTime([
            ("Notas/A.md", "# A\n[[B]] e [[C]]"),
            ("Notas/B.md", "# B\nvolta para [[A]]"),
            ("Notas/C.md", "# C\nsem links"),
            ("Notas/D.md", "# D\ncita [[A]]"),
            ("Notas/E.md", "# E\ncita [[D]]")
        ]);
        return city;
    }

    private static LegacyCityBuilding House(LegacyCity city, string name) => city.Buildings.Single(b => b.Name == name);

    [Fact]
    public void SplitsLinksIntoOutgoingIncomingAndMutual()
    {
        var city = City();
        var a = House(city, "A");
        var focus = LinkFocus.Of(city, a);
        Assert.Equal(["C"], focus.Outgoing.Select(b => b.Name));
        Assert.Equal(["D"], focus.Incoming.Select(b => b.Name));
        Assert.Equal(["B"], focus.Mutual.Select(b => b.Name));
        Assert.Equal(LinkGroup.Focus, focus.GroupOf(a));
        Assert.Equal(LinkGroup.Mutual, focus.GroupOf(House(city, "B")));
        Assert.Null(focus.GroupOf(House(city, "E")));   // E cites D, not A
        Assert.Equal(4, focus.Buildings.Count());

        // seen from D: A is cited by D, E cites D
        var d = LinkFocus.Of(city, House(city, "D"));
        Assert.Equal(["A"], d.Outgoing.Select(b => b.Name));
        Assert.Equal(["E"], d.Incoming.Select(b => b.Name));
        Assert.Empty(d.Mutual);

        // C only receives
        var c = LinkFocus.Of(city, House(city, "C"));
        Assert.Empty(c.Outgoing);
        Assert.Equal(["A"], c.Incoming.Select(b => b.Name));
    }

    [Fact]
    public void FollowsTheStreetsFromTheFocusToEachLinkedBuilding()
    {
        var city = City();
        var a = House(city, "A");
        var focus = LinkFocus.Of(city, a);
        Assert.NotEmpty(focus.Roads);
        Assert.All(focus.Roads.Keys, p => Assert.True(city.HasRoad(p.X, p.Y)));
        Assert.True(focus.Roads.ContainsKey(LinkFocus.RoadStart(a)));
        foreach (var b in new[] { "B", "C", "D" })
            Assert.True(focus.Roads.ContainsKey(LinkFocus.RoadStart(House(city, b))), b);
        // the door of D is on an incoming way, B's on a mutual one
        Assert.Equal(LinkGroup.Incoming, focus.Roads[LinkFocus.RoadStart(House(city, "D"))]);
        Assert.Equal(LinkGroup.Mutual, focus.Roads[LinkFocus.RoadStart(House(city, "B"))]);
        // only the ways of A: E's street to D is not part of it unless shared
        Assert.True(focus.Roads.Count <= city.Roads.Count);
    }

    [Fact]
    public void ABuildingWithoutLinksHasNoGroupsNorStreets()
    {
        var city = new LegacyCity(new FlatTerrain(), p => p + Guid.NewGuid().ToString("N")[..6]);
        city.OpenFirstTime([("Solta.md", "nada"), ("Outra.md", "nada")]);
        var focus = LinkFocus.Of(city, city.Buildings[0]);
        Assert.Empty(focus.Outgoing);
        Assert.Empty(focus.Incoming);
        Assert.Empty(focus.Mutual);
        Assert.Empty(focus.Roads);
        Assert.Single(focus.Buildings);
    }

    [Fact]
    public void IgnoresUnknownIdsAndSelfLinks()
    {
        var a = new LegacyCityBuilding("a", "a.md", "a", null, 0, 0, "house1");
        var b = new LegacyCityBuilding("b", "b.md", "b", null, 10, 0, "house1");
        var focus = LinkFocus.Of(a, [a, b], [("a", "a"), ("a", "ghost"), ("ghost", "a"), ("a", "b")], (_, _) => false);
        Assert.Equal([b], focus.Outgoing);
        Assert.Empty(focus.Incoming);
        Assert.Empty(focus.Roads);   // no streets at all
    }

    private sealed class FlatTerrain : ILegacyCityTerrain
    {
        public bool Buildable(int x, int y) => true;
        public bool Roadable(int x, int y) => true;
        public double RoadCost(int x, int y) => 1;
    }
}
