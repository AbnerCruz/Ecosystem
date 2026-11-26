using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.TextInput;

namespace Lunet.Tests;

/// <summary>
/// What Avalonia 12.1.3's Android input connection does with a text client, step by step, so the keyboard's path
/// can be tested without a phone (estudo do editor, 26/11/2026). A line-by-line port of
/// <c>src/Android/Avalonia.Android/Platform/Input/{TextEditBuffer,EditCommand,AvaloniaInputConnection}.cs</c>: the
/// connection keeps no text, reads <see cref="TextInputMethodClient.SurroundingText"/> and
/// <see cref="TextInputMethodClient.Selection"/>, and edits by "set the selection, press Delete, type". Delete and
/// typing go through the window exactly as Android's key events and <c>TopLevel.TextInput</c> do. It does not
/// replace a real keyboard (Gboard's own decisions are not modelled), only the framework's half.
/// </summary>
public sealed class AndroidImeSimulator(TopLevel top, TextInputMethodClient client)
{
    private (int Start, int End)? _composition;

    public string Text => client.SurroundingText ?? "";
    public TextSelection Selection
    {
        get { var s = client.Selection; return new TextSelection(Math.Min(s.Start, s.End), Math.Max(s.Start, s.End)); }
        set => client.Selection = value;
    }

    /// <summary>InputConnection.setComposingText (the keyboard shows the word being typed).</summary>
    public void SetComposingText(string text, int newCursorPosition = 1)
    {
        if (_composition is { } c && c.Start != c.End)
        {
            var start = c.Start;
            Replace(c.Start, c.End, text);
            _composition = (start, start + text.Length);
        }
        else
        {
            var selection = Selection;
            Replace(selection.Start, selection.End, text);
            _composition = (selection.Start, selection.Start + text.Length);
        }
        var cursor = newCursorPosition > 0 ? Selection.Start + newCursorPosition - 1 : Selection.Start + newCursorPosition;
        Selection = new TextSelection(cursor, cursor);
    }

    /// <summary>InputConnection.commitText.</summary>
    public void CommitText(string text, int newCursorPosition = 1)
    {
        if (_composition is { } c && c.Start != c.End) Replace(c.Start, c.End, text);
        else Replace(Selection.Start, Selection.End, text);
        var cursor = newCursorPosition > 0 ? Selection.Start + newCursorPosition - 1 : Selection.Start + newCursorPosition - text.Length;
        Selection = new TextSelection(cursor, cursor);
    }

    /// <summary>InputConnection.finishComposingText.</summary>
    public void FinishComposing() => _composition = null;

    /// <summary>InputConnection.deleteSurroundingText (the keyboard's backspace on a word or a suggestion).</summary>
    public void DeleteSurroundingText(int before, int after)
    {
        var end = Math.Min(Text.Length, Selection.End + after);
        var endCount = end - Selection.End;
        var start = Math.Max(0, Selection.Start - before);
        Remove(Selection.End, endCount);
        Remove(start, Selection.Start - start);
        Selection = new TextSelection(start, start);
    }

    /// <summary>InputConnection.setSelection (Gboard's cursor drag on the space bar, its "select" tools).</summary>
    public void SetSelection(int start, int end) =>
        Selection = new TextSelection(Math.Clamp(Math.Min(start, end), 0, Text.Length), Math.Clamp(Math.Max(start, end), 0, Text.Length));

    /// <summary>The Enter key as a key event (what a keyboard sends for a multi-line field).</summary>
    public void Enter() => top.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);

    /// <summary>The text before the caret, as the keyboard asks for it to decide capitals and corrections.</summary>
    public string TextBeforeCursor(int n)
    {
        var start = Math.Max(0, Selection.Start - n);
        return Text.Substring(start, Selection.Start - start);
    }

    private void Remove(int index, int length)
    {
        client.Selection = new TextSelection(index, index + length);
        if (length > 0) top.KeyPressQwerty(PhysicalKey.Delete, RawInputModifiers.None);
    }

    private void Replace(int start, int end, string text)
    {
        int a = Math.Min(start, end), b = Math.Max(start, end);
        if (b > a)
        {
            client.Selection = new TextSelection(a, b);
            top.KeyPressQwerty(PhysicalKey.Delete, RawInputModifiers.None);
        }
        top.KeyTextInput(text);
        var index = a + text.Length;
        client.Selection = new TextSelection(index, index);
        _composition = null;
    }

    /// <summary>
    /// The action the keyboard gets for Enter, as <c>AndroidInputMethod.SetOptions</c> computes it from the focused
    /// element's options: "Done" closes the keyboard (<c>PerformEditorAction</c> hides it, then sends Enter).
    /// </summary>
    public static string EnterAction(StyledElement focused)
    {
        var options = TextInputOptions.FromStyledElement(focused);
        return options.ReturnKeyType switch
        {
            TextInputReturnKeyType.Return => "line break",
            TextInputReturnKeyType.Default => options.Multiline ? "line break" : "Done (closes the keyboard)",
            var other => other.ToString(),
        };
    }

    /// <summary>The client the focused element hands to the keyboard (as <c>TextInputMethodManager</c> asks for it).</summary>
    public static TextInputMethodClient? ClientOf(InputElement focused)
    {
        var query = new TextInputMethodClientRequestedEventArgs { RoutedEvent = InputElement.TextInputMethodClientRequestedEvent };
        focused.RaiseEvent(query);
        return query.Client;
    }
}
