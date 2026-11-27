using System.Text.Json;
using System.Text.Json.Serialization;

namespace Lunet.Core;

public readonly record struct EditorViewport(int Start, int End, int X, int Y);

/// <summary>The default tab is replaceable; extra tabs are kept only when explicitly opened.</summary>
public sealed class EditorDocuments
{
    public string? Primary { get; set; }
    public List<string> Additional { get; set; } = [];
    public string? Current { get; set; }
    public Dictionary<string, EditorViewport> Positions { get; set; } = new(StringComparer.Ordinal);

    [JsonIgnore] public IEnumerable<string> Tabs => (Primary is null ? Enumerable.Empty<string>() : new[] { Primary }).Concat(Additional);
    [JsonIgnore] public EditorViewport Viewport => Current is { } path ? Positions.GetValueOrDefault(path) : default;

    public void Open(string path, bool additional = false)
    {
        if (path != Primary && !Additional.Contains(path))
        {
            if (additional && Primary is not null) Additional.Add(path);
            else Primary = path;
        }
        Current = path;
    }

    public void Capture(EditorViewport view)
    {
        if (Current is { } path) Positions[path] = view;
    }

    public void Close()
    {
        if (Current == Primary) Primary = null;
        else if (Current is { } path) Additional.Remove(path);
        Current = Tabs.FirstOrDefault();
    }

    /// <summary>Closes one tab (✕ on it); the current one moves to the neighbour, like an editor's tabs.</summary>
    public void Close(string path)
    {
        var tabs = Tabs.ToList();
        var index = tabs.IndexOf(path);
        if (index < 0) return;
        if (path == Primary) Primary = null; else Additional.Remove(path);
        if (Current == path)
        {
            var rest = Tabs.ToList();
            Current = rest.Count == 0 ? null : rest[Math.Min(index, rest.Count - 1)];
        }
    }

    public void Reconcile(IEnumerable<string> validPaths)
    {
        var valid = validPaths.ToHashSet(StringComparer.Ordinal);
        if (Primary is { } primary && !valid.Contains(primary)) Primary = null;
        Additional.RemoveAll(path => !valid.Contains(path));
        if (Current is null || !Tabs.Contains(Current)) Current = Tabs.FirstOrDefault();
        foreach (var path in Positions.Keys.Where(path => !valid.Contains(path)).ToArray()) Positions.Remove(path);
    }

    /// <summary>The session kept inside the game (read through the game's files), or a new one.</summary>
    public static EditorDocuments Load(LunetProject project, string relativePath)
    {
        try
        {
            if (project.Exists(relativePath) && JsonSerializer.Deserialize<EditorDocuments>(project.ReadText(relativePath)) is
                { Additional: not null, Positions: not null } state) return state;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ProjectException) { }
        return new();
    }

    public void Save(LunetProject project, string relativePath) => project.WriteText(relativePath, JsonSerializer.Serialize(this));

    public static EditorDocuments Load(string path)
    {
        try
        {
            if (File.Exists(path) && JsonSerializer.Deserialize<EditorDocuments>(File.ReadAllText(path)) is
                { Additional: not null, Positions: not null } state) return state;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { }
        return new();
    }

    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        AtomicFile.WriteAllText(path, JsonSerializer.Serialize(this));
    }
}
