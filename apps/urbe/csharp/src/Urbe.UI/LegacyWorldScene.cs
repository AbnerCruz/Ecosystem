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

    private const int ImageCacheCapacity = 16;
    private static readonly Lazy<Task<LegacyWorldScene>> Shared =
        new(() => Task.Run(Create), LazyThreadSafetyMode.ExecutionAndPublication);

    private readonly LegacyTileChunks _tiles;
    private readonly LegacyVegetation _vegetation;
    private readonly Dictionary<(int, int), string> _images = [];
    private readonly Queue<(int, int)> _order = [];
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
