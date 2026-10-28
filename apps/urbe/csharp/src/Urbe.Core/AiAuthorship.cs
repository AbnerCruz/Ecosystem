using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Urbe.Core;

/// <summary>
/// The Assistente's authorship seals, <c>{ia:"modelo"}</c>, kept by the Urbe and never by the agents (UC-51,
/// CST-027–033; contract: docs/csharp/AI-AUTHORSHIP-SEALS.md).
/// <list type="bullet">
/// <item>An authorship block is a heading, a paragraph line, a list item, a whole quote, a table, a fenced code block or
/// a display formula — the blocks the Visual editor draws, with lists split per item. Front matter, rules and
/// <c>:::</c> container fences are structure and are never sealed.</item>
/// <item>A sealed block carries one seal: at the end of its line, inside the last cell of a table, or on the line right
/// after a code block or display formula, so the Markdown stays valid in any editor.</item>
/// <item>Agents read notes without seals (<see cref="Strip"/>) and whatever seals they send are ignored; the app decides
/// the seals of the text they write (<see cref="Attribute"/>): unchanged and moved blocks keep their author, corrections
/// (spelling, accents, punctuation, a word or two, formatting, deletions) keep it too, and created or regenerated blocks
/// get the current model's seal, replacing an older one.</item>
/// <item>A person's edit (model null) never seals and never unseals a block that still exists: editing an AI block in
/// place, splitting or joining it keeps its seal; deleting the block takes the seal along.</item>
/// </list>
/// Blocks are matched like WikiWho's hierarchical attribution: identical blocks first (in order, then moved), then the
/// closest block of each changed region by shared words.
/// </summary>
public static partial class AiAuthorship
{
    /// <summary>One authorship block: its text without seals and the model that wrote it (null: the person).</summary>
    public sealed record Block(string Text, string? Model);

    private enum Place { LineEnd, LastCell, NextLine }

    private readonly record struct Unit(int Start, int End, Place Place);

    private sealed record Cut(int Start, int End, IReadOnlyList<string> Models, bool OwnLine);

    private sealed class Parsed
    {
        public required string Raw { get; init; }
        public required string Plain { get; init; }
        /// <summary>Raw index of each plain character (null: no seal was cut, the indices are the same).</summary>
        public int[]? Map { get; init; }
        /// <summary>The seals kept for attribution: where they were cut in the plain text, and whether on a line of their own.</summary>
        public required List<(int At, string Model, bool OwnLine)> Seals { get; init; }
        public int RawIndex(int plain) => Map is null ? plain : Map[plain];
    }

    /// <summary>The seal written after a block: <c>{ia:"modelo"}</c>, the model's short name (NoteMarks.AiMark).</summary>
    public static string Seal(string model) => NoteMarks.AiMark(model);

    // a seal already in the note keeps its name exactly as written
    private static string Token(string name) => "{ia:\"" + name + "\"}";

    private static string? Name(string? model) => model is null ? null : SealRegex().Match(Seal(model)).Groups[1].Value;

    /// <summary>True when the text has a seal outside code.</summary>
    public static bool HasSeals(string? markdown) =>
        markdown is not null && markdown.Contains("{ia:", StringComparison.Ordinal) && Parse(markdown).Map is not null;

    /// <summary>
    /// The text without its seals, as agents read it: each seal goes with one space next to it, a line holding only seals
    /// goes whole. Seals inside code stay, since there they are text.
    /// </summary>
    public static string Strip(string? markdown)
    {
        var text = markdown ?? "";
        return text.Contains("{ia:", StringComparison.Ordinal) ? Parse(text).Plain : text;
    }

    /// <summary>The authorship blocks of a note, in order.</summary>
    public static IReadOnlyList<Block> Blocks(string? markdown)
    {
        var parsed = Parse(Lf(markdown ?? ""));
        var units = Units(parsed.Plain);
        var models = Models(parsed, units);
        return units.Select((u, i) => new Block(parsed.Plain[u.Start..u.End], models[i])).ToArray();
    }

