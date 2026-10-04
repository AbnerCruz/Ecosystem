using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Urbe.Core;

public enum VaultExportManifestState
{
    Absent,
    Current,
    Future,
    Corrupt
}

public sealed record VaultExportFileEntry(string Path, long Size, string Sha256);

public sealed class VaultExportState
{
    internal VaultExportState(
        IReadOnlyDictionary<string, string> localStorage,
        IReadOnlyDictionary<string, JsonElement>? plugins)
    {
        LocalStorage = localStorage;
        Plugins = plugins;
    }

    public IReadOnlyDictionary<string, string> LocalStorage { get; }
    public IReadOnlyDictionary<string, JsonElement>? Plugins { get; }

    public static VaultExportState Empty { get; } =
        new(
            new ReadOnlyDictionary<string, string>(
                new Dictionary<string, string>(StringComparer.Ordinal)),
            null);
}

public sealed class VaultExportManifest
{
    public const string FileName = "urbe-export.json";
    public const string FormatName = "urbe-export";
    public const int CurrentVersion = 1;

    internal VaultExportManifest(
        int formatVersion,
        string? appVersion,
        string exportedAt,
        string? vaultName,
        int? vaultFormatVersion,
        IReadOnlyList<VaultExportFileEntry> files,
        VaultExportState state)
    {
        FormatVersion = formatVersion;
        AppVersion = appVersion;
        ExportedAt = exportedAt;
        VaultName = vaultName;
        VaultFormatVersion = vaultFormatVersion;
        Files = files;
        State = state;
    }

    public string Format => FormatName;
    public int FormatVersion { get; }
    public string? AppVersion { get; }
    public string ExportedAt { get; }
    public string? VaultName { get; }
    public int? VaultFormatVersion { get; }
    public IReadOnlyList<VaultExportFileEntry> Files { get; }
    public VaultExportState State { get; }
}

public sealed record VaultExportManifestParseResult(
    VaultExportManifestState State,
    VaultExportManifest? Manifest);

public sealed class VaultExportVerification
{
    internal VaultExportVerification(
        IReadOnlyList<string> mismatched,
        IReadOnlyList<string> missing,
        IReadOnlyList<string> extra)
    {
        Mismatched = mismatched;
        Missing = missing;
        Extra = extra;
    }

    public bool Ok => Mismatched.Count == 0 && Missing.Count == 0 && Extra.Count == 0;
    public IReadOnlyList<string> Mismatched { get; }
    public IReadOnlyList<string> Missing { get; }
    public IReadOnlyList<string> Extra { get; }
}

public sealed class VaultExportStateApplyResult
{
    internal VaultExportStateApplyResult(IReadOnlyDictionary<string, string> writes)
    {
        Writes = writes;
    }

    public IReadOnlyDictionary<string, string> Writes { get; }
}

public static class VaultExportStatePolicy
{
    public const string PluginsKey = "urbe.plugins.v1";

    private static readonly string[] ExactKeys =
    [
        "urbe.explorer.v2",
        "urbe.editor.workspace.v1"
    ];

    private static readonly Regex Secret =
        new(
            @"(^|[._-])(ai|ia|key|keys|token|secret|senha|password|apikey|api-key|credential)s?([._-]|$)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static bool Forbidden(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return Secret.IsMatch(key);
    }

    public static bool Allowed(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (Forbidden(key))
            return false;

        return string.Equals(key, PluginsKey, StringComparison.Ordinal) ||
               ExactKeys.Contains(key, StringComparer.Ordinal) ||
               key.StartsWith("urbe.tip.", StringComparison.Ordinal);
    }

