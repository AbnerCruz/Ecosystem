using Urbe.Core;

namespace Urbe.Core.Tests;

/// <summary>Owner (UC-52): children in the world, playing by their doors.</summary>
public sealed class TownKidsTests
{
    private sealed class Town(params LegacyCityBuilding[] buildings) : ILegacyTownMap
    {
        public IReadOnlyList<LegacyCityBuilding> Buildings { get; } = buildings;
        public IReadOnlyList<(string From, string To)> Links { get; } = [];
        public bool Road(int x, int y) => false;
        public bool Water(int x, int y) => y >= 18;
        public LegacyCityBuilding? BuildingAt(int x, int y) => Buildings.LastOrDefault(b => b.Contains(x, y));
    }

    private static readonly LegacyLifeView View = new(0, 0, 1, 800, 600, 0, 0, 40, 20);

    [Fact]
    public void SomeHousesHaveChildrenWhoPlayNearTheDoorAndNeverInTheWater()
    {
        var houses = Enumerable.Range(0, 12).Select(i => new LegacyCityBuilding("b" + i, "N" + i + ".md", "N" + i, null, 2 + (i % 6) * 6, 2 + (i / 6) * 8, "house1")).ToArray();
        var town = new Town(houses);
        var rng = new Random(7);
        var kids = new TownKids(rng.NextDouble);
        Assert.True(kids.Tick(.033, View, true, town));
        Assert.InRange(kids.People.Count, 1, TownKids.Max);
        Assert.All(kids.People, p => Assert.Equal(TownKids.Size, p.Scale));
        // the same houses always have children
        Assert.All(kids.Kids, k => Assert.True(TownKids.ChildrenOf(k.Home) > 0));
        for (int t = 0; t < 2000; t++) kids.Tick(.033, View, true, town);
        Assert.All(kids.Kids, k =>
        {
            Assert.InRange(k.Person.PosX, k.Door.X - TownKids.Reach - 1, k.Door.X + TownKids.Reach + 2);
            Assert.InRange(k.Person.PosY, k.Door.Y - 1, k.Door.Y + TownKids.Reach + 2);
            Assert.False(town.Water((int)Math.Floor(k.Person.PosX), (int)Math.Floor(k.Person.PosY)));
            Assert.Null(town.BuildingAt((int)Math.Floor(k.Person.PosX), (int)Math.Floor(k.Person.PosY)));
        });
        // turned off (residents off, or the rain sends them in): nobody out
        Assert.False(kids.Tick(.033, View, false, town));
        Assert.Empty(kids.People);
    }
}
