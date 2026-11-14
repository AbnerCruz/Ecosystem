namespace Lunet.Studio;

/// <summary>
/// Bounded undo/redo (LUNET-486). Pixel edits store only the touched frame as raw ARGB (4 bytes/pixel);
/// structural edits (frames, FPS) store the whole document. One checkpoint per gesture, not per pixel.
/// </summary>
public sealed class SpriteHistory
{
    public const long MaxBytes = 32L * 1024 * 1024;

    private sealed record Entry(int Frame, uint[]? Pixels, List<uint[]>? Frames, int Fps, int Width, int Height)
    {
        public long Bytes => (Pixels?.LongLength ?? 0) * 4 + (Frames?.Sum(f => f.LongLength) ?? 0) * 4 + 64;
    }

    private readonly List<Entry> _undo = [];
    private readonly List<Entry> _redo = [];

    public int UndoCount => _undo.Count;
    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;
    public long Bytes => _undo.Sum(e => e.Bytes) + _redo.Sum(e => e.Bytes);

    /// <summary>Snapshot of the whole document, for frame add/remove/reorder, FPS or resize.</summary>
    public void Checkpoint(SpriteDocument document) { Push(Full(document)); _redo.Clear(); }

    /// <summary>Snapshot of one frame, for drawing tools.</summary>
    public void CheckpointFrame(SpriteDocument document, int frame)
    {
        Push(new Entry(frame, (uint[])document.Frames[frame].Clone(), null, document.Fps, document.Width, document.Height));
        _redo.Clear();
    }

    public SpriteDocument Undo(SpriteDocument current) => Move(current, _undo, _redo);
    public SpriteDocument Redo(SpriteDocument current) => Move(current, _redo, _undo);

    /// <summary>Restores the last checkpoint without creating a redo step (a cancelled gesture).</summary>
    public SpriteDocument Rollback(SpriteDocument current)
    {
        if (_undo.Count == 0) return current;
        var entry = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        Apply(current, entry);
        return current;
    }

    private static Entry Full(SpriteDocument d) =>
        new(-1, null, d.Frames.Select(f => (uint[])f.Clone()).ToList(), d.Fps, d.Width, d.Height);

    private void Push(Entry entry)
    {
        _undo.Add(entry);
        long total = Bytes;
        while (_undo.Count > 1 && total > MaxBytes) { total -= _undo[0].Bytes; _undo.RemoveAt(0); }
    }

    private static SpriteDocument Move(SpriteDocument current, List<Entry> from, List<Entry> to)
    {
        if (from.Count == 0) return current;
        var entry = from[^1];
        from.RemoveAt(from.Count - 1);
        to.Add(entry.Frame >= 0 && entry.Frame < current.Frames.Count && entry.Width == current.Width && entry.Height == current.Height
            ? new Entry(entry.Frame, (uint[])current.Frames[entry.Frame].Clone(), null, current.Fps, current.Width, current.Height)
            : Full(current));
        Apply(current, entry);
        return current;
    }

    private static void Apply(SpriteDocument document, Entry entry)
    {
        document.Width = entry.Width;
        document.Height = entry.Height;
        document.Fps = entry.Fps;
        if (entry.Frames is not null) document.Frames = entry.Frames.Select(f => (uint[])f.Clone()).ToList();
        else if (entry.Pixels is not null && entry.Frame < document.Frames.Count)
            document.Frames[entry.Frame] = (uint[])entry.Pixels.Clone();
    }
}
