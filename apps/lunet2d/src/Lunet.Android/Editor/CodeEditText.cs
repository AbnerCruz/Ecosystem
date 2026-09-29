using Android.Content;
using Android.Graphics;
using Android.OS;
using Android.Text;
using Android.Text.Style;
using Android.Views;
using Android.Widget;
using Lunet.Editor;
using AndroidColor = Android.Graphics.Color;

namespace Lunet.Android.Editor;

/// <summary>
/// Editor de código: EditText com realce de sintaxe, números de linha, desfazer/refazer com junção de digitação,
/// indentação automática e evento de "pausa na digitação" para análise.
/// </summary>
internal sealed class CodeEditText : EditText
{
    private static readonly Dictionary<TokenKind, AndroidColor> Palette = new()
    {
        [TokenKind.Keyword] = AndroidColor.Rgb(0x56, 0x9C, 0xD6),
        [TokenKind.ControlKeyword] = AndroidColor.Rgb(0xC5, 0x86, 0xC0),
        [TokenKind.Type] = AndroidColor.Rgb(0x4E, 0xC9, 0xB0),
        [TokenKind.String] = AndroidColor.Rgb(0xCE, 0x91, 0x78),
        [TokenKind.Number] = AndroidColor.Rgb(0xB5, 0xCE, 0xA8),
        [TokenKind.Comment] = AndroidColor.Rgb(0x6A, 0x99, 0x55),
        [TokenKind.Preprocessor] = AndroidColor.Rgb(0x9B, 0x9B, 0x9B),
        [TokenKind.Method] = AndroidColor.Rgb(0xDC, 0xDC, 0xAA),
    };

    private const int SettleDelayMilliseconds = 250;

    private readonly UndoHistory _history = new();
    private readonly List<ForegroundColorSpan> _spans = new();
    private readonly Handler _handler = new(Looper.MainLooper!);
    private readonly Paint _gutterPaint = new(PaintFlags.AntiAlias);
    private readonly Paint _gutterBackground = new() { Color = AndroidColor.Rgb(18, 20, 24) };
    private readonly Action _settle;
    private readonly float _density;
    private string _removed = "";
    private int _caretBefore;
    private bool _applying;
    private bool _typedSingle;
    private int _version;
    private int _gutterDigits = 2;

    public CodeEditText(Context context) : base(context)
    {
        _density = context.Resources!.DisplayMetrics!.Density;
        TextSize = 14;
        Gravity = GravityFlags.Top | GravityFlags.Left;
        SetTypeface(Typeface.Monospace, TypefaceStyle.Normal);
        SetHorizontallyScrolling(true);
        InputType = InputTypes.ClassText | InputTypes.TextFlagMultiLine | InputTypes.TextFlagNoSuggestions | InputTypes.TextVariationVisiblePassword;
        SetBackgroundColor(AndroidColor.Rgb(24, 26, 31));
        SetTextColor(AndroidColor.Rgb(230, 230, 230));
        _gutterPaint.Color = AndroidColor.Rgb(120, 126, 138);
        _gutterPaint.TextAlign = global::Android.Graphics.Paint.Align.Right;
        _gutterPaint.SetTypeface(Typeface.Monospace);
        _settle = OnSettled;
        UpdateGutter();
        AddTextChangedListener(new Watcher(this));
    }

    /// <summary>Disparado 250 ms depois da última mudança: (texto, versão, posição do cursor).</summary>
    public event Action<string, int, int>? Settled;

    public int Version => _version;
    public bool CanUndo => _history.CanUndo;
    public bool CanRedo => _history.CanRedo;

    /// <summary>Carrega um arquivo, zerando o histórico de desfazer.</summary>
    public void LoadText(string text)
    {
        _applying = true;
        SetText(text, BufferType.Editable);
        _applying = false;
        _history.Clear();
        _version++;
        UpdateGutter();
        _handler.RemoveCallbacks(_settle);
        _handler.Post(_settle);
    }

    public void Undo() => Apply(_history.Undo());

    public void Redo() => Apply(_history.Redo());

    /// <summary>Substitui um trecho como uma edição normal (entra no histórico).</summary>
    public void Replace(int start, int length, string text)
    {
        var editable = EditableText;
        if (editable is null || start < 0 || start + length > editable.Length()) return;
        editable.Replace(start, start + length, text);
    }

    private void Apply(TextEdit? edit)
    {
        if (edit is not { } e || EditableText is not { } editable) return;
        _applying = true;
        editable.Replace(e.Start, e.Start + e.DeleteLength, e.Insert);
        _applying = false;
        SetSelection(System.Math.Clamp(e.CaretAfter, 0, editable.Length()));
        AfterProgrammaticChange();
    }

    private void AfterProgrammaticChange()
    {
        _version++;
        UpdateGutter();
        _handler.RemoveCallbacks(_settle);
        _handler.PostDelayed(_settle, SettleDelayMilliseconds);
    }

    // ---------- Observação de mudanças ----------

