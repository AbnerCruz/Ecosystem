using System.Numerics;
using Lunet.Content;
using Lunet.Graphics;
using Lunet.Pathfinding;

namespace Lunet.Tests;

[Collection("Frame allocation")]
public class TileMapTests
{
    private const string MapJson = """
        {
          "version": 1, "texture": "Textures/tiles.png",
          "width": 3, "height": 2, "tileWidth": 8, "tileHeight": 8,
          "layers": [
            {"name":"ground","tiles":[1,2,3,4,5,6]},
            {"name":"walls","visible":false,"collision":true,"tiles":[0,0,1,0,0,1]}
          ]
        }
        """;

    [Fact]
    public void Parse_PreservesLayersAndCollision_AndRejectsOutside()
    {
        var map = TileMap.Parse(MapJson);
        Assert.Equal(("Textures/tiles.png", 3, 2, 8, 8, 2),
            (map.TexturePath, map.Width, map.Height, map.TileWidth, map.TileHeight, map.LayerCount));
        Assert.Equal("walls", map.GetLayerName(1));
        Assert.False(map.IsLayerVisible(1));
        Assert.True(map.IsCollisionLayer(1));
        Assert.Equal(5, map.GetTile(0, new GridPoint(1, 1)));
        Assert.True(map.IsBlocked(new(2, 0)));
        Assert.True(map.IsBlocked(new(2, 1)));
        Assert.False(map.IsBlocked(new(1, 1)));
        Assert.True(map.IsBlocked(new(-1, 0)));
        Assert.True(map.IsBlocked(new(3, 0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => map.GetTile(1, new(3, 0)));
        Assert.Equal(new GridPoint(-1, 0), map.WorldToCell(new Vector2(-0.1f, 1), Vector2.Zero));
        Assert.Equal(new GridPoint(1, 1), map.WorldToCell(new Vector2(23, 33), new Vector2(15, 25)));
        Assert.Equal(new RectangleF(23, 33, 8, 8), map.CellBounds(new(1, 1), new(15, 25)));
    }

    [Fact]
    public void CopiesCollisionIntoExistingPathfinder_Deterministically()
    {
        var map = TileMap.Parse(MapJson);
        var grid = new GridPathfinder(map.Width, map.Height);
        map.CopyCollisionTo(grid, 2f);
        Assert.Equal(0f, grid.GetCost(new(2, 0)));
        Assert.Equal(2f, grid.GetCost(new(1, 1)));
        var path = new GridPoint[6];
        Assert.Equal(PathStatus.NotFound, grid.FindPath(new(0, 0), new(2, 0), path).Status);
        Assert.Equal(PathStatus.Found, grid.FindPath(new(0, 0), new(1, 1), path).Status);
        Assert.Throws<ArgumentException>(() => map.CopyCollisionTo(new GridPathfinder(2, 2)));
        Assert.Throws<ArgumentOutOfRangeException>(() => map.CopyCollisionTo(grid, float.NaN));
    }

    [Fact]
    public void Draw_ClipsToViewport_RespectsHiddenLayerAndTilesetUv()
    {
        var map = TileMap.Parse(MapJson);
        var backend = new RecordingBackend();
        var device = new GraphicsDevice(backend, 100, 100);
        using var texture = Texture2D.CreateSolid(device, 24, 16, Color.White);
        var batch = new SpriteBatch(device);
        batch.Begin();
        map.Draw(batch, texture, new RectangleF(8, 0, 8, 8), Vector2.Zero, Color.White);
        batch.End();
        Assert.Single(backend.Batches);
        Assert.Equal(1, backend.Batches[0].QuadCount);
        var verts = backend.Batches[0].Vertices;
        Assert.Equal(new Vector2(8, 0), verts[0].Position);
        Assert.Equal(new Vector2(16, 8), verts[2].Position);
        Assert.Equal(new Vector2(1f / 3f, 0), verts[0].TexCoord);
        Assert.Equal(new Vector2(2f / 3f, 0.5f), verts[2].TexCoord);
        Assert.Throws<InvalidOperationException>(() => map.Draw(batch, texture, new RectangleF(0, 0, 4, 4),
            Vector2.Zero, Color.White));
    }

    [Fact]
    public void Draw_RejectsInvalidTileset_AndOutsideViewportDrawsNothing()
    {
        var map = TileMap.Parse(MapJson);
        var backend = new RecordingBackend();
        var device = new GraphicsDevice(backend, 100, 100);
        using var shortTexture = Texture2D.CreateSolid(device, 8, 8, Color.White);
        var batch = new SpriteBatch(device);
        Assert.Throws<ArgumentException>(() => map.Draw(batch, shortTexture, new RectangleF(0, 0, 16, 16),
            Vector2.Zero, Color.White));
        using var texture = Texture2D.CreateSolid(device, 24, 16, Color.White);
        batch.Begin();
        map.Draw(batch, texture, new RectangleF(200, 200, 32, 32), Vector2.Zero, Color.White);
        batch.End();
        Assert.Empty(backend.Batches);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("null")]
    [InlineData("{bad}")]
    [InlineData("{\"version\":2}")]
    [InlineData("{\"version\":1,\"texture\":\"../wrong.png\"}")]
    public void Parse_RejectsBadDocuments(string json) => Assert.Throws<InvalidDataException>(() => TileMap.Parse(json));

    [Fact]
    public void Parse_RejectsBadLayersAndOversizedGrids()
    {
        Assert.Throws<InvalidDataException>(() => TileMap.Parse(MapJson.Replace("[1,2,3,4,5,6]", "[1,2]")));
        Assert.Throws<InvalidDataException>(() => TileMap.Parse(MapJson.Replace("[1,2,3,4,5,6]", "[1,-2,3,4,5,6]")));
        Assert.Throws<InvalidDataException>(() => TileMap.Parse(MapJson.Replace("\"walls\"", "\"ground\"")));
        Assert.Throws<InvalidDataException>(() => TileMap.Parse(MapJson.Replace("\"width\": 3", "\"width\": 2000000")));
        Assert.Throws<InvalidDataException>(() => TileMap.Parse(MapJson.Replace("\"tileWidth\": 8", "\"tileWidth\": 0")));
    }

    [Fact]
    public void ContentManager_LoadTileMap_UsesOrdinaryContentFiles()
    {
        string root = Path.Combine(Path.GetTempPath(), "lunet-tilemap-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "Data"));
        try
        {
            File.WriteAllText(Path.Combine(root, "Data", "test.json"), MapJson);
            var backend = new RecordingBackend();
            using var content = new ContentManager(new DirectoryContentSource(root), new GraphicsDevice(backend, 100, 100));
            var map = content.LoadTileMap("Data/test.json");
            Assert.Equal("Textures/tiles.png", map.TexturePath);
            Assert.Throws<FileNotFoundException>(() => content.LoadTileMap("Data/missing.json"));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void WorldQueries_DoNotAllocateAfterWarmup()
    {
        var map = TileMap.Parse(MapJson);
        var grid = new GridPathfinder(3, 2);
        for (int i = 0; i < 200; i++) { map.IsBlocked(new(2, 1)); map.CopyCollisionTo(grid); map.WorldToCell(Vector2.One, Vector2.Zero); }
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) { map.IsBlocked(new(2, 1)); map.CopyCollisionTo(grid); map.WorldToCell(Vector2.One, Vector2.Zero); }
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }
}


public class TileMapGuideTests
{
    [Fact]
    public void OfflineGuide_CompilesAndRunsInPreviewHost()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "docs", "guides", "tilemaps.md"))) root = root.Parent;
        Assert.NotNull(root);
        var guide = File.ReadAllText(Path.Combine(root!.FullName, "docs", "guides", "tilemaps.md"));
        var source = guide.Split("```csharp\n")[1].Split("```")[0];
        var compiler = new Lunet.Compiler.GameCompiler(new Lunet.Compiler.LoadedAssembliesReferenceProvider(typeof(Game).Assembly));
        var result = compiler.Compile("TileMapDemo", [new Lunet.Compiler.SourceFile("Game.cs", source)]);
        Assert.True(result.Success, string.Join("\n", result.Diagnostics));
        using var loaded = Lunet.Runtime.GameLoader.Load(result.Assembly!, result.Symbols);
        var backend = new RecordingBackend();
        var host = new GameHost(loaded.Game, backend);
        Assert.True(host.Start(360, 640));
        host.Tick(1.0 / 60);
        Assert.NotEmpty(backend.Batches);
        Assert.False(host.IsFaulted);
        host.SetSurfaceTouches([new Lunet.Input.TouchPoint(1, Lunet.Input.TouchPhase.Pressed, new Vector2(28 + 5 * 48 + 24, 150 + 5 * 48 + 24))]);
        host.Tick(1.0 / 60);
        Assert.False(host.IsFaulted);
    }
}
