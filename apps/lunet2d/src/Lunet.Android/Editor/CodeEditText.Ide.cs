using Android.Graphics;
using Android.Text;
using Android.Text.Style;
using Android.Views;
using Lunet.Editor;
using AndroidColor = Android.Graphics.Color;
using Selection = Lunet.Editor.Selection;
using AndroidPath = Android.Graphics.Path;

namespace Lunet.Android.Editor;

/// <summary>Recursos de IDE do editor: atalhos, multi-cursor, dobrar código, edições por linha e rolagem para o minimapa.</summary>
internal sealed partial class CodeEditText
{
    private readonly List<Selection> _extras = [];
    private readonly AndroidPath _selectionPath = new();
    private readonly Paint _extraCaret = new() { Color = AndroidColor.Rgb(255, 200, 80), StrokeWidth = 3f };
    private readonly Paint _extraSelection = new() { Color = AndroidColor.Argb(90, 90, 160, 255) };
    private readonly Paint _placeholderPaint = new(PaintFlags.AntiAlias) { Color = AndroidColor.Rgb(150, 155, 165) };
    private readonly Paint _placeholderBackground = new(PaintFlags.AntiAlias) { Color = AndroidColor.Argb(70, 120, 130, 150) };
    private readonly List<(AbsoluteSizeSpan Size, ForegroundColorSpan Color, string Placeholder)> _folded = [];
    private bool _inTextChange;
    private bool _settingSelection;
    private int _selectionBeforeStart;
    private int _selectionBeforeEnd;
    private int _lastStart;
    private int _lastInsertedLength;
    private float _touchDownX, _touchDownY;

    private float FoldMarkerWidth => _lineNumbers ? 12 * _density : 0;

    /// <summary>Regiões que podem ser dobradas, atualizadas a cada análise.</summary>
    public IReadOnlyList<FoldRegion> FoldRegions { get; set; } = [];

    public event Action<EditorCommand>? CommandRequested;
    public event Action? ScrollPositionChanged;

    public int ExtraCursorCount => _extras.Count;

    public void ApplySettings(int fontSize, bool showLineNumbers)
    {
        _lineNumbers = showLineNumbers;
        TextSize = fontSize;
        UpdateGutter();
        Invalidate();
    }

    // ---------- Atalhos de teclado físico ----------

    public override bool OnKeyDown(Keycode keyCode, KeyEvent? e)
    {
        if (keyCode == Keycode.Escape && _extras.Count > 0)
        {
            ClearExtraCursors();
            return true;
        }
        if (e is not null && (e.IsCtrlPressed || e.IsAltPressed || keyCode is >= Keycode.F1 and <= Keycode.F12 || keyCode == Keycode.Enter && e.IsCtrlPressed))
        {
            var command = Shortcuts.Resolve(KeyName(keyCode), e.IsCtrlPressed, e.IsShiftPressed, e.IsAltPressed);
            if (command != EditorCommand.None)
            {
                CommandRequested?.Invoke(command);
                return true;
            }
        }
        return base.OnKeyDown(keyCode, e);
    }

    private static string KeyName(Keycode key) => key switch
    {
        Keycode.DpadUp => "Up",
        Keycode.DpadDown => "Down",
        Keycode.DpadLeft => "Left",
        Keycode.DpadRight => "Right",
        _ => key.ToString(),
    };

    // ---------- Edições ----------

    /// <summary>Aplica uma edição (como as de <see cref="LineOperations"/>) entrando no histórico de desfazer.</summary>
    public void ApplyEdit(EditResult edit)
    {
        var editable = EditableText;
        if (editable is null) return;
        editable.Replace(edit.Start, edit.Start + edit.DeleteLength, edit.Insert);
        SetSelectionSafe(edit.SelectionStart, edit.SelectionEnd);
    }

    /// <summary>Troca o texto inteiro alterando só o trecho que difere (mantém o cursor e cria um único passo de desfazer).</summary>
    public void ReplaceWholeText(string newText, int? caret = null)
    {
        var editable = EditableText;
        if (editable is null) return;
        var old = editable.ToString();
        if (old == newText) return;
        var (prefix, removed, inserted) = Diff(old, newText);
        editable.Replace(prefix, prefix + removed.Length, inserted);
        SetSelectionSafe(caret ?? Math.Min(SelectionStart, newText.Length), caret ?? Math.Min(SelectionStart, newText.Length));
    }