    private sealed class Watcher(CodeEditText owner) : Java.Lang.Object, ITextWatcher
    {
        public void BeforeTextChanged(Java.Lang.ICharSequence? s, int start, int count, int after) => owner.BeforeChange(s, start, count);
        public void OnTextChanged(Java.Lang.ICharSequence? s, int start, int before, int count) => owner.OnChange(s, start, count);
        public void AfterTextChanged(IEditable? s) => owner.AfterChange(s);
    }

    private void BeforeChange(Java.Lang.ICharSequence? s, int start, int count)
    {
        if (_applying || s is null) return;
        _removed = count == 0 ? "" : s.SubSequenceFormatted(start, start + count)!.ToString();
        _caretBefore = SelectionStart;
    }

    private void OnChange(Java.Lang.ICharSequence? s, int start, int count)
    {
        if (_applying || s is null) return;
        var inserted = count == 0 ? "" : s.SubSequenceFormatted(start, start + count)!.ToString();
        _typedSingle = count == 1;
        _history.Record(start, _removed, inserted, System.Math.Max(0, _caretBefore), Java.Lang.JavaSystem.CurrentTimeMillis());
        _version++;
    }

    private void AfterChange(IEditable? s)
    {
        if (_applying || s is null) return;
        UpdateGutter();
        TryAutoIndent(s);
        _handler.RemoveCallbacks(_settle);
        _handler.PostDelayed(_settle, SettleDelayMilliseconds);
    }

    private void TryAutoIndent(IEditable s)
    {
        var caret = SelectionStart;
        if (!_typedSingle || caret < 1 || caret > s.Length()) return;
        _typedSingle = false;
        var typed = s.CharAt(caret - 1);
        if (typed != '\n' && typed != '}') return;
        // Só reage a digitação de um caractere (uma inserção de 1 char pelo teclado), não a colar/desfazer.
        var text = s.ToString();
        var without = text.Remove(caret - 1, 1);
        TextEdit? edit = typed == '\n'
            ? IndentationService.NewLine(without, caret - 1)
            : IndentationService.CloseBrace(without, caret - 1);
        if (edit is not { } e) return;
        // Troca o caractere digitado pelo resultado da regra.
        var replaceStart = typed == '}' ? e.Start : caret - 1;
        var removedByRule = typed == '}' ? without.Substring(e.Start, e.DeleteLength) : _removed;
        _applying = true;
        s.Replace(replaceStart, caret, e.Insert);
        _applying = false;
        // O passo de desfazer passa a ser "digitou e a regra reescreveu", num só.
        _history.ReplaceLast(replaceStart, removedByRule, e.Insert);
        SetSelection(System.Math.Clamp(e.CaretAfter, 0, s.Length()));
    }

    private void OnSettled()
    {
        var text = Text ?? "";
        var version = _version;
        Settled?.Invoke(text, version, SelectionStart);
        System.Threading.Tasks.Task.Run(() => SyntaxHighlighter.Classify(text)).ContinueWith(task =>
        {
            if (task.IsFaulted) return;
            var spans = task.Result;
            _handler.Post(() =>
            {
                if (version != _version) return;
                ApplyHighlight(spans);
            });
        });
    }

    private void ApplyHighlight(IReadOnlyList<TokenSpan> spans)
    {
        var editable = EditableText;
        if (editable is null) return;
        foreach (var old in _spans) editable.RemoveSpan(old);
        _spans.Clear();
        var length = editable.Length();
        foreach (var span in spans)
        {
            if (span.Start < 0 || span.Start + span.Length > length || !Palette.TryGetValue(span.Kind, out var color)) continue;
            var colored = new ForegroundColorSpan(color);
            editable.SetSpan(colored, span.Start, span.Start + span.Length, SpanTypes.ExclusiveExclusive);
            _spans.Add(colored);
        }
    }

    // ---------- Line numbers ----------

    private void UpdateGutter()
    {
        var lines = Layout?.LineCount ?? 1;
        var digits = System.Math.Max(2, lines.ToString().Length);
        var textSizePx = TextSize;
        _gutterPaint.TextSize = textSizePx * 0.85f;
        if (digits == _gutterDigits && PaddingLeft > 0) return;
        _gutterDigits = digits;
        var width = (int)(digits * _gutterPaint.MeasureText("0") + 12 * _density);
        SetPadding(width + (int)(6 * _density), PaddingTop, PaddingRight, PaddingBottom);
    }

    protected override void OnDraw(Canvas canvas)
    {
        base.OnDraw(canvas);
        var layout = Layout;
        if (layout is null) return;
        var width = PaddingLeft - (int)(6 * _density);
        canvas.DrawRect(ScrollX, ScrollY, ScrollX + width, ScrollY + Height, _gutterBackground);
        var first = layout.GetLineForVertical(ScrollY);
        var last = layout.GetLineForVertical(ScrollY + Height);
        var top = ExtendedPaddingTop;
        for (var line = first; line <= last; line++)
            canvas.DrawText((line + 1).ToString(), ScrollX + width - 6 * _density, top + layout.GetLineBaseline(line), _gutterPaint);
    }
}
