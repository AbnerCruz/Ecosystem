using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Urbe.Core;

namespace Urbe.Client;

/// <summary>
/// UC-59 (UX P10, P2): opening the app lands on the last note at once; the city is built behind it. While it loads a
/// small pill says so, over everything but never in the way (no touch, no dialog), and the actions that need the
/// city wait for it with a short word instead of failing silently.
/// </summary>
public sealed partial class MainView
{
    private Border? _worldPill;

    /// <summary>The text of the loading pill while it shows, or null (tests).</summary>
    public string? WorldLoadingText => WorldLoading && _worldPill?.Child is StackPanel { Children: [_, TextBlock t] } ? t.Text : null;

    private Control WorldLoadingPill()
    {
        var dot = new Border
        {
            Width = 7, Height = 7, CornerRadius = new CornerRadius(4), VerticalAlignment = VerticalAlignment.Center,
            Background = UrbeTheme.Brush(UrbeTheme.Accent)
        };
        var text = new TextBlock
        {
            Text = "Carregando a cidade…", FontFamily = UrbeTheme.UiFont, FontSize = 12,
            Foreground = UrbeTheme.Brush(UrbeTheme.Text2), VerticalAlignment = VerticalAlignment.Center
        };
        _worldPill = new Border
        {
            Child = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { dot, text } },
            Background = UrbeTheme.Brush(UrbeTheme.Surface, .78),
            BorderBrush = UrbeTheme.Brush("#ffffff", .06), BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(999), Padding = new Thickness(12, 6),
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 0, 84),
            IsHitTestVisible = false, Opacity = 0, ZIndex = 5,
            Transitions = [new DoubleTransition { Property = OpacityProperty, Duration = TimeSpan.FromSeconds(.4) }]
        };
        Avalonia.Automation.AutomationProperties.SetName(_worldPill, "Carregando a cidade");
        return _worldPill;
    }

    private void ShowWorldLoading(bool on)
    {
        if (_worldPill is not null) _worldPill.Opacity = on ? 1 : 0;
    }

    /// <summary>The city is still being built: says so in the editor and returns true (the action waits for it).</summary>
    private bool CityNotReady()
    {
        if (!WorldLoading) return false;
        if (Editor.IsVisible) Editor.Notify("A cidade ainda está carregando; tente de novo em instantes.");
        else ShowLifeNotice("A cidade ainda está carregando…");
        return true;
    }

    /// <summary>Completes once the city of the vault being opened is ready (tests, plugins).</summary>
    public async Task WhenWorldReadyAsync(CancellationToken cancellationToken = default)
    {
        while (WorldLoading) await Task.Delay(10, cancellationToken);
    }
}
