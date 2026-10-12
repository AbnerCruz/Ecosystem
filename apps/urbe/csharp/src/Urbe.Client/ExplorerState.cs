using System.Text.Json;

namespace Urbe.Client;

/// <summary>
/// The Notas state the 1.8.4 keeps on the device, not in the vault (src/explorer/model.js, localStorage
/// 'urbe.explorer.v2'): favourites and the 30 most recently opened notes, by document id. Here it is a small JSON
/// file in the app's own data (<see cref="UrbeApp.LocalStatePath"/>); without one it lives for the session only.
/// </summary>
public sealed class ExplorerState
{
    private readonly string? _path;
    private readonly List<string> _favorites = [];
    private readonly List<string> _recent = [];

    public ExplorerState(string? path)
    {
        _path = path;
        if (path is null || !File.Exists(path)) return;
        try
        {
            using var json = JsonDocument.Parse(File.ReadAllBytes(path));
            Read(json.RootElement, "favorites", _favorites);
            Read(json.RootElement, "recent", _recent);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            // restore(): a state that cannot be read is ignored
        }
    }

    /// <summary>favorites (a Set: in the order they were added).</summary>
    public IReadOnlyList<string> Favorites => _favorites;

    /// <summary>recent: newest first, at most 30.</summary>
    public IReadOnlyList<string> Recent => _recent;

    /// <summary>favorite(id, true).</summary>
    public void Favorite(string id)
    {
        if (!_favorites.Contains(id)) _favorites.Add(id);
        Persist();
    }

    /// <summary>touchRecent(id).</summary>
    public void TouchRecent(string id)
    {
        _recent.Remove(id);
        _recent.Insert(0, id);
        if (_recent.Count > 30) _recent.RemoveRange(30, _recent.Count - 30);
        Persist();
    }

    private void Persist()
    {
        if (_path is null) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_path))!);
            File.WriteAllText(_path, JsonSerializer.Serialize(new { favorites = _favorites, recent = _recent }));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // persist(): best effort, as localStorage
        }
    }

    private static void Read(JsonElement root, string name, List<string> into)
    {
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty(name, out var list) || list.ValueKind != JsonValueKind.Array) return;
        foreach (var x in list.EnumerateArray())
            if (x.ValueKind == JsonValueKind.String && x.GetString() is { Length: > 0 } id && !into.Contains(id)) into.Add(id);
    }
}
