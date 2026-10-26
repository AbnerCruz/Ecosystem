using System.Text.Json;

namespace Urbe.Core;

/// <summary>
/// Optional forward-compatibility declaration for *individual* vault files.
/// A known file whose writer requires an unavailable feature/version remains readable as bytes
/// but cannot be changed in place. This is independent from the APK release history.
/// </summary>
public static class VaultCompatibility
{
    public const string Path = ".urbe/compatibility.json";
    public const int SchemaVersion = 1;

    // These are exact reader/writer capabilities implemented by the current native product.
    // An unknown capability is deliberately not treated as safe merely because its name resembles a known one.
    private static readonly HashSet<string> SupportedFeatures = new(StringComparer.Ordinal)
    {
        "markdown.v1", "page.v1", "graph.v1", "forms.v1", "notelibrary.v1"
    };

    public sealed record Assessment(bool ManifestUnsafe, IReadOnlyDictionary<string, string> BlockedFiles)
    {
        public bool CanWrite(string path) => !ManifestUnsafe && !BlockedFiles.ContainsKey(path);
    }

    /// <summary>
    /// Invalid/unknown manifest versions fail closed for vault writes; the files are never deleted or migrated.
    /// Missing manifest means the existing VaultReader's per-format guards remain authoritative.
    /// </summary>
    public static Assessment Assess(IReadOnlyDictionary<string, VaultFile> files, string? installedVersion)
    {
        var blocked = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!files.TryGetValue(Path, out var file)) return new Assessment(false, blocked);
        try
        {
            using var document = JsonDocument.Parse(file.Bytes);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("schemaVersion", out var schema) ||
                schema.ValueKind != JsonValueKind.Number || !schema.TryGetInt32(out var schemaNumber) ||
                schemaNumber != SchemaVersion ||
                !root.TryGetProperty("files", out var entries) || entries.ValueKind != JsonValueKind.Array)
                return new Assessment(true, blocked);

            foreach (var entry in entries.EnumerateArray())
            {
                if (entry.ValueKind != JsonValueKind.Object || !entry.TryGetProperty("path", out var pathNode) ||
                    pathNode.ValueKind != JsonValueKind.String ||
                    string.IsNullOrWhiteSpace(pathNode.GetString()))
                    return new Assessment(true, blocked);
                var path = pathNode.GetString()!;
                if (path.StartsWith("/", StringComparison.Ordinal) || path.Contains("..", StringComparison.Ordinal) ||
                    path.Contains('\\') || path.StartsWith(".urbe/", StringComparison.OrdinalIgnoreCase))
                    return new Assessment(true, blocked);
                // A user can explicitly remove the incompatible original; stale entries are harmless.
                if (!files.ContainsKey(path)) continue;

                if (!entry.TryGetProperty("minWriterVersion", out var minWriter) ||
                    minWriter.ValueKind != JsonValueKind.String || AppUpdate.Parts(minWriter.GetString()) is null)
                    return new Assessment(true, blocked);
                string? reason = null;
                var required = minWriter.GetString()!;
                if (AppUpdate.Parts(installedVersion) is null || AppUpdate.Newer(required, installedVersion))
                    reason = "Requer Urbe " + required + " ou posterior para editar com segurança.";
                if (!entry.TryGetProperty("features", out var features) ||
                    features.ValueKind != JsonValueKind.Array)
                    return new Assessment(true, blocked);
                foreach (var feature in features.EnumerateArray())
                {
                    if (feature.ValueKind != JsonValueKind.String) return new Assessment(true, blocked);
                    var name = feature.GetString()!;
                    if (!SupportedFeatures.Contains(name))
                        reason = "O recurso " + name + " não é compreendido por esta versão.";
                }
                if (reason is not null) blocked[path] = reason;
            }
            return new Assessment(false, blocked);
        }
        catch (JsonException) { return new Assessment(true, blocked); }
    }
}
