using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Rendering;
using Lunet.Compiler;
using Lunet.Client.Shell;
using Lunet.Client.Ui;

namespace Lunet.Client.Views;

/// <summary>
/// The text a document editor shows, whatever control draws it: the code editor (AvaloniaEdit, LUNET-537 D1) or the
/// notes field. The Lunet keyboard edits through this, so it never depends on one control.
/// </summary>
public interface ITextSurface
{
    Control View { get; }
    /// <summary>The element whose system keyboard (IME) is switched off while the Lunet keyboard is on.</summary>
    InputElement InputTarget { get; }
    /// <summary>The whole text. Setting it loads a document (it is not an undoable edit).</summary>
    string Text { get; set; }
    int SelectionStart { get; }
    int SelectionEnd { get; }
    int Caret { get; set; }
    bool IsFocused { get; }
    void Select(int start, int end);
    /// <summary>Replaces [start, end) by <paramref name="insert"/> as one undo step and puts the caret at <paramref name="caret"/>.</summary>
    void Replace(int start, int end, string insert, int caret);
    void Undo();
    void Redo();
    bool Focus();
    /// <summary>Raised when the person starts using the text (focus or a tap on it).</summary>
    event Action? Engaged;
}

/// <summary>
/// The code editor: AvaloniaEdit with line numbers, C# highlighting, the caret line and selection in the Lunet
/// tokens, horizontal scrolling (code does not wrap) and virtualized lines.
/// </summary>
public sealed class CodeSurface : ITextSurface
{
    private readonly TextEditor _editor;

    /// <summary>Applies the person's editor settings (Ajustes): font size and line numbers.</summary>
    public void Apply(Lunet.Core.EditorSettings settings)
    {
        _editor.FontSize = Math.Clamp(settings.FontSize, Lunet.Core.EditorSettings.MinFontSize, Lunet.Core.EditorSettings.MaxFontSize);
        _editor.ShowLineNumbers = settings.ShowLineNumbers;
    }

    public CodeSurface()
    {
        _editor = new TextEditor
        {
            ShowLineNumbers = true,
            WordWrap = false,
            FontFamily = LunetTokens.MonoFont,
            FontSize = LunetTokens.Type.Code,
            Foreground = LunetTokens.Brush(LunetTokens.Color.Code),
            Background = LunetTokens.Brush(LunetTokens.Color.Background),
            LineNumbersForeground = LunetTokens.Brush(LunetTokens.Color.TextTertiary),
            SyntaxHighlighting = Highlighting(),
            Padding = new Thickness(LunetTokens.Space.Xs, LunetTokens.Space.Xs, LunetTokens.Space.M, LunetTokens.Space.Xl),
        };
        _editor.Options.HighlightCurrentLine = true;
        _editor.Options.ConvertTabsToSpaces = true;
        _editor.Options.IndentationSize = 4;
        _editor.Options.EnableHyperlinks = false;
        _editor.Options.EnableEmailHyperlinks = false;
        _editor.TextArea.TextView.CurrentLineBackground = LunetTokens.Brush(LunetTokens.Ink.I2);
        _editor.TextArea.TextView.CurrentLineBorder = new Pen(LunetTokens.Brush("#00000000"));
        _editor.TextArea.SelectionBrush = LunetTokens.Brush(LunetTokens.Color.Selection);
        _editor.TextArea.SelectionCornerRadius = 3;
        _editor.TextArea.Caret.CaretBrush = LunetTokens.Brush(LunetTokens.Color.Primary);
        AutomationProperties.SetName(_editor.TextArea, "Editor de código");
        _editor.TextArea.TextView.BackgroundRenderers.Add(_diagnostics);
        _editor.Document.TextChanged += (_, _) => TextChanged?.Invoke();
        _editor.TextArea.GotFocus += (_, _) => Engaged?.Invoke();
        _editor.TextArea.AddHandler(InputElement.PointerReleasedEvent, (_, _) => Engaged?.Invoke(),
            Avalonia.Interactivity.RoutingStrategies.Tunnel | Avalonia.Interactivity.RoutingStrategies.Bubble, handledEventsToo: true);
    }