    public static VaultExportState Collect(
        IReadOnlyDictionary<string, string>? storage,
        string vaultName)
    {
        ArgumentNullException.ThrowIfNull(vaultName);

        if (storage is null)
            return VaultExportState.Empty;

        var local = new Dictionary<string, string>(StringComparer.Ordinal);
        Dictionary<string, JsonElement>? plugins = null;

        foreach (var pair in storage)
        {
            if (!Allowed(pair.Key))
                continue;

            if (string.Equals(pair.Key, PluginsKey, StringComparison.Ordinal))
            {
                plugins = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
                JsonObject? all;
                try
                {
                    all = JsonNode.Parse(pair.Value) as JsonObject;
                }
                catch (JsonException)
                {
                    all = null;
                }

                if (all is null)
                    continue;

                var prefix = vaultName + "::";
                foreach (var plugin in all)
                {
                    if (!plugin.Key.StartsWith(prefix, StringComparison.Ordinal) ||
                        plugin.Value is null)
                        continue;

                    using var json = JsonDocument.Parse(plugin.Value.ToJsonString());
                    plugins[plugin.Key[prefix.Length..]] = json.RootElement.Clone();
                }

                continue;
            }

            local[pair.Key] = pair.Value;
        }

        return new VaultExportState(
            new ReadOnlyDictionary<string, string>(local),
            plugins is null
                ? null
                : new ReadOnlyDictionary<string, JsonElement>(plugins));
    }

    public static VaultExportStateApplyResult Apply(
        IReadOnlyDictionary<string, string>? storage,
        VaultExportState? state,
        string vaultName)
    {
        ArgumentNullException.ThrowIfNull(vaultName);

        var writes = new Dictionary<string, string>(StringComparer.Ordinal);
        if (state is null)
            return new VaultExportStateApplyResult(
                new ReadOnlyDictionary<string, string>(writes));

        foreach (var pair in state.LocalStorage)
        {
            if (!Allowed(pair.Key) ||
                string.Equals(pair.Key, PluginsKey, StringComparison.Ordinal))
                continue;
            writes[pair.Key] = pair.Value;
        }

        if (state.Plugins is not null)
        {
            JsonObject all;
            try
            {
                all = storage is not null &&
                      storage.TryGetValue(PluginsKey, out var current) &&
                      JsonNode.Parse(current) is JsonObject parsed
                    ? parsed
                    : new JsonObject();
            }
            catch (JsonException)
            {
                all = new JsonObject();
            }

            foreach (var pair in state.Plugins)
                all[vaultName + "::" + pair.Key] = JsonNode.Parse(pair.Value.GetRawText());

            writes[PluginsKey] = all.ToJsonString();
        }

        return new VaultExportStateApplyResult(
            new ReadOnlyDictionary<string, string>(writes));
    }
}

public static class VaultExportManifestCodec
{
    public static VaultExportManifest Build(
        IEnumerable<VaultFile> source,
        string? appVersion,
        DateTimeOffset now,
        string? vaultName,
        int? vaultFormatVersion,
        VaultExportState? state = null)
    {
        ArgumentNullException.ThrowIfNull(source);

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var files = new List<VaultExportFileEntry>();

        foreach (var file in source)
        {
            ArgumentNullException.ThrowIfNull(file);
            if (string.Equals(file.Path, VaultExportManifest.FileName, StringComparison.Ordinal))
                continue;
            if (!seen.Add(file.Path))
                throw new InvalidDataException("Caminho duplicado no export: " + file.Path);

            files.Add(
                new VaultExportFileEntry(
                    file.Path,
                    file.Bytes.Length,
                    Sha256(file.Bytes.Span)));
        }

        files.Sort((left, right) => StringComparer.Ordinal.Compare(left.Path, right.Path));

        return new VaultExportManifest(
            VaultExportManifest.CurrentVersion,
            appVersion,
            UtcIso(now),
            vaultName,
            vaultFormatVersion,
            files.AsReadOnly(),
            state ?? VaultExportState.Empty);
    }

