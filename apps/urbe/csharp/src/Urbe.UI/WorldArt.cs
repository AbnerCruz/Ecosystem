namespace Urbe.UI;

/// <summary>Pure visual-only selectors: never stored in the vault.</summary>
public static class WorldArt
{
    public static string BuildingClass(string? path, bool isDistrict)
    {
        if (isDistrict) return "world-sprite-hall";
        unchecked
        {
            uint hash = 2166136261;
            foreach (var character in path ?? string.Empty)
            {
                hash ^= char.ToUpperInvariant(character);
                hash *= 16777619;
            }
            return (hash % 3) switch
            {
                0 => "world-sprite-red",
                1 => "world-sprite-ochre",
                _ => "world-sprite-slate"
            };
        }
    }

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
