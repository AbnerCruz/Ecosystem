using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Urbe.Core;

/// <summary>A note as the 1.8.4 documents service hands it to the search ({id,title,path,content,modified}).</summary>
public sealed record LegacySearchDoc(string Id, string Title, string Path, string Content, string? Modified = null);

/// <summary>The excerpt shown under a result: before + <c>match</c> + after.</summary>
public sealed record LegacySearchSnippet(string Before, string Match, string After);

/// <summary>One ranked note: the document, its score and, when the content matched, the excerpt.</summary>
public sealed record LegacySearchHit(LegacySearchDoc Doc, int Score, LegacySearchSnippet? Snippet);

/// <summary>A row of the quick-open list: either a note (<see cref="Doc"/>) or "Criar nota" (<see cref="Create"/>).</summary>
public sealed record LegacyQuickOpenRow(LegacySearchDoc? Doc, string? Create, LegacySearchSnippet? Snippet, string Folder);

/// <summary>What src/ui/quick-open.js render() puts on the screen for a query.</summary>
public sealed record LegacyQuickOpenView(IReadOnlyList<LegacyQuickOpenRow> Rows, bool RecentGroup)
{
    /// <summary>"Nenhuma nota ainda. Digite um nome para criar a primeira." is shown when there is no row.</summary>
    public bool Empty => Rows.Count == 0;
}

/// <summary>JavaScript string semantics the 1.8.4 search relies on (trim, \s, slice, toLowerCase, localeCompare).</summary>
internal static class JsText
{
    /// <summary>The characters of the JavaScript <c>\s</c> class (WhiteSpace + LineTerminator).</summary>
    public const string Space = "\t\n\v\f\r \u00a0\u1680\u2000-\u200a\u2028\u2029\u202f\u205f\u3000\ufeff";
    private static readonly Regex Spaces = new("[" + Space + "]+", RegexOptions.CultureInvariant);
    private static readonly CompareInfo Collation = CultureInfo.InvariantCulture.CompareInfo;

    public static string Trim(string s) => s.Trim(SpaceChars);
    private static readonly char[] SpaceChars = ExpandSpace();

    private static char[] ExpandSpace()
    {
        var list = new List<char> { '\t', '\n', '\v', '\f', '\r', ' ', '\u00a0', '\u1680', '\u2028', '\u2029', '\u202f', '\u205f', '\u3000', '\ufeff' };
        for (var c = '\u2000'; c <= '\u200a'; c++) list.Add(c);
        return [.. list];
    }

    /// <summary><c>s.replace(/\s+/g,' ')</c></summary>
    public static string CollapseSpaces(string s) => Spaces.Replace(s, " ");

    /// <summary><c>s.split(/\s+/).filter(Boolean)</c></summary>
    public static string[] Words(string s) => Spaces.Split(s).Where(w => w.Length > 0).ToArray();

    /// <summary>String.prototype.slice(a, b) for non-negative indexes (clamped).</summary>
    public static string Slice(string s, int start, int end)
    {
        start = Math.Clamp(start, 0, s.Length);
        end = Math.Clamp(end, 0, s.Length);
        return end > start ? s.Substring(start, end - start) : "";
    }

    /// <summary>String.prototype.localeCompare (ICU collation).</summary>
    public static int LocaleCompare(string a, string b) => Collation.Compare(a, b, CompareOptions.None);

    /// <summary>String.prototype.toLowerCase: full case mapping (İ → i̇) and the final-sigma rule.</summary>
    public static string Lower(string s)
    {
        var sb = new StringBuilder(s.Length);
        for (var i = 0; i < s.Length; i++)
        {
            var c = s[i];
            if (c == 'İ') sb.Append("i̇");
            else if (c == 'Σ') sb.Append(FinalSigma(s, i) ? 'ς' : 'σ');
            else sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString();
    }

    /// <summary>String.prototype.toUpperCase for the single-unit keys of keymap.normalize.</summary>
    public static string Upper(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var c in s)
        {
            if (c == 'ß') sb.Append("SS");
            else if (c == 'ŉ') sb.Append("ʼN");
            else if (c == 'ǰ') sb.Append("J̌");
            else if (c == 'ﬀ') sb.Append("FF");
            else if (c == 'ﬁ') sb.Append("FI");
            else if (c == 'ﬂ') sb.Append("FL");
            else sb.Append(char.ToUpperInvariant(c));
        }
        return sb.ToString();
    }