    private void SetSelectionSafe(int start, int end)
    {
        var length = EditableText?.Length() ?? 0;
        _settingSelection = true;
        SetSelection(Math.Clamp(start, 0, length), Math.Clamp(end, 0, length));
        _settingSelection = false;
    }

    private static (int Prefix, string Removed, string Inserted) Diff(string before, string after)
    {
        var prefix = 0;
        var max = Math.Min(before.Length, after.Length);
        while (prefix < max && before[prefix] == after[prefix]) prefix++;
        var suffix = 0;
        while (suffix < max - prefix && before[before.Length - 1 - suffix] == after[after.Length - 1 - suffix]) suffix++;
        return (prefix, before.Substring(prefix, before.Length - prefix - suffix), after.Substring(prefix, after.Length - prefix - suffix));
    }

    // ---------- Multi-cursor ----------

    public List<Selection> AllSelections()
    {
        var all = new List<Selection>(_extras) { new(Math.Min(SelectionStart, SelectionEnd), Math.Max(SelectionStart, SelectionEnd)) };
        return MultiCursor.Normalize(all);
    }

    public void SelectNextOccurrence() => ApplySelections(MultiCursor.SelectNextOccurrence(Text ?? "", AllSelections()));

    public void SelectAllOccurrences() => ApplySelections(MultiCursor.SelectAllOccurrences(Text ?? "", AllSelections()));

    public void AddCursor(bool above) => ApplySelections(MultiCursor.AddCursorVertically(Text ?? "", AllSelections(), above));

    private void ApplySelections(List<Selection> selections)
    {
        if (selections.Count == 0) return;
        var previous = AllSelections();
        // O mais novo (que não existia antes) vira o principal, para o cursor de texto acompanhar a busca.
        var primary = selections.LastOrDefault(s => !previous.Contains(s));
        if (primary == default && !selections.Contains(default)) primary = selections[^1];
        _extras.Clear();
        _extras.AddRange(selections.Where(s => s != primary));
        SetSelectionSafe(primary.Start, primary.End);
        BringPointIntoView(primary.End);
        Invalidate();
    }

    public void ClearExtraCursors()
    {
        if (_extras.Count == 0) return;
        _extras.Clear();
        Invalidate();
    }

    protected override void OnSelectionChanged(int selStart, int selEnd)
    {
        base.OnSelectionChanged(selStart, selEnd);
        if (_extras.Count > 0 && !_inTextChange && !_settingSelection && !_applying) ClearExtraCursors();
    }

    private void ReplicateToExtras(IEditable s)
    {
        try
        {
            var after = s.ToString();
            if (_lastStart < 0 || _lastStart + _lastInsertedLength > after.Length) { _extras.Clear(); return; }
            var before = after.Remove(_lastStart, _lastInsertedLength).Insert(_lastStart, _removed);
            var result = MultiCursor.Replicate(before, after, new Selection(_selectionBeforeStart, _selectionBeforeEnd), _extras);
            if (result is null) { _extras.Clear(); Invalidate(); return; }

            var (prefix, removed, inserted) = Diff(after, result.Text);
            _applying = true;
            s.Replace(prefix, prefix + removed.Length, inserted);
            _applying = false;

            // Um único passo de desfazer para a digitação em todos os cursores.
            var (hp, hRemoved, hInserted) = Diff(before, result.Text);
            _history.ReplaceLast(hp, hRemoved, hInserted);

            var primary = result.Selections[result.PrimaryIndex];
            _extras.Clear();
            for (var i = 0; i < result.Selections.Count; i++)
                if (i != result.PrimaryIndex) _extras.Add(result.Selections[i]);
            SetSelectionSafe(primary.Start, primary.End);
            Invalidate();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            _applying = false;
            _extras.Clear();
        }
    }

