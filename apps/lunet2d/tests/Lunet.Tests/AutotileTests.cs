using Lunet.Graphics;
using Lunet.Pathfinding;
using Lunet.Studio;

namespace Lunet.Tests;

/// <summary>LUNET-506: autotile brushes pick edges and corners from the neighbours.</summary>
public sealed class AutotileTests
{
    private static TileMap Map(int w = 6, int h = 6) => TileMap.Create("Textures/tiles.png", w, h, 16, 16);

    [Fact]
    public void Blob_HasExactly47Masks() => Assert.Equal(47, AutotileSet.BlobMasks.Count);

    [Fact]
    public void Sides16_PicksTheTileByConnectedSides()
    {
        var set = new AutotileSet("grama", 1, AutotileLayout.Sides16);
        Assert.Equal(1, set.TileFor(false, false, false, false, false, false, false, false)); // isolated
        Assert.Equal(1 + 15, set.TileFor(true, false, true, false, true, false, true, false)); // all sides
        Assert.Equal(1 + 2 + 8, set.TileFor(false, true, true, true, false, false, true, false)); // horizontal strip
    }

    [Fact]
    public void Blob_IgnoresCornersWithoutBothSides()
    {
        var set = new AutotileSet("água", 100, AutotileLayout.Blob47);
        var withCorner = set.TileFor(true, true, false, false, false, false, false, false);
        var without = set.TileFor(true, false, false, false, false, false, false, false);
        Assert.Equal(without, withCorner);
        var full = set.TileFor(true, true, true, true, true, true, true, true);
        Assert.Equal(100 + 46, full); // the fully surrounded tile is the last one
        Assert.InRange(set.TileFor(true, false, true, false, true, false, true, false), 100, 146);
    }

    [Fact]
    public void Painting_FixesNeighbours_AndErasingReopensEdges()
    {
        var set = new AutotileSet("grama", 1, AutotileLayout.Sides16);
        var map = Map();
        map = set.Paint(map, 0, 2, 2);
        Assert.Equal(1, map.GetTile(0, new GridPoint(2, 2))); // alone
        map = set.Paint(map, 0, 3, 2);
        Assert.Equal(1 + 2, map.GetTile(0, new GridPoint(2, 2))); // now connects east
        Assert.Equal(1 + 8, map.GetTile(0, new GridPoint(3, 2))); // and west
        map = set.Paint(map, 0, 3, 2, erase: true);
        Assert.Equal(0, map.GetTile(0, new GridPoint(3, 2)));
        Assert.Equal(1, map.GetTile(0, new GridPoint(2, 2)));
        // the map border counts as terrain: a corner cell has closed sides toward the border
        map = set.Paint(map, 0, 0, 0);
        Assert.Equal(1 + 1 + 8, map.GetTile(0, new GridPoint(0, 0)));
    }

    [Fact]
    public void Session_UsesTheTerrainBrushInOneUndoStep()
    {
        var session = new TileStudioSession(Map(), 64) { Terrain = new AutotileSet("grama", 1, AutotileLayout.Sides16) };
        session.BeginStroke();
        Assert.True(session.Paint(1, 1));
        Assert.True(session.Paint(2, 1));
        Assert.True(session.EndStroke());
        Assert.Equal(1 + 2, session.Map.GetTile(0, new GridPoint(1, 1)));
        Assert.True(session.Undo());
        Assert.Equal(0, session.Map.GetTile(0, new GridPoint(1, 1)));
        var tooSmall = new TileStudioSession(Map(), 10) { Terrain = new AutotileSet("água", 1, AutotileLayout.Blob47) };
        tooSmall.BeginStroke();
        Assert.Throws<InvalidOperationException>(() => tooSmall.Paint(0, 0));
    }
}
