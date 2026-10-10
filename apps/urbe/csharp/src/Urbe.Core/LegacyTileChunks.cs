namespace Urbe.Core;

/// <summary>
/// Native C# equivalent of the original terrain.js CH=16 cache.
/// Only requested chunks are generated, preventing 512*512*4 terrain
/// tiles from being materialized on a small Android device.
/// No vault writes, browser JS or visual style substitution.
/// </summary>
public sealed class LegacyTileChunks
{
    public const int ChunkSize = 16;
    public const int DefaultCapacity = 900;
    private readonly LegacyTileSampler _terrain;
    private readonly int _capacity;
    private readonly Dictionary<(int X, int Y),
        IReadOnlyList<LegacyTileSampler.Tile>> _cache = [];
    private readonly Queue<(int X, int Y)> _order = new();

    public LegacyTileChunks(
        LegacyTileSampler terrain, int capacity = DefaultCapacity)
    {
        _terrain = terrain ?? throw new ArgumentNullException(nameof(terrain));
        if (capacity is < 1 or > DefaultCapacity)
            throw new ArgumentOutOfRangeException(nameof(capacity));
        _capacity = capacity;
    }

    public int CachedCount => _cache.Count;
    public long GeneratedChunks { get; private set; }

    // JS Math.floor(-1/16) -> -1; C# integer division -> 0.
    public static int ChunkCoordinate(int tile) =>
        (int)Math.Floor(tile / (double)ChunkSize);

    public IReadOnlyList<LegacyTileSampler.Tile> Get(int chunkX, int chunkY)
    {
        var key = (chunkX, chunkY);
        if (_cache.TryGetValue(key, out var tiles))
            return tiles;

        var result = new LegacyTileSampler.Tile[ChunkSize * ChunkSize];
        int originX = checked(chunkX * ChunkSize);
        int originY = checked(chunkY * ChunkSize);
        for (int row = 0; row < ChunkSize; row++)
        for (int column = 0; column < ChunkSize; column++)
            result[row * ChunkSize + column] =
                _terrain.Sample(originX + column, originY + row);

        // terrain.js evicts the first inserted entry, not LRU.
        if (_cache.Count >= _capacity)
        {
            var oldest = _order.Dequeue();
            _cache.Remove(oldest);
        }

        IReadOnlyList<LegacyTileSampler.Tile> safe = Array.AsReadOnly(result);
        _cache[key] = safe;
        _order.Enqueue(key);
        GeneratedChunks++;
        return safe;
    }

    public LegacyTileSampler.Tile At(int tileX, int tileY)
    {
        int cx = ChunkCoordinate(tileX), cy = ChunkCoordinate(tileY);
        int dx = tileX - cx * ChunkSize;
        int dy = tileY - cy * ChunkSize;
        return Get(cx, cy)[dy * ChunkSize + dx];
    }

    public void Clear()
    {
        _cache.Clear();
        _order.Clear();
    }
}
