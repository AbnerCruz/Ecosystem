using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using C = Urbe.Client.UrbeTokens.Color;

namespace Urbe.Client.Ui;

/// <summary>The kinds of <see cref="UrbeButton"/>.</summary>
public enum ButtonKind { Primary, Secondary, Ghost, Danger }

/// <summary>
/// The button of the visual system (UX.md P1, P4, P9): at least 48 dp, the type scale's Button style, the semantic
/// colours of its kind (also when hovered or pressed: Fluent's greys never show through), the pressed feedback and a
/// haptic tick. The text is its accessible name.
/// </summary>
public class UrbeButton : Button
{
    protected override System.Type StyleKeyOverride => typeof(Button);

    public ButtonKind Kind { get; }

    public UrbeButton(string text, ButtonKind kind = ButtonKind.Secondary, string? icon = null)
    {
        Kind = kind;
        MinHeight = UrbeTokens.MinTouch;
        MinWidth = UrbeTokens.MinTouch;
        Padding = new Thickness(18, 0);
        CornerRadius = new CornerRadius(UrbeTokens.Radius.M);
        HorizontalContentAlignment = HorizontalAlignment.Center;
        VerticalContentAlignment = VerticalAlignment.Center;
        FontFamily = UrbeTheme.UiFont;
        FontSize = UrbeTokens.Type.Button.Size;
        FontWeight = UrbeTokens.Type.Button.Weight;
        var (fill, ink, border) = kind switch
        {
            ButtonKind.Primary => (C.Primary, C.OnPrimary, (string?)null),
            ButtonKind.Danger => (C.Danger, C.OnDanger, null),
            ButtonKind.Ghost => ((string?)null, C.TextPrimary, null),
            _ => (C.SurfaceOverlay, C.TextPrimary, C.BorderStrong)
        };
        IBrush bg = fill is null ? Brushes.Transparent : UrbeTheme.Brush(fill);
        // hovered/pressed: the same fill a little lighter (ghost: a faint surface), never Fluent's own colour
        IBrush hover = fill is null ? UrbeTheme.Brush(C.SurfaceOverlay, .6) : UrbeTheme.Brush(fill, .88);
        IBrush down = fill is null ? UrbeTheme.Brush(C.SurfaceOverlay) : UrbeTheme.Brush(fill, .76);
        Background = bg;
        Foreground = UrbeTheme.Brush(ink);
        BorderBrush = border is null ? Brushes.Transparent : UrbeTheme.Brush(border);
        BorderThickness = new Thickness(border is null ? 0 : 1);
        Resources["ButtonBackgroundPointerOver"] = hover;
        Resources["ButtonBackgroundPressed"] = down;
        Resources["ButtonForegroundPointerOver"] = Foreground;
        Resources["ButtonForegroundPressed"] = Foreground;
        Resources["ButtonBorderBrushPointerOver"] = BorderBrush;
        Resources["ButtonBorderBrushPressed"] = BorderBrush;
        if (icon is null) Content = text;
        else
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal, Spacing = UrbeTokens.Space.Xs,
                Children = { UrbeTheme.Icon(icon, 20, ink, 1.8), new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center } }
            };
        AutomationProperties.SetName(this, text);
        UrbeMotion.Pressable(this, haptic: true);
    }
}

/// <summary>
/// A round or square icon button with a 48 dp touch area (P4) whatever the icon's size; the tip is its accessible name
/// and tooltip.
/// </summary>
public class UrbeIconButton : Button
{
    protected override System.Type StyleKeyOverride => typeof(Button);

    public UrbeIconButton(string icon, string tip, double iconSize = 22, string color = C.TextSecondary, bool round = false)
    {
        Width = UrbeTokens.MinTouch;
        Height = UrbeTokens.MinTouch;
        Padding = new Thickness(0);
        CornerRadius = new CornerRadius(round ? UrbeTokens.MinTouch / 2 : UrbeTokens.Radius.M);
        Background = Brushes.Transparent;
        BorderThickness = new Thickness(0);
        HorizontalContentAlignment = HorizontalAlignment.Center;
        VerticalContentAlignment = VerticalAlignment.Center;
        Content = UrbeTheme.Icon(icon, iconSize, color, 1.8);
        Resources["ButtonBackgroundPointerOver"] = UrbeTheme.Brush(C.SurfaceOverlay, .6);
        Resources["ButtonBackgroundPressed"] = UrbeTheme.Brush(C.SurfaceOverlay);
        ToolTip.SetTip(this, tip);
        AutomationProperties.SetName(this, tip);
        UrbeMotion.Pressable(this, haptic: true);
    }
}
