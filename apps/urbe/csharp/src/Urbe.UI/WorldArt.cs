using System.Collections.Concurrent;
using Urbe.Core;

namespace Urbe.UI;

/// <summary>
/// Original procedural artwork computed by C# at runtime; a data URI is only
/// a transport format between C# and MAUI's WebView, never the source asset.
/// </summary>
public static class WorldArt
{
    private static readonly ConcurrentDictionary<string, string> Cache =
        new(StringComparer.Ordinal);

    private static string Png(string key, int w, int h, Func<byte[]> paint) =>
        Cache.GetOrAdd(key, _ =>
            "data:image/png;base64," +
            Convert.ToBase64String(LegacyWorldSprites.EncodePng(paint(), w, h)));

    private static string Css(string uri) => "background-image:url('" + uri + "');";

    public static string BoardBackgroundStyle => Css(GroundImageUrl(0));

    public static string GroundImageUrl(int variant)
    {
        if ((uint)variant >= 4)
            throw new ArgumentOutOfRangeException(nameof(variant));
        return Png("ground/" + variant, 16, 16,
            () => LegacyWorldPixelTextures.CreateTile("grass", variant));
    }

    public static string TerrainBackgroundStyle(int column, int row) =>
        Css(GroundImageUrl((int)(LegacySeed(column + "/" + row) % 4)));

    public static string DecorationBackgroundStyle(int column, int row)
    {
        var kind = TerrainClass(column, row);
        var seed = column + "/" + row;
        if (kind == "world-terrain-tree")
        {
            var variant = (int)(LegacySeed(seed) % 3);
            return Css(Png("oak/" + variant, 24, 32,
                () => LegacyWorldSprites.CreateTree("oak", variant)));
        }
        if (kind == "world-terrain-flowers")
            return Css(Png("flowers/" + seed, 16, 16, () =>
            {
                var rgba = new byte[16 * 16 * 4];
                LegacyWorldGroundDecor.Paint(
                    rgba, 16, 0, 0, "flowers", column, row);
                return rgba;
            }));

        return "background-image:none;";
    }

    public static string BuildingImageUrl(
        string? path, bool isDistrict, string? savedSprite = null)
    {
        var variant = isDistrict ? 0 : savedSprite switch
        {
            "house1" => 0,
            "house2" => 1,
            "house3" => 2,
            _ => (int)(LegacySeed(path) % 3)
        };
        var type = isDistrict ? "hall" : "house";
        return Png(type + "/temperate/" + variant, 48, 56,
            () => LegacyWorldBuildings.CreateBuilding(type, "temperate", variant));
    }

    public static string BuildingBackgroundStyle(
        string? path, bool isDistrict, string? savedSprite = null) =>
        Css(BuildingImageUrl(path, isDistrict, savedSprite));

    // Kept for existing selection/layout selectors; no static image lookup.
    public static string BuildingClass(string? path, bool isDistrict, string? savedSprite = null)
    {
        if (isDistrict) return "world-sprite-hall";
        var variant = savedSprite switch
        {
            "house1" => 0u,
            "house2" => 1u,
            "house3" => 2u,
            _ => LegacySeed(path) % 3u
        };
        return variant switch
        {
            0 => "world-sprite-red",
            1 => "world-sprite-ochre",
            _ => "world-sprite-slate"
        };
    }

    // Same JS UTF-16 FNV-1a algorithm used by app.js.
    public static uint LegacySeed(string? value)
    {
        unchecked
        {
            uint hash = 2166136261;
            foreach (var character in value ?? string.Empty)
            {
                hash ^= character;
                hash *= 16777619;
            }
            return hash;
        }
    }

    // Provisional placement from the current interactive UI, not the
    // original full biomes/elevation/chunks terrain system.
    public static string TerrainClass(int column, int row)
    {
        var pattern = (column * 7 + row * 13 + column * row) % 11;
        return pattern switch
        {
            0 or 4 => "world-terrain-tree",
            6 or 9 => "world-terrain-flowers",
            _ => "world-terrain-meadow"
        };
    }
}
