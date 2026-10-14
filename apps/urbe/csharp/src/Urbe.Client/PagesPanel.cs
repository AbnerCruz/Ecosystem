using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;

namespace Urbe.Client;

/// <summary>One page of the vault as Páginas lists it: its file, title, icon and number of sections.</summary>
public sealed record PageCard(string Path, string Title, string Icon, int Sections);

/// <summary>
/// Páginas (studio.js home, "Sites e páginas HTML feitos a partir do vault"): every .page.json of the vault, the most
/// recently changed first, each a card with its icon, title, "path · n seções" and "⋯". Tapping a card shows the page
/// inside the Urbe (ADD-0023). The studio that creates and edits pages is UC-35.
/// </summary>
public sealed class PagesPanel : Border
{
    private readonly StackPanel _list = new() { Spacing = 10, Margin = new Thickness(16, 4, 16, 24) };

    public PagesPanel()
    {
        IsVisible = false;
        Background = UrbeTheme.Brush(UrbeTheme.Bg);
        var close = new Button
        {
            Content = UrbeTheme.Icon(UrbeTheme.Icons.Back, 22, UrbeTheme.Text2, 1.8), Width = 44, Height = 44, Padding = new Thickness(0), CornerRadius = new CornerRadius(12),
            Background = Brushes.Transparent, BorderThickness = new Thickness(0), HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center
        };
        ToolTip.SetTip(close, "Fechar");
        close.Click += (_, _) => Close();
        var titles = new StackPanel
        {
            Margin = new Thickness(8, 0), VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                new TextBlock { Text = "Páginas", FontSize = 20, FontWeight = FontWeight.Bold, Foreground = UrbeTheme.Brush(UrbeTheme.Text) },
                new TextBlock { Text = "Sites e páginas HTML feitos a partir do vault", FontSize = 13, Foreground = UrbeTheme.Brush(UrbeTheme.Text3), TextTrimming = TextTrimming.CharacterEllipsis }
            }
        };
        var head = new DockPanel { Margin = new Thickness(8, 6, 8, 8), Height = 60 };
        DockPanel.SetDock(close, Dock.Left);
        head.Children.Add(close);
        head.Children.Add(titles);
        var scroll = new ScrollViewer { HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = _list };
        var layout = new DockPanel();
        DockPanel.SetDock(head, Dock.Top);
        layout.Children.Add(head);
        layout.Children.Add(scroll);
        Child = layout;
    }

    /// <summary>A card was tapped: the host shows the page.</summary>
    public event Action<string>? PageRequested;

    /// <summary>"⋯" of a card: the host shows the page's actions.</summary>
    public event Action<string>? MenuRequested;

    /// <summary>The pages listed now (tests).</summary>
    public IReadOnlyList<PageCard> Listed { get; private set; } = [];

    public void Show(IReadOnlyList<PageCard> pages)
    {
        Listed = pages;
        _list.Children.Clear();
        if (pages.Count == 0)
        {
            _list.Children.Add(new StackPanel
            {
                Margin = new Thickness(8, 48, 8, 0), Spacing = 10, HorizontalAlignment = HorizontalAlignment.Center,
                Children =
                {
                    new TextBlock { Text = "✦", FontSize = 44, HorizontalAlignment = HorizontalAlignment.Center, Foreground = UrbeTheme.Brush(UrbeTheme.Accent) },
                    new TextBlock { Text = "Nenhuma página ainda", FontSize = 18, FontWeight = FontWeight.Bold, HorizontalAlignment = HorizontalAlignment.Center, Foreground = UrbeTheme.Brush(UrbeTheme.Text) },
                    new TextBlock
                    {
                        Text = "Sites, portfólios, currículos, livros… As páginas (.page.json) do vault aparecem aqui e abrem dentro do Urbe.",
                        TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, MaxWidth = 420, Foreground = UrbeTheme.Brush(UrbeTheme.Text3)
                    }
                }
            });
        }
        else
        {
            _list.Children.Add(new TextBlock { Text = "Suas páginas · " + pages.Count, FontSize = 14, FontWeight = FontWeight.SemiBold, Margin = new Thickness(2, 4, 0, 2), Foreground = UrbeTheme.Brush(UrbeTheme.Text2) });
            foreach (var p in pages) _list.Children.Add(Card(p));
        }
        IsVisible = true;
    }

    public void Close() => IsVisible = false;

    private Control Card(PageCard p)
    {
        var icon = new Border
        {
            Width = 52, Height = 52, CornerRadius = new CornerRadius(14), Background = UrbeTheme.Brush(UrbeTheme.Accent, .16),
            Child = new TextBlock { Text = p.Icon, FontSize = 24, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Foreground = UrbeTheme.Brush(UrbeTheme.Text) }
        };
        var text = new StackPanel
        {
            Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, Spacing = 2,
            Children =
            {
                new TextBlock { Text = p.Title, FontSize = 16, FontWeight = FontWeight.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis, Foreground = UrbeTheme.Brush(UrbeTheme.Text) },
                new TextBlock { Text = p.Path + " · " + p.Sections + (p.Sections == 1 ? " seção" : " seções"), FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis, Foreground = UrbeTheme.Brush(UrbeTheme.Text3) }
            }
        };
        var row = new DockPanel();
        DockPanel.SetDock(icon, Dock.Left);
        row.Children.Add(icon);
        row.Children.Add(text);
        var open = new Button
        {
            Content = row, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(12, 10),
            CornerRadius = new CornerRadius(16), Background = UrbeTheme.Brush(UrbeTheme.Surface), BorderBrush = UrbeTheme.Brush(UrbeTheme.Line2), BorderThickness = new Thickness(1)
        };
        Avalonia.Automation.AutomationProperties.SetName(open, p.Title);
        open.Click += (_, _) => PageRequested?.Invoke(p.Path);
        var more = new Button
        {
            Content = UrbeTheme.Icon(UrbeTheme.Icons.More, 20, UrbeTheme.Text2), Width = 40, Height = 40, Padding = new Thickness(0), CornerRadius = new CornerRadius(20),
            Background = Brushes.Transparent, Margin = new Thickness(6, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center,
            HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center
        };
        ToolTip.SetTip(more, "Opções");
        more.Click += (_, _) => MenuRequested?.Invoke(p.Path);
        var line = new DockPanel();
        DockPanel.SetDock(more, Dock.Right);
        line.Children.Add(more);
        line.Children.Add(open);
        return line;
    }
}