    public static byte[] Serialize(VaultExportManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        var root = new JsonObject
        {
            ["format"] = VaultExportManifest.FormatName,
            ["formatVersion"] = manifest.FormatVersion,
            ["appVersion"] = manifest.AppVersion is null ? null : JsonValue.Create(manifest.AppVersion),
            ["exportedAt"] = manifest.ExportedAt,
            ["vault"] = new JsonObject
            {
                ["name"] = manifest.VaultName is null ? null : JsonValue.Create(manifest.VaultName),
                ["formatVersion"] = manifest.VaultFormatVersion is null
                    ? null
                    : JsonValue.Create(manifest.VaultFormatVersion.Value)
            }
        };

        var files = new JsonArray();
        foreach (var file in manifest.Files)
        {
            files.Add(
                new JsonObject
                {
                    ["path"] = file.Path,
                    ["size"] = file.Size,
                    ["sha256"] = file.Sha256
                });
        }

        root["files"] = files;
        root["state"] = SerializeState(manifest.State);

        return Encoding.UTF8.GetBytes(
            root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    public static VaultExportManifestParseResult Parse(ReadOnlyMemory<byte> bytes)
    {
        try
        {
            using var json = JsonDocument.Parse(bytes);
            return Parse(json.RootElement);
        }
        catch (JsonException)
        {
            return new VaultExportManifestParseResult(
                VaultExportManifestState.Corrupt,
                null);
        }
    }

    public static VaultExportManifestParseResult Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return Parse(Encoding.UTF8.GetBytes(text));
    }

    public static VaultExportVerification Verify(
        VaultExportManifest manifest,
        IEnumerable<VaultFile> source)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(source);

        var entries = new Dictionary<string, VaultFile>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in source)
        {
            if (string.Equals(file.Path, VaultExportManifest.FileName, StringComparison.Ordinal))
                continue;
            if (!entries.TryAdd(file.Path, file))
                throw new InvalidDataException("Caminho duplicado no ZIP: " + file.Path);
        }

        var listed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var mismatched = new List<string>();
        var missing = new List<string>();

        foreach (var file in manifest.Files)
        {
            if (!listed.Add(file.Path))
                throw new InvalidDataException("Manifesto contém caminho duplicado: " + file.Path);

            if (!entries.TryGetValue(file.Path, out var actual))
            {
                missing.Add(file.Path);
                continue;
            }

            if (actual.Bytes.Length != file.Size ||
                !string.Equals(
                    Sha256(actual.Bytes.Span),
                    file.Sha256,
                    StringComparison.OrdinalIgnoreCase))
                mismatched.Add(file.Path);
        }

        var extra = entries.Keys
            .Where(path => !listed.Contains(path))
            .ToArray();