    private void DrawExtraSelections(Canvas canvas, Layout layout)
    {
        if (_extras.Count == 0) return;
        var length = EditableText?.Length() ?? 0;
        foreach (var extra in _extras)
        {
            if (extra.End > length) continue;
            if (extra.IsEmpty)
            {
                var line = layout.GetLineForOffset(extra.Start);
                var x = CompoundPaddingLeft + layout.GetPrimaryHorizontal(extra.Start);
                canvas.DrawLine(x, ExtendedPaddingTop + layout.GetLineTop(line), x, ExtendedPaddingTop + layout.GetLineBottom(line), _extraCaret);
            }
            else
            {
                _selectionPath.Reset();
                layout.GetSelectionPath(extra.Start, extra.End, _selectionPath);
                canvas.Save();
                canvas.Translate(CompoundPaddingLeft, ExtendedPaddingTop);
                canvas.DrawPath(_selectionPath, _extraSelection);
                canvas.Restore();
            }
        }
    }

    // ---------- Dobrar código ----------

    /// <summary>Dobra ou desdobra a região que começa na linha (1 = primeira). Devolve falso se não há região ali.</summary>
    public bool ToggleFoldAtLine(int line)
    {
        var region = FoldRegions.FirstOrDefault(r => r.StartLine == line);
        if (region is null) return false;
        if (FoldIndexAt(line - 1) is { } existing)
        {
            Unfold(existing);
            return true;
        }
        var editable = EditableText;
        var layout = Layout;
        if (editable is null || layout is null || region.EndLine > layout.LineCount) return false;
        var hiddenStart = layout.GetLineEnd(line - 1) - 1; // a quebra de linha do cabeçalho
        var hiddenEnd = layout.GetLineEnd(region.EndLine - 1);
        if (hiddenStart < 0 || hiddenEnd <= hiddenStart || hiddenEnd > editable.Length()) return false;
        var size = new AbsoluteSizeSpan(1, false);
        var color = new ForegroundColorSpan(AndroidColor.Transparent);
        editable.SetSpan(size, hiddenStart, hiddenEnd, SpanTypes.ExclusiveExclusive);
        editable.SetSpan(color, hiddenStart, hiddenEnd, SpanTypes.ExclusiveExclusive);
        _folded.Add((size, color, region.Placeholder));
        // Se o cursor ficou dentro do trecho escondido, leva-o para o fim do cabeçalho.
        if (SelectionStart > hiddenStart && SelectionStart < hiddenEnd) SetSelectionSafe(hiddenStart, hiddenStart);
        RequestLayout();
        Invalidate();
        return true;
    }

    /// <summary>Dobra ou desdobra a região mais interna que contém o cursor.</summary>
    public bool ToggleFoldAtCaret()
    {
        var layout = Layout;
        if (layout is null) return false;
        var line = layout.GetLineForOffset(Math.Min(SelectionStart, EditableText?.Length() ?? 0)) + 1;
        var region = FoldRegions.Where(r => r.StartLine <= line && r.EndLine >= line).OrderByDescending(r => r.StartLine).FirstOrDefault();
        return region is not null && ToggleFoldAtLine(region.StartLine);
    }

    public void UnfoldAll()
    {
        if (_folded.Count == 0) return;
        foreach (var index in Enumerable.Range(0, _folded.Count).Reverse().ToList()) Unfold(index);
    }

    private void Unfold(int index)
    {
        var editable = EditableText;
        var (size, color, _) = _folded[index];
        editable?.RemoveSpan(size);
        editable?.RemoveSpan(color);
        _folded.RemoveAt(index);
        RequestLayout();
        Invalidate();
    }

    private int? FoldIndexAt(int headerLine)
    {
        var editable = EditableText;
        var layout = Layout;
        if (editable is null || layout is null) return null;
        for (var i = 0; i < _folded.Count; i++)
        {
            var start = editable.GetSpanStart(_folded[i].Size);
            if (start >= 0 && layout.GetLineForOffset(start) == headerLine) return i;
        }
        return null;
    }

    private bool IsLineHidden(int line)
    {
        if (_folded.Count == 0) return false;
        var editable = EditableText;
        var layout = Layout;
        if (editable is null || layout is null) return false;
        foreach (var (size, _, _) in _folded)
        {
            var start = editable.GetSpanStart(size);
            var end = editable.GetSpanEnd(size);
            if (start < 0) continue;
            if (line > layout.GetLineForOffset(start) && line <= layout.GetLineForOffset(Math.Max(start, end - 1))) return true;
        }
        return false;
    }

