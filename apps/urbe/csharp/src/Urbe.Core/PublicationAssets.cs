using System.Net;
using System.Text.RegularExpressions;

namespace Urbe.Core;

/// <summary>
/// CST-162: a publication's HTML carries its own images, so the exported file works offline and anywhere (its policy
/// only allows <c>data:</c> images). <c>![[foto.png]]</c> becomes an image, and every vault image is embedded; one
/// that is not in the vault says so where it would be, never a broken picture.
/// </summary>
public static partial class PublicationAssets
{
    private static readonly Dictionary<string, string> Mimes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".png"] = "image/png", [".jpg"] = "image/jpeg", [".jpeg"] = "image/jpeg", [".gif"] = "image/gif",
        [".webp"] = "image/webp", [".svg"] = "image/svg+xml", [".bmp"] = "image/bmp"
    };

    /// <summary>The vault images the HTML refers to (as written: a name or a path), to be read before <see cref="Embed"/>.</summary>
    public static IReadOnlyList<string> References(string html) =>
        WikiImage().Matches(html).Select(m => WebUtility.HtmlDecode(m.Groups[1].Value))
            .Concat(Img().Matches(html).Select(m => WebUtility.HtmlDecode(m.Groups[1].Value)).Where(Local))
            .Where(p => Mimes.ContainsKey(Path.GetExtension(p))).Distinct(StringComparer.Ordinal).ToList();

    public static string Embed(string html, Func<string, byte[]?> read)
    {
        string Picture(string reference, string alt) =>
            read(reference) is { } bytes
                ? "<img src=\"data:" + Mimes[Path.GetExtension(reference)] + ";base64," + Convert.ToBase64String(bytes) + "\" alt=\"" + WebUtility.HtmlEncode(alt) + "\">"
                : "<span class=\"missing-image\">[imagem não encontrada: " + WebUtility.HtmlEncode(reference) + "]</span>";
        html = WikiImage().Replace(html, m =>
        {
            var reference = WebUtility.HtmlDecode(m.Groups[1].Value);
            return Mimes.ContainsKey(Path.GetExtension(reference)) ? Picture(reference, Path.GetFileNameWithoutExtension(reference)) : m.Value;
        });
        return Img().Replace(html, m =>
        {
            var reference = WebUtility.HtmlDecode(m.Groups[1].Value);
            return Local(reference) && Mimes.ContainsKey(Path.GetExtension(reference)) ? Picture(reference, WebUtility.HtmlDecode(m.Groups[2].Value)) : m.Value;
        });
    }

    /// <summary>
    /// <c>![[foto.png]]</c> (and <c>![[foto.png|300]]</c>) written as the Markdown image <c>![foto](foto.png)</c>, outside
    /// code, so EPUB and the office formats carry the picture like any other image; other embeds stay as they are.
    /// </summary>
    public static string WikiImagesToMarkdown(string markdown)
    {
        if (!markdown.Contains("![[", StringComparison.Ordinal)) return markdown;
        var lines = markdown.Split('\n');
        bool fence = false;
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].TrimStart().StartsWith("```", StringComparison.Ordinal)) { fence = !fence; continue; }
            if (fence) continue;
            lines[i] = WikiEmbed().Replace(lines[i], m =>
            {
                var target = m.Groups[1].Value.Trim();
                return Mimes.ContainsKey(Path.GetExtension(target))
                    ? "![" + Path.GetFileNameWithoutExtension(target) + "](" + Uri.EscapeDataString(target).Replace("%2F", "/") + ")"
                    : m.Value;
            });
        }
        return string.Join('\n', lines);
    }

    [GeneratedRegex(@"!\[\[([^\]|#]+)(?:\|[^\]]*)?\]\]")]
    private static partial Regex WikiEmbed();

    private static bool Local(string src) => !src.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && !src.Contains("://", StringComparison.Ordinal);

    [GeneratedRegex("!<span class=\"wikilink\" data-note-name=\"([^\"]+)\">[^<]*</span>")]
    private static partial Regex WikiImage();
    [GeneratedRegex("<img src=\"([^\"]*)\" alt=\"([^\"]*)\">")]
    private static partial Regex Img();
}
