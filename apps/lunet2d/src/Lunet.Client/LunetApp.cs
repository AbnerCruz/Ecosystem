using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Media.Transformation;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Lunet.Client.Ui;

namespace Lunet.Client;

/// <summary>
/// The Avalonia application shared by every head. The Fluent base is re-tinted with the Ecosystem visual language
/// (docs/design/linguagem-visual.md v1.0): its palette, then the resource keys of every stock control (fields,
/// combos, switches, sliders, checks, menus, tool tips, scroll bars), so no control keeps the theme's grey look.
/// Buttons get the press scale and colour transitions of the motion tokens.
/// </summary>
public class LunetApp : Application
{
    public override void Initialize()
    {
        RequestedThemeVariant = ThemeVariant.Dark;
        var fluent = new FluentTheme();
        fluent.Palettes[ThemeVariant.Dark] = Palette();
        Styles.Add(fluent);
        // The code editor's control templates (AvaloniaEdit, LUNET-537 D1).
        Styles.Add(new Avalonia.Markup.Xaml.Styling.StyleInclude(new Uri("avares://Lunet.Client/"))
        {
            Source = new Uri("avares://AvaloniaEdit/Themes/Fluent/AvaloniaEdit.xaml"),
        });
        Apply(Resources);
        Styles.Add(ControlStyles());
        Styles.Add(Ui.CodeInput.MultilineTextBoxes()); // Enter = line break in every multi-line field (estudo do editor)
    }

    private static Color C(string hex) => Color.Parse(hex);
    private static IBrush B(string hex) => LunetTokens.Brush(hex);

    /// <summary>The Fluent system colours from the ink scale: what any control not styled below falls back to.</summary>
    public static ColorPaletteResources Palette() => new()
    {
        Accent = C(LunetTokens.Color.Accent),
        RegionColor = C(LunetTokens.Ink.I1),
        AltHigh = C(LunetTokens.Ink.I1), AltMediumHigh = C(LunetTokens.Ink.I2), AltMedium = C(LunetTokens.Ink.I3),
        AltMediumLow = C(LunetTokens.Ink.I3), AltLow = C(LunetTokens.Ink.I4),
        BaseHigh = C(LunetTokens.Ink.I12), BaseMediumHigh = C(LunetTokens.Ink.I11), BaseMedium = C(LunetTokens.Ink.I10),
        BaseMediumLow = C(LunetTokens.Ink.I8), BaseLow = C(LunetTokens.Ink.I6),
        ChromeAltLow = C(LunetTokens.Ink.I11), ChromeBlackHigh = C("#000000"), ChromeBlackLow = C(LunetTokens.Ink.I11),
        ChromeBlackMedium = C(LunetTokens.Ink.I1), ChromeBlackMediumLow = C(LunetTokens.Ink.I2),
        ChromeDisabledHigh = C(LunetTokens.Ink.I6), ChromeDisabledLow = C(LunetTokens.Ink.I9),
        ChromeGray = C(LunetTokens.Ink.I9), ChromeHigh = C(LunetTokens.Ink.I7), ChromeLow = C(LunetTokens.Ink.I3),
        ChromeMedium = C(LunetTokens.Ink.I4), ChromeMediumLow = C(LunetTokens.Ink.I3), ChromeWhite = C(LunetTokens.Ink.I12),
        ListLow = C(LunetTokens.Ink.I4), ListMedium = C(LunetTokens.Ink.I5),
        ErrorText = C(LunetTokens.Color.Danger),
    };

