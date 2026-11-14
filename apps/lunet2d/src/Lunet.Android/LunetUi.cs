using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Text;
using Android.Views;
using Android.Widget;
using AColor = Android.Graphics.Color;

namespace Lunet.Android;

/// <summary>
/// Lunet design tokens and base components (LUNET-483). One source for color, type, spacing and shape,
/// so every surface (workbench, AI, Studios) reads as the same product.
/// </summary>
internal static class LunetUi
{
    // Color tokens (dark, "night sky" — Lunet).
    public static readonly AColor Background = AColor.Rgb(13, 16, 23);
    public static readonly AColor Surface = AColor.Rgb(21, 26, 35);
    public static readonly AColor SurfaceRaised = AColor.Rgb(29, 35, 47);
    public static readonly AColor SurfaceHigh = AColor.Rgb(38, 46, 61);
    public static readonly AColor Border = AColor.Rgb(44, 53, 69);
    public static readonly AColor Text = AColor.Rgb(230, 236, 245);
    public static readonly AColor TextMuted = AColor.Rgb(141, 153, 172);
    public static readonly AColor TextFaint = AColor.Rgb(98, 109, 128);
    public static readonly AColor Accent = AColor.Rgb(124, 108, 255);
    public static readonly AColor AccentSoft = AColor.Argb(46, 124, 108, 255);
    public static readonly AColor AccentText = AColor.Rgb(186, 176, 255);
    public static readonly AColor Success = AColor.Rgb(63, 185, 80);
    public static readonly AColor SuccessSoft = AColor.Argb(40, 63, 185, 80);
    public static readonly AColor Danger = AColor.Rgb(248, 81, 73);
    public static readonly AColor DangerSoft = AColor.Argb(40, 248, 81, 73);
    public static readonly AColor Warning = AColor.Rgb(210, 153, 34);
    public static readonly AColor Scrim = AColor.Argb(150, 0, 0, 0);

    public const int RadiusSmall = 8, Radius = 12, RadiusLarge = 18;
    public const float TextCaption = 11, TextBody = 14, TextLabel = 13, TextTitle = 17, TextHero = 26;

    public static int Dp(Context context, float value) => (int)(value * context.Resources!.DisplayMetrics!.Density + 0.5f);

    public static float WidthDp(Context context) =>
        context.Resources!.DisplayMetrics!.WidthPixels / context.Resources.DisplayMetrics.Density;

    /// <summary>Material window size class: expanded (≥ 840dp) gets side panels; compact/medium stack them.</summary>
    public static bool IsExpanded(Context context) => WidthDp(context) >= 840;

    public static GradientDrawable Shape(Context context, AColor fill, float radius = Radius, AColor? stroke = null)
    {
        var shape = new GradientDrawable();
        shape.SetColor(fill);
        shape.SetCornerRadius(Dp(context, radius));
        if (stroke is { } s) shape.SetStroke(Math.Max(1, Dp(context, 1)), s);
        return shape;
    }

    public static Drawable Pressable(Context context, AColor fill, float radius = Radius, AColor? stroke = null)
    {
        var mask = Shape(context, AColor.White, radius);
        return new RippleDrawable(global::Android.Content.Res.ColorStateList.ValueOf(AColor.Argb(48, 255, 255, 255)),
            Shape(context, fill, radius, stroke), mask);
    }

    public static TextView Label(Context context, string text, float size = TextBody, AColor? color = null, bool bold = false)
    {
        var label = new TextView(context) { Text = text, TextSize = size };
        label.SetTextColor(color ?? Text);
        if (bold) label.SetTypeface(Typeface.Create("sans-serif-medium", TypefaceStyle.Normal), TypefaceStyle.Normal);
        label.SetLineSpacing(0, 1.15f);
        return label;
    }

    /// <summary>Primary / secondary / quiet buttons. <paramref name="keepsKeyboard"/> prevents stealing the IME focus.</summary>
    public static Button Button(Context context, string text, Action action, ButtonStyle style = ButtonStyle.Secondary,
        bool keepsKeyboard = false, string? description = null)
    {
        var button = new Button(context) { Text = text, TextSize = TextLabel };
        button.SetAllCaps(false);
        button.SetMinWidth(0); button.SetMinimumWidth(0);
        button.SetMinHeight(Dp(context, 40)); button.SetMinimumHeight(Dp(context, 40));
        button.SetPadding(Dp(context, 14), 0, Dp(context, 14), 0);
        button.StateListAnimator = null;
        button.Elevation = 0;
        button.ContentDescription = description ?? text;
        Style(context, button, style);
        if (keepsKeyboard) { button.Focusable = false; button.FocusableInTouchMode = false; }
        button.Click += (_, _) => action();
        return button;
    }

