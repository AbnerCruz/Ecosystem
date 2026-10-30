using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace Urbe.Client;

/// <summary>A colour in a small flyout: hex field and three sliders, with live change callbacks (the native counterpart of input[type=color]).</summary>
public static class ColorEditor
{
    public static string Hex(Color c) => "#" + c.R.ToString("x2") + c.G.ToString("x2") + c.B.ToString("x2");

    public static Control Build(string hex, Action<string> changed)
    {
        var color = Color.Parse(hex);
        byte[] ch = [color.R, color.G, color.B];
        var box = new TextBox { Text = Hex(color), FontFamily = UrbeTheme.MonoFont, FontSize = 14, MinHeight = 36, Width = 120, MaxLength = 7 };
        var sliders = new Slider[3];
        bool busy = false;
        void Push()
        {
            var h = Hex(Color.FromRgb(ch[0], ch[1], ch[2]));
            busy = true; box.Text = h; busy = false;
            changed(h);
        }
        var stack = new StackPanel { Spacing = 8, Width = 240 };
        var swatch = new Border { Height = 28, CornerRadius = new CornerRadius(8), BorderBrush = UrbeTheme.Brush(UrbeTheme.Line2), BorderThickness = new Thickness(1) };
        stack.Children.Add(swatch);
        string[] names = ["R", "G", "B"];
        for (int i = 0; i < 3; i++)
        {
            int k = i;
            var s = new Slider { Minimum = 0, Maximum = 255, Value = ch[k], SmallChange = 1, LargeChange = 16 };
            sliders[k] = s;
            s.ValueChanged += (_, e) =>
            {
                if (busy) return;
                ch[k] = (byte)Math.Round(e.NewValue);
                swatch.Background = new SolidColorBrush(Color.FromRgb(ch[0], ch[1], ch[2]));
                Push();
            };
            var row = new DockPanel();
            var label = new TextBlock { Text = names[k], Width = 18, FontFamily = UrbeTheme.UiFont, FontSize = 12, Foreground = UrbeTheme.Brush(UrbeTheme.Text2), VerticalAlignment = VerticalAlignment.Center };
            DockPanel.SetDock(label, Dock.Left);
            row.Children.Add(label);
            row.Children.Add(s);
            stack.Children.Add(row);
        }
        swatch.Background = new SolidColorBrush(color);
        box.TextChanged += (_, _) =>
        {
            if (busy || !Color.TryParse(box.Text, out var parsed) || (box.Text?.Length ?? 0) != 7) return;
            busy = true;
            ch[0] = parsed.R; ch[1] = parsed.G; ch[2] = parsed.B;
            for (int i = 0; i < 3; i++) sliders[i].Value = ch[i];
            swatch.Background = new SolidColorBrush(parsed);
            busy = false;
            changed(Hex(parsed));
        };
        stack.Children.Add(box);
        return stack;
    }

    /// <summary>A swatch button that opens the editor; <paramref name="commit"/> runs once, when the flyout closes with a new colour.</summary>
    public static Button Button(string hex, string name, Action<string> preview, Action<string> commit, double width = 48, double height = 40, double radius = 8)
    {
        var swatch = new Border { Background = new SolidColorBrush(Color.Parse(hex)), CornerRadius = new CornerRadius(Math.Max(0, radius - 3)), Margin = new Thickness(3) };
        string current = hex;
        var b = new Button
        {
            Content = swatch, Width = width, Height = height, Padding = new Thickness(0), CornerRadius = new CornerRadius(radius), Background = Brushes.Transparent,
            BorderBrush = UrbeTheme.Brush(UrbeTheme.Line2), BorderThickness = new Thickness(1), HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch
        };
        Avalonia.Automation.AutomationProperties.SetName(b, name);
        var flyout = new Flyout { Placement = PlacementMode.BottomEdgeAlignedRight };
        flyout.Opening += (_, _) => flyout.Content = new Border { Padding = new Thickness(6), Child = Build(current, h => { current = h; swatch.Background = new SolidColorBrush(Color.Parse(h)); preview(h); }) };
        flyout.Closed += (_, _) => { if (!string.Equals(current, hex, StringComparison.OrdinalIgnoreCase)) commit(current); };
        b.Flyout = flyout;
        return b;
    }
}