    /// <summary>Fluent resource keys mapped to the visual language (§6 of the language).</summary>
    public static void Apply(IResourceDictionary r)
    {
        var none = B("#00000000");
        r["SystemAccentColor"] = C(LunetTokens.Color.Accent);
        r["SystemAccentColorLight1"] = C(LunetTokens.Color.AccentHover);
        r["SystemAccentColorLight2"] = C(LunetTokens.Color.Primary);
        r["SystemAccentColorDark1"] = C(LunetTokens.Color.Accent);
        r["ControlCornerRadius"] = new CornerRadius(LunetTokens.Radius.M);
        r["OverlayCornerRadius"] = new CornerRadius(LunetTokens.Radius.L);
        r["ContentControlThemeFontFamily"] = LunetTokens.UiFont;

        // Fields (§6.2): ink 3, no border at rest; hover ink 4; focus = a 2 px ring in violet 11 (the border is always
        // 2 px and only changes colour, so the text never moves).
        r["TextControlBackground"] = B(LunetTokens.Ink.I3);
        r["TextControlBackgroundPointerOver"] = B(LunetTokens.Ink.I4);
        r["TextControlBackgroundFocused"] = B(LunetTokens.Ink.I4);
        r["TextControlBackgroundDisabled"] = B(LunetTokens.Ink.I3);
        r["TextControlBorderBrush"] = none;
        r["TextControlBorderBrushPointerOver"] = none;
        r["TextControlBorderBrushFocused"] = B(LunetTokens.Color.Primary);
        r["TextControlBorderBrushDisabled"] = none;
        r["TextControlBorderThemeThickness"] = new Thickness(2);
        r["TextControlBorderThemeThicknessFocused"] = new Thickness(2);
        r["TextControlForeground"] = B(LunetTokens.Ink.I12);
        r["TextControlForegroundPointerOver"] = B(LunetTokens.Ink.I12);
        r["TextControlForegroundFocused"] = B(LunetTokens.Ink.I12);
        r["TextControlForegroundDisabled"] = B(LunetTokens.Ink.I9);
        r["TextControlPlaceholderForeground"] = B(LunetTokens.Ink.I10);
        r["TextControlPlaceholderForegroundPointerOver"] = B(LunetTokens.Ink.I10);
        r["TextControlPlaceholderForegroundFocused"] = B(LunetTokens.Ink.I10);
        r["TextControlSelectionHighlightColor"] = B(LunetTokens.Color.Selection);
        r["TextControlThemePadding"] = new Thickness(12, 10);
        r["TextControlThemeMinHeight"] = LunetTokens.MinTouch;
        r["TextControlButtonForeground"] = B(LunetTokens.Ink.I10);
        r["TextControlButtonForegroundPointerOver"] = B(LunetTokens.Ink.I12);

        // Combo (a field with a chevron; the list is an overlay, ink 5).
        r["ComboBoxBackground"] = B(LunetTokens.Ink.I3);
        r["ComboBoxBackgroundPointerOver"] = B(LunetTokens.Ink.I4);
        r["ComboBoxBackgroundPressed"] = B(LunetTokens.Ink.I5);
        r["ComboBoxBackgroundFocused"] = B(LunetTokens.Ink.I4);
        r["ComboBoxBackgroundUnfocused"] = B(LunetTokens.Ink.I3);
        r["ComboBoxBackgroundDisabled"] = B(LunetTokens.Ink.I3);
        r["ComboBoxBorderBrush"] = none;
        r["ComboBoxBorderBrushPointerOver"] = none;
        r["ComboBoxBorderBrushPressed"] = none;
        r["ComboBoxBorderBrushDisabled"] = none;
        r["ComboBoxBorderThemeThickness"] = new Thickness(0);
        r["ComboBoxForeground"] = B(LunetTokens.Ink.I12);
        r["ComboBoxForegroundDisabled"] = B(LunetTokens.Ink.I9);
        r["ComboBoxPlaceHolderForeground"] = B(LunetTokens.Ink.I10);
        r["ComboBoxDropDownGlyphForeground"] = B(LunetTokens.Ink.I10);
        r["ComboBoxDropDownBackground"] = B(LunetTokens.Ink.I5);
        r["ComboBoxDropDownBorderBrush"] = B(LunetTokens.Color.Hairline);
        r["ComboBoxItemBackgroundPointerOver"] = B(LunetTokens.Ink.I6);
        r["ComboBoxItemBackgroundPressed"] = B(LunetTokens.Ink.I7);
        r["ComboBoxItemBackgroundSelected"] = B(LunetTokens.Color.PrimarySoft);
        r["ComboBoxItemBackgroundSelectedPointerOver"] = B(LunetTokens.Color.PrimarySoft);
        r["ComboBoxItemForegroundSelected"] = B(LunetTokens.Ink.I12);
        r["ComboBoxThemeMinHeight"] = LunetTokens.MinTouch;

        // Switch (§6.3): track ink 6, on = violet 9, white knob.
        r["ToggleSwitchFillOff"] = B(LunetTokens.Ink.I6);
        r["ToggleSwitchFillOffPointerOver"] = B(LunetTokens.Ink.I7);
        r["ToggleSwitchFillOffPressed"] = B(LunetTokens.Ink.I8);
        r["ToggleSwitchStrokeOff"] = none;
        r["ToggleSwitchStrokeOffPointerOver"] = none;
        r["ToggleSwitchStrokeOffPressed"] = none;
        r["ToggleSwitchKnobFillOff"] = B(LunetTokens.Ink.I11);
        r["ToggleSwitchKnobFillOffPointerOver"] = B(LunetTokens.Ink.I12);
        r["ToggleSwitchKnobFillOffPressed"] = B(LunetTokens.Ink.I12);
        r["ToggleSwitchFillOn"] = B(LunetTokens.Color.Accent);
        r["ToggleSwitchFillOnPointerOver"] = B(LunetTokens.Color.AccentHover);
        r["ToggleSwitchFillOnPressed"] = B(LunetTokens.Color.Accent);
        r["ToggleSwitchStrokeOn"] = none;
        r["ToggleSwitchKnobFillOn"] = B("#FFFFFF");
        r["ToggleSwitchKnobFillOnPointerOver"] = B("#FFFFFF");
        r["ToggleSwitchKnobFillOnPressed"] = B("#FFFFFF");
        r["ToggleSwitchContentForeground"] = B(LunetTokens.Ink.I12);

        // Slider (§6.3): 4 px track in ink 6, fill violet 9, white thumb.
        r["SliderTrackFill"] = B(LunetTokens.Ink.I6);
        r["SliderTrackFillPointerOver"] = B(LunetTokens.Ink.I7);
        r["SliderTrackFillPressed"] = B(LunetTokens.Ink.I7);
        r["SliderTrackValueFill"] = B(LunetTokens.Color.Accent);
        r["SliderTrackValueFillPointerOver"] = B(LunetTokens.Color.AccentHover);
        r["SliderTrackValueFillPressed"] = B(LunetTokens.Color.AccentHover);
        r["SliderThumbBackground"] = B("#FFFFFF");
        r["SliderThumbBackgroundPointerOver"] = B("#FFFFFF");
        r["SliderThumbBackgroundPressed"] = B(LunetTokens.Ink.I12);
        r["SliderTrackThemeHeight"] = 4.0;
        r["SliderHorizontalThumbWidth"] = 20.0;
        r["SliderHorizontalThumbHeight"] = 20.0;

        // Checks and radios: 1.5 px ink 8 border; checked = violet 9 with a white mark.
        r["CheckBoxCheckBackgroundStrokeUnchecked"] = B(LunetTokens.Ink.I8);
        r["CheckBoxCheckBackgroundFillUnchecked"] = none;
        r["CheckBoxCheckBackgroundFillChecked"] = B(LunetTokens.Color.Accent);
        r["CheckBoxCheckBackgroundStrokeChecked"] = B(LunetTokens.Color.Accent);
        r["CheckBoxCheckGlyphForegroundChecked"] = B("#FFFFFF");
        r["RadioButtonOuterEllipseStroke"] = B(LunetTokens.Ink.I8);
        r["RadioButtonOuterEllipseCheckedFill"] = B(LunetTokens.Color.Accent);
        r["RadioButtonOuterEllipseCheckedStroke"] = B(LunetTokens.Color.Accent);

        // Overlays (§6.5): menus, flyouts and tool tips on ink 5 with a hairline.
        r["MenuFlyoutPresenterBackground"] = B(LunetTokens.Ink.I5);
        r["MenuFlyoutPresenterBorderBrush"] = B(LunetTokens.Color.Hairline);
        r["MenuFlyoutItemBackgroundPointerOver"] = B(LunetTokens.Ink.I6);
        r["FlyoutPresenterBackground"] = B(LunetTokens.Ink.I5);
        r["FlyoutBorderThemeBrush"] = B(LunetTokens.Color.Hairline);
        r["ToolTipBackground"] = B(LunetTokens.Ink.I5);
        r["ToolTipForeground"] = B(LunetTokens.Ink.I12);
        r["ToolTipBorderBrush"] = B(LunetTokens.Color.Hairline);

        // Scroll bars: thin and quiet.
        r["ScrollBarThumbFill"] = B(LunetTokens.Ink.I7);
        r["ScrollBarThumbFillPointerOver"] = B(LunetTokens.Ink.I8);
        r["ScrollBarThumbFillPressed"] = B(LunetTokens.Ink.I9);
        r["ScrollBarTrackFill"] = none;
        r["ScrollBarTrackFillPointerOver"] = none;
    }