    private readonly DiagnosticsRenderer _diagnostics = new();

    /// <summary>The underlying editor (tests and the head's hardware shortcuts).</summary>
    public TextEditor Editor => _editor;

    /// <summary>Raised after every change of the text (typing, keyboard edits, loading).</summary>
    public event Action? TextChanged;

    /// <summary>The diagnostics drawn under the code now (LUNET-536 M1).</summary>
    public IReadOnlyList<LunetDiagnostic> Diagnostics => _diagnostics.Items;

    /// <summary>Shows the analyzer's errors and warnings as wavy underlines (infos are not drawn).</summary>
    public void SetDiagnostics(IReadOnlyList<LunetDiagnostic> diagnostics)
    {
        _diagnostics.Items = diagnostics.Where(d => d.Severity != DiagnosticSeverity.Info).ToArray();
        _editor.TextArea.TextView.InvalidateLayer(KnownLayer.Selection);
    }

    // ---------- Folding (the analyzer's regions: types, methods, blocks, #region, comments, usings) ----------

    private AvaloniaEdit.Folding.FoldingManager? _folding;

    /// <summary>The foldable regions, kept from the analyzer; folded ones stay folded while the text changes.</summary>
    public void SetFolds(IReadOnlyList<Lunet.Editor.FoldRegion> regions)
    {
        _folding ??= AvaloniaEdit.Folding.FoldingManager.Install(_editor.TextArea);
        var doc = _editor.Document;
        var folds = regions.Where(r => r.StartLine >= 1 && r.EndLine <= doc.LineCount && r.EndLine > r.StartLine)
            .Select(r => new AvaloniaEdit.Folding.NewFolding(doc.GetLineByNumber(r.StartLine).EndOffset, doc.GetLineByNumber(r.EndLine).EndOffset) { Name = " " + r.Placeholder })
            .OrderBy(f => f.StartOffset).ToList();
        _folding.UpdateFoldings(folds, -1);
    }

    public int FoldCount => _folding?.AllFoldings.Count() ?? 0;
    public int FoldedCount => _folding?.AllFoldings.Count(f => f.IsFolded) ?? 0;

    /// <summary>Folds or unfolds the innermost region that holds the caret's line. False when there is none.</summary>
    public bool ToggleFoldAtCaret()
    {
        if (_folding is null) return false;
        var line = _editor.Document.GetLineByOffset(_editor.CaretOffset);
        var fold = _folding.AllFoldings.Where(f => f.StartOffset <= line.EndOffset && f.EndOffset >= line.Offset)
            .OrderBy(f => f.EndOffset - f.StartOffset).FirstOrDefault();
        if (fold is null) return false;
        fold.IsFolded = !fold.IsFolded;
        return true;
    }

    public void SetAllFolded(bool folded)
    {
        if (_folding is null) return;
        foreach (var f in _folding.AllFoldings) f.IsFolded = folded;
    }

    /// <summary>Selects the next occurrence of the selection (or of the word at the caret), wrapping around.</summary>
    public bool SelectNextOccurrence()
    {
        var text = Text;
        var (start, end) = (SelectionStart, SelectionEnd);
        if (end == start)
        {
            while (start > 0 && (char.IsLetterOrDigit(text[start - 1]) || text[start - 1] == '_')) start--;
            while (end < text.Length && (char.IsLetterOrDigit(text[end]) || text[end] == '_')) end++;
            if (end == start) return false;
            Select(start, end); // first press: the word itself, as VS Code
            return true;
        }
        var word = text[start..end];
        var next = text.IndexOf(word, end, StringComparison.Ordinal);
        if (next < 0) next = text.IndexOf(word, StringComparison.Ordinal);
        if (next < 0) return false;
        Select(next, next + word.Length);
        _editor.ScrollTo(_editor.Document.GetLineByOffset(next).LineNumber, 1);
        return true;
    }

