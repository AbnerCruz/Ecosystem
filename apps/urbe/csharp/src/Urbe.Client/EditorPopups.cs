using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Urbe.Core;

namespace Urbe.Client;

/// <summary>Drawing of the small editor lists/toolbars. Buttons never take focus: the text box keeps the caret and the soft keyboard.</summary>
internal static class EditorChrome
{
    public static Border Card(Control child) => new()
    {
        Background = UrbeTheme.Brush(UrbeTheme.Surface2), BorderBrush = UrbeTheme.Brush(UrbeTheme.Line2), BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(12), Padding = new Thickness(6), Child = child, IsVisible = false, HorizontalAlignment = HorizontalAlignment.Left,
        BoxShadow = BoxShadows.Parse("0 6 24 0 #66000000")
    };

    public static Button Item(Control content, bool active, Action click)
    {
        var b = new Button
        {
            Content = content, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left,
            MinHeight = 40, Padding = new Thickness(10, 4), CornerRadius = new CornerRadius(8), Focusable = false,
            Background = active ? UrbeTheme.Brush(UrbeTheme.Accent, .14) : Brushes.Transparent, Foreground = UrbeTheme.Brush(UrbeTheme.Text)
        };
        b.Click += (_, _) => click();
        return b;
    }
}

/// <summary>#wikiSuggest: the note suggestions after "[[" (names from the knowledge search; Enter/Tab pick, arrows move, Escape closes).</summary>
internal sealed class WikiSuggestList : Border
{
    private readonly StackPanel _items = new() { MinWidth = 220, MaxWidth = 360 };
    private IReadOnlyList<string> _names = [];
    private int _index;

    public WikiSuggestList()
    {
        var card = EditorChrome.Card(_items);
        Child = card;
        IsVisible = false;
        Margin = new Thickness(0, 4);
    }

    public bool IsOpen => IsVisible;
    public event Action<string>? Chosen;

    public void Show(IReadOnlyList<string> names)
    {
        _names = names;
        _index = Math.Min(_index, Math.Max(0, names.Count - 1));
        Render();
        IsVisible = true;
        ((Border)Child!).IsVisible = true;
    }

    public void Hide() { IsVisible = false; _index = 0; _names = []; }

    private void Render()
    {
        _items.Children.Clear();
        if (_names.Count == 0)
        {
            _items.Children.Add(new TextBlock { Text = "Nenhuma nota encontrada.", Foreground = UrbeTheme.Brush(UrbeTheme.Text3), FontSize = 13, Margin = new Thickness(10, 10) });
            return;
        }
        for (var i = 0; i < _names.Count; i++)
        {
            var name = _names[i];
            _items.Children.Add(EditorChrome.Item(new TextBlock { Text = name, FontSize = 15, TextTrimming = TextTrimming.CharacterEllipsis }, i == _index, () => Chosen?.Invoke(name)));
        }
    }

    /// <summary>Arrow/Enter/Tab/Escape of bodyEditor's keydown while the list is open. True when the key was used.</summary>
    public bool HandleKey(Key key)
    {
        if (!IsOpen) return false;
        switch (key)
        {
            case Key.Down: if (_names.Count > 0) _index = (_index + 1) % _names.Count; Render(); return true;
            case Key.Up: if (_names.Count > 0) _index = (_index - 1 + _names.Count) % _names.Count; Render(); return true;
            case Key.Enter or Key.Tab when _names.Count > 0: Chosen?.Invoke(_names[_index]); return true;
            case Key.Escape: Hide(); return true;
        }
        return false;
    }
}

/// <summary>#urbeSlashMenu: "/" at the start of an empty block lists the block types (visual-tools.js ITEMS, same filter).</summary>
internal sealed class SlashMenu : Border
{
    private readonly StackPanel _items = new() { Width = 300 };
    private IReadOnlyList<LegacyEditorTools.SlashItem> _filtered = [];
    private int _index;

    public SlashMenu()
    {
        Child = EditorChrome.Card(_items);
        IsVisible = false;
        Margin = new Thickness(0, 4);
    }

