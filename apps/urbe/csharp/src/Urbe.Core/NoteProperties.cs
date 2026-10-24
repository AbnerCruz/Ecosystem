using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Urbe.Core;

/// <summary>What a property holds: text, a list (aliases, tags…), a number, a date or a yes/no.</summary>
public enum PropertyKind { Text, List, Number, Date, Check, Raw }

/// <summary>One property of a note's frontmatter (24/10/2026). A <see cref="PropertyKind.Raw"/> one is something the form does not edit (a nested map, a multi-line text): it is kept as it is.</summary>
public sealed class NoteProperty
{
    public NoteProperty(string key, PropertyKind kind, string value, IReadOnlyList<string>? items = null, string? raw = null)
    {
        Key = key; Kind = kind; Value = value; Items = items ?? []; Raw = raw;
    }

    public string Key { get; }
    public PropertyKind Kind { get; }
    /// <summary>The text, number, date ("true"/"false" for a check).</summary>
    public string Value { get; }
    public IReadOnlyList<string> Items { get; }
    /// <summary>The lines as written, until the property is changed.</summary>
    public string? Raw { get; }
}

/// <summary>
/// The properties of a note — its frontmatter between <c>---</c> lines — read and written without disturbing what the form
/// does not understand: each property keeps its own lines until it is changed, the order stays, and a note with no
/// frontmatter gets one only when a property is added. <c>aliases</c>, <c>tags</c> and <c>cssclasses</c> are lists
/// (<c>[a, b]</c> or one per line with <c>-</c>); numbers, dates and yes/no are told apart by how they are written.
/// </summary>
public sealed partial class NoteProperties
{
    /// <summary>Properties that are always lists.</summary>
    public static readonly IReadOnlyList<string> ListKeys = ["aliases", "tags", "cssclasses"];

    private readonly List<NoteProperty> _items;
    private readonly string _body;

    private NoteProperties(List<NoteProperty> items, string body) { _items = items; _body = body; }

    public IReadOnlyList<NoteProperty> Items => _items;