        return new VaultExportVerification(
            mismatched.AsReadOnly(),
            missing.AsReadOnly(),
            Array.AsReadOnly(extra));
    }

    private static VaultExportManifestParseResult Parse(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("format", out var format) ||
            format.ValueKind != JsonValueKind.String ||
            !string.Equals(format.GetString(), VaultExportManifest.FormatName, StringComparison.Ordinal) ||
            !root.TryGetProperty("formatVersion", out var versionElement) ||
            versionElement.ValueKind != JsonValueKind.Number ||
            !versionElement.TryGetInt32(out var version) ||
            !root.TryGetProperty("files", out var filesElement) ||
            filesElement.ValueKind != JsonValueKind.Array)
            return new VaultExportManifestParseResult(
                VaultExportManifestState.Corrupt,
                null);

        if (version > VaultExportManifest.CurrentVersion)
        {
            var future = ParseManifest(root, version);
            return future is null
                ? new VaultExportManifestParseResult(VaultExportManifestState.Corrupt, null)
                : new VaultExportManifestParseResult(VaultExportManifestState.Future, future);
        }

        var current = ParseManifest(root, version);
        return current is null
            ? new VaultExportManifestParseResult(VaultExportManifestState.Corrupt, null)
            : new VaultExportManifestParseResult(VaultExportManifestState.Current, current);
    }

    private static VaultExportManifest? ParseManifest(JsonElement root, int version)
    {
        try
        {
            var files = new List<VaultExportFileEntry>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var item in root.GetProperty("files").EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object ||
                    !item.TryGetProperty("path", out var pathElement) ||
                    pathElement.ValueKind != JsonValueKind.String ||
                    !item.TryGetProperty("size", out var sizeElement) ||
                    sizeElement.ValueKind != JsonValueKind.Number ||
                    !sizeElement.TryGetInt64(out var size) ||
                    size < 0 ||
                    !item.TryGetProperty("sha256", out var hashElement) ||
                    hashElement.ValueKind != JsonValueKind.String)
                    return null;

                var path = pathElement.GetString() ?? string.Empty;
                var hash = hashElement.GetString() ?? string.Empty;
                if (path.Length == 0 || hash.Length == 0 || !seen.Add(path))
                    return null;

                files.Add(new VaultExportFileEntry(path, size, hash));
            }

            string? appVersion = null;
            if (root.TryGetProperty("appVersion", out var app) &&
                app.ValueKind == JsonValueKind.String)
                appVersion = app.GetString();

            var exportedAt = root.TryGetProperty("exportedAt", out var exported) &&
                             exported.ValueKind == JsonValueKind.String
                ? exported.GetString() ?? string.Empty
                : string.Empty;

            string? vaultName = null;
            int? vaultFormat = null;
            if (root.TryGetProperty("vault", out var vault) &&
                vault.ValueKind == JsonValueKind.Object)
            {
                if (vault.TryGetProperty("name", out var name) &&
                    name.ValueKind == JsonValueKind.String)
                    vaultName = name.GetString();

                if (vault.TryGetProperty("formatVersion", out var formatVersion) &&
                    formatVersion.ValueKind == JsonValueKind.Number &&
                    formatVersion.TryGetInt32(out var parsedVersion))
                    vaultFormat = parsedVersion;
            }

            return new VaultExportManifest(
                version,
                appVersion,
                exportedAt,
                vaultName,
                vaultFormat,
                files.AsReadOnly(),
                ParseState(root));
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private static VaultExportState ParseState(JsonElement root)
    {
        if (!root.TryGetProperty("state", out var state) ||
            state.ValueKind != JsonValueKind.Object)
            return VaultExportState.Empty;

        var local = new Dictionary<string, string>(StringComparer.Ordinal);
        if (state.TryGetProperty("localStorage", out var localStorage) &&
            localStorage.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in localStorage.EnumerateObject())
            {
                if (!VaultExportStatePolicy.Allowed(property.Name) ||
                    string.Equals(
                        property.Name,
                        VaultExportStatePolicy.PluginsKey,
                        StringComparison.Ordinal))
                    continue;

                if (property.Value.ValueKind == JsonValueKind.String)
                    local[property.Name] = property.Value.GetString() ?? string.Empty;
                else
                    local[property.Name] = property.Value.ToString();
            }
        }

        Dictionary<string, JsonElement>? plugins = null;
        if (state.TryGetProperty("plugins", out var pluginState) &&
            pluginState.ValueKind == JsonValueKind.Object)
        {
            plugins = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
            foreach (var property in pluginState.EnumerateObject())
                plugins[property.Name] = property.Value.Clone();
        }

        return new VaultExportState(
            new ReadOnlyDictionary<string, string>(local),
            plugins is null
                ? null
                : new ReadOnlyDictionary<string, JsonElement>(plugins));
    }

    private static JsonObject SerializeState(VaultExportState state)
    {
        var local = new JsonObject();
        foreach (var pair in state.LocalStorage.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            if (!VaultExportStatePolicy.Allowed(pair.Key) ||
                string.Equals(
                    pair.Key,
                    VaultExportStatePolicy.PluginsKey,
                    StringComparison.Ordinal))
                continue;
            local[pair.Key] = pair.Value;
        }

        var root = new JsonObject
        {
            ["localStorage"] = local
        };

        if (state.Plugins is not null)
        {
            var plugins = new JsonObject();
            foreach (var pair in state.Plugins.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                plugins[pair.Key] = JsonNode.Parse(pair.Value.GetRawText());
            root["plugins"] = plugins;
        }

        return root;
    }

    internal static string Sha256(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    internal static string UtcIso(DateTimeOffset value) =>
        value.UtcDateTime.ToString(
            "yyyy-MM-dd'T'HH:mm:ss.fff'Z'",
            System.Globalization.CultureInfo.InvariantCulture);
}
