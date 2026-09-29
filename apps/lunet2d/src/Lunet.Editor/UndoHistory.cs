namespace Lunet.Editor;

/// <summary>Histórico de desfazer/refazer com junção de digitação contínua.</summary>
public sealed class UndoHistory
{
    private sealed record Entry(int Start, string Removed, string Inserted, int CaretBefore, long Time);

    private readonly List<Entry> _undo = [];
    private readonly List<Entry> _redo = [];
    private readonly int _capacity;

    public UndoHistory(int capacity = 500) => _capacity = capacity;

    /// <summary>Intervalo (ms) dentro do qual digitação contígua vira um único passo de desfazer.</summary>
    public long CoalesceMilliseconds { get; set; } = 1000;

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    /// <summary>Registra uma mudança já aplicada ao texto: <paramref name="removed"/> foi trocado por <paramref name="inserted"/> em <paramref name="start"/>.</summary>
    public void Record(int start, string removed, string inserted, int caretBefore, long timeMs)
    {
        if (removed.Length == 0 && inserted.Length == 0) return;
        _redo.Clear();
        if (_undo.Count > 0)
        {
            var last = _undo[^1];
            var typing = removed.Length == 0 && last.Removed.Length == 0 && inserted.Length == 1 && inserted[0] != '\n'
                && start == last.Start + last.Inserted.Length && timeMs - last.Time <= CoalesceMilliseconds && !last.Inserted.Contains('\n');
            var backspace = inserted.Length == 0 && last.Inserted.Length == 0 && removed.Length == 1
                && start + 1 == last.Start && timeMs - last.Time <= CoalesceMilliseconds;
            if (typing)
            {
                _undo[^1] = last with { Inserted = last.Inserted + inserted, Time = timeMs };
                return;
            }
            if (backspace)
            {
                _undo[^1] = last with { Start = start, Removed = removed + last.Removed, Time = timeMs };
                return;
            }
        }
        _undo.Add(new Entry(start, removed, inserted, caretBefore, timeMs));
        if (_undo.Count > _capacity) _undo.RemoveAt(0);
    }

    /// <summary>Edição que desfaz o último passo, ou nulo.</summary>
    public TextEdit? Undo()
    {
        if (_undo.Count == 0) return null;
        var entry = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        _redo.Add(entry);
        return new TextEdit(entry.Start, entry.Inserted.Length, entry.Removed, entry.CaretBefore);
    }

    public TextEdit? Redo()
    {
        if (_redo.Count == 0) return null;
        var entry = _redo[^1];
        _redo.RemoveAt(_redo.Count - 1);
        _undo.Add(entry);
        return new TextEdit(entry.Start, entry.Removed.Length, entry.Inserted, entry.Start + entry.Inserted.Length);
    }

    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
    }
}
