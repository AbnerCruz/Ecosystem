using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Urbe.Client.Ui;

/// <summary>
/// The app's text field: a soft filled pill (the Área de trabalho's search), not Fluent's outlined box. With an icon,
/// the icon sits inside on the left. Used by the readers, the study browser and the panels, so every field looks alike.
/// </summary>
public static class SoftField
{
    public static TextBox Style(TextBox box, bool withIcon = false)
    {
        box.MinHeight = 40;
        box.FontSize = 15;
        box.CornerRadius = new CornerRadius(14);
        box.Padding = new Thickness(withIcon ? 40 : 14, 8, 12, 8);
        box.VerticalContentAlignment = VerticalAlignment.Center;
        box.Background = UrbeTheme.Brush(UrbeTheme.Surface2);
        box.BorderBrush = UrbeTheme.Brush(UrbeTheme.Line2);
        box.BorderThickness = new Thickness(1);
        box.Resources["TextControlBackgroundPointerOver"] = UrbeTheme.Brush(UrbeTheme.Surface2);
        box.Resources["TextControlBackgroundFocused"] = UrbeTheme.Brush(UrbeTheme.Surface2);
        box.Resources["TextControlBorderBrushPointerOver"] = UrbeTheme.Brush(UrbeTheme.Line2);
        box.Resources["TextControlBorderBrushFocused"] = UrbeTheme.Brush(UrbeTheme.Accent, .7);
        return box;
    }

    /// <summary>The field with <paramref name="icon"/> inside on the left (search, address).</summary>
    public static Control WithIcon(TextBox box, string icon)
    {
        Style(box, withIcon: true);
        var glyph = UrbeTheme.Icon(icon, 18, UrbeTheme.Text3, 1.8);
        glyph.Margin = new Thickness(13, 0, 0, 0);
        glyph.HorizontalAlignment = HorizontalAlignment.Left;
        glyph.VerticalAlignment = VerticalAlignment.Center;
        glyph.IsHitTestVisible = false;
        return new Grid { Children = { box, glyph } };
    }
}
