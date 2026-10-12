using System.Text.Json;

namespace Urbe.Client;

/// <summary>
/// The Notas state the 1.8.4 keeps on the device, not in the vault (src/explorer/model.js, localStorage
/// 'urbe.explorer.v2'): favourites and the 30 most recently opened notes, by document id, and the folders open in the tree. Here it is a small JSON
/// file in the app's own data (<see cref="UrbeApp.LocalStatePath"/>); without one it lives for the session only.
/// </summary>
public sealed class ExplorerState
{
    private readonly string? _path;
    private readonly List<string> _favorites = [];
    private readonly List<string> _recent = [];
    private readonly List<string> _expanded = [""];

    public ExplorerState(string? path)
    {
        _path = path;
        if (path is null || !File.Exists(path)) return;
        try
        {
            using var json = JsonDocument.Parse(File.ReadAllBytes(path));
            Read(json.RootElement, "favorites", _favorites);
            Read(json.RootElement, "recent", _recent);
            if (json.RootElement.ValueKind == JsonValueKind.Object && json.RootElement.TryGetProperty("expanded", out var ex) && ex.ValueKind == JsonValueKind.Array)
            {
                // restore(): expanded = new Set(x.expanded || ['']); expanded.add('')
                _expanded.Clear();
                foreach (var x in ex.EnumerateArray())
                    if (x.ValueKind == JsonValueKind.String && x.GetString() is { } folder && !_expanded.Contains(folder)) _expanded.Add(folder);
                if (!_expanded.Contains("")) _expanded.Add("");
            }
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

    /// <summary>isExpanded(path): the folder is open in the Notas tree.</summary>
    public bool IsExpanded(string path) => _expanded.Contains(path);

    /// <summary>expand(path, value): opens (true) or closes (false) a folder of the tree, kept on the device.</summary>
    public void Expand(string path, bool value)
    {
        if (value) { if (!_expanded.Contains(path)) _expanded.Add(path); }
        else _expanded.Remove(path);
        Persist();
    }

    /// <summary>forgetFolder(path): the folder and everything under it leave the expanded set.</summary>
    public void Forget(string path)
    {
        if (_expanded.RemoveAll(p => p == path || p.StartsWith(path + "/", StringComparison.Ordinal)) > 0) Persist();
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
            File.WriteAllText(_path, JsonSerializer.Serialize(new { favorites = _favorites, recent = _recent, expanded = _expanded }));
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
