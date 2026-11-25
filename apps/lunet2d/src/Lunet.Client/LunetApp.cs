using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Lunet.Client.Ui;

namespace Lunet.Client;

/// <summary>
/// The Avalonia application shared by every head: dark Fluent base, recoloured with the Lunet tokens so the stock
/// controls (TextBox, ScrollBar, ToolTip, pointer-over states) belong to the same family.
/// </summary>
public class LunetApp : Application
{
    public override void Initialize()
    {
        RequestedThemeVariant = ThemeVariant.Dark;
        Styles.Add(new FluentTheme());
        Apply(Resources);
        Styles.Add(PressedAndHover());
    }

    /// <summary>Fluent resource keys mapped to Lunet tokens.</summary>
    public static void Apply(IResourceDictionary r)
    {
        static Color C(string hex) => Color.Parse(hex);
        r["SystemAccentColor"] = C(LunetTokens.Color.Primary);
        r["SystemAccentColorLight1"] = C(LunetTokens.Color.Primary);
        r["SystemAccentColorDark1"] = C(LunetTokens.Color.Primary);
        r["TextControlBackground"] = LunetTokens.Brush(LunetTokens.Color.Background);
        r["TextControlBackgroundPointerOver"] = LunetTokens.Brush(LunetTokens.Color.Background);
        r["TextControlBackgroundFocused"] = LunetTokens.Brush(LunetTokens.Color.Background);
        r["TextControlForeground"] = LunetTokens.Brush(LunetTokens.Color.Code);
        r["TextControlForegroundFocused"] = LunetTokens.Brush(LunetTokens.Color.Code);
        r["TextControlForegroundPointerOver"] = LunetTokens.Brush(LunetTokens.Color.Code);
        r["TextControlBorderBrushFocused"] = LunetTokens.Brush("#00000000");
        r["TextControlSelectionHighlightColor"] = LunetTokens.Brush("#665b45cf");
        r["ToolTipBackground"] = LunetTokens.Brush(LunetTokens.Color.SurfaceOverlay);
        r["ToolTipForeground"] = LunetTokens.Brush(LunetTokens.Color.TextPrimary);
    }

    /// <summary>Hover and press feedback for the Lunet buttons (the stock Fluent states would repaint them grey).</summary>
    private static Styles PressedAndHover()
    {
        var styles = new Styles();
        foreach (var (cls, hover, pressed) in new[]
        {
            ("lunet-primary", "#b4a6ff", "#9381f0"),
            ("lunet-secondary", LunetTokens.Color.SurfaceOverlay, LunetTokens.Color.BorderStrong),
            ("lunet-ghost", LunetTokens.Color.SurfaceRaised, LunetTokens.Color.SurfaceOverlay),
            ("lunet-icon", LunetTokens.Color.SurfaceRaised, LunetTokens.Color.SurfaceOverlay),
            ("lunet-tab", "#00000000", LunetTokens.Color.SurfaceRaised),
            ("lunet-card", "#00000000", "#00000000"),
        })
        {
            styles.Add(Style(cls, ":pointerover", hover));
            styles.Add(Style(cls, ":pressed", pressed));
        }
        return styles;
    }

    private static Style Style(string cls, string pseudo, string background)
    {
        var style = new Style(x => x.OfType<Button>().Class(cls).Class(pseudo).Template().OfType<Avalonia.Controls.Presenters.ContentPresenter>().Name("PART_ContentPresenter"));
        style.Setters.Add(new Setter(Avalonia.Controls.Presenters.ContentPresenter.BackgroundProperty, LunetTokens.Brush(background)));
        return style;
    }
}
