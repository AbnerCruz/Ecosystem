namespace Urbe.Core;

/// <summary>
/// Ephemeral, deterministic visual layout for the UC-19 city surface.
/// This is never written to mapa.json: host-owned placement remains authoritative.
/// </summary>
public sealed record CityDistrict(
    string Path, string Name, double X, double Y,
    double Width, double Height, int NoteCount);

public sealed record CityBuilding(
    string DocumentId, string Path, string Title, string Folder,
    double X, double Y);

public sealed record CityRoad(double X1, double Y1, double X2, double Y2);

public sealed record CityLayout(
    double Width, double Height,
    IReadOnlyList<CityDistrict> Districts,
    IReadOnlyList<CityBuilding> Buildings,
    IReadOnlyList<CityRoad> Roads,
    int HiddenNoteCount);

public static class CityLayoutBuilder
{
    public const int MaxNotes = 240;
    private const double DistrictWidth = 520;
    private const double DistrictGap = 40;
    private const double OuterMargin = 30;
    private const int HousesPerRow = 5;

    public static CityLayout Build(
        WorldProjectionSnapshot projection,
        KnowledgeIndex knowledge,
        IEnumerable<string>? folders = null)
    {
        ArgumentNullException.ThrowIfNull(projection);
        ArgumentNullException.ThrowIfNull(knowledge);

        var allNotes = projection.Documents
            .Where(note => ArtifactModel.IsNote(note.Path) &&
                           !ArtifactModel.IsSystem(note.Path) &&
                           !NoteTemplateEngine.IsTemplatePath(note.Path))
            .OrderBy(note => note.Path, StringComparer.OrdinalIgnoreCase)
            .ThenBy(note => note.Path, StringComparer.Ordinal)
            .ToArray();
        var notes = allNotes.Take(MaxNotes).ToArray();
        var paths = notes.Select(note => note.Folder)
            .Concat(folders ?? [])
            .Append(string.Empty)
            .Select(DocumentModel.NormalizePath)
            .Where(path => !ArtifactModel.IsSystem(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ThenBy(path => path, StringComparer.Ordinal)
            .ToArray();

        // Keep the root district even when the vault is empty; the first
        // note can therefore appear in the city without a separate seed.
        if (paths.Length == 0)
            paths = [string.Empty];

        var districts = new List<CityDistrict>(paths.Length);
        var buildings = new List<CityBuilding>(notes.Length);
        var top = OuterMargin;

        for (var row = 0; row < paths.Length; row += 2)
        {
            var rowHeight = 0d;
            for (var column = 0; column < 2 && row + column < paths.Length; column++)
            {
                var path = paths[row + column];
                var group = notes.Where(note =>
                        string.Equals(note.Folder, path, StringComparison.OrdinalIgnoreCase))
                    .ToArray();
                var rows = Math.Max(1, (group.Length + HousesPerRow - 1) / HousesPerRow);
                var height = Math.Max(310, 135 + rows * 88);
                var left = OuterMargin + column * (DistrictWidth + DistrictGap);
                var name = path.Length == 0 ? "Raiz" : path[(path.LastIndexOf('/') + 1)..];
                districts.Add(new CityDistrict(path, name, left, top,
                    DistrictWidth, height, group.Length));
                rowHeight = Math.Max(rowHeight, height);

                for (var index = 0; index < group.Length; index++)
                {
                    buildings.Add(new CityBuilding(
                        group[index].DocumentId, group[index].Path,
                        group[index].Title, group[index].Folder,
                        left + 52 + (index % HousesPerRow) * 91,
                        top + 112 + (index / HousesPerRow) * 88));
                }
            }
            top += rowHeight + DistrictGap;
        }

        var byDocumentId = buildings.ToDictionary(
            building => building.DocumentId, StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var roads = new List<CityRoad>();

        foreach (var origin in buildings)
        {
            foreach (var destination in knowledge.Links(origin.DocumentId))
            {
                if (!byDocumentId.TryGetValue(destination.Id, out var target) ||
                    string.Equals(origin.DocumentId, target.DocumentId, StringComparison.Ordinal))
                    continue;
                var first = string.CompareOrdinal(origin.DocumentId, target.DocumentId) < 0
                    ? origin.DocumentId : target.DocumentId;
                var second = first == origin.DocumentId
                    ? target.DocumentId : origin.DocumentId;
                if (!visited.Add(first + "/" + second))
                    continue;

                roads.Add(new CityRoad(
                    origin.X + 31, origin.Y + 28,
                    target.X + 31, target.Y + 28));
            }
        }

        return new CityLayout(
            OuterMargin * 2 + DistrictWidth * 2 + DistrictGap,
            Math.Max(560, top),
            districts.AsReadOnly(), buildings.AsReadOnly(), roads.AsReadOnly(),
            allNotes.Length - notes.Length);
    }
}
