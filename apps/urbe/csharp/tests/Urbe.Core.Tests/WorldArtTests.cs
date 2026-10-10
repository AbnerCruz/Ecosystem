using System.Xml.Linq;
using Urbe.UI;

namespace Urbe.Core.Tests;

public sealed class WorldArtTests
{
    [Fact]
    public void BuildingsHaveStableLocalColorVariations()
    {
        const string path = "Centro/Minha Casa.md";
        var selected = WorldArt.BuildingClass(path, false);
        Assert.Equal(selected, WorldArt.BuildingClass(path, false));
        Assert.Contains(selected, new[] { "world-sprite-red", "world-sprite-ochre", "world-sprite-slate" });
        Assert.Equal("world-sprite-hall", WorldArt.BuildingClass(path, true));
    }

    [Fact]
    public void EachTileUsesOneBoundedDecoration()
    {
        for (var y = 0; y < CityTileLayout.Rows; y++)
        for (var x = 0; x < CityTileLayout.Columns; x++)
            Assert.Contains(WorldArt.TerrainClass(x, y),
                new[] { "world-terrain-tree", "world-terrain-flowers", "world-terrain-meadow" });
    }

    [Theory]
    [InlineData("house-red.svg")]
    [InlineData("house-ochre.svg")]
    [InlineData("house-slate.svg")]
    [InlineData("bairro-hall.svg")]
    [InlineData("grass.svg")]
    [InlineData("stone-road.svg")]
    [InlineData("tree.svg")]
    public void PixelSpritesAreBundledAndContainNoExecutableElements(string assetName)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Urbe.Portable.slnx")))
            root = root.Parent;
        Assert.NotNull(root);
        var file = Path.Combine(root!.FullName, "src", "Urbe.UI", "wwwroot", "world", assetName);
        var svg = XDocument.Load(file);
        Assert.Equal("svg", svg.Root!.Name.LocalName);
        Assert.Equal("crispEdges", svg.Root.Attribute("shape-rendering")?.Value);
        Assert.DoesNotContain(svg.Descendants(), e =>
            e.Name.LocalName is "script" or "foreignObject" or "image");
        Assert.DoesNotContain(svg.Descendants().Attributes(), a =>
            a.Name.LocalName is "href" or "src");
    }
}
