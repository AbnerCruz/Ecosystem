using Urbe.Core;

namespace Urbe.Client.World;

/// <summary>
/// The 1.8.4-beta world, assembled from the terrain engine already ported to
/// Urbe.Core: createWorld('urbe', {spawnX: 36, spawnY: 25}) in app.js.
/// The client only reads it; no terrain rule lives here.
/// </summary>
public sealed class LegacyWorld
{
    /// <summary>World units per terrain tile (app.js TILE).</summary>
    public const int Tile = 32;
    /// <summary>Tiles per chunk (terrain.js CH).</summary>
    public const int ChunkTiles = LegacyTileChunks.ChunkSize;
    /// <summary>Texture pixels per tile (pixel-art.js PX).</summary>
    public const int TilePixels = LegacyWorldPixelTextures.TileSize;

    private readonly object _gate = new();

    public LegacyWorld(string seed = "urbe", int gridSize = 512)
    {
        var climate = LegacyElevationClimateField.Generate(seed, gridSize);
        var hydrology = LegacyHydrologyField.Generate(climate);
        Spawn = LegacyWorldSpawn.Choose(hydrology);
        Terrain = new LegacyTileSampler(hydrology, Spawn);
        Chunks = new LegacyTileChunks(Terrain);
        Vegetation = new LegacyVegetation(Terrain);
    }

    public LegacyWorldSpawn Spawn { get; }
    public LegacyTileSampler Terrain { get; }
    public LegacyTileChunks Chunks { get; }
    public LegacyVegetation Vegetation { get; }

    // LegacyTileChunks is a plain cache; chunk generation runs off the UI thread.
    public LegacyTileSampler.Tile At(int x, int y)
    {
        lock (_gate) return Chunks.At(x, y);
    }

    public LegacyVegetation.Tree? TreeAt(int x, int y)
    {
        lock (_gate) return Vegetation.TreeAt(x, y);
    }

    public IReadOnlyList<string> DecorAt(int x, int y)
    {
        lock (_gate) return Vegetation.DecorAt(x, y);
    }

    public bool Buildable(int x, int y) => LegacyBiomeRules.Buildable(At(x, y).Biome);

    public static double Hash(int x, int y, int salt) => LegacyTerrainMath.TileHash(x, y, salt);
}
