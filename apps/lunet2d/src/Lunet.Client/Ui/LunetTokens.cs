using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace Lunet.Client.Ui;

/// <summary>
/// The visual system of the Lunet shell: the Ecosystem visual language v1.0 (docs/design/linguagem-visual.md,
/// DEC-0069 / ADD-0037). Values are copied from docs/design/tokens.json, the single authority (NN-001); Products do
/// not depend on each other (NN-002). Lunet's leading accent is violet; blue is information, links and selection.
/// Every text/background pair here meets WCAG AA and the APCA floors (LunetClientTests.Tokens_*).
/// </summary>
public static class LunetTokens
{
    /// <summary>The version of docs/design/tokens.json these values come from.</summary>
    public const string LanguageVersion = "1.0.0";

    /// <summary>The smallest touch target: 48 dp.</summary>
    public const double MinTouch = 48;

    /// <summary>The neutral "ink" scale (12 steps, LCH, slightly cool), as in tokens.json.</summary>
    public static class Ink
    {
        public const string I1 = "#0B0B0F", I2 = "#111116", I3 = "#17171E", I4 = "#1D1D26", I5 = "#24242F", I6 = "#2C2C38",
            I7 = "#383846", I8 = "#4A4A5C", I9 = "#6E6E82", I10 = "#9494A9", I11 = "#B4B4C6", I12 = "#EDEDF3";
    }

    public static class Color
    {
        /// <summary>ink 1: the app background (never pure black in the interface).</summary>
        public const string Background = Ink.I1;
        /// <summary>ink 2: bars, navigation, the keyboard deck.</summary>
        public const string BackgroundSubtle = Ink.I2;
        /// <summary>ink 3: fields, list rows, blocks.</summary>
        public const string Surface = Ink.I3;
        /// <summary>ink 4: raised surface and surface hover; tonal buttons.</summary>
        public const string SurfaceRaised = Ink.I4;
        /// <summary>ink 5: sheets, menus, dialogs, pressed.</summary>
        public const string SurfaceOverlay = Ink.I5;
        /// <summary>ink 6: dividers.</summary>
        public const string Border = Ink.I6;
        /// <summary>ink 7: a border where tone cannot separate.</summary>
        public const string BorderStrong = Ink.I7;
        public const string TextPrimary = Ink.I12;
        public const string TextSecondary = Ink.I11;
        public const string TextTertiary = Ink.I10;
        public const string TextDisabled = Ink.I9;
        /// <summary>violet 11: accent text, icons and indicators on dark.</summary>
        public const string Primary = "#A594FF";
        /// <summary>violet 9: the solid of the one main action (white text on it, 4.8:1).</summary>
        public const string Accent = "#6E56F8";
        public const string AccentHover = "#7A64FF";
        public const string OnAccent = "#FFFFFF";
        /// <summary>Kept for older callers: text on <see cref="Accent"/>.</summary>
        public const string OnPrimary = OnAccent;
        /// <summary>violet soft (16%): selected tab, selected chip, selected row.</summary>
        public const string PrimarySoft = "#296E56F8";
        /// <summary>blue: information, links, text selection.</summary>
        public const string Info = "#7CB3FF";
        public const string InfoSolid = "#2563EB";
        public const string InfoSoft = "#292563EB";
        public const string Selection = "#472563EB";
        public const string Success = "#4ADE80";
        public const string Warning = "#FBBF24";
        public const string Danger = "#FF6B7A";
        public const string DangerSoft = "#24FF6B7A";
        /// <summary>Hover and pressed overlays (white at 4% and 8%), the hairline (7%) and the sheet scrim.</summary>
        public const string Hover = "#0AFFFFFF";
        public const string Pressed = "#14FFFFFF";
        public const string Hairline = "#12FFFFFF";
        public const string Scrim = "#A3000000";
        /// <summary>The game surface letterbox (pure black only here).</summary>
        public const string Stage = "#000000";
        /// <summary>Keyboard: the deck behind the keys, letter keys, modifier keys, pressed key.</summary>
        public const string KeyboardDeck = Ink.I2;
        public const string Key = Ink.I4;
        public const string KeyModifier = Ink.I3;
        public const string KeyPressed = Ink.I6;
        /// <summary>Code text (monospace).</summary>
        public const string Code = "#E4E1F2";
    }

    public static class Space
    {
        public const double Xxs = 4, Xs = 8, S = 12, M = 16, L = 24, Xl = 32;
    }

    public static class Radius
    {
        public const double Xs = 6, S = 8, M = 10, L = 14, Xl = 20, Pill = 999;
    }

    public static class Type
    {
        public const double Caption = 12, Label = 13, BodySmall = 14, Callout = 15, Body = 16, TitleSmall = 17, Title = 20, Headline = 24, Display = 32, Code = 14;
    }

    /// <summary>Inter, embedded (Avalonia.Fonts.Inter, OFL); the system sans only if it cannot load.</summary>
    public static FontFamily UiFont { get; } = new("avares://Avalonia.Fonts.Inter/Assets#Inter, Segoe UI, Roboto, Helvetica Neue, Arial, sans-serif");
    /// <summary>JetBrains Mono, embedded (Assets/Fonts, OFL).</summary>
    public static FontFamily MonoFont { get; } = new("avares://Lunet.Client/Assets/Fonts#JetBrains Mono, Cascadia Mono, Consolas, DejaVu Sans Mono, monospace");

    /// <summary>Floating elements only (sheets, menus, dialogs, the floating button): shadow.overlay.</summary>
    public static BoxShadows Low { get; } = BoxShadows.Parse("0 12 32 0 #99000000, 0 2 6 0 #66000000");

    /// <summary>Motion durations (ms) and the press scale, from tokens.json.</summary>
    public static class Motion
    {
        public static readonly TimeSpan Instant = TimeSpan.FromMilliseconds(90), Fast = TimeSpan.FromMilliseconds(150),
            Base = TimeSpan.FromMilliseconds(220), Slow = TimeSpan.FromMilliseconds(300);
        public const double PressScale = 0.97;
    }

    private static readonly Dictionary<string, IImmutableSolidColorBrush> Brushes = new(StringComparer.Ordinal);

    /// <summary>A cached immutable brush for a token value.</summary>
    public static IImmutableSolidColorBrush Brush(string hex)
    {
        if (!Brushes.TryGetValue(hex, out var brush)) Brushes[hex] = brush = new ImmutableSolidColorBrush(Avalonia.Media.Color.Parse(hex));
        return brush;
    }

    /// <summary>WCAG 2.x contrast ratio between two opaque colours.</summary>
    public static double Contrast(string a, string b)
    {
        static double L(string hex)
        {
            var c = Avalonia.Media.Color.Parse(hex);
            static double Lin(byte v) { var x = v / 255.0; return x <= 0.03928 ? x / 12.92 : Math.Pow((x + 0.055) / 1.055, 2.4); }
            return 0.2126 * Lin(c.R) + 0.7152 * Lin(c.G) + 0.0722 * Lin(c.B);
        }
        double la = L(a), lb = L(b);
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }

    public static Thickness All(double v) => new(v);
}
