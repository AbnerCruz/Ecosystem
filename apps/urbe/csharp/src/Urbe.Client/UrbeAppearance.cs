using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Controls.Documents;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Urbe.Core;
using Urbe.Client.World;

namespace Urbe.Client;

/// <summary>
/// customize.js aplicarTela / aplicarCidade natively: the appearance Personalização describes
/// (colours, fonts, text size, corner radii, density, editor column, the world's ground) applied to
/// the running app. The original sets --ui-* variables; here they reach the design tokens
/// (<see cref="UrbeTheme"/>), the inherited font, the corner radii of the controls and the world.
/// </summary>
public static class UrbeAppearance
{
    private static readonly int[] DefaultRadii = [8, 12, 16, 22];
    private static readonly FontFamily DefaultUi = UrbeTheme.UiFont;
    private static int[] _radii = DefaultRadii;
    private static readonly ConditionalWeakTable<MainView, State> States = new();
    private static readonly AttachedProperty<int> ShapeIndex = AvaloniaProperty.RegisterAttached<Control, Control, int>("ShapeIndex", -2);
    private static readonly AttachedProperty<bool> UsesUiFont = AvaloniaProperty.RegisterAttached<Control, Control, bool>("UsesUiFont");

    private sealed class State { public int Ground = -1; }

    /// <summary>data-densidade: compacta, normal or confortavel.</summary>
    public static string Density { get; private set; } = "normal";
    /// <summary>data-animacoes: sistema, ligadas or reduzidas.</summary>
    public static string Animations { get; private set; } = "sistema";
    /// <summary>true when animations should be (almost) skipped.</summary>
    public static bool ReduceMotion => Animations == "reduzidas";

    static UrbeAppearance()
    {
        // controls built after the last Apply (dialogs, panels) are styled when they load
        Control.LoadedEvent.AddClassHandler<Control>((c, _) => Restyle(c));
    }

    /// <summary>.udlg-option (customize.css densidade): min-height and vertical padding of a dialog option.</summary>
    public static (double MinHeight, Thickness Padding) DialogOption => Density switch
    {
        "compacta" => (44, new Thickness(12, 8)),
        "confortavel" => (54, new Thickness(12, 16)),
        _ => (54, new Thickness(12, 8))
    };

    /// <summary>aplicarTela(c) + aplicarCidade(c): everything the configuration changes in the running app.</summary>
    public static void Apply(MainView view)
    {
        var c = view.Customization;
        var v = c.Variables;
        string Var(string name) => v.Vars.Last(x => x.Key == name).Value;

        // colours
        var colors = new Dictionary<string, string>();
        foreach (var (key, token) in LegacyCustomizeTheme.Tokens)
            if (v.Vars.FirstOrDefault(x => x.Key == token) is { Key: not null } kv) colors[key] = kv.Value;
        colors["destaque-tinta"] = Var("--ui-accent-ink");
        UrbeTheme.SetColors(colors);
        if (Application.Current is { } app) app.RequestedThemeVariant = v.Light ? ThemeVariant.Light : ThemeVariant.Dark;
        view.Background = UrbeTheme.Brush(UrbeTheme.Bg);

        // text: the base size and font are inherited by every control that does not set its own
        var ui = ToFamily(Var("--ui-font"));
        UrbeTheme.SetFonts(ui, ToFamily(Var("--ui-editor-font")));
        TextElement.SetFontFamily(view, ui);
        TextElement.SetFontSize(view, Px(Var("--ui-font-size")));

        // shape, density, motion
        _radii = [(int)Px(Var("--ui-r-s")), (int)Px(Var("--ui-r-m")), (int)Px(Var("--ui-r-l")), (int)Px(Var("--ui-r-xl"))];
        Density = v.Attrs["data-densidade"];
        Animations = v.Attrs["data-animacoes"];

        // editor column (the Fonte editor uses --ui-code-size and --ui-editor-line)
        var width = Var("--ui-editor-width");
        view.Editor.ApplyAppearance(width.EndsWith('%') ? double.PositiveInfinity : Px(width), Px(Var("--ui-code-size")), double.Parse(Var("--ui-editor-line"), System.Globalization.CultureInfo.InvariantCulture), Px(Var("--ui-editor-size")));

        // the world's ground and own building art
        var state = States.GetOrCreateValue(view);
        if (state.Ground != c.GroundVersion)
        {
            state.Ground = c.GroundVersion;
            view.World.SetGround(c.Ground, c.BuildingArt);
        }

        foreach (var d in view.GetVisualDescendants().OfType<Control>().Prepend(view)) Restyle(d);
        view.World.InvalidateVisual();
    }

    /// <summary>--ui-r-s, -m, -l, -xl (index 0..3) as they are now.</summary>
    public static double Radius(int index) => _radii[index];

    private static double Px(string css) => double.Parse(css.Replace("px", ""), System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>--ui-font (a CSS list) as an Avalonia font family: named families only, in order.</summary>
    public static FontFamily ToFamily(string css)
    {
        var skip = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "ui-rounded", "ui-monospace", "ui-serif", "ui-sans-serif", "system-ui", "-apple-system", "blinkmacsystemfont", "sans-serif", "serif", "monospace", "cursive", "sf pro text", "sf pro rounded", "sf mono", "sfmono-regular"
        };
        var names = css.Split(',').Select(n => n.Trim().Trim('"', '\'').Trim()).Where(n => n.Length > 0 && !skip.Contains(n)).ToList();
        return names.Count == 0 ? DefaultUi : new FontFamily(string.Join(", ", names));
    }

    /// <summary>The corner radius index (--ui-r-s..xl) and the UI font are remembered per control, so changing the theme later maps them again.</summary>
    private static void Restyle(Control c)
    {
        if (c is TextBlock tb)
        {
            if (c.GetValue(UsesUiFont) || tb.FontFamily.Equals(DefaultUi) || tb.FontFamily.Equals(UrbeTheme.UiFont)) { c.SetValue(UsesUiFont, true); tb.FontFamily = UrbeTheme.UiFont; }
        }
        else if (c is TemplatedControl t)
        {
            if (c.GetValue(UsesUiFont) || t.FontFamily.Equals(DefaultUi) || t.FontFamily.Equals(UrbeTheme.UiFont)) { c.SetValue(UsesUiFont, true); t.FontFamily = UrbeTheme.UiFont; }
            t.CornerRadius = Mapped(c, t.CornerRadius);
        }
        if (c is Border b) b.CornerRadius = Mapped(c, b.CornerRadius);
    }

    private static CornerRadius Mapped(Control c, CornerRadius r)
    {
        int index = c.GetValue(ShapeIndex);
        if (index == -2)
        {
            index = -1;
            if (r.TopLeft == r.TopRight && r.TopLeft == r.BottomLeft && r.TopLeft == r.BottomRight) index = Array.IndexOf(DefaultRadii, (int)r.TopLeft);
            if (index >= 0 && r.TopLeft != DefaultRadii[index]) index = -1;
            if (index >= 0) c.SetValue(ShapeIndex, index);   // a radius that is not a token yet may become one later
        }
        return index < 0 ? r : new CornerRadius(_radii[index]);
    }
}