    private static bool Cased(char c) => char.GetUnicodeCategory(c) is UnicodeCategory.UppercaseLetter or UnicodeCategory.LowercaseLetter or UnicodeCategory.TitlecaseLetter;

    private static bool CaseIgnorable(char c) =>
        c is '\'' or '.' or ':' or '·' or '’' or '‘' or '․' or '‧' or '^' or '`' or '¨' or '­' or '¯' or '´' or '¸'
        || char.GetUnicodeCategory(c) is UnicodeCategory.NonSpacingMark or UnicodeCategory.EnclosingMark or UnicodeCategory.Format or UnicodeCategory.ModifierLetter or UnicodeCategory.ModifierSymbol;

    private static bool FinalSigma(string s, int at)
    {
        var before = false;
        for (var i = at - 1; i >= 0; i--)
        {
            if (CaseIgnorable(s[i])) continue;
            before = Cased(s[i]);
            break;
        }
        if (!before) return false;
        for (var i = at + 1; i < s.Length; i++)
        {
            if (CaseIgnorable(s[i])) continue;
            return !Cased(s[i]);
        }
        return true;
    }
}

/// <summary>
/// src/ui/quick-open.js (1.8.4-beta): the "Buscar notas" ranking and the list it shows. Ported
/// line by line: fold (no accents, no case, character by character), the score ladder (title equal 1000
/// &gt; starts with 700 &gt; contains 500 &gt; every word in the title 400 &gt; path 300 &gt; content 100, plus
/// 5 per extra occurrence up to 50), the excerpt around the first match, the "Recentes" list and the
/// "Criar nota" row. Verified against the original run by Node (tools/search-oracle.mjs).
/// </summary>
public static class LegacyQuickOpen
{
    private static readonly Regex Heading = new(@"^[" + JsText.Space + @"]*#[" + JsText.Space + @"]+([^\n\r\u2028\u2029]+)\n?", RegexOptions.CultureInvariant);
    private static readonly Regex LeadingHashes = new(@"^#+[" + JsText.Space + @"]*", RegexOptions.CultureInvariant);
    private static readonly Regex NoteExtension = new(@"\.(md|markdown)\z", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>fold(): NFD per UTF-16 unit with the combining marks U+0300–U+036F removed, then toLowerCase.
    /// Indexes stay valid in the original text because the removal is made character by character.</summary>
    public static string Fold(string? s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var sb = new StringBuilder(s.Length);
        foreach (var c in s)
        {
            if (c < 'À') { sb.Append(c); continue; } // ASCII and Latin-1 punctuation have no decomposition
            string decomposed;
            try { decomposed = c.ToString().Normalize(NormalizationForm.FormD); }
            catch (ArgumentException) { sb.Append(c); continue; } // a lone surrogate
            var kept = new StringBuilder(decomposed.Length);
            foreach (var d in decomposed) if (d < '̀' || d > 'ͯ') kept.Append(d);
            if (kept.Length == 0) sb.Append(c); else sb.Append(kept);
        }
        return JsText.Lower(sb.ToString());
    }

    /// <summary>rank(all, query, limit): best matches first; an empty query ranks nothing.</summary>
    public static IReadOnlyList<LegacySearchHit> Rank(IEnumerable<LegacySearchDoc> all, string query, int limit = 0)
    {
        var q = JsText.Trim(Fold(query));
        var max = limit > 0 ? limit : 50;
        if (q.Length == 0) return [];
        var words = JsText.Words(q);
        var found = new List<LegacySearchHit>();
        foreach (var doc in all)
        {
            // the heading that repeats the title does not count as an excerpt of the content
            var content = doc.Content ?? "";
            var head = Heading.Match(content);
            var skip = head.Success && JsText.Trim(Fold(head.Groups[1].Value)) == JsText.Trim(Fold(doc.Title)) ? head.Length : 0;
            var title = Fold(doc.Title);
            var path = Fold(doc.Path);
            var text = content.Substring(skip);
            var body = Fold(text);
            var score = 0;
            LegacySearchSnippet? snippet = null;
            if (title == q) score = 1000;
            else if (title.StartsWith(q, StringComparison.Ordinal)) score = 700;
            else if (title.Contains(q, StringComparison.Ordinal)) score = 500;
            else if (words.All(w => title.Contains(w, StringComparison.Ordinal))) score = 400;
            else if (path.Contains(q, StringComparison.Ordinal)) score = 300;
            var first = body.IndexOf(q, StringComparison.Ordinal);
            var at = first;
            if (at < 0 && words.Length > 1 && words.All(w => body.Contains(w, StringComparison.Ordinal)))
                at = body.IndexOf(words[0], StringComparison.Ordinal);
            if (at >= 0)
            {
                if (score == 0) score = 100;
                score += Math.Min(50, Occurrences(body, q) * 5);
                var len = at == first ? q.Length : words[0].Length;
                var start = Math.Max(0, at - 36);
                var end = Math.Min(text.Length, at + len + 64);
                snippet = new LegacySearchSnippet(
                    (start > 0 ? "…" : "") + LeadingHashes.Replace(JsText.CollapseSpaces(JsText.Slice(text, start, at)), ""),
                    JsText.Slice(text, at, at + len),
                    JsText.CollapseSpaces(JsText.Slice(text, at + len, end)) + (end < text.Length ? "…" : ""));
            }
            if (score > 0) found.Add(new LegacySearchHit(doc, score, snippet));
        }
        return found.OrderByDescending(h => h.Score)
            .ThenBy(h => h.Doc.Title, Comparer<string>.Create(JsText.LocaleCompare))
            .Take(max).ToList();
    }

    /// <summary>body.split(q).length - 1: non-overlapping occurrences.</summary>
    private static int Occurrences(string body, string q)
    {
        var n = 0;
        for (var i = body.IndexOf(q, StringComparison.Ordinal); i >= 0; i = body.IndexOf(q, i + q.Length, StringComparison.Ordinal)) n++;
        return n;
    }

    /// <summary>recentDocs(): the Explorer's recent ids, then the most recently modified notes; 12 at most.</summary>
    public static IReadOnlyList<LegacySearchDoc> Recent(IReadOnlyList<LegacySearchDoc> all, IEnumerable<string>? recentIds)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<LegacySearchDoc>();
        foreach (var id in recentIds ?? [])
            if (all.FirstOrDefault(d => d.Id == id) is { } d && seen.Add(d.Id)) result.Add(d);
        foreach (var d in all.OrderBy(d => d.Modified ?? "", Comparer<string>.Create((a, b) => JsText.LocaleCompare(b, a))))
            if (seen.Add(d.Id)) result.Add(d);
        return result.Take(12).ToList();
    }

