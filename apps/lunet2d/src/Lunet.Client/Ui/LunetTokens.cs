using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace Lunet.Client.Ui;

/// <summary>
/// The visual system of the Lunet shell, defined once (ADR 0013). It is the same family as Numa
/// (its design tokens): same near-black surfaces, 8-pt spacing, radii, type scale and
/// 48 dp touch targets; only the accent is Lunet's own (violet). The values are copied, not referenced: Products do not
/// depend on each other (NN-002). Extracting a shared <c>Ecosystem.Ui</c> needs the NN-022 review recorded in ADR 0013.
/// Every text/background pair here meets WCAG AA (checked by <c>LunetClientTests.Tokens_TextPairsMeetAA</c>).
/// </summary>
public static class LunetTokens
{
    /// <summary>The smallest touch target: 48 dp (Android guideline; the old shell used 40 dp keys).</summary>
    public const double MinTouch = 48;

    public static class Color
    {
        public const string Background = "#0e0f11";
        public const string Surface = "#15161a";
        public const string SurfaceRaised = "#1b1d22";
        public const string SurfaceOverlay = "#24262c";
        public const string Border = "#25272d";
        public const string BorderStrong = "#33363e";
        public const string TextPrimary = "#ececf0";
        public const string TextSecondary = "#a3a6af";
        public const string TextTertiary = "#898c95";
        /// <summary>Lunet's accent: the violet of the old shell (#5b45cf) lifted to AA on the dark surfaces.</summary>
        public const string Primary = "#a594ff";
        public const string OnPrimary = "#140c33";
        /// <summary>The accent at low opacity, for the selected tab and the selected card.</summary>
        public const string PrimarySoft = "#33a594ff";
        public const string Success = "#7fdca6";
        public const string Warning = "#f1c46e";
        public const string Danger = "#ff7b86";
        /// <summary>The game surface letterbox.</summary>
        public const string Stage = "#000000";
        /// <summary>Code text (monospace).</summary>
        public const string Code = "#e6e1f5";
    }

    public static class Space
    {
        public const double Xxs = 4, Xs = 8, S = 12, M = 16, L = 24, Xl = 32;
    }

    public static class Radius
    {
        public const double S = 8, M = 12, L = 16, Xl = 24, Pill = 999;
    }

    public static class Type
    {
        public const double Caption = 12, Label = 13, Callout = 15, Body = 16, Title = 20, Headline = 24, Code = 14;
    }

    public static FontFamily UiFont { get; } = new("Segoe UI, Roboto, Inter, Helvetica Neue, Arial, sans-serif");
    public static FontFamily MonoFont { get; } = new("Cascadia Mono, Consolas, JetBrains Mono, DejaVu Sans Mono, monospace");

    public static BoxShadows Low { get; } = BoxShadows.Parse("0 1 2 0 #59000000, 0 6 20 0 #47000000");

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
