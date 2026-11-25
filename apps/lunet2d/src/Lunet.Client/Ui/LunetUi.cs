using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Lunet.Client.Ui;

/// <summary>Visual weight of a button.</summary>
public enum ButtonKind { Primary, Secondary, Ghost }

/// <summary>
/// The component set of the shell: every screen is built from these, never from hand-set colours and sizes
/// (study item 6: "toda tela nova só usa componentes"). Each interactive component has a 48 dp target and an
/// accessible name.
/// </summary>
/// <summary>Haptic feedback hooks (UX P7), set by the Android head; no-ops elsewhere.</summary>
public static class Haptics
{
    /// <summary>A key went down (Android: KeyboardTap).</summary>
    public static Action? KeyTap { get; set; }
    /// <summary>Something was done (Android: Confirm).</summary>
    public static Action? Confirm { get; set; }
}

public static class LunetUi
{
    public static TextBlock Text(string text, double size = LunetTokens.Type.Body, string color = LunetTokens.Color.TextPrimary,
        FontWeight weight = FontWeight.Normal) => new()
    {
        Text = text,
        FontFamily = LunetTokens.UiFont,
        FontSize = size,
        FontWeight = weight,
        Foreground = LunetTokens.Brush(color),
        TextWrapping = TextWrapping.Wrap,
    };

    public static TextBlock Headline(string text) => Text(text, LunetTokens.Type.Headline, weight: FontWeight.Bold);
    public static TextBlock Title(string text) => Text(text, LunetTokens.Type.Title, weight: FontWeight.SemiBold);
    public static TextBlock Secondary(string text) => Text(text, LunetTokens.Type.Callout, LunetTokens.Color.TextSecondary);

    /// <summary>A labelled button, optionally with an icon before the label.</summary>
    public static Button Button(string label, ButtonKind kind = ButtonKind.Secondary, string? icon = null, Action? onClick = null)
    {
        var (bg, fg, border) = kind switch
        {
            ButtonKind.Primary => (LunetTokens.Color.Primary, LunetTokens.Color.OnPrimary, LunetTokens.Color.Primary),
            ButtonKind.Secondary => (LunetTokens.Color.SurfaceRaised, LunetTokens.Color.TextPrimary, LunetTokens.Color.BorderStrong),
            _ => ("#00000000", LunetTokens.Color.TextPrimary, "#00000000"),
        };
        var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = LunetTokens.Space.Xs, VerticalAlignment = VerticalAlignment.Center };
        if (icon is not null) content.Children.Add(LunetIcons.Create(icon, fg, 18, filled: icon == LunetIcons.Play));
        content.Children.Add(new TextBlock
        {
            Text = label, FontFamily = LunetTokens.UiFont, FontSize = LunetTokens.Type.Callout, FontWeight = FontWeight.SemiBold,
            Foreground = LunetTokens.Brush(fg), VerticalAlignment = VerticalAlignment.Center,
        });
        var button = new Button
        {
            Content = content,
            MinHeight = LunetTokens.MinTouch,
            Padding = new Thickness(LunetTokens.Space.M, 0),
            Background = LunetTokens.Brush(bg),
            BorderBrush = LunetTokens.Brush(border),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(LunetTokens.Radius.M),
            VerticalContentAlignment = VerticalAlignment.Center,
            HorizontalContentAlignment = HorizontalAlignment.Center,
        };
        button.Classes.Add("lunet-" + kind.ToString().ToLowerInvariant());
        AutomationProperties.SetName(button, label);
        if (onClick is not null) button.Click += (_, _) => onClick();
        return button;
    }

    /// <summary>A square icon-only button; <paramref name="label"/> is its accessible name and tooltip.</summary>
    public static Button IconButton(string icon, string label, Action? onClick = null, string color = LunetTokens.Color.TextPrimary)
    {
        var button = new Button
        {
            Content = LunetIcons.Create(icon, color, 20, filled: icon == LunetIcons.Play),
            Width = LunetTokens.MinTouch, Height = LunetTokens.MinTouch,
            Padding = new Thickness(0),
            Background = LunetTokens.Brush("#00000000"),
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(LunetTokens.Radius.M),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
        };
        button.Classes.Add("lunet-icon");
        AutomationProperties.SetName(button, label);
        ToolTip.SetTip(button, label);
        if (onClick is not null) button.Click += (_, _) => onClick();
        return button;
    }

    /// <summary>A tab of the workspace bar: icon over label, accent pill when selected.</summary>
    public static Button Tab(string icon, string label, bool selected, Action onClick)
    {
        var fg = selected ? LunetTokens.Color.Primary : LunetTokens.Color.TextSecondary;
        var stack = new StackPanel { Spacing = 2, HorizontalAlignment = HorizontalAlignment.Center };
        stack.Children.Add(new Border
        {
            Background = LunetTokens.Brush(selected ? LunetTokens.Color.PrimarySoft : "#00000000"),
            CornerRadius = new CornerRadius(LunetTokens.Radius.Pill),
            Padding = new Thickness(14, 3),
            HorizontalAlignment = HorizontalAlignment.Center,
            Child = LunetIcons.Create(icon, fg, 20),
        });
        stack.Children.Add(new TextBlock
        {
            Text = label, FontFamily = LunetTokens.UiFont, FontSize = LunetTokens.Type.Caption,
            FontWeight = selected ? FontWeight.SemiBold : FontWeight.Medium, Foreground = LunetTokens.Brush(fg),
            HorizontalAlignment = HorizontalAlignment.Center,
        });
        var button = new Button
        {
            Content = stack, MinHeight = 56, MinWidth = 64, Padding = new Thickness(4),
            Background = LunetTokens.Brush("#00000000"), BorderThickness = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Center,
            Tag = selected,
        };
        button.Classes.Add("lunet-tab");
        if (selected) button.Classes.Add("selected");
        AutomationProperties.SetName(button, label);
        button.Click += (_, _) => onClick();
        return button;
    }

    /// <summary>A raised surface with the family radius.</summary>
    public static Border Card(Control child, double padding = LunetTokens.Space.M) => new()
    {
        Background = LunetTokens.Brush(LunetTokens.Color.Surface),
        BorderBrush = LunetTokens.Brush(LunetTokens.Color.Border),
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(LunetTokens.Radius.L),
        Padding = new Thickness(padding),
        Child = child,
    };

    /// <summary>An honest empty state: what is here, and what to do next. Never a fake feature.</summary>
    public static Control EmptyState(string icon, string title, string body, Control? action = null)
    {
        var stack = new StackPanel
        {
            Spacing = LunetTokens.Space.S, MaxWidth = 440, HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(LunetTokens.Space.L),
        };
        stack.Children.Add(LunetIcons.Create(icon, LunetTokens.Color.Primary, 40));
        var t = Title(title); t.TextAlignment = TextAlignment.Center; stack.Children.Add(t);
        var b = Secondary(body); b.TextAlignment = TextAlignment.Center; stack.Children.Add(b);
        if (action is not null) { action.HorizontalAlignment = HorizontalAlignment.Center; stack.Children.Add(action); }
        return stack;
    }

    public static Border Divider(bool vertical = false) => new()
    {
        Background = LunetTokens.Brush(LunetTokens.Color.Border),
        Width = vertical ? 1 : double.NaN,
        Height = vertical ? double.NaN : 1,
    };
}