    /// <summary>render(): what the list shows for the text typed so far.</summary>
    public static LegacyQuickOpenView View(IReadOnlyList<LegacySearchDoc> all, IEnumerable<string>? recentIds, string typed)
    {
        var q = JsText.Trim(typed ?? "");
        var rows = new List<LegacyQuickOpenRow>();
        var group = false;
        if (q.Length == 0)
        {
            foreach (var d in Recent(all, recentIds)) rows.Add(Row(d, null));
            group = rows.Count > 0;
        }
        else
        {
            foreach (var hit in Rank(all, q, 40)) rows.Add(Row(hit.Doc, hit.Snippet));
            var exact = rows.Any(r => Fold(r.Doc!.Title) == Fold(q));
            if (!exact && q.IndexOfAny(['\\', '/', ':', '*', '?', '"', '<', '>', '|']) < 0)
                rows.Add(new LegacyQuickOpenRow(null, q, null, ""));
        }
        return new LegacyQuickOpenView(rows, group);
    }

    private static LegacyQuickOpenRow Row(LegacySearchDoc d, LegacySearchSnippet? snippet) =>
        new(d, null, snippet, d.Path.Contains('/') ? d.Path[..d.Path.LastIndexOf('/')] : "");

    /// <summary>pick() of "Criar nota": the path of the note typed (<c>name.replace(RE.note,'')+'.md'</c>), in the root.</summary>
    public static string CreatePath(string typed) => NoteExtension.Replace(typed, "") + ".md";