    public bool IsOpen => IsVisible;
    public event Action<string>? Chosen;
    /// <summary>The items in the person's order (Personalização/editor.json), or null.</summary>
    public IReadOnlyList<string>? Order { get; set; }

    public void Show(string query)
    {
        _filtered = EditorLayout.Arrange(LegacyEditorTools.FilterSlashWithAddons(query), Order);
        _index = Math.Min(_index, Math.Max(0, _filtered.Count - 1));
        Render();
        IsVisible = true;
        ((Border)Child!).IsVisible = true;
    }

    public void Hide() { IsVisible = false; _index = 0; }

    private void Render()
    {
        _items.Children.Clear();
        if (_filtered.Count == 0)
        {
            _items.Children.Add(new TextBlock { Text = "Nenhum bloco com esse nome", Foreground = UrbeTheme.Brush(UrbeTheme.Text3), FontSize = 14, Margin = new Thickness(14) });
            return;
        }
        for (var i = 0; i < _filtered.Count; i++)
        {
            var x = _filtered[i];
            Control icon = x.Icon is { } ic && Icons.TryGetValue(ic, out var path)
                ? UrbeTheme.Icon(path, 18, UrbeTheme.Text2)
                : new TextBlock { Text = x.Text ?? "", FontWeight = FontWeight.Bold, FontSize = 12, Foreground = UrbeTheme.Brush(UrbeTheme.Text2), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
            row.Children.Add(new Border { Width = 34, Height = 34, CornerRadius = new CornerRadius(8), Background = UrbeTheme.Brush(UrbeTheme.Surface3), Child = new Grid { Children = { icon } } });
            row.Children.Add(new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                Children =
                {
                    new TextBlock { Text = x.Label, FontSize = 15 },
                    new TextBlock { Text = x.Hint, FontSize = 12, Foreground = UrbeTheme.Brush(UrbeTheme.Text3) }
                }
            });
            var act = x.Act;
            var item = EditorChrome.Item(row, i == _index, () => Chosen?.Invoke(act));
            item.MinHeight = 48;
            _items.Children.Add(item);
        }
    }

    public bool HandleKey(Key key)
    {
        if (!IsOpen) return false;
        switch (key)
        {
            case Key.Down: if (_filtered.Count > 0) _index = (_index + 1) % _filtered.Count; Render(); return true;
            case Key.Up: if (_filtered.Count > 0) _index = (_index - 1 + _filtered.Count) % _filtered.Count; Render(); return true;
            case Key.Enter or Key.Tab when _filtered.Count > 0: Chosen?.Invoke(_filtered[_index].Act); return true;
            case Key.Escape: Hide(); return true;
        }
        return false;
    }

    // ui/icons.js (1.8.4) — the same line icons the toolbar and the "/" menu use
    internal static readonly Dictionary<string, string> Icons = new()
    {
        ["link"] = "M10 14a4 4 0 0 0 5.7 0l3-3a4 4 0 0 0-5.7-5.7l-1 1 M14 10a4 4 0 0 0-5.7 0l-3 3a4 4 0 0 0 5.7 5.7l1-1",
        ["wikilink"] = "M7.5 5H5v14h2.5 M10.5 5H8.5v14h2 M13.5 5h2v14h-2 M16.5 5H19v14h-2.5",
        ["list"] = "M9.5 6.5h10 M9.5 12h10 M9.5 17.5h10 M5 6.5h.01 M5 12h.01 M5 17.5h.01",
        ["task"] = "M7 4.5h9a3 3 0 0 1 3 3v9a3 3 0 0 1-3 3H7a3 3 0 0 1-3-3v-9a3 3 0 0 1 3-3z M8 12l2.8 2.8L16 9.5",
        ["quote"] = "M5 8.5h5v5.5c0 2.5-1.5 4-4 4.5 M14 8.5h5v5.5c0 2.5-1.5 4-4 4.5",
        ["code"] = "M9 7.5L4.5 12 9 16.5 M15 7.5l4.5 4.5-4.5 4.5",
        ["rule"] = "M4 12h16",
        // Caixa (UC-44): a box with a header line
        ["box"] = "M6 4.5h12a2 2 0 0 1 2 2v11a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2v-11a2 2 0 0 1 2-2z M4 9h16",
    };
}
