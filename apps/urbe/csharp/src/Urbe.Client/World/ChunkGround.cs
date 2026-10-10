using Urbe.Core;

namespace Urbe.Client.World;

/// <summary>
/// RGBA of one 16×16-tile chunk (256×256 px), painted from the 1.8.4 textures
/// and ground decorations already ported to Urbe.Core.
///
/// PROVISIONAL (UC-33 spike): this paints the "uniform tile" path of
/// pixel-art.js chunkPixels only — no organic biome borders, ecotone blending,
/// slope shading or shore foam. The faithful port is LegacyChunkPixels
/// (UC-19, PR #428); when it is in main, <see cref="Paint"/> delegates to it and
/// this approximation is deleted. It must not be presented as visual parity.
/// </summary>
public static class ChunkGround
{
    public const int Side = LegacyWorld.ChunkTiles * LegacyWorld.TilePixels;

    private static readonly Lazy<byte[][][]> Textures = new(() =>
        LegacyWorldPixelTextures.Biomes
            .Select(id => Enumerable.Range(0, LegacyWorldPixelTextures.VariantCount)
                .Select(v => LegacyWorldPixelTextures.CreateTile(id, v)).ToArray())
            .ToArray());

    public static byte[] Paint(LegacyWorld world, int chunkX, int chunkY)
    {
        ArgumentNullException.ThrowIfNull(world);
        const int tiles = LegacyWorld.ChunkTiles, px = LegacyWorld.TilePixels;
        var output = new byte[Side * Side * 4];
        int x0 = chunkX * tiles, y0 = chunkY * tiles;
        for (int ty = 0; ty < tiles; ty++)
        for (int tx = 0; tx < tiles; tx++)
        {
            int wx = x0 + tx, wy = y0 + ty;
            var biome = world.At(wx, wy).Biome;
            // pixel-art.js VAR: Math.floor(hash(x, y, 5) * 4).
            int variant = (int)Math.Floor(LegacyWorld.Hash(wx, wy, 5) * 4);
            var texture = Textures.Value[(int)biome][variant];
            for (int row = 0; row < px; row++)
                Buffer.BlockCopy(texture, row * px * 4, output,
                    ((ty * px + row) * Side + tx * px) * 4, px * 4);

            var decor = world.DecorAt(wx, wy);
            for (int i = 0; i < decor.Count; i++)
                LegacyWorldGroundDecor.Paint(output, Side, tx * px, ty * px, decor[i], wx, wy, i);
        }
        return output;
    }
}