    /// <summary>Arrow keys: ↓ min(active+1, last), ↑ max(0, active-1).</summary>
    public static int Move(int active, int delta, int count) =>
        delta > 0 ? Math.Min(active + 1, count - 1) : Math.Max(0, active - 1);

    /// <summary>After render(): active = min(active, max(0, rows-1)).</summary>
    public static int Clamp(int active, int count) => Math.Min(active, Math.Max(0, count - 1));
}

/// <summary>A registered command as the palette sees it (id, title, category, enabled).</summary>
public sealed record LegacyCommand(string Id, string Title, string Category, Func<bool>? Enabled = null)
{
    public bool IsEnabled => Enabled?.Invoke() ?? true;
}

/// <summary>src/ui/command-palette.js (1.8.4-beta): which commands the "Digite um comando…" list shows.</summary>
public static class LegacyCommandPalette
{
    /// <summary>score(command, q): 4 starts with, 3 title contains, 2 anywhere in "title category id", 0 no; 1 when q is empty.</summary>
    public static int Score(LegacyCommand command, string q)
    {
        var text = JsText.Lower(command.Title + " " + command.Category + " " + command.Id);
        if (q.Length == 0) return 1;
        if (text.StartsWith(q, StringComparison.Ordinal)) return 4;
        if (JsText.Lower(command.Title).Contains(q, StringComparison.Ordinal)) return 3;
        return text.Contains(q, StringComparison.Ordinal) ? 2 : 0;
    }

    /// <summary>render(): enabled commands that match, best first, one per "category|title" (the last registered wins), 40 at most.</summary>
    public static IReadOnlyList<LegacyCommand> Filter(IEnumerable<LegacyCommand> commands, string typed)
    {
        var q = JsText.Lower(JsText.Trim(typed ?? ""));
        var sorted = commands.Select(c => (Command: c, Score: Score(c, q)))
            .Where(x => x.Score > 0 && x.Command.IsEnabled)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Command.Title, Comparer<string>.Create(JsText.LocaleCompare))
            .Select(x => x.Command).ToList();
        return sorted.Where((c, i) =>
            {
                var key = c.Category + "|" + c.Title;
                return !sorted.Skip(i + 1).Any(o => o.Category + "|" + o.Title == key);
            })
            .Take(40).ToList();
    }
}

/// <summary>A keydown as src/core/keymap.js reads it.</summary>
public sealed record LegacyKeyInput(string Key, bool Ctrl = false, bool Meta = false, bool Alt = false, bool Shift = false);

/// <summary>What the keymap decides for a keydown: the command to run (or null) and whether the key is swallowed.</summary>
public sealed record LegacyKeyResolution(string? CommandId, bool PreventDefault);

/// <summary>src/core/keymap.js (1.8.4-beta): shortcut normalization and the default bindings.</summary>
public static class LegacyKeymap
{
    private sealed record Binding(string Shortcut, string CommandId, bool AllowInEditor);

    // The original binds 'Mod+\\\\' (two backslashes), which normalize() can never produce: kept as written.
    private static readonly Binding[] Defaults =
    [
        new("Mod+K", "ui.commandPalette.open", true),
        new("Mod+P", "ui.quickOpen.open", true),
        new("Mod+S", "workspace.save", true),
        new("Mod+W", "editor.closeTab", true),
        new("Mod+F", "ui.find.open", true),
        new("Alt+ArrowLeft", "editor.back", true),
        new("Alt+ArrowRight", "editor.forward", true),
        new("Mod+\\\\", "editor.split.open", true),
        new("Mod+N", "document.create", false),
        new("Mod+Shift+F", "ui.explorer.open", true)
    ];

    /// <summary>keymap.list(): the bindings in registration order.</summary>
    public static IReadOnlyList<(string Shortcut, string CommandId)> List() => Defaults.Select(b => (b.Shortcut, b.CommandId)).ToList();

