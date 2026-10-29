using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Urbe.Core.Pages;

/// <summary>
/// CST-059: a page names its images by their place in the vault (an image block, a note's <c>![](Imagens/foto.png)</c>,
/// a background) but is shown by a WebView that reaches nothing outside the page and downloaded as a single file. So
/// each vault image travels inside the HTML as a data: URL. A path is tried from the page's folder, then from the vault
/// root, then as a file name found only once; web addresses, data: URLs and anything that is not an image stay as they are.
/// </summary>
public static partial class PageImages
{
    /// <summary>Larger images stay as links (a page is opened whole in memory).</summary>
    public const int MaxBytes = 16 * 1024 * 1024;

    public static async Task<string> InlineAsync(string html, string pagePath, IReadOnlyCollection<string> files, Func<string, Task<byte[]?>> read)
    {
        if (!html.Contains("src=\"", StringComparison.Ordinal) && !html.Contains("url(", StringComparison.Ordinal)) return html;
        var known = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in files) known.TryAdd(DocumentModel.NormalizePath(f), f);
        var folder = pagePath.Contains('/') ? pagePath[..pagePath.LastIndexOf('/')] : "";
        var found = new Dictionary<string, string?>(StringComparer.Ordinal);

        async Task<string?> Resolve(string raw)
        {
            if (found.TryGetValue(raw, out var done)) return done;
            string? url = null;
            if (Find(WebUtility.HtmlDecode(raw)) is { } path && await read(path) is { Length: > 0 and <= MaxBytes } bytes)
                url = "data:" + Mime(path) + ";base64," + Convert.ToBase64String(bytes);
            return found[raw] = url;
        }

        string? Find(string src)
        {
            src = src.Trim();
            if (src.Length == 0 || src.StartsWith('#') || src.StartsWith("//", StringComparison.Ordinal) || SchemeRegex().IsMatch(src)) return null;
            var cut = src.IndexOfAny(['?', '#']);
            if (cut >= 0) src = src[..cut];
            string decoded;
            try { decoded = Uri.UnescapeDataString(src); } catch (UriFormatException) { decoded = src; }
            if (Mime(decoded) is null) return null;
            foreach (var candidate in new[] { Join(folder, decoded), Join("", decoded) })
                if (candidate is not null && known.TryGetValue(candidate, out var file)) return file;
            var name = decoded.Replace('\\', '/').Split('/')[^1];
            var byName = known.Values.Where(f => string.Equals(f.Replace('\\', '/').Split('/')[^1], name, StringComparison.OrdinalIgnoreCase)).Take(2).ToList();
            return byName.Count == 1 ? byName[0] : null;
        }

        var result = new StringBuilder(html.Length);
        int at = 0;
        foreach (Match m in ImageRefRegex().Matches(html))
        {
            var group = m.Groups["src"].Success ? m.Groups["src"] : m.Groups["url"];
            if (await Resolve(group.Value) is not { } data) continue;
            result.Append(html, at, group.Index - at).Append(data);
            at = group.Index + group.Length;
        }
        return at == 0 ? html : result.Append(html, at, html.Length - at).ToString();
    }

    /// <summary>The vault path of <paramref name="relative"/> seen from <paramref name="folder"/>, or null when it leaves the vault.</summary>
    private static string? Join(string folder, string relative)
    {
        var parts = new List<string>();
        var rel = relative.Replace('\\', '/');
        if (rel.StartsWith('/')) folder = "";
        foreach (var p in (folder + "/" + rel).Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (p == ".") continue;
            if (p == "..") { if (parts.Count == 0) return null; parts.RemoveAt(parts.Count - 1); continue; }
            parts.Add(p);
        }
        return parts.Count == 0 ? null : string.Join("/", parts);
    }

    private static string? Mime(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".png" => "image/png", ".jpg" or ".jpeg" => "image/jpeg", ".gif" => "image/gif", ".webp" => "image/webp",
        ".svg" => "image/svg+xml", ".avif" => "image/avif", ".bmp" => "image/bmp", _ => null
    };

    [GeneratedRegex(@"<img\b[^>]*?\ssrc=""(?<src>[^""]*)""|url\(\s*['""]?(?<url>[^'""()]+?)['""]?\s*\)", RegexOptions.IgnoreCase)]
    private static partial Regex ImageRefRegex();

    [GeneratedRegex(@"^[a-zA-Z][a-zA-Z0-9+.-]*:")]
    private static partial Regex SchemeRegex();
}
