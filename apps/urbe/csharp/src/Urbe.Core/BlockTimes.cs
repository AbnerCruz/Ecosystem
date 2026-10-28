using System.Globalization;

namespace Urbe.Core;

/// <summary>
/// CST-025: when each block of a note got its current text, read from the note's history ("Versões anteriores",
/// <see cref="LegacyHistory"/>) the way a blame reads a repository: no file of its own, so it can never disagree
/// with the note (NN-001). A revision is the text before a burst of edits and the time that burst began; a block
/// absent from it was written in that burst. Blocks compare by their text without the authorship seals, so a moved
/// block keeps its time, a split, joined or pasted one takes the time of that edit, and an undone change takes the
/// time of the undo. A block older than everything the history still keeps has no time (null).
/// </summary>
public static class BlockTimes
{
    public sealed record Entry(int Start, int End, long? Time, bool DayOnly = false);

    /// <summary>A block's time; <paramref name="DayOnly"/> when only the day is known (the note's creation date).</summary>
    public readonly record struct Stamp(long Ms, bool DayOnly);

    /// <param name="newestFirst">The note's revisions, newest first (<see cref="LegacyHistory.List"/>).</param>
    /// <param name="createdMs">When the note was created, for blocks the whole history already had.</param>
    public static IReadOnlyList<Entry> Compute(string current, IReadOnlyList<LegacyHistory.Revision> newestFirst, long? createdMs)
    {
        var index = new Index(newestFirst, createdMs);
        var result = new List<Entry>();
        foreach (var block in MarkdownEngine.SplitBlocks(current))
        {
            var text = current[block.Start..Math.Min(block.End, current.Length)];
            if (Key(text).Length == 0) continue;
            var stamp = index.Next(text);
            result.Add(new Entry(block.Start, block.End, stamp?.Ms, stamp?.DayOnly ?? false));
        }
        return result;
    }

    /// <summary>
    /// The times of a note's blocks asked in document order (the editor's blocks may join several, like a :::
    /// container: the newest of its parts). The n-th copy of a text needs n copies in a revision to have been there.
    /// </summary>
    public sealed class Index
    {
        private readonly List<LegacyHistory.Revision> _revisions;
        private readonly List<Dictionary<string, int>> _texts;
        private readonly long? _floor;
        private readonly Dictionary<string, int> _seen = new(StringComparer.Ordinal);

        private readonly bool _floorDayOnly;

        /// <param name="createdDayOnly">The creation is known only by its day (notas.json keeps dates).</param>
        public Index(IReadOnlyList<LegacyHistory.Revision> newestFirst, long? createdMs, bool createdDayOnly = false)
        {
            _revisions = newestFirst.Where(r => !r.Removed).ToList();
            _texts = _revisions.Select(r => Counts(r.Content)).ToList();
            _floor = newestFirst.Count < LegacyHistory.Limit ? createdMs : null;
            _floorDayOnly = createdDayOnly;
        }

        public Stamp? Next(string blockText)
        {
            long? newest = null;
            bool any = false, floorOnly = true;
            foreach (var part in MarkdownEngine.SplitBlocks(blockText))
            {
                var key = Key(blockText[part.Start..Math.Min(part.End, blockText.Length)]);
                if (key.Length == 0) continue;
                any = true;
                int copy = _seen[key] = _seen.GetValueOrDefault(key) + 1;
                long? time = _floor;
                bool fromFloor = true;
                for (int k = 0; k < _revisions.Count; k++)
                    if (_texts[k].GetValueOrDefault(key) < copy) { time = _revisions[k].Timestamp; fromFloor = false; break; }
                if (time is { } t && (newest is null || t > newest)) { newest = t; floorOnly = fromFloor; }
            }
            return any && newest is { } ms ? new Stamp(ms, floorOnly && _floorDayOnly) : null;
        }
    }

    /// <summary>"14:32" today, "ontem 14:32", "25/10 14:32" this year, "23/09/2025" before; a day alone without a time.</summary>
    public static string Label(DateTime at, DateTime now, bool dayOnly = false)
    {
        if (dayOnly)
            return at.Date == now.Date ? "hoje" : at.Date == now.Date.AddDays(-1) ? "ontem"
                : at.ToString(at.Year == now.Year ? "dd/MM" : "dd/MM/yyyy", CultureInfo.InvariantCulture);
        var time = at.ToString("HH:mm", CultureInfo.InvariantCulture);
        if (at.Date == now.Date) return time;
        if (at.Date == now.Date.AddDays(-1)) return "ontem " + time;
        if (at.Year == now.Year) return at.ToString("dd/MM", CultureInfo.InvariantCulture) + " " + time;
        return at.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
    }

    private static Dictionary<string, int> Counts(string text)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var block in MarkdownEngine.SplitBlocks(text))
        {
            var key = Key(text[block.Start..Math.Min(block.End, text.Length)]);
            if (key.Length > 0) counts[key] = counts.GetValueOrDefault(key) + 1;
        }
        return counts;
    }

    private static string Key(string block) => AiAuthorship.Strip(block).Trim();
}