    /// <summary>
    /// The text with every block's seal in its canonical place and accumulated seals shown once — what the editor draws
    /// and pages print, so a seal stranded inside <c>**bold**</c> by an older version no longer breaks it. The note itself
    /// is only rewritten where someone edits.
    /// </summary>
    public static string Canonical(string? markdown)
    {
        var text = markdown ?? "";
        if (!text.Contains("{ia:", StringComparison.Ordinal)) return text;
        bool crlf = text.Contains("\r\n", StringComparison.Ordinal);
        var parsed = Parse(Lf(text));
        if (parsed.Map is null) return text;
        var units = Units(parsed.Plain);
        var models = Models(parsed, units);
        var sb = new StringBuilder(text.Length);
        int pos = 0;
        for (int j = 0; j < units.Count; j++)
        {
            var u = units[j];
            sb.Append(parsed.Plain, pos, u.Start - pos);
            sb.Append(Placed(parsed.Plain[u.Start..u.End], u.Place, models[j]));
            pos = u.End;
        }
        sb.Append(parsed.Plain, pos, parsed.Plain.Length - pos);
        var result = sb.ToString();
        return crlf ? result.Replace("\n", "\r\n", StringComparison.Ordinal) : result;
    }

    /// <summary>
    /// <paramref name="proposed"/> (its own seals ignored) with the seals the Urbe gives it after
    /// <paramref name="previous"/>: <paramref name="model"/> writes as the Assistente, null as the person. Unchanged blocks
    /// keep their bytes; only the blocks that changed are written in the canonical form.
    /// </summary>
    public static string Attribute(string? previous, string? proposed, string? model)
    {
        model = Name(model);
        var before = previous ?? "";
        var after = proposed ?? "";
        bool crlf = before.Length > 0 ? before.Contains("\r\n", StringComparison.Ordinal) : after.Contains("\r\n", StringComparison.Ordinal);
        before = Lf(before);
        after = Lf(after);
        var old = Parse(before);
        var plain = Parse(after).Plain;
        var oldUnits = Units(old.Plain);
        var oldModels = Models(old, oldUnits);
        var units = Units(plain);
        var oldText = oldUnits.Select(u => old.Plain[u.Start..u.End]).ToArray();
        var text = units.Select(u => plain[u.Start..u.End]).ToArray();

        var origin = Enumerable.Repeat(-1, units.Count).ToArray();
        var same = new bool[units.Count];
        var used = new bool[oldUnits.Count];
        var anchors = Anchors(oldText, text);
        foreach (var (i, j) in anchors) { origin[j] = i; same[j] = true; used[i] = true; }
        // a block moved elsewhere is still the same block
        var free = new Dictionary<string, Queue<int>>(StringComparer.Ordinal);
        for (int i = 0; i < oldText.Length; i++)
            if (!used[i]) { if (!free.TryGetValue(oldText[i], out var q)) free[oldText[i]] = q = new(); q.Enqueue(i); }
        for (int j = 0; j < text.Length; j++)
            if (!same[j] && free.TryGetValue(text[j], out var q) && q.Count > 0)
            {
                var i = q.Dequeue();
                origin[j] = i; same[j] = true; used[i] = true;
            }

        var models = new string?[units.Count];
        for (int j = 0; j < units.Count; j++) if (same[j]) models[j] = oldModels[origin[j]];
        // the changed regions between anchors: each new block against the old blocks it replaced
        int prevI = -1, prevJ = -1;
        foreach (var (ai, aj) in anchors.Append((oldUnits.Count, units.Count)))
        {
            var gapOld = Enumerable.Range(prevI + 1, Math.Max(0, ai - prevI - 1)).Where(i => !used[i]).ToList();
            var gapNew = Enumerable.Range(prevJ + 1, Math.Max(0, aj - prevJ - 1)).Where(j => !same[j]).ToList();
            if (gapNew.Count > 0) AttributeGap(gapOld, gapNew, oldText, oldModels, text, models, model);
            prevI = ai;
            prevJ = aj;
        }

        var sb = new StringBuilder(plain.Length + 32);
        int pos = 0;
        for (int j = 0; j < units.Count; j++)
        {
            var u = units[j];
            sb.Append(plain, pos, u.Start - pos);
            sb.Append(same[j] ? RawSpan(old, oldUnits[origin[j]]) : Placed(text[j], u.Place, models[j]));
            pos = u.End;
        }
        sb.Append(plain, pos, plain.Length - pos);
        var result = sb.ToString();
        return crlf ? result.Replace("\n", "\r\n", StringComparison.Ordinal) : result;
    }

