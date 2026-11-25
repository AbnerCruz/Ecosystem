using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.Highlighting;
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
        _editor.TextArea.TextView.CurrentLineBackground = LunetTokens.Brush("#1b1d22");
        _editor.TextArea.TextView.CurrentLineBorder = new Pen(LunetTokens.Brush("#00000000"));
        _editor.TextArea.SelectionBrush = LunetTokens.Brush("#665b45cf");
        _editor.TextArea.SelectionCornerRadius = 3;
        _editor.TextArea.Caret.CaretBrush = LunetTokens.Brush(LunetTokens.Color.Primary);
        AutomationProperties.SetName(_editor.TextArea, "Editor de código");
        _editor.TextArea.GotFocus += (_, _) => Engaged?.Invoke();
        _editor.TextArea.AddHandler(InputElement.PointerReleasedEvent, (_, _) => Engaged?.Invoke(),
            Avalonia.Interactivity.RoutingStrategies.Tunnel | Avalonia.Interactivity.RoutingStrategies.Bubble, handledEventsToo: true);
    }

    /// <summary>The underlying editor (tests and the head's hardware shortcuts).</summary>
    public TextEditor Editor => _editor;
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
                "Comment" => "#7f8696",
                "String" or "Char" => "#a5d6a7",
                "Preprocessor" => "#c4a7e7",
                "Punctuation" => "#b8bdc8",
                "MethodCall" => "#82aaff",
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
