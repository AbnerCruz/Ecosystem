namespace Urbe.Core;

/// <summary>
/// Camera-independent chunk clipping for the 1.8.4-beta continuous terrain.
/// Coordinates are absolute 16px terrain tiles, not 8x8 house layout cells.
/// No viewport state is persisted to the user's vault.
/// </summary>
public static class LegacyChunkViewport
{
    public sealed record Chunk(int X, int Y, int LeftTiles, int TopTiles);
    public const int MaxAxisTiles = 48;

    public static IReadOnlyList<Chunk> Visible(
        int tileX, int tileY, int widthTiles, int heightTiles)
    {
        if (widthTiles is < 1 or > MaxAxisTiles)
            throw new ArgumentOutOfRangeException(nameof(widthTiles));
        if (heightTiles is < 1 or > MaxAxisTiles)
            throw new ArgumentOutOfRangeException(nameof(heightTiles));

        int lastX = checked(tileX + widthTiles - 1);
        int lastY = checked(tileY + heightTiles - 1);
        int firstChunkX = LegacyTileChunks.ChunkCoordinate(tileX);
        int lastChunkX = LegacyTileChunks.ChunkCoordinate(lastX);
        int firstChunkY = LegacyTileChunks.ChunkCoordinate(tileY);
        int lastChunkY = LegacyTileChunks.ChunkCoordinate(lastY);
        var visible = new List<Chunk>();
        for (int cy = firstChunkY; cy <= lastChunkY; cy++)
        for (int cx = firstChunkX; cx <= lastChunkX; cx++)
        {
            visible.Add(new Chunk(
                cx, cy,
                checked(cx * LegacyTileChunks.ChunkSize - tileX),
                checked(cy * LegacyTileChunks.ChunkSize - tileY)));
        }
        return visible;
    }
}
