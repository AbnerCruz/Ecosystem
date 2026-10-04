using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json.Nodes;

namespace Urbe.Core;

public sealed class WorldStableIdAssignment
{
    internal WorldStableIdAssignment(
        IReadOnlyDictionary<string, string> regions,
        IReadOnlyList<string> assets)
    {
        Regions = regions;
        Assets = assets;
    }

    public IReadOnlyDictionary<string, string> Regions { get; }
    public IReadOnlyList<string> Assets { get; }
}

/// <summary>
/// Stable region/asset identities used by mapa.json. The deterministic branch
/// intentionally mirrors src/world/stable-ids.js byte-for-byte at the
/// algorithmic level: FNV-1a over UTF-16 code units, unsigned 32-bit overflow,
/// then lower-case base36.
/// </summary>
public static class WorldStableIds
{
    public static bool Valid(string prefix, string? value)
    {
        ArgumentNullException.ThrowIfNull(prefix);
        return !string.IsNullOrEmpty(value) &&
               value.StartsWith(prefix + "_", StringComparison.Ordinal) &&
               value.Length > prefix.Length + 1;
    }

    public static string Fresh(string prefix)
    {
        ArgumentException.ThrowIfNullOrEmpty(prefix);
        return prefix + "_" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)[..16];
    }

    public static string Ensure(JsonObject target, string prefix)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentException.ThrowIfNullOrEmpty(prefix);

        var current = StringValue(target["uid"]);
        if (!Valid(prefix, current))
        {
            current = Fresh(prefix);
            target["uid"] = current;
        }

        return current!;
    }

    public static string RegionFor(string? path, ISet<string>? used = null)
    {
        var baseId = "reg_" + Hash("reg:" + (path ?? string.Empty));
        return used is null
            ? baseId
            : Unique(baseId, used);
    }

    /// <summary>
    /// Adds/reconciles IDs in-place, preserving every other field.
    /// </summary>
    public static WorldStableIdAssignment Assign(JsonObject? map)
    {
        var regionsByPath = new Dictionary<string, string>(StringComparer.Ordinal);
        var assetIds = new List<string>();

        if (map is null)
        {
            return new WorldStableIdAssignment(
                new ReadOnlyDictionary<string, string>(regionsByPath),
                assetIds.AsReadOnly());
        }

        var used = new HashSet<string>(StringComparer.Ordinal);
        var regions = map["regioes"] as JsonArray;
        var assets = map["construcoes"] as JsonArray;

        if (regions is not null)
        {
            foreach (var item in regions)
            {
                if (item is not JsonObject region)
                    continue;

                var id = StringValue(region["id"]);
                if (!Valid("reg", id))
                    continue;

                if (!used.Add(id!))
                    region["id"] = null;
            }

            foreach (var item in regions)
            {
                if (item is not JsonObject region)
                    continue;

                var path = StringValue(region["caminho"]) ?? string.Empty;
                var id = StringValue(region["id"]);
                if (!Valid("reg", id))
                {
                    id = Unique("reg_" + Hash("reg:" + path), used);
                    region["id"] = id;
                }

                regionsByPath[path] = id!;
            }
        }

        if (assets is not null)
        {
            foreach (var item in assets)
            {
                if (item is not JsonObject asset)
                    continue;

                var id = StringValue(asset["id"]);
                if (!Valid("ast", id))
                    continue;

                if (!used.Add(id!))
                    asset["id"] = null;
            }

            foreach (var item in assets)
            {
                if (item is not JsonObject asset)
                    continue;

                var id = StringValue(asset["id"]);
                if (!Valid("ast", id))
                {
                    var key = AssetKey(asset);
                    id = Unique("ast_" + Hash("ast:" + key), used);
                    asset["id"] = id;
                }

                assetIds.Add(id!);
            }
        }

        return new WorldStableIdAssignment(
            new ReadOnlyDictionary<string, string>(regionsByPath),
            assetIds.AsReadOnly());
    }

    internal static string Hash(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        uint hash = 2166136261;
        foreach (var ch in text)
        {
            hash ^= ch;
            hash = unchecked(hash * 16777619);
        }

        return ToBase36(hash);
    }

    private static string AssetKey(JsonObject asset)
    {
        JsonObject? first = null;

        if (asset["files"] is JsonArray files && files.Count > 0)
            first = files[0] as JsonObject;
        else if (asset["anexos"] is JsonArray attachments && attachments.Count > 0)
            first = attachments[0] as JsonObject;

        var relPath = first is null ? null : StringValue(first["relPath"]);
        if (!string.IsNullOrEmpty(relPath))
            return relPath;

        var folder = StringValue(asset["caminho"]) ?? string.Empty;
        var fileName = StringValue(asset["fileName"]);
        if (string.IsNullOrEmpty(fileName))
            fileName = StringValue(asset["name"]) ?? string.Empty;

        return folder + "/" + fileName;
    }

    private static string Unique(string baseId, ISet<string> used)
    {
        var id = baseId;
        var suffix = 2;

        while (used.Contains(id))
            id = baseId + "_" + suffix++;

        used.Add(id);
        return id;
    }

    private static string? StringValue(JsonNode? node)
    {
        try
        {
            return node?.GetValue<string>();
        }
        catch (InvalidOperationException)
        {
            return null;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static string ToBase36(uint value)
    {
        const string alphabet = "0123456789abcdefghijklmnopqrstuvwxyz";
        if (value == 0)
            return "0";

        Span<char> buffer = stackalloc char[7];
        var index = buffer.Length;

        while (value > 0)
        {
            buffer[--index] = alphabet[(int)(value % 36)];
            value /= 36;
        }

        return new string(buffer[index..]);
    }
}