    public static void Style(Context context, Button button, ButtonStyle style)
    {
        var (fill, stroke, text) = style switch
        {
            ButtonStyle.Primary => (Accent, (AColor?)null, AColor.White),
            ButtonStyle.Danger => (DangerSoft, (AColor?)null, Danger),
            ButtonStyle.Quiet => (AColor.Transparent, (AColor?)null, TextMuted),
            ButtonStyle.Selected => (AccentSoft, (AColor?)Accent, AccentText),
            _ => (SurfaceRaised, (AColor?)Border, Text),
        };
        button.Background = Pressable(context, fill, Radius, stroke);
        button.SetTextColor(text);
    }

    /// <summary>Square icon button (≥ 44dp touch target) with an accessible name.</summary>
    public static Button Icon(Context context, string glyph, string description, Action action, bool keepsKeyboard = false,
        bool accent = false)
    {
        var button = Button(context, glyph, action, accent ? ButtonStyle.Primary : ButtonStyle.Quiet, keepsKeyboard, description);
        button.TextSize = 18;
        button.SetPadding(0, 0, 0, 0);
        return button;
    }

    public static TextView Chip(Context context, string text, Action action, bool selected = false)
    {
        var chip = Label(context, text, TextLabel, selected ? AccentText : Text);
        chip.SetPadding(Dp(context, 12), Dp(context, 7), Dp(context, 12), Dp(context, 7));
        chip.Background = Pressable(context, selected ? AccentSoft : SurfaceRaised, 99, selected ? Accent : Border);
        chip.Clickable = true;
        chip.Focusable = false;
        chip.SetSingleLine(true);
        chip.Click += (_, _) => action();
        return chip;
    }

    public static EditText Input(Context context, string hint, string text = "", bool singleLine = true)
    {
        var input = new EditText(context) { Hint = hint, Text = text, TextSize = TextBody };
        input.SetTextColor(Text);
        input.SetHintTextColor(TextFaint);
        input.Background = Shape(context, Surface, Radius, Border);
        input.SetPadding(Dp(context, 12), Dp(context, 10), Dp(context, 12), Dp(context, 10));
        if (singleLine) input.SetSingleLine(true);
        return input;
    }

    public static LinearLayout Row(Context context, int spacingDp = 8)
    {
        var row = new LinearLayout(context) { Orientation = Orientation.Horizontal };
        row.SetGravity(GravityFlags.CenterVertical);
        row.ShowDividers = ShowDividers.Middle;
        row.SetDividerDrawable(Spacer(context, spacingDp));
        return row;
    }

    public static LinearLayout Column(Context context, int spacingDp = 8)
    {
        var column = new LinearLayout(context) { Orientation = Orientation.Vertical };
        column.ShowDividers = ShowDividers.Middle;
        column.SetDividerDrawable(Spacer(context, spacingDp));
        return column;
    }

    private static Drawable Spacer(Context context, int dp)
    {
        var spacer = new GradientDrawable();
        spacer.SetColor(AColor.Transparent);
        spacer.SetSize(Dp(context, dp), Dp(context, dp));
        return spacer;
    }

    public static View Divider(Context context)
    {
        var line = new View(context);
        line.SetBackgroundColor(Border);
        line.LayoutParameters = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, Math.Max(1, Dp(context, 1)));
        return line;
    }

    public static LinearLayout.LayoutParams Weight(float weight = 1) => new(0, ViewGroup.LayoutParams.WrapContent, weight);

    public static LinearLayout.LayoutParams Wrap() => new(ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent);

    public static LinearLayout.LayoutParams FillWidth(int heightPx = ViewGroup.LayoutParams.WrapContent) =>
        new(ViewGroup.LayoutParams.MatchParent, heightPx);

    public static Typeface Mono => Typeface.Monospace!;

    public static ISpanned? Bold(string text) => Html.FromHtml("<b>" + Html.EscapeHtml(text) + "</b>", FromHtmlOptions.ModeCompact);
}

internal enum ButtonStyle { Primary, Secondary, Quiet, Danger, Selected }
