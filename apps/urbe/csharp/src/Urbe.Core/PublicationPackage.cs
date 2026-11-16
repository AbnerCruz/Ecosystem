using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json.Nodes;

namespace Urbe.Core;

/// <summary>
/// CST-163: a publication delivered as a package. One ZIP with the publication (index.html, its images inside, so it
/// opens alone) and manifest.json: each file with its type, size and SHA-256, the publication's id and title, and the
/// version of each source it was made from. Someone who receives it can check that nothing changed on the way.
/// </summary>
public static partial class PublicationPackage
{
    private static readonly Dictionary<string, string> MediaTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".mp3"] = "audio/mpeg", [".m4a"] = "audio/mp4", [".ogg"] = "audio/ogg", [".oga"] = "audio/ogg", [".wav"] = "audio/wav",
        [".flac"] = "audio/flac", [".mp4"] = "video/mp4", [".webm"] = "video/webm", [".mov"] = "video/quicktime"
    };

    /// <summary>The audio and video the publication embeds (<c>![[aula.mp3]]</c>), by the name written, to be read before <see cref="Build"/>.</summary>
    public static IReadOnlyList<string> MediaReferences(string html) =>
        MediaEmbed().Matches(html).Select(m => System.Net.WebUtility.HtmlDecode(m.Groups[1].Value))
            .Where(r => MediaTypes.ContainsKey(Path.GetExtension(r))).Distinct(StringComparer.Ordinal).ToList();

    public static byte[] Build(PublicationProject project, string html, IReadOnlyDictionary<string, string> sourceVersions, DateTimeOffset at,
        IReadOnlyDictionary<string, byte[]?>? media = null)
    {
        // each embedded audio/video goes to media/ and plays from there; one missing from the vault says so in place
        var files = new List<(string Path, string Mime, byte[] Bytes)>();
        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        html = MediaEmbed().Replace(html, m =>
        {
            var reference = System.Net.WebUtility.HtmlDecode(m.Groups[1].Value);
            if (!MediaTypes.TryGetValue(Path.GetExtension(reference), out var mime)) return m.Value;
            if (media?.GetValueOrDefault(reference) is not { } bytes)
                return "<span class=\"missing-media\">[mídia não encontrada: " + System.Net.WebUtility.HtmlEncode(reference) + "]</span>";
            if (!names.TryGetValue(reference, out var packed))
            {
                packed = "media/" + (files.Count + 1) + "-" + ArtifactModel.SafeName(Path.GetFileName(reference));
                names[reference] = packed;
                files.Add((packed, mime, bytes));
            }
            var tag = mime.StartsWith("video/", StringComparison.Ordinal) ? "video" : "audio";
            return "<" + tag + " controls preload=\"metadata\" src=\"" + System.Net.WebUtility.HtmlEncode(packed) + "\" title=\"" +
                System.Net.WebUtility.HtmlEncode(Path.GetFileNameWithoutExtension(reference)) + "\"></" + tag + ">";
        });
        if (files.Count > 0) // the page's policy lets it play its own files (and nothing from elsewhere)
            html = html.Replace("default-src 'none';", "default-src 'none'; media-src 'self' file:;", StringComparison.Ordinal);
        var index = System.Text.Encoding.UTF8.GetBytes(html);
        var manifest = new JsonObject
        {
            ["format"] = "urbe-publication-package",
            ["version"] = 1,
            ["publicationId"] = project.PublicationId,
            ["title"] = project.Title,
            ["createdAt"] = at.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            ["files"] = new JsonArray(files.Prepend((Path: "index.html", Mime: "text/html; charset=utf-8", Bytes: index)).Select(f => (JsonNode)new JsonObject
            {
                ["path"] = f.Path, ["mime"] = f.Mime, ["bytes"] = f.Bytes.Length,
                ["sha256"] = Convert.ToHexString(SHA256.HashData(f.Bytes)).ToLowerInvariant()
            }).ToArray()),
            ["sources"] = new JsonObject(sourceVersions.OrderBy(kv => kv.Key, StringComparer.Ordinal)
                .Select(kv => KeyValuePair.Create(kv.Key, (JsonNode?)JsonValue.Create(kv.Value))))
        };
        using var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            Write(zip, "index.html", index);
            foreach (var (path, _, bytes) in files) Write(zip, path, bytes);
            Write(zip, "manifest.json", System.Text.Encoding.UTF8.GetBytes(manifest.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true })));
        }
        return output.ToArray();
    }

    /// <summary>"2,3 MB" / "840 KB": the size said when a product is saved.</summary>
    public static string Size(long bytes) => bytes >= 1 << 20
        ? (bytes / 1048576.0).ToString("0.0", System.Globalization.CultureInfo.GetCultureInfo("pt-BR")) + " MB"
        : Math.Max(1, (int)Math.Round(bytes / 1024.0)) + " KB";

    private static void Write(ZipArchive zip, string name, byte[] bytes)
    {
        using var entry = zip.CreateEntry(name, CompressionLevel.Optimal).Open();
        entry.Write(bytes);
    }

    [System.Text.RegularExpressions.GeneratedRegex("!<span class=\"wikilink\" data-note-name=\"([^\"]+)\">[^<]*</span>")]
    private static partial System.Text.RegularExpressions.Regex MediaEmbed();
}