    /// <summary>Puts the caret on a 1-based line and column and brings it into view (Problemas → código).</summary>
    public void GoTo(int line, int column)
    {
        var doc = _editor.Document;
        line = Math.Clamp(line, 1, doc.LineCount);
        var l = doc.GetLineByNumber(line);
        Caret = Math.Min(l.Offset + Math.Max(0, column - 1), l.EndOffset);
        _editor.ScrollTo(line, column);
    }
    public Control View => _editor;
    public InputElement InputTarget => _editor.TextArea;
    public event Action? Engaged;

    public string Text
    {
        get => _editor.Text ?? "";
        set
        {
            _editor.Document.Text = value;
            _editor.Document.UndoStack.ClearAll();
            _editor.CaretOffset = 0;
        }
    }

    public int SelectionStart => _editor.SelectionLength > 0 ? _editor.SelectionStart : _editor.CaretOffset;
    public int SelectionEnd => _editor.SelectionLength > 0 ? _editor.SelectionStart + _editor.SelectionLength : _editor.CaretOffset;
    public int Caret { get => _editor.CaretOffset; set { _editor.SelectionLength = 0; _editor.CaretOffset = Math.Clamp(value, 0, _editor.Document.TextLength); } }
    public bool IsFocused => _editor.TextArea.IsFocused;

    public void Select(int start, int end)
    {
        int a = Math.Clamp(Math.Min(start, end), 0, _editor.Document.TextLength);
        int b = Math.Clamp(Math.Max(start, end), 0, _editor.Document.TextLength);
        _editor.Select(a, b - a);
        _editor.CaretOffset = b;
    }

    public void Replace(int start, int end, string insert, int caret)
    {
        int a = Math.Clamp(Math.Min(start, end), 0, _editor.Document.TextLength);
        int b = Math.Clamp(Math.Max(start, end), 0, _editor.Document.TextLength);
        _editor.Document.Replace(a, b - a, insert); // one undo group per call
        Caret = caret;
        _editor.TextArea.Caret.BringCaretToView();
    }

    public void Undo() => _editor.Undo();
    public void Redo() => _editor.Redo();
    public bool Focus() => _editor.TextArea.Focus();

    /// <summary>C# highlighting recoloured for the dark Lunet theme (the built-in definition is for light themes).</summary>
    private static IHighlightingDefinition? Highlighting()
    {
        var definition = HighlightingManager.Instance.GetDefinition("C#");
        if (definition is null) return null;
        foreach (var color in definition.NamedHighlightingColors)
        {
            var hex = color.Name switch
            {
                "Comment" => LunetTokens.Ink.I10,
                "String" or "Char" => "#a5d6a7",
                "Preprocessor" => "#c4a7e7",
                "Punctuation" => LunetTokens.Ink.I11,
                "MethodCall" => LunetTokens.Color.Info,
                "NumberLiteral" => "#f7c97e",
                "NullOrValueKeywords" or "TrueFalse" => "#ff9e80",
                "Keywords" or "GotoKeywords" or "ContextKeywords" or "ExceptionKeywords" or "CheckedKeyword" or "UnsafeKeywords"
                    or "OperatorKeywords" or "ParameterModifiers" or "Modifiers" or "Visibility" or "NamespaceKeywords"
                    or "GetSetAddRemove" or "ThisOrBaseReference" or "TypeKeywords" or "ValueTypeKeywords" or "ReferenceTypeKeywords"
                    or "SemanticKeywords" => LunetTokens.Color.Primary,
                _ => LunetTokens.Color.Code,
            };
            color.Foreground = new SimpleHighlightingBrush(Color.Parse(hex));
            color.Background = null;
        }
        return definition;
    }
}

