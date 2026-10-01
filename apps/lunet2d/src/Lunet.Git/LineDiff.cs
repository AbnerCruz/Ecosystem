using System.Text;

namespace Lunet.Git;

/// <summary>Diferença linha a linha (algoritmo de Myers) e formatação em diff unificado.</summary>
public static class LineDiff
{
    public enum Op { Equal, Delete, Insert }

    public readonly record struct Edit(Op Op, int OldIndex, int NewIndex);

    /// <summary>Sequência de edições que transforma <paramref name="a"/> em <paramref name="b"/>.</summary>
    public static List<Edit> Compute(IReadOnlyList<string> a, IReadOnlyList<string> b)
    {
        // Tira prefixo e sufixo comuns: a maioria das edições é pequena.
        var prefix = 0;
        while (prefix < a.Count && prefix < b.Count && a[prefix] == b[prefix]) prefix++;
        var suffix = 0;
        while (suffix < a.Count - prefix && suffix < b.Count - prefix && a[a.Count - 1 - suffix] == b[b.Count - 1 - suffix]) suffix++;

        var edits = new List<Edit>();
        for (var i = 0; i < prefix; i++) edits.Add(new Edit(Op.Equal, i, i));
        var middleA = a.Skip(prefix).Take(a.Count - prefix - suffix).ToArray();
        var middleB = b.Skip(prefix).Take(b.Count - prefix - suffix).ToArray();
        foreach (var edit in Myers(middleA, middleB))
            edits.Add(new Edit(edit.Op, edit.Op == Op.Insert ? -1 : edit.OldIndex + prefix, edit.Op == Op.Delete ? -1 : edit.NewIndex + prefix));
        for (var i = 0; i < suffix; i++) edits.Add(new Edit(Op.Equal, a.Count - suffix + i, b.Count - suffix + i));
        return edits;
    }

    private static List<Edit> Myers(string[] a, string[] b)
    {
        var n = a.Length;
        var m = b.Length;
        if (n == 0) return Enumerable.Range(0, m).Select(j => new Edit(Op.Insert, -1, j)).ToList();
        if (m == 0) return Enumerable.Range(0, n).Select(i => new Edit(Op.Delete, i, -1)).ToList();

        var max = n + m;
        var offset = max;
        var trace = new List<int[]>();
        var v = new int[2 * max + 2];
        for (var d = 0; d <= max; d++)
        {
            trace.Add((int[])v.Clone());
            for (var k = -d; k <= d; k += 2)
            {
                int x;
                if (k == -d || k != d && v[offset + k - 1] < v[offset + k + 1]) x = v[offset + k + 1];
                else x = v[offset + k - 1] + 1;
                var y = x - k;
                while (x < n && y < m && a[x] == b[y]) { x++; y++; }
                v[offset + k] = x;
                if (x >= n && y >= m) return Backtrack(trace, a, b, offset);
            }
        }
        throw new InvalidOperationException("Diff não convergiu.");
    }

    private static List<Edit> Backtrack(List<int[]> trace, string[] a, string[] b, int offset)
    {
        var edits = new List<Edit>();
        var x = a.Length;
        var y = b.Length;
        for (var d = trace.Count - 1; d >= 0; d--)
        {
            var v = trace[d];
            var k = x - y;
            int previousK;
            if (k == -d || k != d && v[offset + k - 1] < v[offset + k + 1]) previousK = k + 1;
            else previousK = k - 1;
            var previousX = v[offset + previousK];
            var previousY = previousX - previousK;
            while (x > previousX && y > previousY)
            {
                x--;
                y--;
                edits.Add(new Edit(Op.Equal, x, y));
            }
            if (d > 0)
            {
                if (x == previousX) { y--; edits.Add(new Edit(Op.Insert, -1, y)); }
                else { x--; edits.Add(new Edit(Op.Delete, x, -1)); }
            }
        }
        edits.Reverse();
        return edits;
    }

    /// <summary>Diff unificado (como <c>git diff</c>) entre dois textos. Devolve vazio se são iguais.</summary>
    public static string Unified(string oldPath, string newPath, string oldText, string newText, int context = 3)
    {
        var oldLines = SplitLines(oldText, out var oldNoNewline);
        var newLines = SplitLines(newText, out var newNoNewline);
        var edits = Compute(oldLines, newLines);
        if (edits.All(e => e.Op == Op.Equal) && oldNoNewline == newNoNewline) return "";

        var builder = new StringBuilder();
        builder.Append("--- ").Append(oldPath).Append('\n').Append("+++ ").Append(newPath).Append('\n');

        var i = 0;
        while (i < edits.Count)
        {
            while (i < edits.Count && edits[i].Op == Op.Equal) i++;
            if (i >= edits.Count) break;
            var start = Math.Max(0, i - context);
            var end = i;
            var lastChange = i;
            while (end < edits.Count)
            {
                if (edits[end].Op != Op.Equal) lastChange = end;
                else if (end - lastChange > context * 2) break;
                end++;
            }
            end = Math.Min(edits.Count, lastChange + context + 1);
            var oldStart = edits.Skip(start).Take(end - start).Where(e => e.Op != Op.Insert).Select(e => e.OldIndex).DefaultIfEmpty(-1).First();
            var newStart = edits.Skip(start).Take(end - start).Where(e => e.Op != Op.Delete).Select(e => e.NewIndex).DefaultIfEmpty(-1).First();
            var oldCount = edits.Skip(start).Take(end - start).Count(e => e.Op != Op.Insert);
            var newCount = edits.Skip(start).Take(end - start).Count(e => e.Op != Op.Delete);
            builder.Append($"@@ -{(oldCount == 0 ? Math.Max(0, oldStart) : oldStart + 1)},{oldCount} +{(newCount == 0 ? Math.Max(0, newStart) : newStart + 1)},{newCount} @@\n");
            for (var index = start; index < end; index++)
            {
                var edit = edits[index];
                switch (edit.Op)
                {
                    case Op.Equal:
                        builder.Append(' ').Append(oldLines[edit.OldIndex]).Append('\n');
                        if (edit.OldIndex == oldLines.Count - 1 && oldNoNewline) builder.Append("\\ No newline at end of file\n");
                        break;
                    case Op.Delete:
                        builder.Append('-').Append(oldLines[edit.OldIndex]).Append('\n');
                        if (edit.OldIndex == oldLines.Count - 1 && oldNoNewline) builder.Append("\\ No newline at end of file\n");
                        break;
                    default:
                        builder.Append('+').Append(newLines[edit.NewIndex]).Append('\n');
                        if (edit.NewIndex == newLines.Count - 1 && newNoNewline) builder.Append("\\ No newline at end of file\n");
                        break;
                }
            }
            i = end;
        }
        return builder.ToString();
    }

    public static List<string> SplitLines(string text, out bool noTrailingNewline)
    {
        noTrailingNewline = text.Length > 0 && text[^1] != '\n';
        if (text.Length == 0) return [];
        var lines = text.Split('\n').Select(l => l.TrimEnd('\r')).ToList();
        if (!noTrailingNewline) lines.RemoveAt(lines.Count - 1);
        return lines;
    }
}
