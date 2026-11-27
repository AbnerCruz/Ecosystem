using Avalonia;
using Avalonia.Input;
using Avalonia.Input.TextInput;
using Avalonia.Interactivity;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;

namespace Lunet.Client.Views;

/// <summary>
/// The bridge between the system keyboard (IME) and the code editor, in place of the one AvaloniaEdit ships.
/// Avalonia's Android input connection keeps no text of its own: every keyboard command (compose a word, commit it,
/// delete before the caret, move the caret) is turned into "select this range of <see cref="SurroundingText"/>, then
/// delete and type". The range is read from and written to <see cref="Selection"/>, so the selection must be an index
/// into the surrounding text, and setting it must always move the caret. AvaloniaEdit 12.0's client broke both
/// (estudo do editor, 26/11/2026, achados A1–A3): it reported the 1-based column as a 0-based index (the keyboard
/// saw the caret one letter to the right), ignored a new selection while nothing was selected (so the keyboard's
/// "replace this word" deleted the letter after the caret instead) and never asked for the keyboard on a tap.
/// <para>The surrounding text is a window of whole lines around the caret (about <see cref="Reach"/> characters on
/// each side), not the whole file: Android reads it several times per key. The window keeps its place while the
/// caret stays inside it (its ends are anchors that follow the edits), so the indices of one keyboard command never
/// shift halfway through.</para>
/// </summary>
public sealed class CodeInputClient : TextInputMethodClient
{
    /// <summary>Characters of context on each side of the caret.</summary>
    public const int Reach = 1024;

    private readonly TextArea _area;
    private TextDocument? _document;
    private TextAnchor? _start, _end;

    public CodeInputClient(TextArea area)
    {
        _area = area;
        _area.Caret.PositionChanged += (_, _) => OnCaretOrSelection();
        _area.SelectionChanged += (_, _) => OnCaretOrSelection();
        _area.DocumentChanged += (_, _) => Attach(_area.Document);
        // A tap on the editor asks for the keyboard (also after Back closed it while the editor kept the focus).
        _area.AddHandler(InputElement.PointerReleasedEvent, (_, e) => { if (_area.IsFocused && !TouchTap.Moved(e)) RaiseInputPaneActivationRequested(); },
            RoutingStrategies.Bubble, handledEventsToo: true);
        Attach(_area.Document);
    }

    /// <summary>Raised when the editor asked for the keyboard (tests).</summary>
    public int KeyboardRequests { get; private set; }

    protected override void RaiseInputPaneActivationRequested()
    {
        KeyboardRequests++;
        base.RaiseInputPaneActivationRequested();
    }

    private void Attach(TextDocument? document)
    {
        if (ReferenceEquals(document, _document)) return;
        if (_document is not null) _document.Changed -= OnDocumentChanged;
        _document = document;
        _start = _end = null;
        if (_document is not null) _document.Changed += OnDocumentChanged;
        Reset();
    }

    /// <summary>
    /// A new text was loaded (another file): the keyboard drops what it was composing and reads the text again.
    /// </summary>
    public void Reset()
    {
        _start = _end = null;
        RequestReset();
    }

    private void OnDocumentChanged(object? sender, DocumentChangeEventArgs e) => RaiseSurroundingTextChanged();

    private void OnCaretOrSelection()
    {
        RaiseCursorRectangleChanged();
        RaiseSurroundingTextChanged();
        RaiseSelectionChanged();
    }

    /// <summary>The window [start, end) of the document the keyboard sees, moved only when the selection leaves it.</summary>
    public (int Start, int End) Window
    {
        get
        {
            var doc = _document;
            if (doc is null) return (0, 0);
            var (a, b) = DocumentSelection();
            if (_start is { IsDeleted: false } s && _end is { IsDeleted: false } e && s.Offset <= a && b <= e.Offset && e.Offset <= doc.TextLength)
                return (s.Offset, e.Offset);
            int from = doc.GetLineByOffset(Math.Max(0, a - Reach)).Offset;
            int to = doc.GetLineByOffset(Math.Min(doc.TextLength, b + Reach)).EndOffset;
            // A single very long line: cut it, keeping the selection inside.
            if (a - from > 4 * Reach) from = Math.Max(0, a - Reach);
            if (to - b > 4 * Reach) to = Math.Min(doc.TextLength, b + Reach);
            _start = doc.CreateAnchor(from);
            _start.MovementType = AnchorMovementType.BeforeInsertion;
            _start.SurviveDeletion = true;
            _end = doc.CreateAnchor(to);
            _end.MovementType = AnchorMovementType.AfterInsertion;
            _end.SurviveDeletion = true;
            return (from, to);
        }
    }

    private (int Start, int End) DocumentSelection()
    {
        if (_area.Selection.IsEmpty || _area.Selection.SurroundingSegment is not { } segment)
            return (_area.Caret.Offset, _area.Caret.Offset);
        return (segment.Offset, segment.EndOffset);
    }

    public override Visual TextViewVisual => _area;
    public override bool SupportsPreedit => false;
    public override bool SupportsSurroundingText => true;

    public override string SurroundingText
    {
        get
        {
            if (_document is null) return "";
            var (start, end) = Window;
            return _document.GetText(start, end - start);
        }
    }

    public override TextSelection Selection
    {
        get
        {
            var (start, _) = Window;
            var (a, b) = DocumentSelection();
            return new TextSelection(a - start, b - start);
        }
        set
        {
            if (_document is null) return;
            var (start, end) = Window;
            int a = Math.Clamp(start + Math.Min(value.Start, value.End), start, end);
            int b = Math.Clamp(start + Math.Max(value.Start, value.End), start, end);
            if (a == b)
            {
                _area.ClearSelection();
                _area.Caret.Offset = a;
            }
            else
            {
                _area.Selection = AvaloniaEdit.Editing.Selection.Create(_area, a, b);
                _area.Caret.Offset = b;
            }
        }
    }

    public override Rect CursorRectangle
    {
        get
        {
            var transform = _area.TextView.TransformToVisual(_area);
            if (transform is null) return default;
            var rect = _area.Caret.CalculateCaretRectangle().TransformToAABB(transform.Value);
            var scroll = _area.TextView.ScrollOffset;
            return rect.WithX(rect.X - scroll.X).WithY(rect.Y - scroll.Y);
        }
    }

    /// <summary>The keyboard's own Cut/Copy/Paste/Select all (Gboard's clipboard bar, the selection toolbar).</summary>
    public override void ExecuteContextMenuAction(ContextMenuAction action)
    {
        var command = action switch
        {
            ContextMenuAction.Copy => AvaloniaEdit.ApplicationCommands.Copy,
            ContextMenuAction.Cut => AvaloniaEdit.ApplicationCommands.Cut,
            ContextMenuAction.Paste => AvaloniaEdit.ApplicationCommands.Paste,
            ContextMenuAction.SelectAll => AvaloniaEdit.ApplicationCommands.SelectAll,
            _ => null,
        };
        if (command is not null && command.CanExecute(null, _area)) command.Execute(null, _area);
    }
}
