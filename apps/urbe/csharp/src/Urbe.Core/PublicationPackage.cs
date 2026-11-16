using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json.Nodes;

namespace Urbe.Core;

/// <summary>
/// CST-163: a publication delivered as a package. One ZIP with the publication (index.html, its images inside, so it
/// opens alone) and manifest.json: each file with its type, size and SHA-256, the publication's id and title, and the
/// version of each source it was made from. Someone who receives it can check that nothing changed on the way.
/// </summary>
public static class PublicationPackage
{
    public static byte[] Build(PublicationProject project, string html, IReadOnlyDictionary<string, string> sourceVersions, DateTimeOffset at)
    {
        var index = System.Text.Encoding.UTF8.GetBytes(html);
        var manifest = new JsonObject
        {
            ["format"] = "urbe-publication-package",
            ["version"] = 1,
            ["publicationId"] = project.PublicationId,
            ["title"] = project.Title,
            ["createdAt"] = at.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            ["files"] = new JsonArray(new JsonObject
            {
                ["path"] = "index.html", ["mime"] = "text/html; charset=utf-8", ["bytes"] = index.Length,
                ["sha256"] = Convert.ToHexString(SHA256.HashData(index)).ToLowerInvariant()
            }),
            ["sources"] = new JsonObject(sourceVersions.OrderBy(kv => kv.Key, StringComparer.Ordinal)
                .Select(kv => KeyValuePair.Create(kv.Key, (JsonNode?)JsonValue.Create(kv.Value))))
        };
        using var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            Write(zip, "index.html", index);
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
}
