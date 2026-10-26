using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Urbe.Core;

/// <summary>The newest direct release for this app: its version, the file to download and its notes.</summary>
public sealed record AppRelease(string Version, string Url, string Notes);

/// <summary>
/// UC-24/UC-25: "Verificar atualização", the 1.8.4's update check (src/native/bridge.js update.check) for the native
/// client. The releases come from the Ecosystem's GitHub Releases filtered by the Urbe C# tag and by the asset of the
/// platform (docs/distribution/direct-releases.md: never /releases/latest of the whole repository). Only on request:
/// nothing is checked in the background, and the download is opened in the browser, never installed by the app.
/// </summary>
public static class AppUpdate
{
    /// <summary>The releases asked for (the newest 100, drafts included, which are skipped).</summary>
    public const string ReleasesUrl = "https://api.github.com/repos/AbnerCruz/Ecosystem/releases?per_page=100";

    /// <summary>The tag of a release of the native client: urbe-csharp-v2.0.0-beta.18.</summary>
    public const string TagPrefix = "urbe-csharp-v";

    private const string Downloads = "https://github.com/AbnerCruz/Ecosystem/releases/download/";

    /// <summary>The file of each platform in a release (urbe-csharp-mobile-release.yml).</summary>
    public static string AndroidAsset(string version) => "Urbe-CSharp-" + version + "-Android.apk";

    /// <inheritdoc cref="AndroidAsset"/>
    public static string WindowsAsset(string version) => "Urbe-CSharp-" + version + "-Windows-x64.zip";

    private static readonly Regex Semver = new(@"^v?(\d+)\.(\d+)\.(\d+)(?:-([0-9A-Za-z.-]+))?$", RegexOptions.CultureInvariant);

    /// <summary>parts(v): a version x.y.z with an optional pre-release, or null.</summary>
    public static (long Major, long Minor, long Patch, string Pre)? Parts(string? version)
    {
        var m = Semver.Match(version ?? "");
        if (!m.Success || !long.TryParse(m.Groups[1].Value, out var a) || !long.TryParse(m.Groups[2].Value, out var b) || !long.TryParse(m.Groups[3].Value, out var c)) return null;
        return (a, b, c, m.Groups[4].Value);
    }

    /// <summary>newer(a, b): a is a later version than b (a final version is later than its pre-releases; beta.10 after beta.9).</summary>
    public static bool Newer(string? a, string? b)
    {
        if (Parts(a) is not { } x || Parts(b) is not { } y) return false;
        if (x.Major != y.Major) return x.Major > y.Major;
        if (x.Minor != y.Minor) return x.Minor > y.Minor;
        if (x.Patch != y.Patch) return x.Patch > y.Patch;
        if (x.Pre == y.Pre) return false;
        if (x.Pre.Length == 0) return true;
        if (y.Pre.Length == 0) return false;
        return ComparePre(x.Pre, y.Pre) > 0;
    }

    // localeCompare(…, {numeric: true}): runs of digits compare as numbers, the rest as text
    private static int ComparePre(string a, string b)
    {
        int i = 0, j = 0;
        while (i < a.Length && j < b.Length)
        {
            if (char.IsAsciiDigit(a[i]) && char.IsAsciiDigit(b[j]))
            {
                int si = i, sj = j;
                while (i < a.Length && char.IsAsciiDigit(a[i])) i++;
                while (j < b.Length && char.IsAsciiDigit(b[j])) j++;
                var na = a[si..i].TrimStart('0');
                var nb = b[sj..j].TrimStart('0');
                if (na.Length != nb.Length) return na.Length.CompareTo(nb.Length);
                var c = string.CompareOrdinal(na, nb);
                if (c != 0) return c;
            }
            else
            {
                var c = string.Compare(a[i].ToString(), b[j].ToString(), StringComparison.OrdinalIgnoreCase);
                if (c != 0) return c;
                i++;
                j++;
            }
        }
        return (a.Length - i).CompareTo(b.Length - j);
    }

    /// <summary>
    /// directRelease(list): of the GitHub releases (JSON), the newest one of the native client that carries the
    /// platform's file (<paramref name="assetFor"/>) downloadable from the Ecosystem's releases; null when none.
    /// </summary>
    /// <summary>Every published, downloadable release on this response page, sorted by actual version.
    /// Use pagination at the caller if the user asks for older entries than the first page.
    /// Drafts, other products, invalid tags and foreign download URLs are rejected.</summary>
    public static IReadOnlyList<AppRelease> List(string json, Func<string, string> assetFor)
    {
        JsonNode? root;
        try { root = JsonNode.Parse(json); }
        catch (System.Text.Json.JsonException) { return []; }
        var releases = new Dictionary<string, AppRelease>(StringComparer.Ordinal);
        foreach (var rel in (root as JsonArray ?? []).OfType<JsonObject>())
        {
            if (rel["draft"] is JsonValue d && d.TryGetValue<bool>(out var draft) && draft) continue;
            var tag = Text(rel["tag_name"]);
            if (!tag.StartsWith(TagPrefix, StringComparison.Ordinal)) continue;
            var version = tag[TagPrefix.Length..];
            if (Parts(version) is null) continue;
            var expected = assetFor(version);
            var asset = (rel["assets"] as JsonArray ?? []).OfType<JsonObject>()
                .FirstOrDefault(a => Text(a["name"]) == expected &&
                    Text(a["browser_download_url"]).StartsWith(Downloads + tag + "/", StringComparison.Ordinal));
            if (asset is null) continue;
            releases.TryAdd(version, new AppRelease(version, Text(asset["browser_download_url"]), Text(rel["body"])));
        }
        var sorted = releases.Values.ToList();
        sorted.Sort((x, y) => Newer(x.Version, y.Version) ? -1 : Newer(y.Version, x.Version) ? 1 :
            string.CompareOrdinal(y.Version, x.Version));
        return sorted;
    }

    public static AppRelease? Pick(string json, Func<string, string> assetFor) =>
        List(json, assetFor).FirstOrDefault();

    private static string Text(JsonNode? n) => n is JsonValue v && v.TryGetValue<string>(out var s) ? s : "";
}
