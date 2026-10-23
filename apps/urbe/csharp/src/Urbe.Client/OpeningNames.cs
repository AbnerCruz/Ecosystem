using System.Text.Json;
using System.Text.Json.Nodes;

namespace Urbe.Client;

/// <summary>
/// UC-78 (observation of 22/10/2026: the opening should be instant): the names of the last vault opened — the paths of
/// its notes, folders and other files, and how its boxes look (.urbe/area.json) — kept in the app's own data, never in
/// the vault and never a note's text. The next opening shows the tree from them at once, while the folder is read; the
/// folder read stays the only authority (NN-001), and replaces them as soon as it arrives.
/// </summary>
public sealed class OpeningNames(string? path)
{
    public const int Version = 1;

    /// <summary>What is kept: the vault's name, the paths and the boxes' presentation.</summary>
    public sealed record Names(string Vault, IReadOnlyList<string> Notes, IReadOnlyList<string> Folders, IReadOnlyList<string> Files, string? Area = null);

    private string? _last;

    /// <summary>The names kept at the last opening, or null (none, unreadable or of another version).</summary>
    public Names? Read()
    {
        if (path is null || !File.Exists(path)) return null;
        try
        {
            if (JsonNode.Parse(File.ReadAllText(path)) is not JsonObject root || (int?)root["versao"] != Version || (string?)root["vault"] is not { Length: > 0 } vault) return null;
            static List<string> List(JsonNode? node) => node is JsonArray a ? [.. a.Select(x => x is JsonValue v && v.TryGetValue<string>(out var s) ? s : null).OfType<string>().Where(s => s.Length > 0)] : [];
            return new Names(vault, List(root["notas"]), List(root["pastas"]), List(root["arquivos"]), (string?)root["area"]);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or InvalidOperationException or FormatException)
        {
            return null;
        }
    }

    /// <summary>Keeps the names of the vault on the screen; nothing is written when they did not change. Best effort.</summary>
    public void Remember(Names names)
    {
        if (path is null) return;
        var root = new JsonObject
        {
            ["versao"] = Version,
            ["vault"] = names.Vault,
            ["notas"] = new JsonArray([.. names.Notes.Order(StringComparer.Ordinal).Select(p => (JsonNode)p)]),
            ["pastas"] = new JsonArray([.. names.Folders.Order(StringComparer.Ordinal).Select(p => (JsonNode)p)]),
            ["arquivos"] = new JsonArray([.. names.Files.Order(StringComparer.Ordinal).Select(p => (JsonNode)p)]),
            ["area"] = names.Area
        };
        var text = root.ToJsonString();
        if (text == _last) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            // written beside and moved over, so a closed app never leaves half a file
            var temp = path + ".tmp";
            File.WriteAllText(temp, text);
            File.Move(temp, path, overwrite: true);
            _last = text;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* only the next opening is slower */ }
    }
}
