namespace Urbe.Core;

/// <summary>
/// Pixel-exact, deterministic 16×16 RGBA ground textures from the Urbe
/// 1.8.4-beta world/pixel-art.js. This is a portable, read-only graphics input;
/// it does not generate or persist terrain and does not render the city.
/// </summary>
public static class LegacyWorldPixelTextures
{
    public const int TileSize = 16;
    public const int VariantCount = 4;

    private static readonly string[] Ids =
    [
        "deep", "sea", "river", "lake", "beach", "grass", "meadow",
        "forest", "dense", "swamp", "taiga", "tundra", "snow",
        "hills", "mountain", "peak", "desert", "savanna", "steppe"
    ];

    // Each row corresponds to Ids: base, shadow, highlight, detail.
    // These values are intentionally identical to PAL in the legacy renderer.
    private static readonly string[][] Palettes =
    [
        ["#23466f", "#1f3f66", "#284f7a", "#2c5683"],
        ["#3b77ad", "#356ea3", "#4282b8", "#5594c6"],
        ["#4a8cc2", "#4383ba", "#5597cb", "#79b3dc"],
        ["#4a8cc2", "#4383ba", "#5597cb", "#79b3dc"],
        ["#e2d09b", "#d6c38b", "#ead9a8", "#c9b47a"],
        ["#6e9a47", "#628c3f", "#7aa652", "#86b25b"],
        ["#7aa550", "#6d9846", "#86b15a", "#93bd63"],
        ["#557f38", "#4a7331", "#608b41", "#3f6a2c"],
        ["#436b2e", "#3a6128", "#4c7634", "#335a24"],
        ["#5c6a41", "#525f39", "#66744a", "#48675e"],
        ["#5a7250", "#506848", "#657d59", "#46604a"],
        ["#9ba48c", "#8f9881", "#a7b097", "#b8bfab"],
        ["#eef2f5", "#e1e8ee", "#f7f9fb", "#cfd9e2"],
        ["#86934f", "#7a8747", "#929f59", "#6f7b43"],
        ["#8b8279", "#7c736b", "#9a9188", "#6a625b"],
        ["#b8b3ad", "#a8a29c", "#e9edf0", "#948e88"],
        ["#dcc487", "#d0b779", "#e6d196", "#c3a86a"],
        ["#c3b264", "#b6a45a", "#cfbe70", "#a99650"],
        ["#aead6f", "#a0a063", "#bab97b", "#939556"]
    ];

    public static IReadOnlyList<string> Biomes { get; } = Array.AsReadOnly(Ids);

    /// <summary>
    /// Returns an independent 1,024-byte RGBA buffer in row-major order.
    /// Neither the palette nor this buffer is a vault authority.
    /// </summary>
    public static byte[] CreateTile(string biome, int variant)
    {
        ArgumentNullException.ThrowIfNull(biome);
        var index = Array.IndexOf(Ids, biome);
        if (index < 0)
            throw new ArgumentException("Bioma desconhecido.", nameof(biome));
        if ((uint)variant >= VariantCount)
            throw new ArgumentOutOfRangeException(nameof(variant));

        var palette = Palettes[index].Select(ParseRgb).ToArray();
        var seed = index * 97 + variant * 13;
        var result = new byte[TileSize * TileSize * 4];

        for (var y = 0; y < TileSize; y++)
        for (var x = 0; x < TileSize; x++)
        {
            var random = Hash(x, y, seed);
            var color = palette[0];
            if (index <= 3)
            {
                var wave = (y + (int)Math.Floor(Hash(x / 5, y, seed) * 3)) % 5 == 0
                           && Hash(x, y, seed + 1) < 0.55;
                color = wave ? palette[3] :
                    random < 0.18 ? palette[1] :
                    random > 0.9 ? palette[2] : palette[0];
            }
            else if (biome is "snow" or "peak")
            {
                color = random < 0.12 ? palette[1] :
                    random > 0.94 ? palette[2] : palette[0];
                if (biome == "peak" && Hash(x >> 2, y >> 2, seed) < 0.35)
                    color = palette[random < 0.5 ? 3 : 1];
            }
            else if (biome is "mountain" or "hills")
            {
                color = random < 0.2 ? palette[1] :
                    random > 0.85 ? palette[2] : palette[0];
                if (Hash(x >> 1, y >> 2, seed + 3) < 0.08)
                    color = palette[3];
            }
            else if (biome is "beach" or "desert")
            {
                color = random < 0.14 ? palette[1] :
                    random > 0.9 ? palette[2] : palette[0];
                if (Hash(x, y, seed + 9) < 0.03)
                    color = palette[3];
                if (biome == "desert" && (y + x / 4) % 7 == 0 && random < 0.5)
                    color = palette[2];
            }
            else
            {
                color = random < 0.2 ? palette[1] :
                    random > 0.86 ? palette[2] : palette[0];
                if (Hash(x, y >> 1, seed + 5) < 0.07)
                    color = palette[3];
                if (y > 0 &&
                    Hash(x, y - 1, seed + 5) < 0.07 &&
                    Hash(x, y, seed + 6) < 0.5)
                    color = palette[1];
            }

            var offset = (y * TileSize + x) * 4;
            result[offset] = color.R;
            result[offset + 1] = color.G;
            result[offset + 2] = color.B;
            result[offset + 3] = 255;
        }

        return result;
    }

    private static Rgb ParseRgb(string hex) => new(
        Convert.ToByte(hex.Substring(1, 2), 16),
        Convert.ToByte(hex.Substring(3, 2), 16),
        Convert.ToByte(hex.Substring(5, 2), 16));

    // JavaScript Math.imul(), signed 32-bit wrap and >>> unsigned shift.
    // Do not replace with System.HashCode, Random or unbounded integer math:
    // the resulting pixels would no longer match Urbe 1.8.4-beta.
    internal static double Hash(int x, int y, int seed)
    {
        var h = unchecked((uint)(
            x * 374761393 + y * 668265263 +
            seed * unchecked((int)2246822519u)));
        h = unchecked((h ^ (h >> 13)) * 1274126177u);
        return (h ^ (h >> 16)) / 4294967296d;
    }

    private readonly record struct Rgb(byte R, byte G, byte B);
}
