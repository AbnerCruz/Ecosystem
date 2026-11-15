namespace Urbe.Core;

/// <summary>
/// The undo history of a note written in Visual, across its blocks: each change is kept as the piece of text that
/// changed (never a copy of the note), typing is grouped by word as a text editor does, and undo/redo give the note's
/// text and where the caret goes. A change that does not come after the last state (the note shown again) starts over.
/// </summary>
public sealed class EditHistory
{
    /// <summary>One change: at <see cref="Start"/>, <see cref="Removed"/> became <see cref="Inserted"/>.</summary>
    public sealed record Change(int Start, string Removed, string Inserted, long At);

    private readonly List<Change> _undo = [];
    private readonly List<Change> _redo = [];
    private string _text = "";

    /// <summary>A pause longer than this ends a group of typing (ms).</summary>
    public const int GroupPauseMs = 1200;

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;
    public string Text => _text;

    /// <summary>A new note (or one shown again): nothing to undo.</summary>
    public void Reset(string text)
    {
        _text = text;
        _undo.Clear();
        _redo.Clear();
    }

    /// <summary>The note became <paramref name="next"/> at <paramref name="nowMs"/>; <paramref name="group"/> false keeps it a step of its own.</summary>
    public void Record(string next, long nowMs, bool group = true)
    {
        if (string.Equals(next, _text, StringComparison.Ordinal)) return;
        var change = Diff(_text, next, nowMs);
        _text = next;
        _redo.Clear();
        if (group && _undo.Count > 0 && Joins(_undo[^1], change))
        {
            var last = _undo[^1];
            _undo[^1] = change.Removed.Length == 0
                ? last with { Inserted = last.Inserted + change.Inserted, At = nowMs }
                // a letter deleted at the end of what was just typed
                : last with { Inserted = last.Inserted[..^change.Removed.Length], At = nowMs };
            if (_undo[^1].Removed.Length == 0 && _undo[^1].Inserted.Length == 0) _undo.RemoveAt(_undo.Count - 1);
            return;
        }
        _undo.Add(change);
    }

    /// <summary>Undoes the last step: the note's text and the caret (where the undone change was), or null.</summary>
    public (string Text, int Caret)? Undo()
    {
        if (_undo.Count == 0) return null;
        var c = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        _redo.Add(c);
        _text = _text[..c.Start] + c.Removed + _text[(c.Start + c.Inserted.Length)..];
        return (_text, c.Start + c.Removed.Length);
    }

    /// <summary>Does again the last step undone, or null.</summary>
    public (string Text, int Caret)? Redo()
    {
        if (_redo.Count == 0) return null;
        var c = _redo[^1];
        _redo.RemoveAt(_redo.Count - 1);
        _undo.Add(c);
        _text = _text[..c.Start] + c.Inserted + _text[(c.Start + c.Removed.Length)..];
        return (_text, c.Start + c.Inserted.Length);
    }

    private static Change Diff(string before, string after, long now)
    {
        int start = 0, end = before.Length, endAfter = after.Length;
        while (start < end && start < endAfter && before[start] == after[start]) start++;
        while (end > start && endAfter > start && before[end - 1] == after[endAfter - 1]) { end--; endAfter--; }
        return new Change(start, before[start..end], after[start..endAfter], now);
    }

    // typing goes on: the letter comes right after what the group typed, soon, and a space or a line ends a word;
    // a letter erased right at its end is part of it too
    private static bool Joins(Change last, Change next)
    {
        if (next.At - last.At > GroupPauseMs || last.Inserted.Length == 0) return false;
        var end = last.Start + last.Inserted.Length;
        if (next.Removed.Length == 0 && next.Inserted.Length == 1 && next.Start == end)
            return !char.IsWhiteSpace(last.Inserted[^1]) || char.IsWhiteSpace(next.Inserted[0]) || string.IsNullOrWhiteSpace(last.Inserted);
        return next.Inserted.Length == 0 && next.Removed.Length == 1 && next.Start + 1 == end && last.Inserted.Length > 1 &&
            string.Equals(last.Inserted[^1].ToString(), next.Removed, StringComparison.Ordinal);
    }
}
