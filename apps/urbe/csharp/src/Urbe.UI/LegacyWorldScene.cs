using Urbe.Core;

namespace Urbe.UI;

/// <summary>
/// Original 'urbe' terrain generator, prepared off the Blazor render thread.
/// Only chunks intersecting the current camera rectangle are encoded as PNG.
/// PNG/data URI is a WebView transport for C#-generated pixels, never a
/// substituted static atlas. No JS runtime, HTTP, or vault writes.
/// </summary>
public sealed class LegacyWorldScene
{
    public sealed record VisibleChunk(int X, int Y, int LeftTiles,
        int TopTiles, string DataUri);
    public sealed record VisibleTree(int X, int Y, string DataUri);
    public sealed record View(
        IReadOnlyList<VisibleChunk> Chunks,
        IReadOnlyList<VisibleTree> Trees);

    private const int ImageCacheCapacity = 16;
    private static readonly Lazy<Task<LegacyWorldScene>> Shared =
        new(() => Task.Run(Create), LazyThreadSafetyMode.ExecutionAndPublication);

    private readonly LegacyTileChunks _tiles;
    private readonly LegacyVegetation _vegetation;
    private readonly Dictionary<(int, int), string> _images = [];
    private readonly Queue<(int, int)> _order = [];
    private readonly Dictionary<(string Kind, int Variant), string> _treeSprites = [];
    private readonly object _lock = new();

    private LegacyWorldScene(LegacyTileChunks tiles, LegacyVegetation vegetation)
    {
        _tiles = tiles;
        _vegetation = vegetation;
    }

    private static LegacyWorldScene Create()
    {
        // app.js: UrbeTerrain.createWorld('urbe',{spawnX:36,spawnY:25}).
        var climate = LegacyElevationClimateField.Generate("urbe");
        var hydro = LegacyHydrologyField.Generate(climate);
        var spawn = LegacyWorldSpawn.Choose(hydro, spawnX: 36, spawnY: 25);
        var sampler = new LegacyTileSampler(hydro, spawn);
        return new LegacyWorldScene(
            new LegacyTileChunks(sampler, capacity: 64),
            new LegacyVegetation(sampler));
    }

    public static async Task<IReadOnlyList<VisibleChunk>> LoadAsync(
        int tileX, int tileY, int widthTiles, int heightTiles)
    {
        // Reject malicious/unbounded camera ranges before expensive work.
        var requested = LegacyChunkViewport.Visible(
            tileX, tileY, widthTiles, heightTiles);
        var world = await Shared.Value.ConfigureAwait(false);
        return await Task.Run(() => world.Render(requested)).ConfigureAwait(false);
    }

    /// <summary>
    /// Builds a world frame from the same deterministic 1.8.4 terrain
    /// sampler. Trees occupy their own layer as in app.js drawTrees(), above
    /// ground/roads and below interactive buildings; never stored in vault.
    /// </summary>
    public static async Task<View> LoadViewAsync(
        int tileX, int tileY, int widthTiles, int heightTiles)
    {
        var chunks = LegacyChunkViewport.Visible(
            tileX, tileY, widthTiles, heightTiles);
        var world = await Shared.Value.ConfigureAwait(false);
        return await Task.Run(() =>
        {
            lock (world._lock)
            {
                return new View(world.Render(chunks),
                    world.Trees(tileX, tileY, widthTiles, heightTiles));
            }
        }).ConfigureAwait(false);
    }

    private IReadOnlyList<VisibleTree> Trees(
        int tileX, int tileY, int widthTiles, int heightTiles)
    {
        var result = new List<VisibleTree>();
        // Original drawSprite places the crown 0.8 tiles above its anchor.
        // A one-tile halo prevents abrupt disappearing at camera edges.
        int minX = checked(tileX - 2), minY = checked(tileY - 3);
        int maxX = checked(tileX + widthTiles + 1);
        int maxY = checked(tileY + heightTiles + 1);
        for (int y = minY; y < maxY; y++)
        for (int x = minX; x < maxX; x++)
        {
            var tree = _vegetation.TreeAt(x, y);
            if (tree is null) continue;
            var key = (tree.Kind, tree.Variant);
            if (!_treeSprites.TryGetValue(key, out var url))
            {
                var rgba = LegacyWorldSprites.CreateTree(tree.Kind, tree.Variant);
                var png = LegacyWorldSprites.EncodePng(rgba, 24, 32);
                url = "data:image/png;base64," + Convert.ToBase64String(png);
                _treeSprites.Add(key, url);
            }
            result.Add(new VisibleTree(x, y, url));
        }
        return result;
    }

    private IReadOnlyList<VisibleChunk> Render(
        IReadOnlyList<LegacyChunkViewport.Chunk> visible)
    {
        var result = new List<VisibleChunk>(visible.Count);
        foreach (var chunk in visible)
            result.Add(new VisibleChunk(
                chunk.X, chunk.Y, chunk.LeftTiles, chunk.TopTiles,
                GetDataUri(chunk.X, chunk.Y)));
        return result;
    }

    private string GetDataUri(int chunkX, int chunkY)
    {
        lock (_lock)
        {
            var key = (chunkX, chunkY);
            if (_images.TryGetValue(key, out string? existing))
                return existing;
            byte[] rgba = LegacyChunkPixels.Render(
                _tiles, _vegetation, chunkX, chunkY);
            var encoded = LegacyWorldSprites.EncodePng(
                rgba, LegacyChunkPixels.Side, LegacyChunkPixels.Side);
            var url = "data:image/png;base64," + Convert.ToBase64String(encoded);
            if (_images.Count >= ImageCacheCapacity)
                _images.Remove(_order.Dequeue());
            _images.Add(key, url);
            _order.Enqueue(key);
            return url;
        }
    }
}
