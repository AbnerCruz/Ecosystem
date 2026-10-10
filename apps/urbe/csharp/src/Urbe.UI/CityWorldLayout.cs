using Urbe.Core;

namespace Urbe.UI;

/// <summary>
/// Viewport projection of ALL Urbe lots, not a 64-cell board. Saved physical
/// map coordinates win; implicit notes/folders have stable, deterministic
/// homes, so panning never changes their locations or vault metadata.
/// Locations are bairro-local and match the existing .urbe/mapa.json contract.
/// </summary>
public static class CityWorldLayout
{
    public const int MaxCoordinate = 4096;
    public const int MaxViewportTiles = 48;
    public const int ImplicitColumns = 8;

    public static IReadOnlyList<CityTileLayout.Tile> Visible(
        IEnumerable<WorkspaceExplorerEntry> entries,
        WorldProjection world, int firstX, int firstY,
        int width, int height, bool includeVacant)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(world);
        if (width is < 1 or > MaxViewportTiles ||
            height is < 1 or > MaxViewportTiles)
            throw new ArgumentOutOfRangeException(nameof(width));

        int lastX = checked(firstX + width - 1);
        int lastY = checked(firstY + height - 1);
        var source = entries
            .Where(e => e.IsFolder || ArtifactModel.IsNote(e.Path))
            .OrderByDescending(e => e.IsFolder)
            .ThenBy(e => e.Path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var occupied = new Dictionary<(int X, int Y), WorkspaceExplorerEntry>();
        var assigned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Explicit positions reserve their slots even when outside viewport.
        foreach (var entry in source.Where(e => !e.IsFolder))
        {
            var spatial = world.ProjectDocument(entry.Path);
            if (spatial?.X is not double x || spatial.Y is not double y ||
                !double.IsFinite(x) || !double.IsFinite(y) ||
                x != Math.Floor(x) || y != Math.Floor(y) ||
                x < -MaxCoordinate || x > MaxCoordinate ||
                y < -MaxCoordinate || y > MaxCoordinate)
                continue;
            if (occupied.TryAdd(((int)x, (int)y), entry))
                assigned.Add(entry.Path);
        }

        // Assign overflow after the first 64 plots instead of hiding notes.
        int cursor = 0;
        foreach (var entry in source)
        {
            if (assigned.Contains(entry.Path))
                continue;
            while (occupied.ContainsKey((cursor % ImplicitColumns,
                        cursor / ImplicitColumns)))
                cursor++;
            occupied[(cursor % ImplicitColumns, cursor / ImplicitColumns)] = entry;
            assigned.Add(entry.Path);
            cursor++;
        }

        var result = new List<CityTileLayout.Tile>();
        if (includeVacant)
        {
            for (int y = firstY; y <= lastY; y++)
            for (int x = firstX; x <= lastX; x++)
            {
                if (x is < -MaxCoordinate or > MaxCoordinate ||
                    y is < -MaxCoordinate or > MaxCoordinate)
                    continue;
                occupied.TryGetValue((x, y), out var entry);
                result.Add(new CityTileLayout.Tile(x, y, entry));
            }
        }
        else
        {
            foreach (var item in occupied)
                if (item.Key.X >= firstX && item.Key.X <= lastX &&
                    item.Key.Y >= firstY && item.Key.Y <= lastY)
                    result.Add(new CityTileLayout.Tile(
                        item.Key.X, item.Key.Y, item.Value));
            result.Sort((a, b) => a.Row == b.Row
                ? a.Column.CompareTo(b.Column) : a.Row.CompareTo(b.Row));
        }

        return result.AsReadOnly();
    }
}
