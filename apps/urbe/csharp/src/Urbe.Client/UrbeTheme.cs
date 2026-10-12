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
    public const string Warn = "#f1c46e";
    public const string Code = "#f0d9a8";
    public const double EditorSize = 17;

    private static readonly Dictionary<string, IBrush> Brushes = new(StringComparer.Ordinal);

    /// <summary>
    /// Personalização: the design tokens above are the default theme. A token colour is drawn with a
    /// shared mutable brush, so changing the theme (<see cref="SetColors"/>) repaints every control
    /// that already uses it, like the --ui-* CSS variables did.
    /// </summary>
    private static readonly Dictionary<string, string> TokenKey = new(StringComparer.OrdinalIgnoreCase)
    {
        [Bg] = "fundo", [Surface] = "superficie", [Surface2] = "superficie2", [Surface3] = "superficie3", [Line] = "linha", [Line2] = "linha2",
        [Text] = "texto", [Text2] = "texto2", [Text3] = "texto3", [Accent] = "destaque", [Ok] = "sucesso", [Danger] = "perigo", [Warn] = "aviso", [Code] = "codigo", [AccentInk] = "destaque-tinta"
    };
    private static readonly Dictionary<string, string> CurrentColor = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<(string, double), SolidColorBrush> LiveBrushes = [];

    private static string Resolve(string hex) => CurrentColor.TryGetValue(hex, out var now) ? now : hex;

    public static IBrush Brush(string hex)
    {
        if (TokenKey.ContainsKey(hex)) return Live(hex, 1);
        if (!Brushes.TryGetValue(hex, out var brush))
            Brushes[hex] = brush = new SolidColorBrush(Color.Parse(hex)).ToImmutable();
        return brush;
    }

    public static IBrush Brush(string hex, double opacity) =>
        TokenKey.ContainsKey(hex) ? Live(hex, opacity) : new SolidColorBrush(Color.Parse(hex), opacity).ToImmutable();

    private static SolidColorBrush Live(string hex, double opacity)
    {
        var key = (hex.ToLowerInvariant(), opacity);
        if (!LiveBrushes.TryGetValue(key, out var brush))
            LiveBrushes[key] = brush = new SolidColorBrush(Color.Parse(Resolve(hex)), opacity);
        return brush;
    }

    /// <summary>The colour a token has now (default token hex in, current hex out).</summary>
    public static string Current(string tokenHex) => Resolve(tokenHex);

    /// <summary>Accent at 14 % (--ui-accent-soft), a live brush.</summary>
    public static IBrush AccentSoft => Live(Accent, .14);

    /// <summary>
    /// Applies a palette (configuration colour name → hex; "destaque-tinta" is --ui-accent-ink) to the
    /// shared token brushes. An empty map restores the default theme.
    /// </summary>
    public static void SetColors(IReadOnlyDictionary<string, string> colors)
    {
        foreach (var (token, key) in TokenKey)
        {
            if (colors.TryGetValue(key, out var hex)) CurrentColor[token] = hex; else CurrentColor.Remove(token);
        }
        foreach (var ((hex, opacity), brush) in LiveBrushes) brush.Color = Color.Parse(Resolve(hex));
    }

    // --ui-font: system UI sans-serif (Segoe UI on Windows, Roboto on Android).
    public static FontFamily UiFont { get; private set; } = new("Segoe UI, Roboto, Inter, Helvetica Neue, Arial, sans-serif");
    public static FontFamily MonoFont { get; } = new("Cascadia Mono, Consolas, JetBrains Mono, DejaVu Sans Mono, monospace");
    /// <summary>--ui-editor-font: the font of the notes in the Visual editor.</summary>
    public static FontFamily EditorFont { get; private set; } = UiFont;
    public static void SetFonts(FontFamily ui, FontFamily editor) { UiFont = ui; EditorFont = editor; }

    public static Typeface UiTypeface(FontWeight weight = FontWeight.Normal) => new(UiFont, FontStyle.Normal, weight);

    /// <summary>Line icons in the style of the 1.8.4 set (24×24, stroke 2).</summary>
    public static Control Icon(string data, double size = 20, string color = Text, double stroke = 2)
    {
        return new Viewbox
        {
            Width = size,
            Height = size,
            Child = new Avalonia.Controls.Shapes.Path
            {
                Data = Geometry.Parse(data),
                Stroke = Brush(color),
                StrokeThickness = stroke,
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
        // City, Notes and Assistant (sparkle): ui/icons.js of the 1.8.4, exactly
        public const string City = "M3 21h18 M5 21V9l5-3v15 M10 21V4l9 4v13 M13 10h3M13 14h3M13 18h3";
        public const string Notes = "M14 3H7a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2V8z M14 3v5h5 M9 13h6M9 17h4";
        public const string Assistant = "M12 3.5l1.9 5 5.1 1.9-5.1 1.9-1.9 5-1.9-5L5 10.4l5.1-1.9z M18.5 15.5l.7 1.8 1.8.7-1.8.7-.7 1.8-.7-1.8-1.8-.7 1.8-.7z";
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
        public const string Open = "M14 4.5h5.5V10 M19.5 4.5L11 13 M18 14v4a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h4";
        public const string Star = "M12 4l2.4 5 5.3.6-3.9 3.6 1.1 5.3L12 15.9l-4.9 2.6 1.1-5.3-3.9-3.6 5.3-.6z";
        public const string Move = "M4.5 12h15M15 7.5l4.5 4.5-4.5 4.5";
        public const string Copy = "M10.5 8.5h7a2 2 0 0 1 2 2v7a2 2 0 0 1-2 2h-7a2 2 0 0 1-2-2v-7a2 2 0 0 1 2-2z M15.5 8.5V6.5a2 2 0 0 0-2-2h-7a2 2 0 0 0-2 2v7a2 2 0 0 0 2 2h2";
        public const string Clock = "M12 4a8 8 0 1 0 0 16a8 8 0 1 0 0-16z M12 7.5V12l3 2";
        public const string Trash = "M4.5 7h15 M9.5 7V4.5h5V7 M6.5 7l.9 12.5h9.2L17.5 7 M10.2 11v5.5M13.8 11v5.5";
        // ui/icons.js (1.8.4), exactly, for Notas (mobile-ui.js): chevron, page, restore, layers, search, close, more
        public const string Chevron = "M9.5 6l6 6-6 6";
        public const string Page = "M6.5 3.5h11a3 3 0 0 1 3 3v11a3 3 0 0 1-3 3h-11a3 3 0 0 1-3-3v-11a3 3 0 0 1 3-3z M3.5 9h17 M9 20.5V9";
        public const string Restore = "M4.5 12a7.5 7.5 0 1 0 2.2-5.3L4.5 9 M4.5 4.5V9H9";
        public const string Layers = "M12 3.5l8.5 4.5-8.5 4.5L3.5 8z M3.5 12.5l8.5 4.5 8.5-4.5";
        public const string SearchUi = "M11 4.5a6.5 6.5 0 1 0 0 13a6.5 6.5 0 1 0 0-13z M20 20l-4.2-4.2";
        public const string CloseUi = "M6.5 6.5l11 11M17.5 6.5l-11 11";
        public const string MoreUi = "M5.5 10.7a1.3 1.3 0 1 0 0 2.6a1.3 1.3 0 1 0 0-2.6z M12 10.7a1.3 1.3 0 1 0 0 2.6a1.3 1.3 0 1 0 0-2.6z M18.5 10.7a1.3 1.3 0 1 0 0 2.6a1.3 1.3 0 1 0 0-2.6z";
    }
}
