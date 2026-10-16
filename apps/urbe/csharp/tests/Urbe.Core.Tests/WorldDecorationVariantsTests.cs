using Urbe.Core;

namespace Urbe.Core.Tests;

/// <summary>UC-53: variations, mirroring and turning of decorations, and the brush.</summary>
public sealed class WorldDecorationVariantsTests
{
    [Fact]
    public void EveryVariationAndTurnedDrawingFillsWholeTiles()
    {
        foreach (var k in WorldDecorations.Kinds)
        {
            Assert.NotEmpty(k.Variations);
            for (int v = 0; v < k.Variations.Count; v++)
                foreach (var turned in new[] { false, true })
                {
                    var grid = WorldDecorations.Sprite(k, v, turned);
                    var rows = Assert.Single(grid.Frames);
                    Assert.Equal(k.H * WorldDecorations.CellsPerTile, rows.Length);
                    Assert.All(rows, row => Assert.Equal(k.W * WorldDecorations.CellsPerTile, row.Length));
                    Assert.All(rows.SelectMany(r => r).Where(c => c != '.'), c => Assert.True(grid.Palette.ContainsKey(c), $"{k.Id}/{v}: {c}"));
                }
            // a variation differs from the default in colours or drawing
            for (int v = 1; v < k.Variations.Count; v++)
                Assert.NotEqual(LegacyLifeSprites.Render(WorldDecorations.Sprite(k, 0, false), 0, false).Pixels,
                    LegacyLifeSprites.Render(WorldDecorations.Sprite(k, v, false), 0, false).Pixels);
        }
        Assert.True(WorldDecorations.Kinds.Count >= 18);
        Assert.True(WorldDecorations.Find("cerca")!.Turns);
        Assert.True(WorldDecorations.Find("carroca")!.Flips && WorldDecorations.Find("placa")!.Flips);
        Assert.Equal(WorldDecorations.Kinds.Count, WorldDecorations.Kinds.Select(k => k.Id).Distinct().Count());
    }

    [Fact]
    public void ALookRoundTripsAndDefaultsAreNotWritten()
    {
        var p = WorldPrefs.Default with
        {
            Decorations = [new("d1", "cerca", 1, 1, Variant: 2, Turned: true), new("d2", "carroca", 3, 1, Variant: 1, Flipped: true), new("d3", "poco", 6, 1)]
        };
        var text = p.Text();
        Assert.Contains("\"variacao\": 2", text);
        Assert.Contains("\"girada\": true", text);
        Assert.Contains("\"espelhada\": true", text);
        var back = WorldPrefs.Parse(text, out _);
        Assert.Equal(p, back);
        // the plain one is written exactly as before UC-53
        var plain = (WorldPrefs.Default with { Decorations = [new("d3", "poco", 6, 1)] }).Text();
        Assert.DoesNotContain("variacao", plain);
        Assert.DoesNotContain("espelhada", plain);
        Assert.DoesNotContain("girada", plain);
    }

    [Fact]
    public void OldFilesLoadUnchangedAndUnknownLooksAreTheDefault()
    {
        var old = WorldPrefs.Parse("{\"versao\":1,\"decoracoes\":[{\"id\":\"a\",\"tipo\":\"banco\",\"x\":2,\"y\":2}]}", out _);
        Assert.Equal(new WorldDecoration("a", "banco", 2, 2), Assert.Single(old.Decorations));
        var odd = WorldPrefs.Parse("{\"versao\":1,\"decoracoes\":[" +
            "{\"id\":\"a\",\"tipo\":\"banco\",\"x\":2,\"y\":2,\"variacao\":99,\"girada\":true,\"espelhada\":true}," +
            "{\"id\":\"b\",\"tipo\":\"flores\",\"x\":4,\"y\":2,\"variacao\":\"rosa\"}," +
            "{\"id\":\"c\",\"tipo\":\"flores\",\"x\":5,\"y\":2,\"variacao\":-1}]}", out _);
        // the bench neither turns nor mirrors; unknown variations fall back to the first
        Assert.All(odd.Decorations, d => Assert.Equal((0, false, false), (d.Variant, d.Flipped, d.Turned)));
        Assert.Equal(3, odd.Decorations.Count);
    }

    [Fact]
    public void LineStepsOneTileAtATime()
    {
        Assert.Equal([(0, 0), (1, 0), (2, 0), (3, 0)], WorldDecorations.Line(0, 0, 3, 0));
        var diagonal = WorldDecorations.Line(5, 5, 1, 2).ToList();
        Assert.Equal((5, 5), diagonal[0]);
        Assert.Equal((1, 2), diagonal[^1]);
        Assert.All(diagonal.Zip(diagonal.Skip(1)), p => Assert.True(Math.Max(Math.Abs(p.First.X - p.Second.X), Math.Abs(p.First.Y - p.Second.Y)) == 1));
        Assert.Single(WorldDecorations.Line(2, 2, 2, 2));
    }

    [Fact]
    public void TheBrushPlacesOnePerFreeTileAlongTheStroke()
    {
        var fence = WorldDecorations.Find("cerca")!;
        var start = WorldPrefs.Default with { Decorations = [new("d1", "poco", 3, 0)] };
        // tile (5, 0) is "water": not valid for a single placement either
        bool Fits(int x, int y) => x != 5 && start.DecorationAt(x, y) is null;
        var after = start.Paint(fence, WorldDecorations.Line(0, 0, 7, 0), Fits, out var placed, variant: 1, turned: true);
        Assert.Equal([0, 1, 2, 4, 6, 7], placed.Select(d => d.X));
        Assert.All(placed, d => Assert.Equal(("cerca", 1, true, false), (d.Kind, d.Variant, d.Turned, d.Flipped)));
        Assert.Equal(7, after.Decorations.Count);
        Assert.Equal(after.Decorations.Count, after.Decorations.Select(d => d.Id).Distinct().Count());
        // painting over the same stroke again adds nothing
        after.Paint(fence, WorldDecorations.Line(0, 0, 7, 0), (x, y) => x != 5 && after.DecorationAt(x, y) is null, out var again);
        Assert.Empty(again);

        // a two-tile cart takes every other tile; the bench cannot be mirrored, so it is not
        var cart = WorldDecorations.Find("carroca")!;
        WorldPrefs.Default.Paint(cart, WorldDecorations.Line(0, 3, 5, 3), (_, _) => true, out var carts, flipped: true);
        Assert.Equal([0, 2, 4], carts.Select(d => d.X));
        Assert.All(carts, d => Assert.True(d.Flipped));
        WorldPrefs.Default.Paint(WorldDecorations.Find("banco")!, [(0, 0)], (_, _) => true, out var benches, flipped: true, variant: 7);
        Assert.Equal((0, false), (benches[0].Variant, benches[0].Flipped));
    }
}
