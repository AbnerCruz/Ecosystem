using Lunet.Core;

namespace Lunet.Tests;

public sealed class ProjectAssetCatalogTests
{
    [Fact]
    public void GroupsEditableSpriteAndGeneratedPngAsOneResource()
    {
        var catalog = ProjectAssetCatalog.Build(new[]
        {
            "lunet.json", "Game.cs", "Content/Textures/lumi.sprite.json",
            "Content/Textures/lumi.png", "Content/Textures/tiles.png",
            "Content/Data/fase.tilemap.json", "Content/Audio/jump.wav"
        });
        Assert.Equal(4, catalog.Count);
        var sprite = Assert.Single(catalog, a => a.Kind == ProjectAssetKind.Sprite);
        Assert.Equal("Content/Textures/lumi.sprite.json", sprite.Path);
        Assert.Equal("Content/Textures/lumi.png", sprite.PreviewPath);
        Assert.Equal("Content.LoadTexture(\"Textures/lumi.png\")", sprite.UsageCode);
        Assert.Equal("Content.LoadTileMap(\"Data/fase.tilemap.json\")",
            Assert.Single(catalog, a => a.Kind == ProjectAssetKind.TileMap).UsageCode);
        Assert.Equal("Content.LoadSound(\"Audio/jump.wav\")",
            Assert.Single(catalog, a => a.Kind == ProjectAssetKind.Audio).UsageCode);
    }

    [Fact]
    public void DoesNotShowSourceCodeCacheOrGeneratedImageTwice()
    {
        var catalog = ProjectAssetCatalog.Build(new[]
        {
            "Game.cs", "README.md", ".lunet/cache/atlas.png",
            "Content/Textures/a.sprite.json", "Content/Textures/a.png",
            "Content/Data/foo.json", "Assets/run.sprite.json", "Assets/run.png"
        });
        Assert.Equal(3, catalog.Count);
        Assert.Equal("Content.ReadText(\"Data/foo.json\")", catalog.Single(a => a.Kind == ProjectAssetKind.Data).UsageCode);
        Assert.Equal("Content.LoadTexture(\"Assets/run.png\")", catalog.Single(a => a.Path.StartsWith("Assets/")).UsageCode);
    }

    [Fact]
    public void MissingDerivedPngDoesNotInventLoadableTexture()
    {
        var asset = Assert.Single(ProjectAssetCatalog.Build(new[] { "Assets/unfinished.sprite.json" }));
        Assert.Null(asset.PreviewPath);
        Assert.Null(asset.RuntimePath);
    }

    [Fact]
    public void AcceptsMixedCaseExtensionsWithoutDuplicatingDerivedSource()
    {
        var assets = ProjectAssetCatalog.Build(new[] { "Content/FX/Dash.SPRITE.JSON", "Content/FX/Dash.PNG", "Content/Fonts/Bit.TTF", "Assets/Track.OGG" });
        Assert.Equal(3, assets.Count);
        Assert.Contains(assets, a => a.Kind == ProjectAssetKind.Font && a.UsageCode == "Content.LoadTrueTypeFont(\"Fonts/Bit.TTF\", 24)");
        Assert.Contains(assets, a => a.Kind == ProjectAssetKind.Audio && a.UsageCode == "Content.LoadMusic(\"Assets/Track.OGG\")");
    }
}