    /// <summary>
    /// Erases the seals left alone on a line when their text was deleted (the line break stays, so unrelated paragraphs
    /// never join). The seal on the line right after a code block or display formula belongs to it and stays.
    /// </summary>
    public static string RemoveOrphans(string? markdown)
    {
        var text = markdown ?? "";
        if (!text.Contains("{ia:", StringComparison.Ordinal)) return text;
        var code = Code(text);
        var result = new StringBuilder(text.Length);
        int start = 0;
        bool changed = false;
        while (start <= text.Length)
        {
            int newline = text.IndexOf('\n', start);
            int end = newline < 0 ? text.Length : newline;
            var line = text[start..end];
            bool orphan = false;
            if (line.Contains("{ia:", StringComparison.Ordinal))
            {
                var marks = SealRegex().Matches(line).Where(m => !InCode(code, start + m.Index)).ToList();
                orphan = marks.Count > 0 && string.IsNullOrWhiteSpace(Without(line, marks)) && !ClosesBlock(text, start);
            }
            changed |= orphan;
            if (!orphan) result.Append(line);
            if (newline < 0) break;
            result.Append('\n');
            start = newline + 1;
        }
        return changed ? result.ToString() : text;
    }

    /// <summary>
    /// The drawn blocks of a note with each code block or display formula taking the seal line right after it, so the
    /// Visual editor draws and edits them together (the seal is never a block of its own).
    /// </summary>
    public static IReadOnlyList<MarkdownBlock> WithSealLines(IReadOnlyList<MarkdownBlock> blocks, string? markdown)
    {
        var text = markdown ?? "";
        if (!text.Contains("{ia:", StringComparison.Ordinal)) return blocks;
        var result = new List<MarkdownBlock>(blocks.Count);
        for (int i = 0; i < blocks.Count; i++)
        {
            var b = blocks[i];
            if (i + 1 < blocks.Count && blocks[i + 1] is { Kind: "paragraph" } next && next.Start == b.End + 1 && next.End <= text.Length &&
                SealRegex().IsMatch(text[next.Start..next.End]) && string.IsNullOrWhiteSpace(SealRegex().Replace(text[next.Start..next.End], "")) &&
                ClosesBlock(text, next.Start))
            {
                result.Add(b with { End = next.End });
                i++;
                continue;
            }
            result.Add(b);
        }
        return result;
    }

    // ---------------------------------------------------------------- attribution of a changed region

