using Lunet.Graphics;
using Lunet.Pathfinding;

namespace Lunet.Studio;

public enum AutotileLayout
{
    /// <summary>16 tiles: base + mask of the 4 sides (N=1, E=2, S=4, W=8). Simple terrains, paths, walls.</summary>
    Sides16,
    /// <summary>47 tiles ("blob"): sides plus corners (a corner counts only when both of its sides connect). Smooth terrains.</summary>
    Blob47,
}

/// <summary>
/// An autotile terrain (LUNET-506): consecutive tiles of the tileset, from <see cref="BaseTile"/>, in the standard order of
/// <see cref="Layout"/>. Painting the terrain picks the right edge/corner tile for each cell from its neighbours.
/// </summary>
public sealed record AutotileSet(string Name, int BaseTile, AutotileLayout Layout)
{
    public int Count => Layout == AutotileLayout.Sides16 ? 16 : 47;
    public bool Contains(int tile) => tile >= BaseTile && tile < BaseTile + Count;

    private static readonly int[] BlobIndex = BuildBlobIndex(out _);
    /// <summary>The 47 reduced 8-neighbour masks in tile order (the order a blob tileset is drawn in).</summary>
    public static IReadOnlyList<int> BlobMasks { get; } = BuildBlobMasks();

    private static int Reduce(int mask)
    {
        // bits: N=1, NE=2, E=4, SE=8, S=16, SW=32, W=64, NW=128; a corner counts only with both adjacent sides.
        bool n = (mask & 1) != 0, e = (mask & 4) != 0, s = (mask & 16) != 0, w = (mask & 64) != 0;
        int r = mask & (1 | 4 | 16 | 64);
        if (n && e && (mask & 2) != 0) r |= 2;
        if (s && e && (mask & 8) != 0) r |= 8;
        if (s && w && (mask & 32) != 0) r |= 32;
        if (n && w && (mask & 128) != 0) r |= 128;
        return r;
    }

    private static int[] BuildBlobMasks() => Enumerable.Range(0, 256).Select(Reduce).Distinct().Order().ToArray();

    private static int[] BuildBlobIndex(out int[] masks)
    {
        masks = BuildBlobMasks();
        var index = new int[256];
        for (int m = 0; m < 256; m++) index[m] = Array.IndexOf(masks, Reduce(m));
        return index;
    }

    /// <summary>The tile for a cell given which neighbours are the same terrain.</summary>
    public int TileFor(bool n, bool ne, bool e, bool se, bool s, bool sw, bool w, bool nw)
    {
        if (Layout == AutotileLayout.Sides16)
            return BaseTile + (n ? 1 : 0) + (e ? 2 : 0) + (s ? 4 : 0) + (w ? 8 : 0);
        int mask = (n ? 1 : 0) | (ne ? 2 : 0) | (e ? 4 : 0) | (se ? 8 : 0) | (s ? 16 : 0) | (sw ? 32 : 0) | (w ? 64 : 0) | (nw ? 128 : 0);
        return BaseTile + BlobIndex[mask];
    }

    /// <summary>Re-picks the tile of one cell of a layer (no-op when the cell is not this terrain).</summary>
    public TileMap Resolve(TileMap map, int layer, int x, int y)
    {
        if ((uint)x >= (uint)map.Width || (uint)y >= (uint)map.Height || !Contains(map.GetTile(layer, new GridPoint(x, y)))) return map;
        bool Is(int dx, int dy)
        {
            int cx = x + dx, cy = y + dy;
            // Outside the map counts as the same terrain, so terrain touching the border has no false edge.
            return (uint)cx >= (uint)map.Width || (uint)cy >= (uint)map.Height || Contains(map.GetTile(layer, new GridPoint(cx, cy)));
        }
        int tile = TileFor(Is(0, -1), Is(1, -1), Is(1, 0), Is(1, 1), Is(0, 1), Is(-1, 1), Is(-1, 0), Is(-1, -1));
        return map.WithTile(layer, new GridPoint(x, y), tile);
    }

    /// <summary>Paints (or with <paramref name="erase"/>, clears) the terrain at a cell and fixes it and its 8 neighbours.</summary>
    public TileMap Paint(TileMap map, int layer, int x, int y, bool erase = false)
    {
        if ((uint)x >= (uint)map.Width || (uint)y >= (uint)map.Height) return map;
        map = map.WithTile(layer, new GridPoint(x, y), erase ? 0 : BaseTile + (Layout == AutotileLayout.Sides16 ? 15 : Count - 1));
        for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
                map = Resolve(map, layer, x + dx, y + dy);
        return map;
    }
}
