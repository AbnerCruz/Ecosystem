using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media.TextFormatting;
using Avalonia.Styling;
using Avalonia.VisualTree;

namespace Urbe.Client.Ui;

/// <summary>
/// Selecting, copying, cutting, pasting and selecting everything, the same in every text of the app, on the phone as
/// with a mouse (editor review, 15/11/2026).
/// <para>
/// On a phone, Avalonia opens the text box's <see cref="Control.ContextFlyout"/> next to the selection handles (a long
/// press, a handle let go), and the Fluent theme's flyout says "Cut / Copy / Paste" in English with no "select all";
/// the Portuguese <see cref="ContextMenu"/> this class used to set was never shown there, because a flyout wins. Every
/// text box now gets a Portuguese flyout with Recortar, Copiar, Colar and Selecionar tudo (horizontal, as the platform
/// shows it, on a phone), and the theme's own strings are Portuguese too. Read-only texts (the Assistente's answers,
/// code, forms) get Copiar and Selecionar tudo, and a long press selects the word under the finger and opens them.
/// </para>
/// </summary>
public static class TextActions
{
    private static bool _installed;

    // boxes whose menu is drawn by their owner (the Visual block: its bubble holds these actions)
    private static readonly ConditionalWeakTable<TextBox, object> OwnMenu = new();
    private static readonly ConditionalWeakTable<Control, object> Attached = new();

    public static void Install()
    {
        if (_installed) return;
        _installed = true;
        // the theme's own words (any flyout or menu the theme still opens) in Portuguese
        if (Application.Current is { } app)
        {
            app.Resources["StringTextFlyoutCutText"] = "Recortar";
            app.Resources["StringTextFlyoutCopyText"] = "Copiar";
            app.Resources["StringTextFlyoutPasteText"] = "Colar";
        }
        InputElement.GotFocusEvent.AddClassHandler<TextBox>((box, _) => Attach(box));
        // a long press on a read-only text, anywhere in the app: the word under the finger and Copiar / Selecionar tudo
        InputElement.HoldingEvent.AddClassHandler<SelectableTextBlock>((text, e) => OnHolding(text, e));
    }

    /// <summary>The box's menu is drawn by its owner (it calls <see cref="Copy"/>, <see cref="Cut"/>… itself).</summary>
    public static void UseOwnMenu(TextBox box)
    {
        OwnMenu.AddOrUpdate(box, new object());
        box.ContextFlyout = null;
        box.ContextMenu = null;
    }

    public static void Attach(TextBox box)
    {
        if (OwnMenu.TryGetValue(box, out _) || box.PasswordChar != default || Attached.TryGetValue(box, out _)) return;
        Attached.Add(box, new object());
        var cut = Item("Recortar", () => box.Cut());
        var copy = Item("Copiar", () => box.Copy());
        var paste = Item("Colar", () => box.Paste());
        var all = Item("Selecionar tudo", () => { box.SelectAll(); TouchTextScroll.ShowSelectionHandles(box); });
        var flyout = Flyout([cut, copy, paste, all]);
        flyout.Opening += (_, _) =>
        {
            var text = box.Text ?? "";
            var selected = box.SelectionStart != box.SelectionEnd;
            copy.IsVisible = selected;
            cut.IsVisible = selected && !box.IsReadOnly;
            paste.IsVisible = !box.IsReadOnly;
            all.IsVisible = text.Length > 0 && Math.Abs(box.SelectionEnd - box.SelectionStart) < text.Length;
        };
        box.ContextFlyout = flyout;
        box.ContextMenu = null;
    }

    public static void Attach(SelectableTextBlock text)
    {
        if (Attached.TryGetValue(text, out _)) return;
        Attached.Add(text, new object());
        var copy = Item("Copiar", () => text.Copy());
        var all = Item("Selecionar tudo", () => text.SelectAll());
        var flyout = Flyout([copy, all]);
        flyout.Opening += (_, _) => copy.IsVisible = text.SelectionStart != text.SelectionEnd;
        text.ContextFlyout = flyout;
        text.ContextMenu = null;
    }

    // a finger held on a read-only text: the word under it is selected and the menu opens beside it
    private static void OnHolding(SelectableTextBlock text, HoldingRoutedEventArgs e)
    {
        if (e.HoldingState != HoldingState.Started || e.Handled) return;
        Attach(text);
        var full = TextOf(text);
        if (full.Length == 0) return;
        if (text.SelectionStart == text.SelectionEnd)
        {
            var hit = text.TextLayout.HitTestPoint(e.Position);
            var (from, to) = VisualSurface.WordAt(full, hit.TextPosition + (hit.IsTrailing ? 1 : 0));
            if (to > from) { text.SelectionStart = from; text.SelectionEnd = to; }
        }
        e.Handled = true;
        PlatformHaptics.Hold();
        if (text.ContextFlyout is PopupFlyoutBase menu) menu.ShowAt(text, true); else text.ContextFlyout?.ShowAt(text);
    }

    /// <summary>The text a SelectableTextBlock shows (its runs, line breaks and embedded items as one character each).</summary>
    internal static string TextOf(TextBlock tb)
    {
        if (tb.Inlines is not { Count: > 0 } inlines) return tb.Text ?? "";
        var sb = new System.Text.StringBuilder();
        void Add(Inline i)
        {
            switch (i)
            {
                case Run r: sb.Append(r.Text); break;
                case LineBreak: sb.Append('\n'); break;
                case InlineUIContainer: sb.Append('￼'); break;
                case Span span: foreach (var c in span.Inlines) Add(c); break;
            }
        }
        foreach (var i in inlines) Add(i);
        return sb.ToString();
    }

    private static MenuFlyout Flyout(MenuItem[] items)
    {
        var flyout = new MenuFlyout { ItemsSource = items };
        // a phone shows the actions in a row by the selection, closing when the finger goes elsewhere (the platform's way)
        if (OperatingSystem.IsAndroid() || OperatingSystem.IsIOS())
        {
            flyout.ShowMode = FlyoutShowMode.Transient;
            if (Application.Current?.TryFindResource("HorizontalMenuFlyoutPresenter", out var presenter) == true && presenter is ControlTheme pt)
                flyout.FlyoutPresenterTheme = pt;
            if (Application.Current?.TryFindResource("HorizontalMenuItem", out var item) == true && item is ControlTheme it)
                flyout.ItemContainerTheme = it;
        }
        return flyout;
    }

    private static MenuItem Item(string name, Action action)
    {
        var item = new MenuItem { Header = name, MinHeight = UrbeTokens.MinTouch };
        item.Click += (_, _) => action();
        return item;
    }

    /// <summary>Copies the selection of <paramref name="box"/>; false when nothing is selected.</summary>
    public static bool Copy(TextBox box)
    {
        if (box.SelectionStart == box.SelectionEnd) return false;
        box.Copy();
        return true;
    }

    public static async Task<bool> CopyAsync(Control owner, string text)
    {
        if (TopLevel.GetTopLevel(owner)?.Clipboard is not { } clipboard) return false;
        try { await clipboard.SetTextAsync(text); return true; }
        catch (Exception e) when (e is not OutOfMemoryException) { return false; }
    }
}
