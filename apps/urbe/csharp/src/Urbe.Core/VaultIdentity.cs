using System.Text.Json;
using System.Text.RegularExpressions;

namespace Urbe.Core;

public sealed record VaultIdentityEntry(string Path, string Fingerprint, string Seen);

public sealed class VaultIdentityState
{
    public VaultIdentityState(IReadOnlyDictionary<string, VaultIdentityEntry> documents)
    {
        Documents = documents;
    }

    public IReadOnlyDictionary<string, VaultIdentityEntry> Documents { get; }
}

public static class VaultIdentity
{
    public const int Version = 1;
    public const string Path = ".urbe/identity.json";

    private static readonly Regex TrailingSpaces = new(@"[ 	]+$", RegexOptions.Multiline | RegexOptions.CultureInvariant);
    private static readonly Regex TrailingNewlines = new(@"
+$", RegexOptions.CultureInvariant);

    public static string Normalize(string? text)
    {
        var value = (text ?? string.Empty).Replace("
", "
", StringComparison.Ordinal)
            .Replace("", "
", StringComparison.Ordinal);
        value = TrailingSpaces.Replace(value, string.Empty);
        return TrailingNewlines.Replace(value, string.Empty);
    }

    public static string Fingerprint(string? text)
    {
        var value = Normalize(text);
        if (value.Length == 0)
            return string.Empty;

        unchecked
        {
            uint h1 = 2166136261u;
            uint h2 = 0x811c9dc5u ^ 0x5bd1e995u;

            for (var i = 0; i < value.Length; i++)
            {
                var c = value[i];
                h1 ^= c;
                h1 *= 16777619u;
                h2 ^= (uint)(c + i);
                h2 *= 16777619u;
            }

            return h1.ToString("x8") + h2.ToString("x8") + ":" + ToBase36(value.Length);
        }
    }

    public static VaultIdentityState? Parse(ReadOnlyMemory<byte> bytes, out bool future)
    {
        future = false;
        try
        {
            using var json = JsonDocument.Parse(bytes);
            var root = json.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("version", out var versionElement) ||
                !versionElement.TryGetInt32(out var version))
                return null;

            if (version > Version)
            {
                future = true;
                return null;
            }

            if (version != Version ||
                !root.TryGetProperty("docs", out var docs) ||
                docs.ValueKind != JsonValueKind.Object)
                return null;

            var result = new Dictionary<string, VaultIdentityEntry>(StringComparer.Ordinal);
            foreach (var property in docs.EnumerateObject())
            {
                if (property.Value.ValueKind != JsonValueKind.Object)
                    continue;
                if (!property.Value.TryGetProperty("path", out var path) ||
                    path.ValueKind != JsonValueKind.String)
                    continue;

                var fingerprint = property.Value.TryGetProperty("fingerprint", out var fp) &&
                                  fp.ValueKind == JsonValueKind.String
                    ? fp.GetString() ?? string.Empty
                    : string.Empty;
                var seen = property.Value.TryGetProperty("seen", out var seenElement) &&
                           seenElement.ValueKind == JsonValueKind.String
                    ? seenElement.GetString() ?? string.Empty
                    : string.Empty;

                result[property.Name] = new VaultIdentityEntry(path.GetString() ?? string.Empty, fingerprint, seen);
            }

            return new VaultIdentityState(result);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static VaultIdentityState Build(
        IEnumerable<VaultWriteDocument> documents,
        VaultIdentityState? previous,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(documents);
        var timestamp = now.UtcDateTime.ToString("O");
        var result = new Dictionary<string, VaultIdentityEntry>(StringComparer.Ordinal);
        var previousDocs = previous?.Documents;

        foreach (var document in documents)
        {
            var fingerprint = Fingerprint(document.Content);
            var seen = timestamp;

            if (previousDocs is not null &&
                previousDocs.TryGetValue(document.Id, out var old) &&
                string.Equals(old.Path, document.Path, StringComparison.Ordinal) &&
                string.Equals(old.Fingerprint, fingerprint, StringComparison.Ordinal) &&
                !string.IsNullOrEmpty(old.Seen))
                seen = old.Seen;

            result[document.Id] = new VaultIdentityEntry(document.Path, fingerprint, seen);
        }

        return new VaultIdentityState(result);
    }

    public static byte[] Serialize(VaultIdentityState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteNumber("version", Version);
            writer.WritePropertyName("docs");
            writer.WriteStartObject();

            foreach (var pair in state.Documents.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                writer.WritePropertyName(pair.Key);
                writer.WriteStartObject();
                writer.WriteString("path", pair.Value.Path);
                writer.WriteString("fingerprint", pair.Value.Fingerprint);
                writer.WriteString("seen", pair.Value.Seen);
                writer.WriteEndObject();
            }

            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        return stream.ToArray();
    }

    private static string ToBase36(int value)
    {
        const string alphabet = "0123456789abcdefghijklmnopqrstuvwxyz";
        if (value == 0)
            return "0";

        Span<char> buffer = stackalloc char[16];
        var index = buffer.Length;
        var current = value;

        while (current > 0)
        {
            buffer[--index] = alphabet[current % 36];
            current /= 36;
        }

        return new string(buffer[index..]);
    }
}
