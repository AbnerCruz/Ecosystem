using Urbe.UI;

namespace Urbe.Core.Tests;

public sealed class WorldArtTests
{
    [Theory]
    [InlineData("house1", "world-sprite-red")]
    [InlineData("house2", "world-sprite-ochre")]
    [InlineData("house3", "world-sprite-slate")]
    public void RespectsOriginalSavedSpriteNames(string sprite, string expected)
    {
        Assert.Equal(expected, WorldArt.BuildingClass("Centro/Casa.md", false, sprite));
        Assert.Equal("world-sprite-hall", WorldArt.BuildingClass("Centro", true));
    }

    [Fact]
    public void OriginalSeedUsesCaseSensitiveUtf16()
    {
        const string path = "Centro/Casa.md";
        Assert.Equal(WorldArt.LegacySeed(path), WorldArt.LegacySeed(path));
        Assert.NotEqual(WorldArt.LegacySeed(path), WorldArt.LegacySeed("centro/Casa.md"));
        var expected = WorldArt.LegacySeed(path) % 3u switch
        {
            0 => "world-sprite-red",
            1 => "world-sprite-ochre",
            _ => "world-sprite-slate"
        };
        Assert.Equal(expected, WorldArt.BuildingClass(path, false));
    }

    [Fact]
    public void SourceOfArtIsPinnedAndOriginal()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Urbe.Portable.slnx")))
            root = root.Parent;
        Assert.NotNull(root);
        var script = File.ReadAllText(Path.Combine(root!.FullName, "tools", "export-legacy-v184.mjs"));
        Assert.Contains("d47cf2902e8aeb5ca2c529d2e4c9617fb71ca931", script);
        Assert.Contains("../../src/world/pixel-art.js", script);
        Assert.Contains("art.building(", script);
        Assert.Contains("art.tree(", script);
        Assert.Contains("art.texture(", script);
    }
}
