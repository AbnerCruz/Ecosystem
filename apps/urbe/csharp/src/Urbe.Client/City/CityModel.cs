using Urbe.Client.World;
using Urbe.Core;

namespace Urbe.Client.City;

public sealed record CityNote(string Path, string Content)
{
    public string Name => System.IO.Path.GetFileNameWithoutExtension(Path);
    public string Folder => Path.Contains('/') ? Path[..Path.LastIndexOf('/')] : "";
}

/// <summary>A note drawn as a 3×3 house (app.js world.buildings, tipo 'nota').</summary>
public sealed class CityHouse(CityNote note, int x, int y)
{
    public CityNote Note { get; set; } = note;
    public int X { get; } = x;
    public int Y { get; } = y;
    public int W => 3;
    public int H => 3;
    public bool Contains(int tx, int ty) => tx >= X && tx < X + W && ty >= Y && ty < Y + H;
}

/// <summary>A folder drawn as a neighbourhood around its houses.</summary>
public sealed record CityDistrict(string Path, int X, int Y, int W, int H)
{
    public string Name => Path.Contains('/') ? Path[(Path.LastIndexOf('/') + 1)..] : Path;
}

/// <summary>
/// Notes → houses and folders → neighbourhoods, on buildable terrain near the spawn.
/// PROVISIONAL (UC-33 spike): a deterministic block layout. The 1.8.4 lot rule,
/// organic regions and roads from [[links]] are UC-19 and must come from the
/// original algorithms, not from this layout.
/// </summary>
public sealed class CityModel
{
    private const int Step = 5; // 3×3 house + 2 tiles of spacing (app.js LOTE_GAP).

    public List<CityHouse> Houses { get; } = [];
    public List<CityDistrict> Districts { get; } = [];

    public static CityModel Build(IEnumerable<CityNote> notes, LegacyWorld world)
    {
        ArgumentNullException.ThrowIfNull(notes);
        ArgumentNullException.ThrowIfNull(world);
        var model = new CityModel();
        var groups = notes.GroupBy(n => n.Folder)
            .OrderBy(g => g.Key.Length == 0 ? 0 : 1).ThenBy(g => g.Key, StringComparer.Ordinal)
            .ToList();
        // Districts are packed in rows around the 1.8.4 base area (72×50 tiles).
        int cursorX = 4, cursorY = 4, rowHeight = 0;
        const int rowWidth = 64;
        foreach (var group in groups)
        {
            var items = group.OrderBy(n => n.Path, StringComparer.Ordinal).ToList();
            int columns = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(items.Count)));
            int rows = (int)Math.Ceiling(items.Count / (double)columns);
            int width = columns * Step + 1, height = rows * Step + 2;
            if (cursorX + width > rowWidth) { cursorX = 4; cursorY += rowHeight + 3; rowHeight = 0; }
            int placed = 0, slot = 0;
            var houses = new List<CityHouse>();
            while (placed < items.Count && slot < items.Count * 4)
            {
                int hx = cursorX + 1 + slot % columns * Step;
                int hy = cursorY + 2 + slot / columns * Step;
                slot++;
                if (!LotIsBuildable(world, hx, hy)) continue;
                houses.Add(new CityHouse(items[placed++], hx, hy));
            }
            model.Houses.AddRange(houses);
            if (group.Key.Length > 0 && houses.Count > 0)
            {
                int x0 = houses.Min(h => h.X) - 1, y0 = houses.Min(h => h.Y) - 2;
                int x1 = houses.Max(h => h.X + h.W) + 1, y1 = houses.Max(h => h.Y + h.H) + 1;
                model.Districts.Add(new CityDistrict(group.Key, x0, y0, x1 - x0, y1 - y0));
            }
            cursorX += width + 3;
            rowHeight = Math.Max(rowHeight, (int)Math.Ceiling(slot / (double)columns) * Step + 2);
        }
        return model;
    }

    public CityHouse? HouseAt(int tx, int ty)
    {
        for (int i = Houses.Count - 1; i >= 0; i--)
            if (Houses[i].Contains(tx, ty)) return Houses[i];
        return null;
    }

    public bool Occupied(int tx, int ty) =>
        HouseAt(tx, ty) is not null || HouseAt(tx, ty - 1) is not null;

    /// <summary>app.js urbeArteDaConstrucao for a Markdown note.</summary>
    public static (string Kind, string Style, int Variant, bool Flowers) Art(CityHouse house, LegacyWorld world)
    {
        var biome = world.At(house.X + house.W / 2, house.Y + house.H / 2).Biome;
        var style = LegacyWorldBuildings.StyleForBiome(LegacyBiomeRules.Id(biome));
        int variant = (int)(LegacyTerrainMath.Hash32(house.Note.Path) % 3);
        bool flowers = LegacyTerrainMath.Hash32(house.Note.Name) % 3 == 0;
        return ("house", style, variant, flowers);
    }

    private static bool LotIsBuildable(LegacyWorld world, int x, int y)
    {
        for (int ty = y; ty < y + 4; ty++)
        for (int tx = x; tx < x + 3; tx++)
            if (!world.Buildable(tx, ty)) return false;
        return true;
    }
}
