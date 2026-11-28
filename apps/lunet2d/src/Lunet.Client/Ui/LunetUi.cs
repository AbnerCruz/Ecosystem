using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Lunet.Client.Ui;

/// <summary>Visual weight of a button.</summary>
public enum ButtonKind
{
    /// <summary>The one main action of a screen: solid violet, white text.</summary>
    Primary,
    /// <summary>Tonal (ink 4): secondary actions.</summary>
    Secondary,
    /// <summary>Text only: tertiary actions, bars.</summary>
    Ghost,
    /// <summary>The chosen option of a group (chip, segment, tab): violet soft with violet text, never the solid.</summary>
    Selected,
    Destructive,
}

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
    /// <summary>"Reduzir movimento" (§8): entries become an instant appearance. Captures turn it on too.</summary>
    public static bool ReduceMotion { get; set; }

    /// <summary>
    /// Entry motion of the visual language (§8): fade in while rising <paramref name="rise"/> dp, with the enter curve.
    /// Sheets use 24 dp over 300 ms; pages and Studios 16 dp over 220 ms. Only opacity and transform animate.
    /// </summary>
    public static T Enter<T>(T control, double rise = 16, TimeSpan? duration = null) where T : Control
    {
        if (ReduceMotion) return control;
        var time = duration ?? LunetTokens.Motion.Base;
        var enter = new Avalonia.Animation.Easings.SplineEasing(0, 0, 0, 1);
        control.Opacity = 0;
        control.RenderTransform = Avalonia.Media.Transformation.TransformOperations.Parse($"translateY({rise.ToString(System.Globalization.CultureInfo.InvariantCulture)}px)");
        control.Transitions = new Avalonia.Animation.Transitions
        {
            new Avalonia.Animation.DoubleTransition { Property = Visual.OpacityProperty, Duration = time, Easing = enter },
            new Avalonia.Animation.TransformOperationsTransition { Property = Visual.RenderTransformProperty, Duration = time, Easing = enter },
        };
        void Start(object? sender, VisualTreeAttachmentEventArgs e)
        {
            control.AttachedToVisualTree -= Start;
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                control.Opacity = 1;
                control.RenderTransform = Avalonia.Media.Transformation.TransformOperations.Parse("translateY(0px)");
            }, Avalonia.Threading.DispatcherPriority.Render);
            // Never leave a screen invisible: if the render clock does not run the transition (a paused renderer),
            // the transitions are dropped after their time and the control snaps to its final, visible state.
            Avalonia.Threading.DispatcherTimer.RunOnce(() => control.Transitions = null, time + TimeSpan.FromMilliseconds(120));
        }
        control.AttachedToVisualTree += Start;
        return control;
    }

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
        // §6.1: principal = solid violet 9 with white text (one per screen); tonal = ink 4; ghost = text only. No borders.
        var (bg, fg) = kind switch
        {
            ButtonKind.Primary => (LunetTokens.Color.Accent, LunetTokens.Color.OnAccent),
            ButtonKind.Secondary => (LunetTokens.Color.SurfaceRaised, LunetTokens.Color.TextPrimary),
            ButtonKind.Destructive => (LunetTokens.Color.DangerSoft, LunetTokens.Color.Danger),
            ButtonKind.Selected => (LunetTokens.Color.PrimarySoft, LunetTokens.Color.Primary),
            _ => ("#00000000", LunetTokens.Color.TextPrimary),
        };
        var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = LunetTokens.Space.Xs, VerticalAlignment = VerticalAlignment.Center };
        if (icon is not null) content.Children.Add(LunetIcons.Create(icon, fg, 20, filled: icon == LunetIcons.Play));
        content.Children.Add(new TextBlock
        {
            Text = label, FontFamily = LunetTokens.UiFont, FontSize = LunetTokens.Type.Callout, FontWeight = FontWeight.SemiBold,
            Foreground = LunetTokens.Brush(fg), VerticalAlignment = VerticalAlignment.Center,
        });
        var button = new Button
        {
            Content = content,
            MinHeight = LunetTokens.MinTouch,
            Padding = new Thickness(icon is null ? 20 : LunetTokens.Space.M, 0, 20, 0),
            Background = LunetTokens.Brush(bg),
            BorderThickness = new Thickness(0),
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
        // M3 navigation item (LUNET-537 D4): 56×28 pill indicator behind a 22 dp icon, label below.
        var stack = new StackPanel { Spacing = 4, HorizontalAlignment = HorizontalAlignment.Center };
        stack.Children.Add(new Border
        {
            Width = 56, Height = 28,
            Background = LunetTokens.Brush(selected ? LunetTokens.Color.PrimarySoft : "#00000000"),
            CornerRadius = new CornerRadius(LunetTokens.Radius.Pill),
            HorizontalAlignment = HorizontalAlignment.Center,
            Child = LunetIcons.Create(icon, fg, 22),
        });
        stack.Children.Add(new TextBlock
        {
            Text = label, FontFamily = LunetTokens.UiFont, FontSize = LunetTokens.Type.Caption,
            FontWeight = selected ? FontWeight.SemiBold : FontWeight.Medium, Foreground = LunetTokens.Brush(fg),
            HorizontalAlignment = HorizontalAlignment.Center,
        });
        var button = new Button
        {
            Content = stack, MinHeight = 64, MinWidth = 64, Padding = new Thickness(4, 8, 4, 6),
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

    /// <summary>A card (§6.4): ink 3, radius l, no border. Only for heterogeneous content; never a card in a card.</summary>
    public static Border Card(Control child, double padding = LunetTokens.Space.M) => new()
    {
        Background = LunetTokens.Brush(LunetTokens.Color.Surface),
        CornerRadius = new CornerRadius(LunetTokens.Radius.L),
        Padding = new Thickness(padding),
        Child = child,
    };

    /// <summary>A section header (§6.4): label 13/600 in capitals, ink 10, 24 above and 8 below.</summary>
    public static TextBlock Section(string text)
    {
        var t = Text(text.ToUpperInvariant(), LunetTokens.Type.Label, LunetTokens.Color.TextTertiary, FontWeight.SemiBold);
        t.LetterSpacing = 0.5;
        t.Margin = new Thickness(0, LunetTokens.Space.L, 0, LunetTokens.Space.Xs);
        return t;
    }

    /// <summary>
    /// A labelled field (§6.2): the label outside and above (13/500, ink 11), the control, and a hint or error below.
    /// Placeholders are examples, never the label.
    /// </summary>
    public static StackPanel Field(string label, Control input, string? hint = null)
    {
        var stack = new StackPanel { Spacing = 6 };
        stack.Children.Add(Text(label, LunetTokens.Type.Label, LunetTokens.Color.TextSecondary, FontWeight.Medium));
        if (input is TextBox box && string.IsNullOrEmpty(AutomationProperties.GetName(box))) AutomationProperties.SetName(box, label);
        stack.Children.Add(input);
        if (hint is not null) stack.Children.Add(Text(hint, LunetTokens.Type.Caption, LunetTokens.Color.TextTertiary));
        return stack;
    }

    /// <summary>
    /// A list row (§6.4): 40 dp leading tile, title 15/600 and subtitle 14 ink 11, optional trailing control; the whole
    /// row is the touch target, hover and press are overlays.
    /// </summary>
    public static Button Row(string title, string? subtitle, Control? leading = null, Control? trailing = null, Action? onClick = null)
    {
        var text = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        var t = Text(title, LunetTokens.Type.Callout, weight: FontWeight.SemiBold);
        t.TextTrimming = TextTrimming.CharacterEllipsis; t.TextWrapping = TextWrapping.NoWrap;
        text.Children.Add(t);
        if (!string.IsNullOrEmpty(subtitle))
        {
            var sub = Text(subtitle, LunetTokens.Type.BodySmall, LunetTokens.Color.TextSecondary);
            sub.TextTrimming = TextTrimming.CharacterEllipsis; sub.TextWrapping = TextWrapping.NoWrap;
            text.Children.Add(sub);
        }
        var dock = new DockPanel();
        if (leading is not null) { leading.Margin = new Thickness(0, 0, LunetTokens.Space.S, 0); DockPanel.SetDock(leading, Dock.Left); dock.Children.Add(leading); }
        if (trailing is not null) { trailing.Margin = new Thickness(LunetTokens.Space.S, 0, 0, 0); DockPanel.SetDock(trailing, Dock.Right); dock.Children.Add(trailing); }
        dock.Children.Add(text);
        var button = new Button
        {
            Content = dock, MinHeight = 56, Padding = new Thickness(LunetTokens.Space.S, LunetTokens.Space.Xs),
            HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Center, Background = LunetTokens.Brush("#00000000"),
            BorderThickness = new Thickness(0), CornerRadius = new CornerRadius(LunetTokens.Radius.M),
        };
        button.Classes.Add("lunet-row");
        AutomationProperties.SetName(button, string.IsNullOrEmpty(subtitle) ? title : $"{title}, {subtitle}");
        if (onClick is not null) button.Click += (_, _) => onClick();
        return button;
    }

    /// <summary>A 40 dp leading tile for a row: an icon on ink 4, radius s.</summary>
    public static Border Tile(string icon, string color = LunetTokens.Color.Primary) => new()
    {
        Width = 40, Height = 40, CornerRadius = new CornerRadius(LunetTokens.Radius.S),
        Background = LunetTokens.Brush(LunetTokens.Color.SurfaceRaised),
        Child = LunetIcons.Create(icon, color, 20),
    };

    /// <summary>A notice (status line in a page): ink 3, radius m, a 3 dp coloured lead and an icon.</summary>
    public static Border Notice(string text, string color)
    {
        var icon = color == LunetTokens.Color.Danger || color == LunetTokens.Color.Warning ? LunetIcons.Problems : LunetIcons.Check;
        var row = new DockPanel();
        var i = LunetIcons.Create(icon, color, 18);
        i.Margin = new Thickness(0, 1, LunetTokens.Space.S, 0); i.VerticalAlignment = VerticalAlignment.Top;
        DockPanel.SetDock(i, Dock.Left);
        row.Children.Add(i);
        row.Children.Add(Text(text, LunetTokens.Type.BodySmall));
        return new Border
        {
            Child = row, Padding = new Thickness(LunetTokens.Space.S, 10), Background = LunetTokens.Brush(LunetTokens.Color.Surface),
            CornerRadius = new CornerRadius(LunetTokens.Radius.M),
        };
    }

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