    /// <summary>
    /// The states of the house components (§6.6): hover adds white at 4%, pressed white at 8% and a 0.97 scale; colour
    /// and scale animate with the motion tokens (fast / instant). Fields and combos fade between their tones.
    /// </summary>
    private static Styles ControlStyles()
    {
        var styles = new Styles();
        var standard = new SplineEasing(0.2, 0, 0, 1);

        // Every button: the colour fade and the press scale (a transform: no layout pass, §8).
        var button = new Style(x => x.OfType<Button>());
        button.Setters.Add(new Setter(Visual.RenderTransformOriginProperty, RelativePoint.Center));
        button.Setters.Add(new Setter(Animatable.TransitionsProperty, new Transitions
        {
            new TransformOperationsTransition { Property = Visual.RenderTransformProperty, Duration = LunetTokens.Motion.Instant, Easing = standard },
            new DoubleTransition { Property = Visual.OpacityProperty, Duration = LunetTokens.Motion.Fast, Easing = standard },
        }));
        styles.Add(button);
        var pressed = new Style(x => x.OfType<Button>().Class(":pressed"));
        pressed.Setters.Add(new Setter(Visual.RenderTransformProperty, TransformOperations.Parse($"scale({LunetTokens.Motion.PressScale.ToString(System.Globalization.CultureInfo.InvariantCulture)})")));
        styles.Add(pressed);

        // The presenter (where the fill is drawn) fades its background on hover and press.
        var presenter = new Style(x => x.OfType<Button>().Template().OfType<ContentPresenter>().Name("PART_ContentPresenter"));
        presenter.Setters.Add(new Setter(Animatable.TransitionsProperty, new Transitions
        {
            new BrushTransition { Property = ContentPresenter.BackgroundProperty, Duration = LunetTokens.Motion.Fast, Easing = standard },
        }));
        styles.Add(presenter);

        foreach (var (cls, hover, press) in new[]
        {
            ("lunet-primary", LunetTokens.Color.AccentHover, LunetTokens.Color.Accent),
            ("lunet-secondary", LunetTokens.Ink.I5, LunetTokens.Ink.I6),
            ("lunet-ghost", LunetTokens.Color.Hover, LunetTokens.Color.Pressed),
            ("lunet-selected", "#3D6E56F8", "#526E56F8"),
            ("lunet-icon", LunetTokens.Color.Hover, LunetTokens.Color.Pressed),
            ("lunet-tab", "#00000000", LunetTokens.Color.Hover),
            ("lunet-card", LunetTokens.Ink.I4, LunetTokens.Ink.I5),
            ("lunet-row", LunetTokens.Color.Hover, LunetTokens.Color.Pressed),
        })
        {
            styles.Add(State(cls, ":pointerover", hover));
            styles.Add(State(cls, ":pressed", press));
        }

        // Fields and combos fade between rest, hover and focus.
        foreach (var type in new[] { typeof(TextBox), typeof(ComboBox) })
        {
            var field = new Style(x => x.OfType(type).Template().OfType<Border>().Name("PART_BorderElement"));
            field.Setters.Add(new Setter(Animatable.TransitionsProperty, new Transitions
            {
                new BrushTransition { Property = Border.BackgroundProperty, Duration = LunetTokens.Motion.Fast, Easing = standard },
                new BrushTransition { Property = Border.BorderBrushProperty, Duration = LunetTokens.Motion.Fast, Easing = standard },
            }));
            styles.Add(field);
        }
        var text = new Style(x => x.OfType<TextBox>());
        text.Setters.Add(new Setter(TemplatedControl.FontFamilyProperty, LunetTokens.UiFont));
        text.Setters.Add(new Setter(TemplatedControl.FontSizeProperty, LunetTokens.Type.Body));
        text.Setters.Add(new Setter(TemplatedControl.CornerRadiusProperty, new CornerRadius(LunetTokens.Radius.M)));
        styles.Add(text);
        var combo = new Style(x => x.OfType<ComboBox>());
        combo.Setters.Add(new Setter(TemplatedControl.FontFamilyProperty, LunetTokens.UiFont));
        combo.Setters.Add(new Setter(TemplatedControl.FontSizeProperty, LunetTokens.Type.Body));
        combo.Setters.Add(new Setter(TemplatedControl.CornerRadiusProperty, new CornerRadius(LunetTokens.Radius.M)));
        combo.Setters.Add(new Setter(TemplatedControl.PaddingProperty, new Thickness(12, 0, 8, 0)));
        styles.Add(combo);
        var window = new Style(x => x.OfType<TopLevel>());
        window.Setters.Add(new Setter(TemplatedControl.FontFamilyProperty, LunetTokens.UiFont));
        styles.Add(window);
        return styles;
    }

    private static Style State(string cls, string pseudo, string background)
    {
        var style = new Style(x => x.OfType<Button>().Class(cls).Class(pseudo).Template().OfType<ContentPresenter>().Name("PART_ContentPresenter"));
        style.Setters.Add(new Setter(ContentPresenter.BackgroundProperty, LunetTokens.Brush(background)));
        return style;
    }
}
