using Urbe.Core;

namespace Urbe.UI;

/// <summary>
/// A bounded, deterministic top-down lot layout. Physical note coordinates in
/// .urbe/mapa.json take precedence; other entries fill unoccupied tiles.
/// No filesystem or browser APIs are involved.
/// </summary>
public static class CityTileLayout
{
    public const int Columns = 8;
    public const int Rows = 8;
    public const int Capacity = Columns * Rows;

    public sealed record Tile(int Column, int Row, WorkspaceExplorerEntry? Entry)
    {
        public bool IsEmpty => Entry is null;
    }

    public static IReadOnlyList<Tile> Build(
        IEnumerable<WorkspaceExplorerEntry> entries, WorldProjection world)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(world);

        var visible = entries
            .Where(entry => entry.IsFolder || ArtifactModel.IsNote(entry.Path))
            .OrderByDescending(entry => entry.IsFolder)
            .ThenBy(entry => entry.Path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var placed = new WorkspaceExplorerEntry?[Capacity];
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in visible.Where(entry => !entry.IsFolder))
        {
            var spatial = world.ProjectDocument(entry.Path);
            if (spatial?.X is not double x || spatial.Y is not double y ||
                !double.IsFinite(x) || !double.IsFinite(y) ||
                x != Math.Floor(x) || y != Math.Floor(y) ||
                x < 0 || x >= Columns || y < 0 || y >= Rows)
                continue;

            var slot = (int)y * Columns + (int)x;
            if (placed[slot] is null && used.Add(entry.Path))
                placed[slot] = entry;
        }

        var index = 0;
        foreach (var entry in visible)
        {
            if (!used.Add(entry.Path))
                continue;
            while (index < Capacity && placed[index] is not null)
                index++;
            if (index == Capacity)
                break;
            placed[index++] = entry;
        }

        return Array.AsReadOnly(Enumerable.Range(0, Capacity)
            .Select(i => new Tile(i % Columns, i / Columns, placed[i]))
            .ToArray());
    }
}
