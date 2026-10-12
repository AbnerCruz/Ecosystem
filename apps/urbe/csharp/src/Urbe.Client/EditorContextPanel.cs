using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Urbe.Core;

namespace Urbe.Client;

/// <summary>
/// editor/chrome.js renderContext (.uec-context): "Contexto" with the note's Outline (its headings, indented by level;
/// a tap puts the caret at the line and closes the panel) and its Backlinks (the notes that link to it; a tap opens one).
/// 320 px at the right of the editor, the whole width minus 8 px on each side up to 600 px.
/// </summary>
public sealed class EditorContextPanel : Border
{
    private readonly StackPanel _body = new();

    public EditorContextPanel()
    {
        IsVisible = false;
        Margin = new Thickness(8);
        Background = UrbeTheme.Brush(UrbeTheme.Surface);
        BorderBrush = UrbeTheme.Brush(UrbeTheme.Line2);
        BorderThickness = new Thickness(1);
        CornerRadius = new CornerRadius(16);
        BoxShadow = BoxShadows.Parse("0 18 60 0 #8C000000");
        ClipToBounds = true;
        var close = new Button
        {
            Content = UrbeTheme.Icon(UrbeTheme.Icons.CloseUi, 20, UrbeTheme.Text),
            Background = Brushes.Transparent, BorderThickness = new Thickness(0), Padding = new Thickness(4)
        };
        ToolTip.SetTip(close, "Fechar");
        close.Click += (_, _) => IsVisible = false;
        var header = new DockPanel
        {
            Height = 42, Margin = new Thickness(12, 0),
            Children =
            {
                new TextBlock { Text = "Contexto", FontWeight = FontWeight.Bold, FontSize = 14, Foreground = UrbeTheme.Brush(UrbeTheme.Text), VerticalAlignment = VerticalAlignment.Center }
            }
        };
        DockPanel.SetDock(close, Dock.Right);
        header.Children.Insert(0, close);
        Child = new DockPanel
        {
            Children =
            {
                new Border { Child = header, BorderBrush = UrbeTheme.Brush(UrbeTheme.Line2), BorderThickness = new Thickness(0, 0, 0, 1), [DockPanel.DockProperty] = Dock.Top },
                new ScrollViewer { Content = new Border { Padding = new Thickness(8), Child = _body } }
            }
        };
    }

    /// <summary>A heading was chosen: its 1-based line.</summary>
    public event Action<int>? LineChosen;

    /// <summary>A backlink was chosen: the note's path.</summary>
    public event Action<string>? BacklinkChosen;

    /// <summary>The panel's width for the editor's width (min(320px, 88vw); left:8px under 600 px).</summary>
    public void Place(double width)
    {
        bool narrow = width <= 600;
        HorizontalAlignment = narrow ? HorizontalAlignment.Stretch : HorizontalAlignment.Right;
        Width = narrow ? double.NaN : Math.Min(320, width * .88);
    }

    /// <summary>renderContext: Outline and Backlinks of the note as it is now.</summary>
    public void Show(string content, IEnumerable<(string Title, string Path)> backlinks)
    {
        _body.Children.Clear();
        _body.Children.Add(Section("Outline", false));
        var outline = LegacyEditorContext.Outline(content);
        if (outline.Count == 0) _body.Children.Add(Empty("Sem títulos."));
        foreach (var h in outline)
        {
            int line = h.Line;
            _body.Children.Add(Item(h.Title, 10 + (h.Level - 1) * 12, () => { IsVisible = false; LineChosen?.Invoke(line); }));
        }
        _body.Children.Add(Section("Backlinks", true));
        var links = backlinks.ToList();
        if (links.Count == 0) _body.Children.Add(Empty("Sem backlinks."));
        foreach (var (title, path) in links)
            _body.Children.Add(Item(title, 10, () => BacklinkChosen?.Invoke(path)));
        IsVisible = true;
    }

    /// <summary>The titles listed (tests).</summary>
    public IEnumerable<string> Items => _body.Children.OfType<Button>().Select(b => (b.Content as TextBlock)?.Text ?? "");

    private static Control Section(string title, bool rule)
    {
        var h3 = new TextBlock
        {
            Text = title.ToUpperInvariant(), FontSize = 11, FontWeight = FontWeight.Bold, LetterSpacing = .88,
            Foreground = UrbeTheme.Brush(UrbeTheme.Text3), Margin = new Thickness(6, 4, 6, 8)
        };
        return rule
            ? new Border { BorderBrush = UrbeTheme.Brush(UrbeTheme.Line2), BorderThickness = new Thickness(0, 1, 0, 0), Margin = new Thickness(0, 10, 0, 0), Padding = new Thickness(0, 8, 0, 0), Child = h3 }
            : h3;
    }

    private static TextBlock Empty(string text) =>
        new() { Text = text, Margin = new Thickness(8), FontSize = 12, Foreground = UrbeTheme.Brush(UrbeTheme.Text2), TextWrapping = TextWrapping.Wrap };

    private static Button Item(string text, double left, Action run)
    {
        var b = new Button
        {
            Content = new TextBlock { Text = text, FontSize = 14, FontWeight = FontWeight.Medium, Foreground = UrbeTheme.Brush(UrbeTheme.Text), TextWrapping = TextWrapping.Wrap },
            HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left,
            Background = Brushes.Transparent, BorderThickness = new Thickness(0), CornerRadius = new CornerRadius(8),
            Padding = new Thickness(left, 8, 10, 8)
        };
        b.Click += (_, _) => run();
        return b;
    }
}
