using System.Text.Json;

namespace Lunet.Core;

/// <summary>A snapshot of projects that have a persistent GameId in their on-disk manifest.</summary>
public sealed record LauncherProjectEntry(string FolderName, string GameId);

/// <summary>
/// Read-only lookup for Home shortcuts. Folder names can change; GameId is the identity.
/// Missing, malformed and ambiguous IDs never resolve to an unrelated project.
/// </summary>
public static class LauncherProjectCatalog
{
    public static IReadOnlyList<LauncherProjectEntry> Scan(ProjectStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        var entries = new List<LauncherProjectEntry>();
        foreach (var name in store.List())
        {
            try
            {
                if (store.Root.LocalDirectory is { } root)
                {
                    var folder = Path.Combine(root, name);
                    var manifest = Path.Combine(folder, ProjectStore.ManifestFileName);
                    if ((File.GetAttributes(folder) & FileAttributes.ReparsePoint) != 0 ||
                        (File.GetAttributes(manifest) & FileAttributes.ReparsePoint) != 0)
                        continue;
                }
                using var document = JsonDocument.Parse(store.Root.Read(name + "/" + ProjectStore.ManifestFileName));
                if (document.RootElement.ValueKind != JsonValueKind.Object) continue;
                if (!document.RootElement.TryGetProperty("gameId", out var id) &&
                    !document.RootElement.TryGetProperty("GameId", out id)) continue;
                if (id.ValueKind != JsonValueKind.String ||
                    !Guid.TryParse(id.GetString(), out var parsed)) continue;
                entries.Add(new LauncherProjectEntry(name, parsed.ToString("D")));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
                JsonException or ArgumentException)
            {
                // An unreadable manifest is not a reason to discard user files or rewrite it.
            }
        }
        return entries;
    }

    public static string? UniqueNameForId(IEnumerable<LauncherProjectEntry> entries, string? gameId)
    {
        if (!Guid.TryParse(gameId, out var id)) return null;
        var matches = entries.Where(x => Guid.TryParse(x.GameId, out var candidate) && candidate == id)
            .Take(2).ToArray();
        return matches.Length == 1 ? matches[0].FolderName : null;
    }

    public static string? UniqueIdForName(IEnumerable<LauncherProjectEntry> entries, string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        var matches = entries.Where(x => string.Equals(x.FolderName, name, StringComparison.Ordinal))
            .Take(2).ToArray();
        return matches.Length == 1 ? matches[0].GameId : null;
    }
}