    public NoteProperty? Get(string key) => _items.FirstOrDefault(p => string.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase));

    public IReadOnlyList<string> Aliases => Get("aliases")?.Items ?? [];
    public IReadOnlyList<string> Tags => Get("tags")?.Items ?? [];

    /// <summary>The aliases of a note's Markdown (for resolving links).</summary>
    public static IReadOnlyList<string> AliasesOf(string? markdown) =>
        markdown is not null && markdown.StartsWith("---", StringComparison.Ordinal) ? Parse(markdown).Aliases : [];

    // ------------------------------------------------------------------ reading

    public static NoteProperties Parse(string? markdown)
    {
        var text = (markdown ?? "").Replace("\r\n", "\n");
        var items = new List<NoteProperty>();
        if (!text.StartsWith("---\n", StringComparison.Ordinal)) return new NoteProperties(items, text);
        var close = text.IndexOf("\n---", 3, StringComparison.Ordinal);
        while (close >= 0 && close + 4 < text.Length && text[close + 4] != '\n') close = text.IndexOf("\n---", close + 1, StringComparison.Ordinal);
        if (close < 0) return new NoteProperties(items, text);
        var body = close + 4 >= text.Length ? "" : text[(close + 5)..];
        var lines = text[4..close].Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var m = KeyLine().Match(line);
            if (!m.Success) { if (line.Trim().Length > 0) items.Add(new NoteProperty("", PropertyKind.Raw, line, raw: line)); continue; }
            var key = m.Groups[1].Value;
            var rest = m.Groups[2].Value.Trim();
            var block = new List<string> { line };
            while (i + 1 < lines.Length && (lines[i + 1].StartsWith(' ') || lines[i + 1].StartsWith('\t') || lines[i + 1].TrimStart().StartsWith("- ", StringComparison.Ordinal) || lines[i + 1].Trim() == "-"))
            {
                block.Add(lines[++i]);
            }
            var raw = string.Join("\n", block);
            items.Add(Read(key, rest, block.Skip(1).ToList(), raw));
        }
        return new NoteProperties(items, body);
    }

    private static NoteProperty Read(string key, string rest, List<string> more, string raw)
    {
        bool listKey = ListKeys.Contains(key, StringComparer.OrdinalIgnoreCase);
        var dashes = more.Where(l => l.Trim().Length > 0).ToList();
        if (rest.Length == 0 && dashes.Count > 0 && dashes.All(l => l.TrimStart().StartsWith('-')))
            return new NoteProperty(key, PropertyKind.List, "", dashes.Select(l => Unquote(l.TrimStart()[1..].Trim())).Where(s => s.Length > 0).ToList(), raw);
        if (dashes.Count > 0) return new NoteProperty(key, PropertyKind.Raw, rest, raw: raw);   // a nested map or a folded text
        if (rest.StartsWith('[') && rest.EndsWith(']'))
            return new NoteProperty(key, PropertyKind.List, "", Split(rest[1..^1]), raw);
        if (listKey && rest.Length == 0) return new NoteProperty(key, PropertyKind.List, "", [], raw);
        if (listKey) return new NoteProperty(key, PropertyKind.List, "", rest.Split(rest.Contains(',') ? ',' : ' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(Unquote).ToList(), raw);
        if (rest is "|" or ">" or "|-" or ">-") return new NoteProperty(key, PropertyKind.Raw, rest, raw: raw);
        var value = Unquote(rest);
        if (rest.Length > 0 && !rest.StartsWith('"') && !rest.StartsWith('\''))
        {
            if (rest is "true" or "false") return new NoteProperty(key, PropertyKind.Check, rest, raw: raw);
            if (Regex.IsMatch(rest, @"^-?\d+([.,]\d+)?$")) return new NoteProperty(key, PropertyKind.Number, rest, raw: raw);
            if (Regex.IsMatch(rest, @"^\d{4}-\d{2}-\d{2}([T ]\d{2}:\d{2}(:\d{2})?)?$")) return new NoteProperty(key, PropertyKind.Date, rest, raw: raw);
        }
        return new NoteProperty(key, PropertyKind.Text, value, raw: raw);
    }

    private static List<string> Split(string inner) =>
        [.. inner.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(Unquote).Where(s => s.Length > 0)];

    private static string Unquote(string s)
    {
        s = s.Trim();
        if (s.Length >= 2 && ((s[0] == '"' && s[^1] == '"') || (s[0] == '\'' && s[^1] == '\'')))
            return s[0] == '"' ? s[1..^1].Replace("\\\"", "\"").Replace("\\\\", "\\") : s[1..^1].Replace("''", "'");
        return s;
    }

    // ------------------------------------------------------------------ writing

    /// <summary>Sets a text, number, date or yes/no (the property is added at the end when it is new).</summary>
    public NoteProperties Set(string key, PropertyKind kind, string value) => Replace(key, new NoteProperty(key, kind, value, raw: null));

    /// <summary>Sets a list; an empty one is kept as <c>key: []</c>, so the property stays for the form.</summary>
    public NoteProperties SetList(string key, IEnumerable<string> items) =>
        Replace(key, new NoteProperty(key, PropertyKind.List, "", [.. items.Select(s => s.Trim()).Where(s => s.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase)], raw: null));

    public NoteProperties Remove(string key)
    {
        var next = _items.Where(p => !string.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase)).ToList();
        return new NoteProperties(next, _body);
    }

    private NoteProperties Replace(string key, NoteProperty property)
    {
        var next = new List<NoteProperty>(_items);
        int at = next.FindIndex(p => string.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase));
        if (at >= 0) next[at] = new NoteProperty(next[at].Key, property.Kind, property.Value, property.Items, null);
        else next.Add(property);
        return new NoteProperties(next, _body);
    }

    /// <summary>The note's Markdown with these properties; no frontmatter at all when there are none.</summary>
    public string Render()
    {
        if (_items.Count == 0) return _body;
        var sb = new StringBuilder("---\n");
        foreach (var p in _items) sb.Append(p.Raw ?? Write(p)).Append('\n');
        sb.Append("---\n");
        if (_body.Length > 0) sb.Append(_body);
        return sb.ToString();
    }

    private static string Write(NoteProperty p)
    {
        switch (p.Kind)
        {
            case PropertyKind.List:
                if (p.Items.Count == 0) return p.Key + ": []";
                return p.Key + ":\n" + string.Join("\n", p.Items.Select(i => "  - " + Quote(i)));
            case PropertyKind.Check: return p.Key + ": " + (p.Value == "true" ? "true" : "false");
            case PropertyKind.Number: return p.Key + ": " + (decimal.TryParse(p.Value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var n) ? n.ToString(CultureInfo.InvariantCulture) : Quote(p.Value));
            case PropertyKind.Date: return p.Key + ": " + (p.Value.Length > 0 && Regex.IsMatch(p.Value, @"^\d{4}-\d{2}-\d{2}") ? p.Value : Quote(p.Value));
            default: return p.Key + ": " + (p.Value.Length == 0 ? "" : Quote(p.Value));
        }
    }

    /// <summary>A value as it is written: plain when that reads back the same, else in double quotes.</summary>
    private static string Quote(string s)
    {
        bool plain = s.Length > 0 && s == s.Trim() && !Regex.IsMatch(s, @"^[\[\]{}>|*&!%@`'""#,?:\-]|: | #|,$")
            && s is not ("true" or "false" or "null" or "~") && !Regex.IsMatch(s, @"^-?\d+([.,]\d+)?$") && !Regex.IsMatch(s, @"^\d{4}-\d{2}-\d{2}");
        return plain ? s : "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }

    /// <summary>A tag as the form writes it: no leading <c>#</c>, spaces become hyphens.</summary>
    public static string NormalizeTag(string tag) => Regex.Replace(tag.Trim().TrimStart('#'), @"\s+", "-");

    [GeneratedRegex(@"^([^\s:#][^:]*?)\s*:(?:\s(.*)|$)")]
    private static partial Regex KeyLine();
}