    /// <summary>normalize(event): "Mod+Alt+Shift+Key" (Ctrl or Meta is Mod; one-character keys are upper-cased).</summary>
    public static string Normalize(LegacyKeyInput e)
    {
        var parts = new List<string>();
        if (e.Ctrl || e.Meta) parts.Add("Mod");
        if (e.Alt) parts.Add("Alt");
        if (e.Shift) parts.Add("Shift");
        parts.Add(e.Key.Length == 1 ? JsText.Upper(e.Key) : e.Key);
        return string.Join('+', parts);
    }

    /// <summary>The keydown listener: a binding runs unless focus is in an input/textarea/contentEditable and the binding
    /// is not allowInEditor, or its command is not registered; a handled key is preventDefault-ed.</summary>
    public static LegacyKeyResolution Resolve(LegacyKeyInput e, bool targetIsEditable, Func<string, bool> hasCommand) =>
        Resolve(e, targetIsEditable, hasCommand, null);

    /// <summary>
    /// UC-61: the same, with the person's own shortcuts (Personalização/atalhos.json) over the defaults — a shortcut there
    /// runs its command (an empty one turns the default off). A shortcut without Mod or Alt never runs while typing.
    /// </summary>
    public static LegacyKeyResolution Resolve(LegacyKeyInput e, bool targetIsEditable, Func<string, bool> hasCommand, IReadOnlyDictionary<string, string>? user)
    {
        var shortcut = Normalize(e);
        if (user is not null && user.TryGetValue(shortcut, out var mine))
        {
            if (mine.Length == 0 || !hasCommand(mine)) return new LegacyKeyResolution(null, false);
            if (targetIsEditable && !shortcut.StartsWith("Mod+", StringComparison.Ordinal) && !shortcut.StartsWith("Alt+", StringComparison.Ordinal)) return new LegacyKeyResolution(null, false);
            return new LegacyKeyResolution(mine, true);
        }
        var binding = Defaults.FirstOrDefault(b => b.Shortcut == shortcut);
        if (binding is null) return new LegacyKeyResolution(null, false);
        if (targetIsEditable && !binding.AllowInEditor) return new LegacyKeyResolution(null, false);
        if (!hasCommand(binding.CommandId)) return new LegacyKeyResolution(null, false);
        return new LegacyKeyResolution(binding.CommandId, true);
    }
}

/// <summary>
/// UC-61: the person's own shortcuts, Personalização/atalhos.json — {"versao": 1, "atalhos": {"Mod+Shift+K": "ui.constellation",
/// "Mod+N": ""}}. Keys are written as the keymap normalizes them ("Mod+Alt+Shift+Key"); an empty command turns a default off.
/// </summary>
public static class UserShortcuts
{
    public const string Path = "Personalização/atalhos.json";

    public const string Template = """
        {
          "versao": 1,
          "atalhos": {
            "Mod+Shift+K": "ui.constellation",
            "Mod+Shift+E": "ui.workspace",
            "Mod+B": "ui.sidebar.toggle"
          }
        }
        """;

    /// <summary>The shortcuts of the file (shortcut → command id), or none when it is missing or unreadable.</summary>
    public static IReadOnlyDictionary<string, string> Parse(string? json)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(json)) return result;
        try
        {
            if (System.Text.Json.Nodes.JsonNode.Parse(json) is System.Text.Json.Nodes.JsonObject root && root["atalhos"] is System.Text.Json.Nodes.JsonObject map)
                foreach (var (key, value) in map)
                    if (Canonical(key) is { } shortcut && value is System.Text.Json.Nodes.JsonValue v && v.TryGetValue<string>(out var command)) result[shortcut] = command.Trim();
        }
        catch (System.Text.Json.JsonException) { }
        return result;
    }

    /// <summary>A shortcut as the keymap writes it (modifiers in the order Mod, Alt, Shift; one-letter keys upper-case), or null.</summary>
    public static string? Canonical(string written)
    {
        var parts = written.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return null;
        var key = parts[^1];
        bool mod = false, alt = false, shift = false;
        foreach (var p in parts[..^1])
            switch (p.ToLowerInvariant())
            {
                case "mod" or "ctrl" or "cmd" or "control": mod = true; break;
                case "alt": alt = true; break;
                case "shift": shift = true; break;
                default: return null;
            }
        return LegacyKeymap.Normalize(new LegacyKeyInput(key, mod, false, alt, shift));
    }
}
