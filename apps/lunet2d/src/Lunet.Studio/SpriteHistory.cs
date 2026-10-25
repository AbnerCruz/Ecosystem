namespace Lunet.Studio;

/// <summary>Bounded undo/redo history. One checkpoint per gesture, not per pixel.</summary>
public sealed class SpriteHistory
{
    private readonly List<string> _undo = [];
    private readonly List<string> _redo = [];
    public void Checkpoint(SpriteDocument document)
    {
        _undo.Add(document.ToJson()); _redo.Clear();
        // Maximum 16 MB of history (plus the current document).
        while (_undo.Count > 1 && _undo.Sum(s => (long)s.Length * 2) > 16_000_000) _undo.RemoveAt(0);
    }
    public SpriteDocument Undo(SpriteDocument current) => Move(current, _undo, _redo);
    public SpriteDocument Redo(SpriteDocument current) => Move(current, _redo, _undo);
    private static SpriteDocument Move(SpriteDocument current, List<string> from, List<string> to)
    {
        if (from.Count == 0) return current;
        var next = SpriteDocument.Parse(from[^1]); from.RemoveAt(from.Count - 1); to.Add(current.ToJson()); return next;
    }
}