/// <summary>Draws a wavy underline under each diagnostic's span (red errors, amber warnings).</summary>
internal sealed class DiagnosticsRenderer : IBackgroundRenderer
{
    public IReadOnlyList<LunetDiagnostic> Items { get; set; } = [];
    public KnownLayer Layer => KnownLayer.Selection;

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (Items.Count == 0 || textView.Document is not { } doc || !textView.VisualLinesValid) return;
        foreach (var d in Items)
        {
            if (d.Line < 1 || d.Line > doc.LineCount) continue;
            var startLine = doc.GetLineByNumber(d.Line);
            int start = Math.Min(startLine.Offset + Math.Max(0, d.Column - 1), startLine.EndOffset);
            int endLineNo = Math.Clamp(d.EndLine, d.Line, doc.LineCount);
            var endLine = doc.GetLineByNumber(endLineNo);
            int end = Math.Min(endLine.Offset + Math.Max(0, d.EndColumn - 1), endLine.EndOffset);
            if (end <= start) end = Math.Min(start + 1, doc.TextLength);
            if (end <= start) continue;
            var pen = new Pen(LunetTokens.Brush(d.Severity == DiagnosticSeverity.Error ? LunetTokens.Color.Danger : LunetTokens.Color.Warning), 1.4);
            var segment = new AvaloniaEdit.Document.TextSegment { StartOffset = start, EndOffset = end };
            foreach (var rect in BackgroundGeometryBuilder.GetRectsForSegment(textView, segment))
            {
                var geometry = new StreamGeometry();
                using (var g = geometry.Open())
                {
                    double y = rect.Bottom - 1, x = rect.Left;
                    g.BeginFigure(new Point(x, y), false);
                    for (bool up = true; x < rect.Right; up = !up)
                    {
                        x = Math.Min(x + 3, rect.Right);
                        g.LineTo(new Point(x, up ? y - 2.5 : y));
                    }
                    g.EndFigure(false);
                }
                drawingContext.DrawGeometry(null, pen, geometry);
            }
        }
    }
}

/// <summary>The notes field: prose with the system keyboard (dictation, accents, suggestions).</summary>
public sealed class NotesSurface : ITextSurface
{
    private readonly TextBox _box = new()
    {
        AcceptsReturn = true, AcceptsTab = false, TextWrapping = TextWrapping.Wrap,
        FontFamily = LunetTokens.UiFont, FontSize = LunetTokens.Type.Body,
        Foreground = LunetTokens.Brush(LunetTokens.Color.TextPrimary),
        Background = LunetTokens.Brush(LunetTokens.Color.Background),
        BorderThickness = new Thickness(0), CornerRadius = new CornerRadius(0),
        Padding = new Thickness(LunetTokens.Space.M, LunetTokens.Space.S),
    };

    public NotesSurface()
    {
        AutomationProperties.SetName(_box, "Editor de notas");
        _box.GotFocus += (_, _) => Engaged?.Invoke();
    }

    public TextBox Box => _box;
    public Control View => _box;
    public InputElement InputTarget => _box;
    public event Action? Engaged;
    public string Text { get => _box.Text ?? ""; set { _box.Text = value; _box.CaretIndex = 0; } }
    public int SelectionStart => Math.Min(_box.SelectionStart, _box.SelectionEnd);
    public int SelectionEnd => Math.Max(_box.SelectionStart, _box.SelectionEnd);
    public int Caret { get => _box.CaretIndex; set { _box.SelectionStart = _box.SelectionEnd = value; _box.CaretIndex = value; } }
    public bool IsFocused => _box.IsFocused;
    public void Select(int start, int end) { _box.SelectionStart = start; _box.SelectionEnd = end; }

    public void Replace(int start, int end, string insert, int caret)
    {
        _box.SelectionStart = start;
        _box.SelectionEnd = end;
        _box.SelectedText = insert;
        Caret = caret;
    }

    public void Undo() => _box.Undo();
    public void Redo() => _box.Redo();
    public bool Focus() => _box.Focus();
}