    private void DrawFoldMarker(Canvas canvas, Layout layout, int line, float x, int top)
    {
        if (FoldRegions.Count == 0) return;
        var region = FoldRegions.FirstOrDefault(r => r.StartLine == line + 1);
        if (region is null) return;
        var folded = FoldIndexAt(line) is not null;
        var baseline = top + layout.GetLineBaseline(line);
        canvas.DrawText(folded ? "▸" : "▾", x, baseline, _gutterPaint);
    }

    private void DrawFoldPlaceholders(Canvas canvas, Layout layout)
    {
        if (_folded.Count == 0) return;
        var editable = EditableText;
        if (editable is null) return;
        _placeholderPaint.TextSize = TextSize * 0.85f;
        foreach (var (size, _, placeholder) in _folded)
        {
            var start = editable.GetSpanStart(size);
            if (start < 0) continue;
            var line = layout.GetLineForOffset(start);
            var x = CompoundPaddingLeft + layout.GetLineRight(line) + 6 * _density;
            var baseline = ExtendedPaddingTop + layout.GetLineBaseline(line);
            var width = _placeholderPaint.MeasureText(placeholder);
            canvas.DrawRoundRect(x - 4 * _density, baseline - TextSize, x + width + 4 * _density, baseline + 4 * _density, 4 * _density, 4 * _density, _placeholderBackground);
            canvas.DrawText(placeholder, x, baseline, _placeholderPaint);
        }
    }

    public override bool OnTouchEvent(MotionEvent? e)
    {
        if (e is null) return base.OnTouchEvent(e);
        var gutterWidth = _lineNumbers ? PaddingLeft - 6 * _density : 0;
        if (e.Action == MotionEventActions.Down)
        {
            _touchDownX = e.GetX();
            _touchDownY = e.GetY();
        }
        else if (e.Action == MotionEventActions.Up && FoldRegions.Count > 0 && Layout is { } layout)
        {
            var slop = 12 * _density;
            var moved = Math.Abs(e.GetX() - _touchDownX) > slop || Math.Abs(e.GetY() - _touchDownY) > slop;
            var contentY = e.GetY() + ScrollY - ExtendedPaddingTop;
            if (!moved && _touchDownX <= gutterWidth && _touchDownX > gutterWidth - FoldMarkerWidth - 10 * _density)
            {
                var line = layout.GetLineForVertical((int)contentY);
                if (ToggleFoldAtLine(line + 1)) return true;
            }
            else if (!moved && _folded.Count > 0)
            {
                // Toque no marcador "{ … }" ao lado do cabeçalho desdobra.
                var contentX = e.GetX() + ScrollX;
                var line = layout.GetLineForVertical((int)contentY);
                if (FoldIndexAt(line) is { } index && contentX > CompoundPaddingLeft + layout.GetLineRight(line))
                {
                    Unfold(index);
                    return true;
                }
            }
        }
        return base.OnTouchEvent(e);
    }

    // ---------- Rolagem (minimapa) ----------

    protected override void OnScrollChanged(int l, int t, int oldl, int oldt)
    {
        base.OnScrollChanged(l, t, oldl, oldt);
        ScrollPositionChanged?.Invoke();
    }

    public int FirstVisibleLine => Layout?.GetLineForVertical(ScrollY) ?? 0;

    public int VisibleLineCount
    {
        get
        {
            var layout = Layout;
            return layout is null ? 1 : Math.Max(1, layout.GetLineForVertical(ScrollY + Height) - layout.GetLineForVertical(ScrollY));
        }
    }

    public int TotalLines => Layout?.LineCount ?? 1;

    public void ScrollToLine(int line)
    {
        var layout = Layout;
        if (layout is null) return;
        line = Math.Clamp(line, 0, layout.LineCount - 1);
        var maxScroll = Math.Max(0, layout.Height + ExtendedPaddingTop + ExtendedPaddingBottom - Height);
        ScrollTo(ScrollX, Math.Clamp(layout.GetLineTop(line), 0, maxScroll));
    }
}
