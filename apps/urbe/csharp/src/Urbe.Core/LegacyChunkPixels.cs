namespace Urbe.Core;

/// <summary>
/// Pixel composition from the original Urbe 1.8.4-beta pixel-art.js chunkPixels.
/// Generates one 16x16 tile chunk (256x256 RGBA) on demand in memory. The
/// biomes, elevations and decoration are supplied by the original terrain
/// algorithms; there are no PNG assets, replacement palettes or vault writes.
/// </summary>
public static class LegacyChunkPixels
{
    public const int Tiles = LegacyTileChunks.ChunkSize;
    public const int Side = Tiles * LegacyWorldPixelTextures.TileSize;
    private const int TilePx = LegacyWorldPixelTextures.TileSize;
    private const int Halo = Tiles + 2;

    // Exactly the 19 biome IDs and four variants from pixel-art.js.
    private static readonly Lazy<byte[][][]> Textures = new(() =>
        LegacyWorldPixelTextures.Biomes
            .Select(id => Enumerable.Range(0, 4)
                .Select(variant => LegacyWorldPixelTextures.CreateTile(id, variant))
                .ToArray()).ToArray());

    public static byte[] Render(
        LegacyTileChunks world, LegacyVegetation vegetation,
        int chunkX, int chunkY)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(vegetation);
        return Render(world.At, vegetation.DecorAt, chunkX, chunkY);
    }

    /// <summary>
    /// Also accepts a deterministic terrain oracle for independent pixel tests.
    /// Negative chunks use floor coordinates, as in the original JavaScript.
    /// </summary>
    public static byte[] Render(
        Func<int, int, LegacyTileSampler.Tile> sample,
        Func<int, int, IReadOnlyList<string>> decor,
        int chunkX, int chunkY)
    {
        ArgumentNullException.ThrowIfNull(sample);
        ArgumentNullException.ThrowIfNull(decor);
        int x0 = checked(chunkX * Tiles), y0 = checked(chunkY * Tiles);
        var biome = new LegacyBiome[Halo * Halo];
        var elevation = new float[Halo * Halo];
        var slope = new float[Halo * Halo];
        var tint = new float[Halo * Halo];
        var variants = new byte[Halo * Halo];
        var result = new byte[Side * Side * 4];

        int Idx(int x, int y) => y * Halo + x;
        for (int y = 0; y < Halo; y++)
        for (int x = 0; x < Halo; x++)
        {
            int wx = x0 + x - 1, wy = y0 + y - 1;
            var tile = sample(wx, wy);
            int i = Idx(x, y);
            biome[i] = tile.Biome;
            // Original chunk() stores elevations into a Float32Array.
            elevation[i] = (float)tile.Elevation;
            variants[i] = (byte)Math.Floor(Hash(wx, wy, 5) * 4);
        }

        for (int y = 0; y < Halo; y++)
        for (int x = 0; x < Halo; x++)
        {
            int i = Idx(x, y);
            slope[i] = elevation[Idx(Math.Max(0, x - 1), Math.Max(0, y - 1))] -
                       elevation[Idx(Math.Min(Halo - 1, x + 1), Math.Min(Halo - 1, y + 1))];
            int wx = x0 + x - 1, wy = y0 + y - 1;
            tint[i] = (float)((ValueNoise(wx / 7d, wy / 7d, 91) - .5) * .16 +
                              (ValueNoise(wx / 2.5 + 50, wy / 2.5, 91) - .5) * .05);
        }

        for (int ty = 0; ty < Tiles; ty++)
        for (int tx = 0; tx < Tiles; tx++)
        {
            int k = Idx(tx + 1, ty + 1);
            var id = biome[k];
            int wx = x0 + tx, wy = y0 + ty;
            bool uniform = biome[k - 1] == id && biome[k + 1] == id &&
                           biome[k - Halo] == id && biome[k + Halo] == id &&
                           biome[k - Halo - 1] == id && biome[k - Halo + 1] == id &&
                           biome[k + Halo - 1] == id && biome[k + Halo + 1] == id;
            var own = Textures.Value[(int)id][variants[k]];
            double s00 = slope[k - Halo - 1] + slope[k - Halo] + slope[k - 1] + slope[k];
            double s10 = slope[k - Halo] + slope[k - Halo + 1] + slope[k] + slope[k + 1];
            double s01 = slope[k - 1] + slope[k] + slope[k + Halo - 1] + slope[k + Halo];
            double s11 = slope[k] + slope[k + 1] + slope[k + Halo] + slope[k + Halo + 1];
            double t00 = tint[k - Halo - 1] + tint[k - Halo] + tint[k - 1] + tint[k];
            double t10 = tint[k - Halo] + tint[k - Halo + 1] + tint[k] + tint[k + 1];
            double t01 = tint[k - 1] + tint[k] + tint[k + Halo - 1] + tint[k + Halo];
            double t11 = tint[k] + tint[k + 1] + tint[k + Halo] + tint[k + Halo + 1];

            for (int py = 0; py < TilePx; py++)
            {
                double fy = (py + .5) / TilePx;
                int row = ((ty * TilePx + py) * Side + tx * TilePx) * 4;
                double sl0 = s00 + (s01 - s00) * fy;
                double sl1 = s10 + (s11 - s10) * fy;
                double tn0 = t00 + (t01 - t00) * fy;
                double tn1 = t10 + (t11 - t10) * fy;

                for (int px = 0; px < TilePx; px++)
                {
                    int from = (py * TilePx + px) * 4, to = row + px * 4;
                    double fx = (px + .5) / TilePx;
                    double red, green, blue, shade = 0;
                    bool water;
                    double rocky = Rocky(id) ? 10 : 5;
                    if (uniform)
                    {
                        red = own[from];
                        green = own[from + 1];
                        blue = own[from + 2];
                        water = Water(id);
                        if (water)
                            shade = -Math.Clamp(
                                (LegacyBiomeRules.SeaLevel - elevation[k]) / .12, 0, 1) * .28;
                    }
                    else
                    {
                        int gx = wx * TilePx + px, gy = wy * TilePx + py;
                        double uu = tx + fx +
                            (ValueNoise(gx / 24d, gy / 24d, 301) - .5) * .7 +
                            (ValueNoise(gx / 11d, gy / 11d, 303) - .5) * .12;
                        double vv = ty + fy +
                            (ValueNoise(gx / 24d + 57, gy / 24d - 31, 302) - .5) * .7 +
                            (ValueNoise(gx / 11d - 19, gy / 11d + 23, 304) - .5) * .12;
                        double cu = uu - .5, cv = vv - .5;
                        int iu = Math.Clamp((int)Math.Floor(cu), -1, Tiles - 1);
                        int iv = Math.Clamp((int)Math.Floor(cv), -1, Tiles - 1);
                        double fu = Math.Clamp(cu - iu, 0, 1);
                        double fv = Math.Clamp(cv - iv, 0, 1);
                        int[] indices =
                        [
                            Idx(iu + 1, iv + 1), Idx(iu + 2, iv + 1),
                            Idx(iu + 1, iv + 2), Idx(iu + 2, iv + 2)
                        ];
                        double[] weights =
                        [
                            (1 - fu) * (1 - fv), fu * (1 - fv),
                            (1 - fu) * fv, fu * fv
                        ];
                        double wet = 0, most = -1;
                        int wetIndex = -1;
                        for (int q = 0; q < 4; q++)
                        {
                            if (!Water(biome[indices[q]])) continue;
                            wet += weights[q];
                            if (weights[q] > most)
                            {
                                most = weights[q];
                                wetIndex = indices[q];
                            }
                        }
                        if (wet > 0 && wet < 1)
                            wet += (ValueNoise(gx / 9d, gy / 9d, 305) - .5) * .16;
                        water = wet > .5;

                        if (water)
                        {
                            var wetTexture = Textures.Value[(int)biome[wetIndex]][variants[wetIndex]];
                            red = wetTexture[from];
                            green = wetTexture[from + 1];
                            blue = wetTexture[from + 2];
                            shade = -Math.Clamp(
                                (LegacyBiomeRules.SeaLevel - elevation[wetIndex]) / .12, 0, 1) * .28;
                            if (wet < .57 && Hash(gx, gy, 78) < .75)
                            {
                                red = 236;
                                green = 244;
                                blue = 246;
                                shade = 0;
                            }
                            else if (wet < .72)
                            {
                                shade += .12;
                            }
                        }
                        else
                        {
                            double au = Smooth(fu), av = Smooth(fv);
                            double[] blends =
                            [
                                (1 - au) * (1 - av), au * (1 - av),
                                (1 - au) * av, au * av
                            ];
                            double sr = 0, sg = 0, sb = 0, sum = 0, rockSum = 0;
                            for (int q = 0; q < 4; q++)
                            {
                                int selected = indices[q];
                                double weight = blends[q];
                                if (Water(biome[selected]) || weight <= 0) continue;
                                var texture = Textures.Value[(int)biome[selected]][variants[selected]];
                                sr += texture[from] * weight;
                                sg += texture[from + 1] * weight;
                                sb += texture[from + 2] * weight;
                                sum += weight;
                                rockSum += (Rocky(biome[selected]) ? 10 : 5) * weight;
                            }
                            if (sum > 0)
                            {
                                red = sr / sum;
                                green = sg / sum;
                                blue = sb / sum;
                                rocky = rockSum / sum;
                            }
                            else
                            {
                                int landIndex = indices[0];
                                for (int q = 0; q < 4; q++)
                                    if (!Water(biome[indices[q]]))
                                        landIndex = indices[q];
                                var fallback = Textures.Value[(int)biome[landIndex]][variants[landIndex]];
                                red = fallback[from];
                                green = fallback[from + 1];
                                blue = fallback[from + 2];
                            }
                        }
                    }

                    if (!water)
                    {
                        double sl = (sl0 + (sl1 - sl0) * fx) * .25;
                        double tn = (tn0 + (tn1 - tn0) * fx) * .25;
                        shade = Math.Clamp(sl * rocky, -.3, .3) + tn;
                    }

                    double factor = 1 + shade;
                    result[to] = Clamped(red * factor);
                    result[to + 1] = Clamped(green * factor);
                    result[to + 2] = Clamped(blue * factor);
                    result[to + 3] = 255;
                }
            }

            var details = decor(wx, wy);
            if (details is not null)
                for (int slot = 0; slot < details.Count; slot++)
                    LegacyWorldGroundDecor.Paint(
                        result, Side, tx * TilePx, ty * TilePx,
                        details[slot], wx, wy, slot);
        }
        return result;
    }

    private static bool Water(LegacyBiome biome) => biome <= LegacyBiome.Lake;
    private static bool Rocky(LegacyBiome biome) =>
        biome is LegacyBiome.Mountain or LegacyBiome.Peak or
            LegacyBiome.Hills or LegacyBiome.Snow;

    private static double Hash(int x, int y, int salt) =>
        LegacyWorldPixelTextures.Hash(x, y, salt);

    private static double Smooth(double t)
    {
        t = Math.Clamp(t, 0, 1);
        return t * t * (3 - 2 * t);
    }

    private static double ValueNoise(double x, double y, int salt)
    {
        int ix = (int)Math.Floor(x), iy = (int)Math.Floor(y);
        double fx = x - ix, fy = y - iy;
        double u = fx * fx * (3 - 2 * fx), v = fy * fy * (3 - 2 * fy);
        double a = Hash(ix, iy, salt), b = Hash(ix + 1, iy, salt);
        double c = Hash(ix, iy + 1, salt), d = Hash(ix + 1, iy + 1, salt);
        return a + (b - a) * u + (c - a) * v +
               (a - b - c + d) * u * v;
    }

    // Uint8ClampedArray stores round-to-even and saturates to [0,255].
    private static byte Clamped(double value) =>
        (byte)Math.Round(Math.Clamp(value, 0, 255), MidpointRounding.ToEven);
}
