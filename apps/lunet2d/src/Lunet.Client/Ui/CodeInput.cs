using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.TextInput;
using Avalonia.Styling;

namespace Lunet.Client.Ui;

/// <summary>
/// What the system keyboard is told about a text field (estudo do editor, 26/11/2026). Avalonia reads these attached
/// options of the focused element when it hands it to the keyboard; on Android a field that is not declared
/// multi-line gets the "Done" action, and Enter then <b>closes the keyboard</b> before it is delivered — the "teclado
/// sem quebra de linha / fechando sozinho" of the owner's reports. Neither TextBox (AcceptsReturn) nor AvaloniaEdit
/// declares it, so every multi-line field goes through here (the app style does it for any TextBox with AcceptsReturn).
/// </summary>
public static class CodeInput
{
    /// <summary>Multi-line field whose Enter inserts a line break; <paramref name="suggestions"/> false for code.</summary>
    public static void Configure(StyledElement field, bool suggestions)
    {
        TextInputOptions.SetMultiline(field, true);
        TextInputOptions.SetReturnKeyType(field, TextInputReturnKeyType.Return);
        if (!suggestions) TextInputOptions.SetShowSuggestions(field, false);
    }

    /// <summary>The style that declares every multi-line TextBox of the app (notes, the AI, JSON fields, Inspector).</summary>
    public static Avalonia.Styling.Style MultilineTextBoxes() =>
        new(x => x.OfType<TextBox>().PropertyEquals(TextBox.AcceptsReturnProperty, true))
        {
            Setters =
            {
                new Avalonia.Styling.Setter(TextInputOptions.MultilineProperty, true),
                new Avalonia.Styling.Setter(TextInputOptions.ReturnKeyTypeProperty, TextInputReturnKeyType.Return),
            },
        };
}
