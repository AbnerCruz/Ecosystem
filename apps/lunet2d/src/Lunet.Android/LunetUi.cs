using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Text;
using Android.Views;
using Android.Widget;
using Lunet.Studio;
using Lunet.Core;
using AColor = Android.Graphics.Color;

namespace Lunet.Android;

/// <summary>
/// Lunet design tokens and base components (LUNET-483). One source for color, type, spacing and shape,
/// so every surface (workbench, AI, Studios) reads as the same product.
/// </summary>
internal static class LunetUi
{
    internal static bool IsLight { get; private set; }

    internal static bool ApplyTheme(Context context, WorkspaceTheme theme)
    {
        var light = UsesLightTheme(context, theme);
        bool changed = IsLight != light;
        IsLight = light;
        return changed;
    }

    internal static bool UsesLightTheme(Context context, WorkspaceTheme theme) =>
        theme == WorkspaceTheme.Light ||
        (theme == WorkspaceTheme.System && (context.Resources!.Configuration!.UiMode & global::Android.Content.Res.UiMode.NightMask)
            != global::Android.Content.Res.UiMode.NightYes);

    internal static Typeface EditorTypeface(EditorFont font) => font switch
    {
        EditorFont.Condensed => Typeface.Create("sans-serif-condensed", TypefaceStyle.Normal)!,
        EditorFont.Serif => Typeface.Serif!,
        _ => Typeface.Monospace!,
    };
    public static AColor Background => IsLight ? AColor.Rgb(247, 248, 253) : AColor.Rgb(13, 16, 23);
    public static AColor Surface => IsLight ? AColor.Rgb(255, 255, 255) : AColor.Rgb(21, 26, 35);
    public static AColor SurfaceRaised => IsLight ? AColor.Rgb(239, 241, 249) : AColor.Rgb(29, 35, 47);
    public static AColor SurfaceHigh => IsLight ? AColor.Rgb(228, 232, 244) : AColor.Rgb(38, 46, 61);
    public static AColor Border => IsLight ? AColor.Rgb(165, 173, 192) : AColor.Rgb(44, 53, 69);
    public static AColor Text => IsLight ? AColor.Rgb(27, 35, 54) : AColor.Rgb(230, 236, 245);
    public static AColor TextMuted => IsLight ? AColor.Rgb(72, 84, 109) : AColor.Rgb(141, 153, 172);
    public static AColor TextFaint => IsLight ? AColor.Rgb(81, 93, 117) : AColor.Rgb(145, 156, 176);
    public static AColor Accent => IsLight ? AColor.Rgb(75, 56, 179) : AColor.Rgb(91, 69, 207);
    public static AColor AccentSoft => IsLight ? AColor.Rgb(228, 223, 255) : AColor.Argb(46, 124, 108, 255);
    public static AColor AccentText => IsLight ? AColor.Rgb(65, 46, 167) : AColor.Rgb(186, 176, 255);
    public static AColor Success => IsLight ? AColor.Rgb(22, 108, 42) : AColor.Rgb(63, 185, 80);
    public static AColor SuccessSoft => IsLight ? AColor.Rgb(222, 242, 225) : AColor.Argb(40, 63, 185, 80);
    public static AColor Danger => IsLight ? AColor.Rgb(169, 35, 36) : AColor.Rgb(248, 81, 73);
    public static AColor DangerSoft => IsLight ? AColor.Rgb(254, 229, 228) : AColor.Argb(40, 248, 81, 73);
    public static AColor Warning => IsLight ? AColor.Rgb(116, 77, 2) : AColor.Rgb(210, 153, 34);
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
        return new RippleDrawable(global::Android.Content.Res.ColorStateList.ValueOf(IsLight ? AColor.Argb(36, 0, 0, 0) : AColor.Argb(48, 255, 255, 255)),
            Shape(context, fill, radius, stroke), mask);
    }

    public static TextView Label(Context context, string text, float size = TextBody, AColor? color = null, bool bold = false)
    {
        var label = new TextView(context) { Text = text, TextSize = size };
        label.SetTextColor(color ?? Text);
        WithLeadingIcon(label, size);
        if (bold) label.SetTypeface(Typeface.Create("sans-serif-medium", TypefaceStyle.Normal), TypefaceStyle.Normal);
        label.SetLineSpacing(0, 1.15f);
        return label;
    }

    /// <summary>Troca um caractere inicial conhecido ("✦ Lunet IA", "⚠ 2 erros") por ícone vetorial desenhado à esquerda do texto.</summary>
    public static void WithLeadingIcon(TextView view, float textSizeSp)
    {
        var text = view.Text ?? "";
        if (!LunetIcons.TrySplitLabel(text, out var icon, out var rest, out _) || rest.Length == 0) return;
        view.Text = rest;
        SetLeadingIcon(view, icon, textSizeSp);
    }

    public static void SetLeadingIcon(TextView view, VectorIcon icon, float textSizeSp)
    {
        var drawable = new IconDrawable(view.Context!, icon, Math.Max(14, textSizeSp + 3), new AColor(view.CurrentTextColor));
        view.CompoundDrawablePadding = Dp(view.Context!, 6);
        view.SetCompoundDrawablesRelativeWithIntrinsicBounds(drawable, null, null, null);
    }

    /// <summary>Ícone vetorial solto (não clicável), pelo nome do catálogo.</summary>
    public static ImageView IconImage(Context context, string icon, float sizeDp = 24, AColor? color = null)
    {
        var view = new ImageView(context);
        view.SetImageDrawable(new IconDrawable(context, LunetIcons.Get(icon), sizeDp, color ?? TextMuted));
        view.SetScaleType(ImageView.ScaleType.Center);
        return view;
    }

    /// <summary>Primary / secondary / quiet buttons. <paramref name="keepsKeyboard"/> prevents stealing the IME focus.</summary>
    public static Button Button(Context context, string text, Action action, ButtonStyle style = ButtonStyle.Secondary,
        bool keepsKeyboard = false, string? description = null)
    {
        var button = new IconButton(context) { TextSize = TextLabel };
        button.SetAllCaps(false);
        button.SetMinWidth(0); button.SetMinimumWidth(0);
        button.SetMinHeight(Dp(context, 40)); button.SetMinimumHeight(Dp(context, 48));
        button.SetPadding(Dp(context, 14), 0, Dp(context, 14), 0);
        button.StateListAnimator = null;
        button.Elevation = 0;
        button.ContentDescription = description ?? text.Replace('\n', ' ');
        Style(context, button, style);
        button.SetLabel(text);
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
        (button as IconButton)?.SyncIconColor();
    }

    /// <summary>Troca texto e ícone de um botão criado por <see cref="Button"/> (ex.: "▶" → "■").</summary>
    public static void SetLabel(Button button, string text)
    {
        if (button is IconButton icon) icon.SetLabel(text);
        else button.Text = text;
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

    /// <summary>Botão quadrado com ícone vetorial pelo nome do catálogo (sem depender de caracteres Unicode).</summary>
    public static Button IconByName(Context context, string icon, string description, Action action, bool keepsKeyboard = false,
        bool accent = false, ButtonStyle? style = null)
    {
        var button = new IconButton(context) { TextSize = TextLabel };
        button.SetAllCaps(false);
        button.SetMinWidth(0); button.SetMinimumWidth(0);
        button.SetMinHeight(Dp(context, 40)); button.SetMinimumHeight(Dp(context, 48));
        button.SetPadding(0, 0, 0, 0);
        button.StateListAnimator = null;
        button.Elevation = 0;
        button.ContentDescription = description;
        Style(context, button, style ?? (accent ? ButtonStyle.Primary : ButtonStyle.Quiet));
        button.SetIcon(LunetIcons.Get(icon), IconSlot.Center);
        if (keepsKeyboard) { button.Focusable = false; button.FocusableInTouchMode = false; }
        button.Click += (_, _) => action();
        return button;
    }

    public static TextView Chip(Context context, string text, Action action, bool selected = false)
    {
        var chip = Label(context, text, TextLabel, selected ? AccentText : Text);
        WithLeadingIcon(chip, TextLabel);
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
