namespace Urbe.Core;

/// <summary>
/// Urbe 1.8.4-beta pixel-art.js chunkFarPixels: the half-resolution ground of a chunk
/// (8 px per tile) with tree canopies and their shadows painted in. app.js draws it
/// instead of the full chunk and the individual trees when the camera zoom is below .5.
/// Writes follow Uint8ClampedArray rules (clamp, round half to even).
/// </summary>
public static class LegacyChunkFarPixels
{
    public const int Side = LegacyChunkPixels.Side / 2;

    private static readonly Dictionary<string, byte[][]> Canopy = new(StringComparer.Ordinal)
    {
        ["oak"] = Rgb("#355f27", "#4f8636", "#72a84c"),
        ["birch"] = Rgb("#44762f", "#6fa246", "#9ac765"),
        ["pine"] = Rgb("#1f4230", "#2f5a3c", "#467a4c"),
        ["deadpine"] = Rgb("#463b31", "#62533f", "#7e6c58"),
        ["acacia"] = Rgb("#56632a", "#788a38", "#9dad4e"),
        ["cactus"] = Rgb("#34603a", "#4f8646", "#73aa5e"),
        ["willow"] = Rgb("#435f2b", "#63873f", "#88ad58"),
        ["palm"] = Rgb("#357030", "#559340", "#7cbb5a")
    };
    private static readonly byte[] SnowColor = Rgb("#eef3f6")[0];

    public static byte[] Render(byte[] ground, Func<int, int, LegacyVegetation.Tree?> treeAt, int chunkX, int chunkY)
    {
        ArgumentNullException.ThrowIfNull(ground);
        ArgumentNullException.ThrowIfNull(treeAt);
        const int ch = LegacyChunkPixels.Tiles, N = LegacyChunkPixels.Side, H = Side;
        if (ground.Length != N * N * 4) throw new ArgumentException("Chunk RGBA inválido.", nameof(ground));
        const double sc = LegacyWorldPixelTextures.TileSize / 2.0;
        var output = new byte[H * H * 4];
        int x0 = chunkX * ch, y0 = chunkY * ch;
        for (int y = 0; y < H; y++)
        for (int x = 0; x < H; x++)
        {
            int o = (y * H + x) * 4, a = ((y * 2) * N + x * 2) * 4, b = a + 4, c = a + N * 4, d = c + 4;
            for (int k = 0; k < 3; k++) output[o + k] = (byte)((ground[a + k] + ground[b + k] + ground[c + k] + ground[d + k]) >> 2);
            output[o + 3] = 255;
        }

        var list = new List<(int X, int Y, LegacyVegetation.Tree T, int I)>();
        for (int j = -1; j <= ch; j++)
        for (int i = -1; i <= ch; i++)
        {
            int wx = x0 + i, wy = y0 + j;
            if (treeAt(wx, wy) is { } t) list.Add((wx, wy, t, list.Count));
        }
        list = list.OrderBy(p => p.Y).ThenBy(p => p.X).ThenBy(p => p.I).ToList();

        void Put(double px, double py, byte[] color, double f)
        {
            int ix = (int)px, iy = (int)py;
            if (px < 0 || py < 0 || px >= H || py >= H) return;
            int o = (iy * H + ix) * 4;
            output[o] = Clamped(color[0] * f);
            output[o + 1] = Clamped(color[1] * f);
            output[o + 2] = Clamped(color[2] * f);
        }
        void Shadow(double px, double py, double f)
        {
            int ix = (int)px, iy = (int)py;
            if (px < 0 || py < 0 || px >= H || py >= H) return;
            int o = (iy * H + ix) * 4;
            output[o] = Clamped(output[o] * f);
            output[o + 1] = Clamped(output[o + 1] * f);
            output[o + 2] = Clamped(output[o + 2] * f);
        }

        foreach (var (wx, wy, t, _) in list)
        {
            var cols = Canopy.TryGetValue(t.Kind, out var known) ? known : Canopy["oak"];
            double jx = (LegacyTerrainMath.TileHash(wx, wy, 31) - .5) * .65, jy = (LegacyTerrainMath.TileHash(wx, wy, 32) - .5) * .4;
            double ccx = (wx - x0 + .5 + jx) * sc, ccy = (wy - y0 + .35 + jy) * sc;
            double r = (t.Kind == "cactus" ? 1.7 : t.Kind == "deadpine" ? 2.5 : t.Kind == "pine" ? 3.8 : 4.5 + t.Variant * .5) * (sc / 8), r2 = r * r;
            // shadow cast to the south-east
            for (double dy = -r; dy <= r; dy++)
            for (double dx = -r; dx <= r; dx++)
            {
                if (dx * dx + dy * dy > r2) continue;
                Shadow(LegacyJsMath.Round(ccx + dx + 1.5), LegacyJsMath.Round(ccy + dy + 2), .72);
            }
            for (double dy = -r; dy <= r; dy++)
            for (double dx = -r; dx <= r; dx++)
            {
                double dd = dx * dx + dy * dy;
                if (dd > r2) continue;
                double l = (-dx - dy) / (r * 1.6);
                var c = l > .35 ? cols[2] : l < -.35 ? cols[0] : cols[1];
                if (t.Snow && l > .1) c = SnowColor;
                double rx = LegacyJsMath.Round(ccx + dx), ry = LegacyJsMath.Round(ccy + dy);
                double jit = 1 + (LegacyTerrainMath.TileHash((int)rx + wx * 31, (int)ry + wy * 17, 55) - .5) * .12;
                Put(rx, ry, c, jit);
            }
            if (t.Kind is "pine" or "deadpine")
                Put(LegacyJsMath.Round(ccx - r * .3), LegacyJsMath.Round(ccy - r * .4), cols[2], 1.05);
        }
        return output;
    }

    /// <summary>Uint8ClampedArray store: clamp to [0, 255], round half to even, NaN → 0.</summary>
    private static byte Clamped(double v)
    {
        if (double.IsNaN(v) || v <= 0) return 0;
        if (v >= 255) return 255;
        return (byte)Math.Round(v, MidpointRounding.ToEven);
    }

    private static byte[][] Rgb(params string[] hex) =>
        hex.Select(h => new[] { Convert.ToByte(h.Substring(1, 2), 16), Convert.ToByte(h.Substring(3, 2), 16), Convert.ToByte(h.Substring(5, 2), 16) }).ToArray();
}
