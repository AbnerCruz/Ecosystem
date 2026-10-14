using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;

namespace Urbe.Client;

/// <summary>
/// The split pane of the editor (editor/split-ui.js #urbeSplitPane): another note beside the one being edited, read
/// only, with its title, ⇄ (Trocar painéis) and × (Fechar). Its text is the 1.8.4's small Markdown: headings, **bold**
/// and [[links]] marked, line breaks kept.
/// </summary>
public sealed class SplitPane : Border
{
    private readonly TextBlock _title = new() { FontSize = 14, FontWeight = FontWeight.Bold, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
    private readonly SelectableTextBlock _article = new() { TextWrapping = TextWrapping.Wrap, FontSize = 15, LineHeight = 22 };

    public SplitPane()
    {
        IsVisible = false;
        Background = UrbeTheme.Brush(UrbeTheme.Surface);
        BorderBrush = UrbeTheme.Brush(UrbeTheme.Line);
        BorderThickness = new Thickness(1, 0, 0, 0);
        _title.Foreground = UrbeTheme.Brush(UrbeTheme.Text);
        _article.Foreground = UrbeTheme.Brush(UrbeTheme.Text);
        var swap = HeadButton("⇄", "Trocar painéis");
        swap.Click += (_, _) => SwapRequested?.Invoke();
        var close = HeadButton("×", "Fechar");
        close.Click += (_, _) => CloseRequested?.Invoke();
        var head = new DockPanel { Margin = new Thickness(14, 8, 8, 8) };
        DockPanel.SetDock(close, Dock.Right);
        DockPanel.SetDock(swap, Dock.Right);
        head.Children.Add(close);
        head.Children.Add(swap);
        head.Children.Add(_title);
        var scroll = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = new Border { Padding = new Thickness(16, 4, 16, 32), Child = _article }
        };
        var layout = new DockPanel();
        DockPanel.SetDock(head, Dock.Top);
        layout.Children.Add(head);
        layout.Children.Add(scroll);
        Child = layout;
    }

    /// <summary>⇄</summary>
    public event Action? SwapRequested;

    /// <summary>×</summary>
    public event Action? CloseRequested;

    /// <summary>The note shown (tests).</summary>
    public string? Path { get; private set; }

    /// <summary>The text shown, without marks (tests).</summary>
    public string ShownText => string.Concat((_article.Inlines ?? []).Select(i => i is Run r ? r.Text : i is LineBreak ? "\n" : ""));

    private static Button HeadButton(string text, string tip)
    {
        var b = new Button
        {
            Content = text, Width = 36, Height = 36, Padding = new Thickness(0), CornerRadius = new CornerRadius(10), FontSize = 16,
            Background = Brushes.Transparent, Foreground = UrbeTheme.Brush(UrbeTheme.Text2),
            HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center
        };
        ToolTip.SetTip(b, tip);
        return b;
    }

    private static readonly Regex Heading = new(@"^(#{1,6})\s+(.+)$");
    private static readonly Regex Marks = new(@"\*\*(.+?)\*\*|\[\[([^\]]+)\]\]");

    /// <summary>render(): the note's title and its text in the small Markdown of split-ui.js.</summary>
    public void Show(string path, string title, string content)
    {
        Path = path;
        _title.Text = title;
        var inlines = new InlineCollection();
        var lines = content.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (Heading.Match(line) is { Success: true } h)
            {
                double size = h.Groups[1].Length switch { 1 => 24, 2 => 20, 3 => 18, 4 => 16, _ => 15 };
                AddMarked(inlines, h.Groups[2].Value, size, FontWeight.Bold);
            }
            else AddMarked(inlines, line, null, FontWeight.Normal);
            if (i < lines.Length - 1) inlines.Add(new LineBreak());
        }
        _article.Inlines = inlines;
        IsVisible = true;
    }

    private static void AddMarked(InlineCollection inlines, string text, double? size, FontWeight weight)
    {
        int at = 0;
        foreach (Match m in Marks.Matches(text))
        {
            if (m.Index > at) inlines.Add(Run(text[at..m.Index], size, weight));
            if (m.Groups[1].Success) inlines.Add(Run(m.Groups[1].Value, size, FontWeight.Bold));
            else
            {
                var wiki = Run("[[" + m.Groups[2].Value + "]]", size, weight);
                wiki.Foreground = UrbeTheme.Brush(UrbeTheme.Accent);
                inlines.Add(wiki);
            }
            at = m.Index + m.Length;
        }
        if (at < text.Length) inlines.Add(Run(text[at..], size, weight));
    }

    private static Run Run(string text, double? size, FontWeight weight)
    {
        var r = new Run(text) { FontWeight = weight };
        if (size is { } s) r.FontSize = s;
        return r;
    }

    public void Hide()
    {
        IsVisible = false;
        Path = null;
    }
}