    private static void AttributeGap(List<int> gapOld, List<int> gapNew, string[] oldText, string?[] oldModels,
        string[] text, string?[] models, string? model)
    {
        var oldTokens = gapOld.ToDictionary(i => i, i => Tokens(oldText[i]));
        var pool = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var t in oldTokens.Values.SelectMany(x => x)) pool[t] = pool.GetValueOrDefault(t) + 1;
        bool compare = (long)gapOld.Count * gapNew.Count <= 250_000;
        for (int k = 0; k < gapNew.Count; k++)
        {
            int j = gapNew[k];
            var tokens = Tokens(text[j]);
            int best = -1;
            double score = 0;
            if (compare)
                foreach (var i in gapOld)
                {
                    var s = Dice(tokens, oldTokens[i]);
                    // a person's edit leans to the sealed block: a seal is never lost to a tie
                    if (s > score || (s == score && best >= 0 && model is null && oldModels[i] is not null && oldModels[best] is null))
                    {
                        score = s;
                        best = i;
                    }
                }
            if (score < .5) best = -1;
            // the same number of blocks in and out: each new block rewrites the one in its place
            if (best < 0 && gapOld.Count == gapNew.Count) best = gapOld[k];
            models[j] = model is null
                ? best >= 0 ? oldModels[best] : null
                : best >= 0 && IsCorrection(tokens, pool) ? oldModels[best] : model;
        }
    }

    /// <summary>
    /// A correction keeps the block's author: every word was already in the region (or is a near spelling of one), or
    /// only a few isolated words are new — no run of three new words, at most two (or a fifth of the block), and the
    /// kept words at least twice the new ones (replacing the only words of a short block is writing it anew).
    /// </summary>
    private static bool IsCorrection(List<string> tokens, Dictionary<string, int> pool)
    {
        int fresh = 0, run = 0, longest = 0;
        foreach (var t in tokens)
        {
            if (Take(pool, t) || TakeVariant(pool, t)) { run = 0; continue; }
            fresh++;
            longest = Math.Max(longest, ++run);
        }
        return fresh == 0 || (longest <= 2 && fresh <= Math.Max(2, tokens.Count / 5) && tokens.Count - fresh >= 2 * fresh);
    }

    private static bool Take(Dictionary<string, int> pool, string t)
    {
        if (!pool.TryGetValue(t, out var n) || n == 0) return false;
        pool[t] = n - 1;
        return true;
    }

    private static bool TakeVariant(Dictionary<string, int> pool, string t)
    {
        int allowed = Math.Max(1, t.Length / 4);
        foreach (var (key, n) in pool)
            if (n > 0 && Math.Abs(key.Length - t.Length) <= allowed && Distance(key, t, allowed) <= allowed)
            {
                pool[key] = n - 1;
                return true;
            }
        return false;
    }

    private static int Distance(string a, string b, int limit)
    {
        var prev = new int[b.Length + 1];
        var cur = new int[b.Length + 1];
        for (int j = 0; j <= b.Length; j++) prev[j] = j;
        for (int i = 1; i <= a.Length; i++)
        {
            cur[0] = i;
            int low = cur[0];
            for (int j = 1; j <= b.Length; j++)
            {
                cur[j] = Math.Min(Math.Min(cur[j - 1] + 1, prev[j] + 1), prev[j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
                low = Math.Min(low, cur[j]);
            }
            if (low > limit) return low;
            (prev, cur) = (cur, prev);
        }
        return prev[b.Length];
    }

    private static double Dice(List<string> a, List<string> b)
    {
        if (a.Count == 0 || b.Count == 0) return 0;
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var t in b) counts[t] = counts.GetValueOrDefault(t) + 1;
        int shared = 0;
        foreach (var t in a) if (Take(counts, t)) shared++;
        return 2.0 * shared / (a.Count + b.Count);
    }

    /// <summary>Words, lower case and without accents ("Não" and "nao" are the same word).</summary>
    private static List<string> Tokens(string text)
    {
        var d = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(d.Length);
        foreach (var c in d)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(char.ToLowerInvariant(c));
        return WordRegex().Matches(sb.ToString()).Select(m => m.Value).ToList();
    }

    /// <summary>The identical blocks kept in order: shared start and end, then the longest common sequence between.</summary>
    private static List<(int I, int J)> Anchors(string[] a, string[] b)
    {
        var result = new List<(int, int)>();
        int head = 0;
        while (head < a.Length && head < b.Length && a[head] == b[head]) { result.Add((head, head)); head++; }
        int tail = 0;
        while (tail < a.Length - head && tail < b.Length - head && a[a.Length - 1 - tail] == b[b.Length - 1 - tail]) tail++;
        int n = a.Length - head - tail, m = b.Length - head - tail;
        if (n > 0 && m > 0 && (long)n * m <= 1_000_000)
        {
            var lcs = new int[n + 1, m + 1];
            for (int i = n - 1; i >= 0; i--)
                for (int j = m - 1; j >= 0; j--)
                    lcs[i, j] = a[head + i] == b[head + j] ? lcs[i + 1, j + 1] + 1 : Math.Max(lcs[i + 1, j], lcs[i, j + 1]);
            for (int i = 0, j = 0; i < n && j < m;)
            {
                if (a[head + i] == b[head + j]) { result.Add((head + i, head + j)); i++; j++; }
                else if (lcs[i + 1, j] >= lcs[i, j + 1]) i++;
                else j++;
            }
        }
        for (int k = tail; k > 0; k--) result.Add((a.Length - k, b.Length - k));
        return result;
    }

    // ---------------------------------------------------------------- blocks and seal places

    private static List<Unit> Units(string plain)
    {
        var units = new List<Unit>();
        foreach (var b in MarkdownEngine.SplitBlocks(plain))
        {
            switch (b.Kind)
            {
                case "frontmatter" or "rule": break;
                case "code": units.Add(new(b.Start, b.End, Place.NextLine)); break;
                case "table": units.Add(new(b.Start, b.End, Place.LastCell)); break;
                case "list":
                    for (int s = b.Start; s < b.End;)
                    {
                        int e = plain.IndexOf('\n', s, b.End - s);
                        if (e < 0) e = b.End;
                        if (plain.AsSpan(s, e - s).Trim().Length > 0) units.Add(new(s, e, Place.LineEnd));
                        s = e + 1;
                    }
                    break;
                case "paragraph":
                    var body = plain.AsSpan(b.Start, b.End - b.Start);
                    if (body.TrimStart().StartsWith(":::", StringComparison.Ordinal)) break;
                    units.Add(new(b.Start, b.End, body.Contains('\n') ? Place.NextLine : Place.LineEnd));
                    break;
                default: units.Add(new(b.Start, b.End, Place.LineEnd)); break;
            }
        }
        return units;
    }

    /// <summary>The model of each block of the parsed text: its last seal (accumulated seals count as one).</summary>
    private static string?[] Models(Parsed parsed, List<Unit> units)
    {
        var models = new string?[units.Count];
        foreach (var (at, model, ownLine) in parsed.Seals)
        {
            int lo = 0, hi = units.Count - 1, found = -1;
            while (lo <= hi)
            {
                int mid = (lo + hi) / 2;
                if (units[mid].Start <= at) { found = mid; lo = mid + 1; } else hi = mid - 1;
            }
            if (found < 0 || at > units[found].End) continue;
            if (ownLine && (units[found].End != at || units[found].Place != Place.NextLine)) continue;
            models[found] = model;
        }
        return models;
    }

    private static string RawSpan(Parsed parsed, Unit u)
    {
        int start = u.Start == 0 ? 0 : parsed.RawIndex(u.Start - 1) + 1;
        int end = u.End >= parsed.Plain.Length ? parsed.Raw.Length : parsed.RawIndex(u.End);
        return parsed.Raw[start..end];
    }

    private static string Placed(string text, Place place, string? model)
    {
        if (model is null) return text;
        var seal = Token(model);
        if (place == Place.NextLine) return text + "\n" + seal;
        int lineStart = text.LastIndexOf('\n') + 1;
        int end = text.Length;
        if (place == Place.LastCell)
        {
            var row = text.AsSpan(lineStart).TrimEnd();
            if (row.EndsWith("|", StringComparison.Ordinal) && row.Length > 1) end = lineStart + row.Length - 1;
        }
        while (end > lineStart && text[end - 1] is ' ' or '\t') end--;
        return text[..end] + (end > lineStart ? " " : "") + seal + text[end..];
    }

    // ---------------------------------------------------------------- cutting the seals out

    private static Parsed Parse(string raw)
    {
        var none = new Parsed { Raw = raw, Plain = raw, Seals = [] };
        if (!raw.Contains("{ia:", StringComparison.Ordinal)) return none;
        var code = Code(raw);
        var marks = SealRegex().Matches(raw).Where(m => !InCode(code, m.Index)).ToList();
        if (marks.Count == 0) return none;
        var cuts = new List<Cut>();
        for (int k = 0; k < marks.Count;)
        {
            int lineStart = raw.LastIndexOf('\n', Math.Max(0, marks[k].Index - 1)) + 1;
            if (marks[k].Index == 0) lineStart = 0;
            int lineEnd = raw.IndexOf('\n', marks[k].Index);
            if (lineEnd < 0) lineEnd = raw.Length;
            var onLine = new List<Match>();
            while (k < marks.Count && marks[k].Index < lineEnd) onLine.Add(marks[k++]);
            var models = onLine.Select(m => m.Groups[1].Value).ToList();
            if (string.IsNullOrWhiteSpace(Without(raw[lineStart..lineEnd], onLine.Select(m => (m.Index - lineStart, m.Length)))))
            {
                // a line of seals only: it goes with the line break before it (the line's own break stays)
                var attached = ClosesBlock(raw, lineStart);
                cuts.Add(lineStart > 0
                    ? new Cut(lineStart - 1, lineEnd, attached ? models : [], true)
                    : new Cut(0, lineEnd < raw.Length ? lineEnd + 1 : lineEnd, [], true));
                continue;
            }
            foreach (var m in onLine)
            {
                int s = m.Index, e = m.Index + m.Length;
                int previousEnd = cuts.Count > 0 ? cuts[^1].End : 0;
                if (s > lineStart && s - 1 >= previousEnd && raw[s - 1] == ' ') s--;
                else if (e < lineEnd && raw[e] == ' ') e++;
                cuts.Add(new Cut(s, e, [m.Groups[1].Value], false));
            }
        }
        var plain = new StringBuilder(raw.Length);
        var map = new List<int>(raw.Length);
        var seals = new List<(int, string, bool)>();
        int pos = 0;
        foreach (var cut in cuts)
        {
            for (int i = pos; i < cut.Start; i++) { plain.Append(raw[i]); map.Add(i); }
            foreach (var model in cut.Models) seals.Add((plain.Length, model, cut.OwnLine));
            pos = Math.Max(pos, cut.End);
        }
        for (int i = pos; i < raw.Length; i++) { plain.Append(raw[i]); map.Add(i); }
        return new Parsed { Raw = raw, Plain = plain.ToString(), Map = map.ToArray(), Seals = seals };
    }

    /// <summary>The line before <paramref name="lineStart"/> closes fenced code or a display formula.</summary>
    private static bool ClosesBlock(string text, int lineStart)
    {
        if (lineStart == 0) return false;
        int previousStart = lineStart >= 2 ? text.LastIndexOf('\n', lineStart - 2) + 1 : 0;
        var previous = text[previousStart..(lineStart - 1)].TrimEnd('\r');
        return MarkdownEngine.IsFenceLine(previous) || previous.TrimEnd().EndsWith("$$", StringComparison.Ordinal);
    }

    private static string Without(string line, IEnumerable<Match> marks) => Without(line, marks.Select(m => (m.Index, m.Length)));

    private static string Without(string line, IEnumerable<(int Index, int Length)> marks)
    {
        var sb = new StringBuilder(line);
        foreach (var (index, length) in marks.OrderByDescending(m => m.Index)) sb.Remove(index, length);
        return sb.ToString();
    }

    private static List<(int Start, int End)> Code(string text) =>
        text.Contains('`') ? CodeRegex().Matches(text).Select(m => (m.Index, m.Index + m.Length)).ToList() : [];

    private static bool InCode(List<(int Start, int End)> code, int index) => code.Any(c => index >= c.Start && index < c.End);

    private static string Lf(string text) => text.Contains('\r') ? text.Replace("\r\n", "\n", StringComparison.Ordinal) : text;

    [GeneratedRegex(@"\{ia:""([^""\n]{1,80})""\}")]
    private static partial Regex SealRegex();

    // the same code spans NoteMarks leaves untouched
    [GeneratedRegex(@"```[\s\S]*?(?:```|$)|`[^`\n]*`")]
    private static partial Regex CodeRegex();

    [GeneratedRegex(@"[\p{L}\p{N}]+")]
    private static partial Regex WordRegex();
}
