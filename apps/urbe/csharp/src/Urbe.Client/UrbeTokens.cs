using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;

namespace Urbe.Client;

/// <summary>
/// The one visual system of the interface (UX.md P9, UC-69): type scale, 8-pt spacing, radii, elevation, semantic
/// colours and motion, defined once. Colours map onto the 1.8.4 tokens of <see cref="UrbeTheme"/> (same values, live
/// brushes), so the palette stays as it is. The feel aimed at is warm (owner, UC-69: "o
/// Notion tá frio, o Obsidian tá morno"): generous rounded shapes, soft elevation, lively but discreet motion and a
/// satisfying confirmation when something is done — never decoration in the way of writing.
/// </summary>
public static class UrbeTokens
{
    /// <summary>The smallest touch target (P4): 48 dp.</summary>
    public const double MinTouch = 48;

    /// <summary>A text style of the type scale.</summary>
    public sealed record TextStyle(double Size, double LineHeight, FontWeight Weight = FontWeight.Normal)
    {
        /// <summary>Applies size, line height, weight and the UI font to a text block.</summary>
        public T Apply<T>(T text) where T : TextBlock
        {
            text.FontFamily = UrbeTheme.UiFont;
            text.FontSize = Size;
            text.LineHeight = LineHeight;
            text.FontWeight = Weight;
            return text;
        }
    }

    /// <summary>Type scale (sizes in dp, line heights in dp).</summary>
    public static class Type
    {
        public static readonly TextStyle Caption = new(12, 16, FontWeight.Medium);
        public static readonly TextStyle Footnote = new(13, 18);
        public static readonly TextStyle Label = new(13, 18, FontWeight.SemiBold);
        public static readonly TextStyle Callout = new(15, 22.5);
        public static readonly TextStyle Button = new(15, 20, FontWeight.SemiBold);
        public static readonly TextStyle Body = new(16, 24);
        public static readonly TextStyle BodyStrong = new(16, 24, FontWeight.SemiBold);
        public static readonly TextStyle Title = new(20, 28, FontWeight.Bold);
        public static readonly TextStyle Headline = new(24, 32, FontWeight.Bold);
    }

    /// <summary>Spacing on the 8-pt grid (with the 4 and 12 half steps).</summary>
    public static class Space
    {
        public const double Xxs = 4, Xs = 8, S = 12, M = 16, L = 24, Xl = 32;
    }

    /// <summary>Corner radii.</summary>
    public static class Radius
    {
        public const double S = 8, M = 12, L = 16, Xl = 24, Pill = 999;
        public static CornerRadius Top(double r) => new(r, r, 0, 0);
    }

    /// <summary>Elevation (the 1.8.4 shadows: shadow-1 of the top bar, the notice pill, the dialog card).</summary>
    public static class Elevation
    {
        public static readonly BoxShadows None = default;
        public static readonly BoxShadows Low = BoxShadows.Parse("0 1 2 0 #59000000, 0 6 20 0 #47000000");
        public static readonly BoxShadows Mid = BoxShadows.Parse("0 4 18 0 #40000000");
        public static readonly BoxShadows High = BoxShadows.Parse("0 18 60 0 #8C000000");
    }

    /// <summary>Semantic colours: names by role over the 1.8.4 token values (hex, for <see cref="UrbeTheme.Brush(string)"/>).</summary>
    public static class Color
    {
        public const string Background = UrbeTheme.Bg;
        public const string Surface = UrbeTheme.Surface;
        public const string SurfaceRaised = UrbeTheme.Surface2;
        public const string SurfaceOverlay = UrbeTheme.Surface3;
        public const string Border = UrbeTheme.Line;
        public const string BorderStrong = UrbeTheme.Line2;
        public const string TextPrimary = UrbeTheme.Text;
        public const string TextSecondary = UrbeTheme.Text2;
        public const string TextTertiary = UrbeTheme.Text3;
        public const string Primary = UrbeTheme.Accent;
        public const string OnPrimary = UrbeTheme.AccentInk;
        public const string Success = UrbeTheme.Ok;
        public const string Warning = UrbeTheme.Warn;
        public const string Danger = UrbeTheme.Danger;
        /// <summary>Text on a danger fill (.ui-btn-danger).</summary>
        public const string OnDanger = "#2a0a0e";
        /// <summary>The backdrop under a dialog or sheet: rgba(5,6,8,.6).</summary>
        public const string Scrim = "#99050608";
        /// <summary>The notice pill (life.js avisar): rgba(14,18,26,.74).</summary>
        public const string Toast = "#bd0e121a";
        public const string OnToast = "#eef2fa";
    }

    /// <summary>Motion (P6): short, meaningful, natural curves. Reduced when the system asks for no animations.</summary>
    public static class Motion
    {
        /// <summary>The pressed feedback of a touch (P1: well under 100 ms).</summary>
        public static readonly TimeSpan Press = TimeSpan.FromMilliseconds(90);
        public static readonly TimeSpan Fast = TimeSpan.FromMilliseconds(150);
        public static readonly TimeSpan Normal = TimeSpan.FromMilliseconds(220);
        public static readonly TimeSpan Slow = TimeSpan.FromMilliseconds(300);

        /// <summary>Things arriving: fast start, soft landing.</summary>
        public static Easing EaseOut => new SplineEasing(.2, 0, 0, 1);
        /// <summary>Things leaving: soft start, quick exit.</summary>
        public static Easing EaseIn => new SplineEasing(.3, 0, 1, 1);
        /// <summary>A spring-like settle (a slight overshoot), for sheets and pressed buttons coming back.</summary>
        public static Easing Spring => new SplineEasing(.2, .9, .25, 1.06);
        public static Easing Standard => new SplineEasing(.2, 0, .2, 1);

        /// <summary>No movement, only instant changes (Android "Remover animações", tests). Set by the platform.</summary>
        public static bool Reduced { get; set; }
    }
}
