using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Urbe.Core;

namespace Urbe.Client;

/// <summary>
/// "Fixar em painel" (owner, DEC-0045, UC-39): a stretch of text kept at the top of the editor, with its own scroll,
/// while the note below goes on scrolling and being written. Writing on the phone with something else as reference
/// meant going up and down the same note. It stays when another note opens, so a passage of one note can be the
/// reference for writing another. Nothing is written to the vault: it is only the screen.
/// </summary>
public sealed class PinnedPanel : Border
{
    private readonly TextBlock _from = new()
    {
        FontSize = 12,
        FontWeight = FontWeight.SemiBold,
        Foreground = UrbeTheme.Brush(UrbeTheme.Text3),
        TextTrimming = TextTrimming.CharacterEllipsis,
        VerticalAlignment = VerticalAlignment.Center
    };
    private readonly ScrollViewer _scroll = new()
    {
        HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
        VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto
    };

    public PinnedPanel()
    {
        IsVisible = false;
        Background = UrbeTheme.Brush(UrbeTheme.Surface);
        BorderBrush = UrbeTheme.Brush(UrbeTheme.Accent, .35);
        BorderThickness = new Thickness(0, 0, 0, 2);
        Padding = new Thickness(16, 8, 8, 10);
        var close = new Button
        {
            Content = UrbeTheme.Icon(UrbeTheme.Icons.CloseUi, 16, UrbeTheme.Text2, 1.8),
            Width = 32, Height = 32, Padding = new Thickness(0), CornerRadius = new CornerRadius(8),
            Background = Brushes.Transparent, BorderThickness = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center
        };
        ToolTip.SetTip(close, "Desafixar");
        close.Click += (_, _) => Unpin();
        var pin = UrbeTheme.Icon(UrbeTheme.Icons.Pin, 15, UrbeTheme.Accent, 1.8);
        pin.VerticalAlignment = VerticalAlignment.Center;
        var head = new DockPanel { Margin = new Thickness(0, 0, 0, 4) };
        DockPanel.SetDock(close, Dock.Right);
        DockPanel.SetDock(pin, Dock.Left);
        head.Children.Add(close);
        head.Children.Add(pin);
        _from.Margin = new Thickness(6, 0);
        head.Children.Add(_from);
        _scroll.Padding = new Thickness(0, 0, 8, 0);
        var grid = new Grid { RowDefinitions = new RowDefinitions("Auto,*") };
        grid.Children.Add(head);
        Grid.SetRow(_scroll, 1);
        grid.Children.Add(_scroll);
        Child = grid;
    }

    /// <summary>The pinned Markdown, or null when nothing is pinned.</summary>
    public string? Text { get; private set; }

    /// <summary>The note the pinned text came from.</summary>
    public string? SourcePath { get; private set; }

    public void Pin(string markdown, string sourcePath)
    {
        Text = markdown;
        SourcePath = sourcePath;
        _from.Text = "Fixado de " + System.IO.Path.GetFileName(sourcePath);
        _scroll.Content = new MarkdownReader().Build(NoteMarks.Parse(markdown));
        _scroll.Offset = default;
        IsVisible = true;
    }

    public void Unpin()
    {
        Text = SourcePath = null;
        _scroll.Content = null;
        IsVisible = false;
    }
}
