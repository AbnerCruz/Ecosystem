using Urbe.Core;

namespace Urbe.Core.Tests;

/// <summary>DEC-0045 (UC-42): someone from the nearest house goes to the well, works and comes back.</summary>
public sealed class TownTradesTests
{
    private sealed class Town(params LegacyCityBuilding[] buildings) : ILegacyTownMap
    {
        public IReadOnlyList<LegacyCityBuilding> Buildings { get; } = buildings;
        public IReadOnlyList<(string From, string To)> Links { get; } = [];
        public bool Road(int x, int y) => false;
        public bool Water(int x, int y) => x == 9 && y is >= 3 and <= 12;   // a stream between the house and the well
        public LegacyCityBuilding? BuildingAt(int x, int y) => Buildings.LastOrDefault(b => b.Contains(x, y));
    }

    private static readonly LegacyLifeView View = new(0, 0, 1, 800, 600, 0, 0, 30, 20);

    private static double Seq() => .5;

    [Fact]
    public void TheNearestNeighbourFetchesWaterAndGoesBackHome()
    {
        var home = new LegacyCityBuilding("b1", "Casa.md", "Casa", null, 4, 4, "house1");
        var far = new LegacyCityBuilding("b2", "Longe.md", "Longe", null, 28, 2, "house1");
        var town = new Town(home, far);
        var well = new WorldDecoration("d1", "poco", 12, 8);
        var trades = new TownTrades(Seq);
        var phases = new List<TownTrades.Phase>();
        var visited = new List<(double X, double Y)>();
        for (int i = 0; i < 2000; i++)
        {
            trades.Tick(.033, View, true, 1, [well], town);
            var w = Assert.Single(trades.Workers);
            if (phases.Count == 0 || phases[^1] != w.Phase) phases.Add(w.Phase);
            if (w.Phase != TownTrades.Phase.Home) visited.Add((w.Person.PosX, w.Person.PosY));
        }
        var worker = Assert.Single(trades.Workers);
        Assert.Same(home, worker.Person.From);
        Assert.Equal("bucket", worker.Person.Look.Acc);
        Assert.Equal("oficio", worker.Person.Kind);
        Assert.Equal([TownTrades.Phase.Home, TownTrades.Phase.Going, TownTrades.Phase.Working, TownTrades.Phase.Returning, TownTrades.Phase.Home], phases.Take(5));
        // never inside a house or in the water
        Assert.All(visited, p => Assert.False(town.Water((int)Math.Floor(p.X), (int)Math.Floor(p.Y)) || town.BuildingAt((int)Math.Floor(p.X), (int)Math.Floor(p.Y)) is not null));
        // at work, beside the well (the tile below it)
        Assert.Equal((12.5, 9.62), worker.Path[^1]);
    }

    [Fact]
    public void NobodyWhenNoHouseIsNearOrTheWellIsOutOfSightOrLifeIsOff()
    {
        var far = new LegacyCityBuilding("b2", "Longe.md", "Longe", null, 60, 60, "house1");
        var trades = new TownTrades(Seq);
        trades.Tick(.033, View, true, 1, [new WorldDecoration("d1", "poco", 12, 8)], new Town(far));
        Assert.Empty(trades.Workers);

        var home = new LegacyCityBuilding("b1", "Casa.md", "Casa", null, 4, 4, "house1");
        var town = new Town(home);
        trades.Tick(.033, View, true, 1, [new WorldDecoration("d1", "lenha", 12, 8), new WorldDecoration("d2", "banco", 14, 8)], town);
        Assert.Equal("sack", Assert.Single(trades.Workers).Person.Look.Acc);   // a bench is not a trade
        trades.Tick(.033, View with { X0 = 100, X1 = 130 }, true, 1, [new WorldDecoration("d1", "lenha", 12, 8)], town);
        Assert.Empty(trades.Workers);
        trades.Tick(.033, View, true, 1, [new WorldDecoration("d1", "roca", 12, 8)], town);
        Assert.Single(trades.Workers);
        trades.Tick(.033, View, false, 1, [new WorldDecoration("d1", "roca", 12, 8)], town);
        Assert.Empty(trades.Workers);
    }
}
