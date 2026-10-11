using System.Numerics;
using Lunet.Graphics;
using Lunet.Pathfinding;

namespace Lunet.Tests;

public sealed class TileMapEditingTests
{
    [Fact]
    public void EditorOperations_PreservePreviousVersion_AndRoundTripToRuntime()
    {
        var original = TileMap.Create("Textures/chão.png", 3, 2, 16, 16);
        Assert.Equal("ground", original.GetLayerName(0));
        var edited = original.WithTile(0, new GridPoint(0, 0), 1)
            .AddLayer("paredes", visible: false, collision: true)
            .WithTile(1, new GridPoint(2, 1), 2)
            .RenameLayer(0, "piso")
            .MoveLayer(1, 0);
        Assert.Equal(1, original.LayerCount);
        Assert.Equal(0, original.GetTile(0, new GridPoint(0, 0)));
        Assert.Equal("paredes", edited.GetLayerName(0));
        Assert.Equal("piso", edited.GetLayerName(1));
        Assert.Equal(2, edited.GetTile(0, new GridPoint(2, 1)));
        Assert.False(edited.IsLayerVisible(0));
        Assert.True(edited.IsCollisionLayer(0));
        Assert.True(edited.IsBlocked(new GridPoint(2, 1)));
        Assert.False(original.IsBlocked(new GridPoint(2, 1)));

        var loaded = TileMap.Parse(edited.ToJson());
        Assert.Equal(edited.ToJson(), loaded.ToJson());
        var grid = new GridPathfinder(3, 2);
        loaded.CopyCollisionTo(grid);
        Assert.Equal(0f, grid.GetCost(new GridPoint(2, 1)));
        Assert.True(loaded.OverlapsCollision(new RectangleF(32, 16, 16, 16), Vector2.Zero));
    }

    [Fact]
    public void EditorOperations_AllowTogglingVisibilityAndCollisionWithoutChangingTiles()
    {
        var map = TileMap.Create("Textures/tiles.png", 2, 2, 8, 8)
            .WithTile(0, new GridPoint(1, 1), 1);
        var hidden = map.WithLayerSettings(0, false, true);
        Assert.True(map.IsLayerVisible(0));
        Assert.False(map.IsCollisionLayer(0));
        Assert.False(hidden.IsLayerVisible(0));
        Assert.True(hidden.IsCollisionLayer(0));
        Assert.True(hidden.IsBlocked(new GridPoint(1, 1)));
        Assert.Same(hidden, hidden.WithLayerSettings(0, false, true));
        Assert.Same(map, map.WithTile(0, new GridPoint(1, 1), 1));
        Assert.Same(map, map.MoveLayer(0, 0));
        Assert.Same(map, map.RenameLayer(0, "ground"));
    }

    [Fact]
    public void EditorOperations_RecomputeMaximumTileAfterReplacementAndLayerRemoval()
    {
        var map = TileMap.Create("Textures/tiles.png", 2, 2, 8, 8)
            .WithTile(0, new GridPoint(0, 0), 100)
            .WithTile(0, new GridPoint(0, 0), 1)
            .AddLayer("temporaria")
            .WithTile(1, new GridPoint(0, 0), 500);
        var removed = map.RemoveLayer(1);
        Assert.Equal(1, removed.GetTile(0, new GridPoint(0, 0)));

        var backend = new RecordingBackend();
        var device = new GraphicsDevice(backend, 64, 64);
        using var texture = Texture2D.CreateSolid(device, 8, 8, Color.White);
        var batch = new SpriteBatch(device);
        batch.Begin();
        removed.Draw(batch, texture, new RectangleF(0, 0, 16, 16), Vector2.Zero, Color.White);
        batch.End();
        Assert.Single(backend.Batches);
        Assert.Equal(1, backend.Batches[0].QuadCount);
        Assert.Equal(500, map.GetTile(1, new GridPoint(0, 0)));
    }

    [Fact]
    public void EditorOperations_RejectInvalidInputsAndLayerOverflow()
    {
        Assert.Throws<ArgumentException>(() => TileMap.Create("../a.png", 1, 1, 8, 8));
        Assert.Throws<ArgumentOutOfRangeException>(() => TileMap.Create("Textures/a.png", 0, 1, 8, 8));
        Assert.Throws<ArgumentOutOfRangeException>(() => TileMap.Create("Textures/a.png", 1025, 1024, 8, 8));
        Assert.Throws<ArgumentOutOfRangeException>(() => TileMap.Create("Textures/a.png", 1, 1, 0, 8));

        var map = TileMap.Create("Textures/a.png", 1, 1, 8, 8);
        Assert.Throws<InvalidOperationException>(() => map.RemoveLayer(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => map.WithTile(1, new GridPoint(0, 0), 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => map.WithTile(0, new GridPoint(2, 0), 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => map.WithTile(0, new GridPoint(0, 0), -1));
        Assert.Throws<ArgumentException>(() => map.AddLayer("ground"));
        Assert.Throws<ArgumentException>(() => map.AddLayer(" "));
        Assert.Throws<ArgumentOutOfRangeException>(() => map.MoveLayer(0, 1));

        for (int i = 1; i < 32; i++) map = map.AddLayer("layer-" + i);
        Assert.Equal(32, map.LayerCount);
        Assert.Throws<InvalidOperationException>(() => map.AddLayer("overflow"));
        Assert.Throws<ArgumentException>(() => map.RenameLayer(1, "ground"));
        Assert.Throws<ArgumentException>(() => map.RenameLayer(1, " "));
    }
}
