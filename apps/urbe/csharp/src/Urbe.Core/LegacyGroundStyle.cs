using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Urbe.Core;

/// <summary>
/// UrbeArt.configure(cfg) of src/world/pixel-art.js: the ground palette per biome (base, shadow,
/// light, detail; a lone base colour derives the others in the original proportions) and the
/// biome textures (the generated ones with that palette, or the pack's own 16×16 images).
/// <see cref="LegacyChunkPixels"/> paints the world with it; null is the original art.
/// </summary>
public sealed class LegacyGroundStyle
{
    /// <summary>[biome index in <see cref="LegacyWorldPixelTextures.Biomes"/>][variant 0..3] → 1,024-byte RGBA.</summary>
    public byte[][][] Textures { get; }
    /// <summary>PAL after configure: four hex colours per biome.</summary>
    public IReadOnlyDictionary<string, string[]> Palette { get; }

    private LegacyGroundStyle(byte[][][] textures, Dictionary<string, string[]> palette) { Textures = textures; Palette = palette; }

    /// <summary>The original art as a style (PAL0 and generated textures).</summary>
    public static LegacyGroundStyle Original { get; } = Configure(null, null);

    /// <summary>The base colour of a biome (PAL[id][0], also the minimap colour).</summary>
    public string BaseColor(string biome) => Palette[biome][0];

    private static bool IsHex(string? c) => c is not null && Regex.IsMatch(c, "^#[0-9a-fA-F]{6}$");

    private static int[] Rgb(string h) => [Convert.ToInt32(h[1..3], 16), Convert.ToInt32(h[3..5], 16), Convert.ToInt32(h[5..7], 16)];

    /// <summary>configure({palette, textures}): <paramref name="palette"/> biome id → colours, <paramref name="textures"/> biome id → RGBA 16×16 images.</summary>
    public static LegacyGroundStyle Configure(
        IReadOnlyDictionary<string, IReadOnlyList<string>>? palette,
        IReadOnlyDictionary<string, IReadOnlyList<byte[]>>? textures)
    {
        var ids = LegacyWorldPixelTextures.Biomes;
        var pal = new Dictionary<string, string[]>();
        var all = new byte[ids.Count][][];
        for (int n = 0; n < ids.Count; n++)
        {
            var id = ids[n];
            var baseColors = LegacyWorldPixelTextures.DefaultPalette(id);
            IReadOnlyList<string>? p = null;
            palette?.TryGetValue(id, out p);
            string? P(int i) => p is not null && i < p.Count ? p[i] : null;
            var colors = new string[4];
            for (int i = 0; i < 4; i++)
            {
                if (IsHex(P(i))) { colors[i] = P(i)!; continue; }
                if (!IsHex(P(0))) { colors[i] = baseColors[i]; continue; }
                var b0 = Rgb(baseColors[0]); var bi = Rgb(baseColors[i]); var c = Rgb(P(0)!);
                colors[i] = "#" + string.Concat(c.Select((v, k) =>
                {
                    double f = b0[k] != 0 ? (double)bi[k] / b0[k] : 1;
                    return Math.Max(0, Math.Min(255, (int)Math.Floor(v * f + .5))).ToString("x2", CultureInfo.InvariantCulture);
                }));
            }
            pal[id] = colors;
            IReadOnlyList<byte[]>? own = null;
            textures?.TryGetValue(id, out own);
            var t = own?.Where(a => a is { Length: LegacyWorldPixelTextures.TileSize * LegacyWorldPixelTextures.TileSize * 4 }).ToArray() ?? [];
            all[n] = new byte[4][];
            for (int v = 0; v < 4; v++)
            {
                if (t.Length > 0)
                {
                    var d = (byte[])t[v % t.Length].Clone();
                    for (int o = 3; o < d.Length; o += 4) d[o] = 255;
                    all[n][v] = d;
                }
                else all[n][v] = LegacyWorldPixelTextures.CreateTile(id, v, colors);
            }
        }
        return new LegacyGroundStyle(all, pal);
    }
}
