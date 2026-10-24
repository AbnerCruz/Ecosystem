using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;

namespace Urbe.Client.Ui;

/// <summary>Clipboard actions preserve the exact source and the selected range, across all native text fields.</summary>
public static class TextActions
{
    private static bool _installed;
    public static void Install()
    {
        if (_installed) return;
        _installed = true;
        InputElement.GotFocusEvent.AddClassHandler<TextBox>((box, _) => Attach(box));
    }

    public static void Attach(TextBox box)
    {
        if (box.ContextMenu is not null || box.PasswordChar != default) return;
        var copy = Item("Copiar", () => box.Copy());
        var cut = Item("Recortar", () => box.Cut());
        var paste = Item("Colar", () => box.Paste());
        var all = Item("Selecionar tudo", () => { box.SelectAll(); TouchTextScroll.ShowSelectionHandles(box); });
        var menu = new ContextMenu { ItemsSource = new[] { copy, cut, paste, all } };
        menu.Opening += (_, _) =>
        {
            copy.IsEnabled = box.SelectionStart != box.SelectionEnd;
            cut.IsEnabled = copy.IsEnabled && !box.IsReadOnly;
            paste.IsEnabled = !box.IsReadOnly;
        };
        box.ContextMenu = menu;
    }

    public static void Attach(SelectableTextBlock text)
    {
        var menu = new ContextMenu { ItemsSource = new[]
        {
            Item("Copiar", () => text.Copy()), Item("Selecionar tudo", () => text.SelectAll())
        } };
        text.ContextMenu = menu;
    }

    private static MenuItem Item(string name, Action action)
    {
        var item = new MenuItem { Header = name, MinHeight = UrbeTokens.MinTouch };
        item.Click += (_, _) => action();
        return item;
    }

    public static async Task<bool> CopyAsync(Control owner, string text)
    {
        if (TopLevel.GetTopLevel(owner)?.Clipboard is not { } clipboard) return false;
        try { await clipboard.SetTextAsync(text); return true; }
        catch (Exception e) when (e is not OutOfMemoryException) { return false; }
    }
}
