using Avalonia.Controls;

using Avalonia.Media;

namespace Urbe.Client;

/// <summary>Design tokens of the 1.8.4-beta interface (src/styles, --ui-*), as native brushes.</summary>
public static class UrbeTheme
{
    public const string Bg = "#0e0f11";
    public const string Surface = "#15161a";
    public const string Surface2 = "#1b1d22";
    public const string Surface3 = "#24262c";
    public const string Line = "#25272d";
    public const string Line2 = "#33363e";
    public const string Text = "#ececf0";
    public const string Text2 = "#a3a6af";
    public const string Text3 = "#6d7079";
    public const string Accent = "#8fb3ff";
    public const string AccentInk = "#0b1325";
    public const string Ok = "#7fdca6";
    public const string Danger = "#ff7b86";
    public const double EditorSize = 17;

    private static readonly Dictionary<string, IBrush> Brushes = new(StringComparer.Ordinal);

    public static IBrush Brush(string hex)
    {
        if (!Brushes.TryGetValue(hex, out var brush))
            Brushes[hex] = brush = new SolidColorBrush(Color.Parse(hex)).ToImmutable();
        return brush;
    }

    public static IBrush Brush(string hex, double opacity) =>
        new SolidColorBrush(Color.Parse(hex), opacity).ToImmutable();

    // --ui-font: system UI sans-serif (Segoe UI on Windows, Roboto on Android).
    public static FontFamily UiFont { get; } = new("Segoe UI, Roboto, Inter, Helvetica Neue, Arial, sans-serif");
    public static FontFamily MonoFont { get; } = new("Cascadia Mono, Consolas, JetBrains Mono, DejaVu Sans Mono, monospace");

    public static Typeface UiTypeface(FontWeight weight = FontWeight.Normal) => new(UiFont, FontStyle.Normal, weight);

    /// <summary>Line icons in the style of the 1.8.4 set (24×24, stroke 2).</summary>
    public static Control Icon(string data, double size = 20, string color = Text)
    {
        return new Viewbox
        {
            Width = size,
            Height = size,
            Child = new Avalonia.Controls.Shapes.Path
            {
                Data = Geometry.Parse(data),
                Stroke = Brush(color),
                StrokeThickness = 2,
                StrokeLineCap = PenLineCap.Round,
                StrokeJoin = PenLineJoin.Round,
                // region: <rect stroke-dasharray="3 2.6"> (Avalonia dashes are in stroke widths)
                StrokeDashArray = data == Icons.Region ? [1.5, 1.3] : null,
                Width = 24,
                Height = 24,
                Stretch = Stretch.None
            }
        };
    }

    public static class Icons
    {
        public const string City = "M3 21h18 M5 21V8l7-4 7 4v13 M9 21v-5h6v5 M9 11h.01 M15 11h.01";
        public const string Notes = "M14 3H6a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V9z M14 3v6h6 M8 13h8 M8 17h6";
        public const string Assistant = "M12 3l1.8 4.7L18.5 9.5l-4.7 1.8L12 16l-1.8-4.7L5.5 9.5l4.7-1.8z M19 15l.8 2.2 2.2.8-2.2.8L19 21l-.8-2.2-2.2-.8 2.2-.8z";
        public const string Search = "M11 4a7 7 0 1 0 0 14a7 7 0 1 0 0-14z M20 20l-4-4";
        public const string Map = "M3 6l6-3 6 3 6-3v15l-6 3-6-3-6 3z M9 3v15 M15 6v15";
        public const string Sliders = "M4 7h10 M18 7h2 M4 17h4 M12 17h8 M14 5v4 M8 15v4";
        public const string Plus = "M12 5v14 M5 12h14";
        public const string Back = "M15 18l-6-6 6-6";
        public const string Close = "M6 6l12 12 M18 6L6 18";
        public const string More = "M5 12h.01 M12 12h.01 M19 12h.01";
        // ui/icons.js (1.8.4): plus, folder, region (dashed), house, upload, file
        public const string Folder = "M3.5 7.5a2 2 0 0 1 2-2h3.8l2 2h7.2a2 2 0 0 1 2 2v7.5a2 2 0 0 1-2 2h-13a2 2 0 0 1-2-2z";
        public const string Region = "M6 4h12a2 2 0 0 1 2 2v12a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2z";
        public const string House = "M4 11l8-6.5 8 6.5 M6 9.5v10h12v-10 M10 19.5v-5h4v5";
        public const string Upload = "M12 15.5V4.5M7.5 9l4.5-4.5L16.5 9 M4.5 15v3a2 2 0 0 0 2 2h11a2 2 0 0 0 2-2v-3";
        public const string File = "M14 3.5H7.5a2 2 0 0 0-2 2v13a2 2 0 0 0 2 2h9a2 2 0 0 0 2-2V8z M14 3.5V8h4.5";
        public const string Pencil = "M4 20h4L19 9l-4-4L4 16z M14 6l4 4";
    }
}
