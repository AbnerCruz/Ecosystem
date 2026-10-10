namespace Urbe.UI;

/// <summary>1.8.4-beta sprite compatibility. Artwork comes from the original Canvas, not new SVGs.</summary>
public static class WorldArt
{
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

    // Same arithmetic as app.js semente(): FNV-1a, JS UTF-16 code units.
    public static uint LegacySeed(string? text)
    {
        unchecked
        {
            uint hash = 2166136261;
            foreach (var character in text ?? string.Empty)
            {
                hash ^= character;
                hash *= 16777619;
            }
            return hash;
        }
    }

    // Provisional geography until the terrain.js/C# port, NOT full visual parity.
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
