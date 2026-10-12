using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Urbe.Core;

namespace Urbe.Client;

/// <summary>
/// "Localizar e substituir" of the 1.8.4 (src/editor/find-ui.js, #urbeFindBar in editor-chrome.css + theme.css): Localizar,
/// the count, Substituir, Próximo, Substituir, Todos and ×, at the top of the editor, over the plain text (it switches to
/// Fonte). Próximo selects the next hit in turn; Substituir replaces the first hit and Todos every hit
/// (<see cref="LegacyEditorFind"/>). Enter in Localizar is Próximo; Escape closes. Up to 600 px wide the two fields share
/// the first line and the buttons and the count go below.
/// </summary>
public sealed class EditorFindBar : Border
{
    private readonly Func<TextBox> _editor;
    private readonly Action _toSource;
    private readonly Grid _grid = new();
    private readonly TextBlock _status = new() { FontSize = 11, VerticalAlignment = VerticalAlignment.Center };
    private readonly Button _next, _one, _all, _close;
    private int _cursor;

    public EditorFindBar(Func<TextBox> editor, Action toSource)
    {
        _editor = editor;
        _toSource = toSource;
        IsVisible = false;
        Background = UrbeTheme.Brush(UrbeTheme.Surface);
        BorderBrush = UrbeTheme.Brush(UrbeTheme.Line);
        BorderThickness = new Thickness(0, 0, 0, 1);
        Padding = new Thickness(9, 7);
        Query = Field("Localizar");
        Replacement = Field("Substituir");
        _status.Foreground = UrbeTheme.Brush(UrbeTheme.Text2);
        _next = Action("Próximo", Next);
        _one = Action("Substituir", () => Replace(false));
        _all = Action("Todos", () => Replace(true));
        _close = Action("×", Close);
        Query.TextChanged += (_, _) => Update();
        Query.AddHandler(KeyDownEvent, (_, e) =>
        {
            if (e.Key == Key.Enter) { e.Handled = true; Next(); }
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        AddHandler(KeyDownEvent, (_, e) =>
        {
            if (e.Key == Key.Escape) { e.Handled = true; Close(); }
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        _grid.ColumnDefinitions = new ColumnDefinitions("*");
        Child = _grid;
        Layout(narrow: false);
        SizeChanged += (_, e) => Layout(e.NewSize.Width <= 600);
    }

    public TextBox Query { get; }
    public TextBox Replacement { get; }

    /// <summary>"n resultado(s)" or "Nenhum resultado".</summary>
    public string Status => _status.Text ?? "";

    /// <summary>open(): to Fonte, shown, from the first hit, Localizar focused with its text selected, counted.</summary>
    public void Open()
    {
        _toSource();
        IsVisible = true;
        _cursor = 0;
        Avalonia.Threading.Dispatcher.UIThread.Post(() => { Query.Focus(); Query.SelectAll(); }, Avalonia.Threading.DispatcherPriority.Loaded);
        Update();
    }

    public void Close() => IsVisible = false;

    private IReadOnlyList<LegacyFindHit> Hits() => LegacyEditorFind.Find(_editor().Text, Query.Text);

    private void Update()
    {
        int n = Hits().Count;
        _status.Text = n > 0 ? n + " resultado" + (n == 1 ? "" : "s") : "Nenhum resultado";
    }

    /// <summary>next(): selects hit (cursor mod n) in the text and moves the cursor on.</summary>
    public void Next()
    {
        var hits = Hits();
        if (hits.Count == 0) return;
        var hit = hits[_cursor % hits.Count];
        var editor = _editor();
        editor.Focus();
        editor.SelectionStart = hit.Index;
        editor.SelectionEnd = hit.Index + hit.Length;
        _cursor = (_cursor + 1) % hits.Count;
    }

    /// <summary>replace(all): the first hit, or every hit, replaced in the note's text; the count again.</summary>
    public void Replace(bool all)
    {
        if (string.IsNullOrEmpty(Query.Text)) return;
        var editor = _editor();
        var text = editor.Text ?? "";
        var next = LegacyEditorFind.Replace(text, Query.Text, Replacement.Text, all);
        if (!string.Equals(next, text, StringComparison.Ordinal)) editor.Text = next;
        _cursor = 0;
        Update();
    }

    private static TextBox Field(string placeholder)
    {
        // #urbeFindBar input: 36px, radius 8, line-2 border, bg, 500 14px, padding 0 9px
        var box = new TextBox
        {
            PlaceholderText = placeholder,
            Height = 36,
            MinHeight = 36,
            MinWidth = 0,
            FontSize = 14,
            FontWeight = FontWeight.Medium,
            Padding = new Thickness(9, 0),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            BorderBrush = UrbeTheme.Brush(UrbeTheme.Line2),
            Background = UrbeTheme.Brush(UrbeTheme.Bg),
            Foreground = UrbeTheme.Brush(UrbeTheme.Text),
            VerticalContentAlignment = VerticalAlignment.Center
        };
        foreach (var key in new[] { "TextControlBackground", "TextControlBackgroundPointerOver", "TextControlBackgroundFocused" })
            box.Resources[key] = UrbeTheme.Brush(UrbeTheme.Bg);
        foreach (var key in new[] { "TextControlBorderBrush", "TextControlBorderBrushPointerOver", "TextControlBorderBrushFocused" })
            box.Resources[key] = UrbeTheme.Brush(UrbeTheme.Line2);
        return box;
    }

    private static Button Action(string label, System.Action run)
    {
        // #urbeFindBar button: 36px, radius 8, surface-3 with a line-2 border, 600 13px
        var b = new Button
        {
            Content = label,
            Height = 36,
            Padding = new Thickness(10, 0),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            BorderBrush = UrbeTheme.Brush(UrbeTheme.Line2),
            Background = UrbeTheme.Brush(UrbeTheme.Surface3),
            Foreground = UrbeTheme.Brush(UrbeTheme.Text),
            FontSize = 13,
            FontWeight = FontWeight.SemiBold,
            VerticalContentAlignment = VerticalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        b.Click += (_, _) => run();
        return b;
    }

    private bool? _narrow;

    /// <summary>One line (gap 6px; Localizar grows from 180px and Substituir from 160px), or up to 600px wide two: the
    /// fields half and half, then the buttons and the count (flex-wrap, order 3).</summary>
    private void Layout(bool narrow)
    {
        if (_narrow == narrow) return;
        _narrow = narrow;
        _grid.Children.Clear();
        _grid.RowDefinitions.Clear();
        _grid.ColumnDefinitions.Clear();
        Control[] items = [Query, _status, Replacement, _next, _one, _all, _close];
        foreach (var c in items) c.Margin = new Thickness(0);
        if (!narrow)
        {
            _grid.ColumnDefinitions = new ColumnDefinitions("180*,Auto,160*,Auto,Auto,Auto,Auto");
            for (int i = 0; i < items.Length; i++)
            {
                items[i].Margin = new Thickness(i == 0 ? 0 : 6, 0, 0, 0);
                Grid.SetColumn(items[i], i);
                Grid.SetRow(items[i], 0);
                _grid.Children.Add(items[i]);
            }
            return;
        }
        _grid.RowDefinitions = new RowDefinitions("Auto,Auto");
        _grid.ColumnDefinitions = new ColumnDefinitions("*,*");
        Query.Margin = new Thickness(0, 0, 3, 0);
        Replacement.Margin = new Thickness(3, 0, 0, 0);
        Grid.SetColumn(Query, 0);
        Grid.SetColumn(Replacement, 1);
        Grid.SetRow(Query, 0);
        Grid.SetRow(Replacement, 0);
        _grid.Children.Add(Query);
        _grid.Children.Add(Replacement);
        var second = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Margin = new Thickness(0, 6, 0, 0), Children = { _next, _one, _all, _close, _status } };
        foreach (var b in new[] { _next, _one, _all, _close }) b.Padding = new Thickness(8, 0);
        Grid.SetRow(second, 1);
        Grid.SetColumnSpan(second, 2);
        _grid.Children.Add(second);
    }
}
