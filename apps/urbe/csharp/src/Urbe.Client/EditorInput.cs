using Avalonia.Controls;
using Avalonia.Input.TextInput;

namespace Urbe.Client;

/// <summary>Native text input semantics shared by the note's two surfaces and the multiline composer.</summary>
public static class EditorInput
{
    public static void ConfigureMultiline(TextBox box)
    {
        box.AcceptsReturn = true;
        // AcceptsReturn controls TextBox's key handling; it does not set the IME options.
        // Android maps single-line/default to ActionDone, which hides the IME before sending Enter.
        TextInputOptions.SetMultiline(box, true);
        TextInputOptions.SetReturnKeyType(box, TextInputReturnKeyType.Return);
    }

    public static void Replace(TextBox box, string next)
    {
        var before = box.Text ?? "";
        if (before == next) return;
        int start = 0, end = before.Length, nextEnd = next.Length;
        while (start < end && start < nextEnd && before[start] == next[start]) start++;
        while (end > start && nextEnd > start && before[end - 1] == next[nextEnd - 1]) { end--; nextEnd--; }
        box.SelectionStart = start;
        box.SelectionEnd = end;
        box.SelectedText = next[start..nextEnd];
    }
}
