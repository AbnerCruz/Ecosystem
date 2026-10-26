using Lunet.Graphics;
using Lunet.Studio;
using Lunet.Pathfinding;

namespace Lunet.Tests;

public sealed class TileStudioSessionTests
{
    [Fact]
    public void PaintingOneGestureStoresOneUndoAndRoundTripsRuntimeJson()
    {
        var edit = new TileStudioSession(TileMap.Create("Textures/tiles.png", 4, 3, 16, 16), 12);
        edit.Tile = 3;
        edit.BeginStroke();
        Assert.True(edit.Paint(0, 0));
        Assert.True(edit.Paint(1, 0));
        Assert.False(edit.Paint(1, 0));
        Assert.False(edit.Paint(-1, 0));
        Assert.True(edit.EndStroke());
        Assert.Equal(3, TileMap.Parse(edit.Map.ToJson()).GetTile(0, new GridPoint(1, 0)));
        Assert.True(edit.Undo());
        Assert.Equal(0, edit.Map.GetTile(0, new GridPoint(0, 0)));
        Assert.False(edit.Undo());
        Assert.True(edit.Redo());
        Assert.Equal(3, edit.Map.GetTile(0, new GridPoint(0, 0)));
    }

    [Fact]
    public void LayersCollisionAndHistoryUseSameRuntimeContract()
    {
        var edit = new TileStudioSession(TileMap.Create("Textures/tiles.png", 2, 2, 8, 8), 4);
        edit.AddLayer("obstáculos", collision: true);
        Assert.Equal(1, edit.Layer);
        edit.Tile = 2;
        edit.BeginStroke();
        edit.Paint(0, 1);
        edit.EndStroke();
        edit.ToggleVisibility();
        Assert.True(edit.Map.IsBlocked(new GridPoint(0, 1)));
        Assert.False(edit.Map.IsLayerVisible(1));
        edit.Undo();
        Assert.True(edit.Map.IsLayerVisible(1));
        edit.MoveLayer(-1);
        Assert.Equal("obstáculos", edit.Map.GetLayerName(0));
        Assert.True(TileMap.Parse(edit.Map.ToJson()).IsBlocked(new GridPoint(0, 1)));
        edit.RemoveLayer();
        Assert.Equal(1, edit.Map.LayerCount);
        edit.Undo();
        Assert.Equal(2, edit.Map.LayerCount);
    }

    [Fact]
    public void NoopDoesNotConsumeHistoryAndInvalidTilesAreRejected()
    {
        var edit = new TileStudioSession(TileMap.Create("Textures/a.png", 2, 2, 8, 8), 1);
        edit.BeginStroke();
        Assert.True(edit.Paint(0, 0));
        Assert.True(edit.EndStroke());
        edit.BeginStroke();
        Assert.False(edit.Paint(0, 0));
        Assert.False(edit.EndStroke());
        edit.Tile = 2;
        edit.BeginStroke();
        Assert.Throws<ArgumentOutOfRangeException>(() => edit.Paint(1, 1));
        edit.EndStroke();
        Assert.True(edit.Undo());
        Assert.False(edit.CanUndo);
        Assert.Throws<InvalidDataException>(() => new TileStudioSession(
            TileMap.Create("Textures/a.png", 1, 1, 8, 8).WithTile(0, new GridPoint(0, 0), 2), 1));
    }

    [Theory]
    [InlineData(16, 64, 16)]
    [InlineData(32, 128, 16)]
    [InlineData(64, 128, 4)]
    public void InAppTilesetAndMapCanBeCreatedWithoutImportedFiles(int tile, int atlasSize, int count)
    {
        // The Android editor writes this source as .sprite.json and the RGBA
        // sheet as Content/Textures/*.png; maps use the same runtime JSON v1.
        var atlas = SpriteDocument.Create(atlasSize);
        atlas.Rectangle(0, 0, 0, tile - 1, tile - 1, 0x80448822, filled: true);
        var restored = SpriteDocument.Parse(atlas.ToJson());
        Assert.Single(restored.Frames);
        Assert.Equal((byte)0x44, restored.SheetRgba()[0]);
        Assert.Equal((byte)0x80, restored.SheetRgba()[3]);
        var map = TileMap.Create("Textures/tiles-new.png", 16, 16, tile, tile);
        var session = new TileStudioSession(map, count);
        session.Tile = count;
        session.BeginStroke();
        Assert.True(session.Paint(3, 2));
        session.EndStroke();
        var reloaded = TileMap.Parse(session.Map.ToJson());
        Assert.Equal("Textures/tiles-new.png", reloaded.TexturePath);
        Assert.Equal(count, reloaded.GetTile(0, new GridPoint(3, 2)));
    }
}
