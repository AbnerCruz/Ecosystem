using Urbe.Core;

namespace Urbe.Core.Tests;

/// <summary>
/// Where the villagers deliberately leave the 1.8.4 (owner, DEC-0045): they no longer vanish all at once when the
/// roads are rebuilt, nor while they are still on screen. The oracle tests keep proving everything else.
/// </summary>
public sealed class LegacyVillagersTests
{
    // Two linked houses with doors at (1,3) and (11,3) and a street along y = 4.
    private static Town TwoHouses() => new(
        [new LegacyCityBuilding("a", "A.md", "A", null, 0, 0, "house1"), new LegacyCityBuilding("b", "B.md", "B", null, 10, 0, "house1")],
        [("a", "b")],
        Enumerable.Range(1, 11).Select(x => (x, 4)));

    private static readonly LegacyLifeView Whole = new(0, 0, 1, 800, 600, -5, -5, 20, 15);

    [Fact]
    public void RoadsChanged_KeepsWhoeverStillHasTheirStreet()
    {
        var town = TwoHouses();
        var villagers = new LegacyVillagers(LegacyJsMath.Rng(1));
        Assert.True(villagers.Tick(3000, Whole, true, 1, town));
        var before = villagers.People.ToList();
        Assert.Contains(before, a => a.Kind == "andarilho");

        villagers.RoadsChanged(town);

        Assert.Equal(before, villagers.People);
    }

    [Fact]
    public void RoadsChanged_DropsOnlyWhoseStreetIsGone()
    {
        var town = TwoHouses();
        var villagers = new LegacyVillagers(LegacyJsMath.Rng(1));
        villagers.Tick(3000, Whole, true, 1, town);
        Assert.Contains(villagers.People, a => a.Kind == "andarilho");

        town.Roads.Remove((6, 4));
        villagers.RoadsChanged(town);

        Assert.DoesNotContain(villagers.People, a => a.Kind == "andarilho");
    }

    [Fact]
    public void Repopulate_KeepsAWalkerInSightAfterTheirHouseLeavesTheView()
    {
        var town = TwoHouses();
        var villagers = new LegacyVillagers(LegacyJsMath.Rng(1));
        villagers.Tick(3000, Whole, true, 1, town);
        var walker = villagers.People.First(a => a.Kind == "andarilho");
        walker.S = walker.Route!.Count / 2.0;
        walker.PauseTime = 0;
        villagers.Tick(3033, Whole, true, 1, town);   // walks a step mid-street, around x = 6

        // Only the middle of the street is in view: neither house is, so the 1.8.4 dropped the walker here.
        var middle = new LegacyLifeView(0, 0, 1, 800, 600, 4, 2, 8, 6);
        villagers.Tick(6000, middle, true, 1, town);

        Assert.Contains(walker, villagers.People);
    }

    private sealed class Town(IReadOnlyList<LegacyCityBuilding> buildings, IReadOnlyList<(string, string)> links, IEnumerable<(int, int)> roads)
        : ILegacyTownMap
    {
        public HashSet<(int, int)> Roads { get; } = [.. roads];
        public IReadOnlyList<LegacyCityBuilding> Buildings { get; } = buildings;
        public IReadOnlyList<(string From, string To)> Links { get; } = links;
        public bool Road(int x, int y) => Roads.Contains((x, y));
        public bool Water(int x, int y) => false;
        public LegacyCityBuilding? BuildingAt(int x, int y) => Buildings.LastOrDefault(b => b.Contains(x, y));
    }
}
