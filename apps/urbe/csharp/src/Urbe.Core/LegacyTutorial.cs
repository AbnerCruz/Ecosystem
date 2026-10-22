using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Urbe.Core;

/// <summary>
/// tutorial.js (1.8.4): the "Tutorial" folder created once per vault, its version (build-tutorial.mjs) and the mark
/// .urbe/tutorial.json that says it was already created in this vault.
/// </summary>
public static class LegacyTutorial
{
    public const string Start = "Tutorial/Comece aqui.md";
    public const string MarkPath = ".urbe/tutorial.json";
    public const string Folder = "Tutorial/";

    /// <summary>build-tutorial.mjs: sha256(JSON.stringify(files)) in hex, first 12 characters; files in path order.</summary>
    public static string Version(IEnumerable<(string Path, string Content)> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        var json = new StringBuilder("{");
        bool first = true;
        foreach (var (path, content) in files)
        {
            if (!first) json.Append(',');
            first = false;
            JsString(json, path);
            json.Append(':');
            JsString(json, content);
        }
        json.Append('}');
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(json.ToString())))[..12];
    }

    /// <summary>marcar: JSON.stringify({versao, em: new Date().toISOString()}).</summary>
    public static string Mark(string version, DateTimeOffset now)
    {
        var json = new StringBuilder("{\"versao\":");
        JsString(json, version);
        json.Append(",\"em\":");
        JsString(json, now.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture));
        return json.Append('}').ToString();
    }

    /// <summary>restaurar: the Tutorial files missing from the vault and those whose text differs from the original.</summary>
    public static (List<string> Missing, List<string> Changed) Compare(IEnumerable<(string Path, string Content)> files, Func<string, string?> current)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(current);
        List<string> missing = [], changed = [];
        foreach (var (path, content) in files)
        {
            var now = current(path);
            if (now is null) missing.Add(path);
            else if (!string.Equals(now, content, StringComparison.Ordinal)) changed.Add(path);
        }
        return (missing, changed);
    }

    /// <summary>JSON.stringify of a string (ECMAScript QuoteJSONString).</summary>
    private static void JsString(StringBuilder sb, string s)
    {
        sb.Append('"');
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\b': sb.Append("\\b"); break;
                case '\f': sb.Append("\\f"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (c < 0x20 || (char.IsSurrogate(c) && !(char.IsHighSurrogate(c) && i + 1 < s.Length && char.IsLowSurrogate(s[i + 1]))
                        && !(char.IsLowSurrogate(c) && i > 0 && char.IsHighSurrogate(s[i - 1]))))
                        sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    else sb.Append(c);
                    break;
            }
        }
        sb.Append('"');
    }
}
