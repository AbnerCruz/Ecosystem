using Avalonia;
using Avalonia.Animation;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using C = Urbe.Client.UrbeTokens.Color;

namespace Urbe.Client.Ui;

/// <summary>The tone of a toast: a plain notice, something done (a warm check and a confirm haptic) or a caution.</summary>
public enum ToastKind { Info, Success, Warning }

/// <summary>
/// A discreet notice (UX.md P3): a pill that fades and slides in, never takes the touch (unless it offers an action),
/// never waits for an answer and goes away by itself. Screen readers hear it (a polite live region).
/// </summary>
public sealed class UrbeToast : Border
{
    private readonly TextBlock _text = new();
    private readonly DockPanel _row = new();
    private Button? _action;
    private DispatcherTimer? _timer;
    private readonly double _shownTop, _hiddenTop;
    private readonly Transitions _transitions;
    private readonly Border _badge = new();

    /// <param name="top">The distance from the top while shown (it slides 8 dp down into it).</param>
    /// <param name="fade">The fade/slide duration (default <see cref="UrbeTokens.Motion.Normal"/>).</param>
    public UrbeToast(double top = 70, TimeSpan? fade = null)
    {
        _shownTop = top;
        _hiddenTop = top - 8;
        _text.Foreground = UrbeTheme.Brush(C.OnToast);
        _text.FontFamily = UrbeTheme.UiFont;
        _text.FontWeight = FontWeight.SemiBold;
        _text.FontSize = UrbeTokens.Type.Footnote.Size;
        _text.TextWrapping = TextWrapping.Wrap;
        _text.MaxLines = 3;
        _text.TextTrimming = TextTrimming.CharacterEllipsis;
        _text.VerticalAlignment = VerticalAlignment.Center;
        // the badge of a Success/Warning toast: a small round mark in the app's palette, springing in (warmth, UC-69)
        _badge.Width = _badge.Height = 18;
        _badge.CornerRadius = new CornerRadius(9);
        _badge.Margin = new Thickness(-4, 0, 8, 0);
        _badge.VerticalAlignment = VerticalAlignment.Center;
        _badge.IsVisible = false;
        _badge.RenderTransformOrigin = RelativePoint.Center;
        DockPanel.SetDock(_badge, Dock.Left);
        _row.Children.Add(_badge);
        _row.Children.Add(_text);
        Child = _row;
        Background = UrbeTheme.Brush(C.Toast);
        CornerRadius = new CornerRadius(UrbeTokens.Radius.Xl);
        Padding = new Thickness(14, 7);
        HorizontalAlignment = HorizontalAlignment.Center;
        VerticalAlignment = VerticalAlignment.Top;
        Margin = new Thickness(0, _hiddenTop, 0, 0);
        IsHitTestVisible = false;
        Opacity = 0;
        BoxShadow = UrbeTokens.Elevation.Mid;
        var time = fade ?? UrbeTokens.Motion.Normal;
        _transitions =
        [
            new DoubleTransition { Property = OpacityProperty, Duration = time, Easing = UrbeTokens.Motion.EaseOut },
            new ThicknessTransition { Property = MarginProperty, Duration = time, Easing = UrbeTokens.Motion.EaseOut }
        ];
        AutomationProperties.SetLiveSetting(this, AutomationLiveSetting.Polite);
    }

    /// <summary>The text shown now, or null when hidden.</summary>
    public string? Text => IsShown ? _text.Text : null;

    public bool IsShown { get; private set; }

    /// <summary>The action of the notice shown now (tests), or null.</summary>
    public string? ActionText => IsShown ? _action?.Content as string : null;

    /// <summary>
    /// Shows <paramref name="text"/> for <paramref name="duration"/> (default 4.5 s, the 1.8.4 notice), replacing the
    /// one shown. An optional action (e.g. "Desfazer") makes the pill touchable.
    /// </summary>
    public void Show(string text, TimeSpan? duration = null, string? action = null, Action? onAction = null, ToastKind kind = ToastKind.Info)
    {
        _text.Text = text;
        Kind = kind;
        ShowBadge(kind);
        if (kind == ToastKind.Success) PlatformHaptics.Confirm();
        AutomationProperties.SetName(this, text);
        if (_action is not null) { _row.Children.Remove(_action); _action = null; }
        if (action is not null && onAction is not null)
        {
            _action = new Button
            {
                Content = action, Background = Brushes.Transparent, Foreground = UrbeTheme.Brush(C.Primary), FontWeight = FontWeight.Bold,
                FontSize = UrbeTokens.Type.Footnote.Size, MinHeight = 36, Padding = new Thickness(12, 0), Margin = new Thickness(8, -6, -10, -6),
                VerticalContentAlignment = VerticalAlignment.Center
            };
            AutomationProperties.SetName(_action, action);
            _action.Click += (_, _) => { onAction(); Hide(); };
            DockPanel.SetDock(_action, Dock.Right);
            _row.Children.Insert(0, _action);
        }
        IsHitTestVisible = _action is not null;
        Transitions = UrbeTokens.Motion.Reduced ? null : _transitions;
        if (Parent is Control host) MaxWidth = Math.Max(120, host.Bounds.Width * .8);
        Opacity = 1;
        Margin = new Thickness(0, _shownTop, 0, 0);
        IsShown = true;
        _timer?.Stop();
        _timer = new DispatcherTimer { Interval = duration ?? TimeSpan.FromSeconds(4.5) };
        _timer.Tick += (_, _) => Hide();
        _timer.Start();
    }

    /// <summary>The tone of the toast shown last.</summary>
    public ToastKind Kind { get; private set; }

    private void ShowBadge(ToastKind kind)
    {
        _badge.IsVisible = kind != ToastKind.Info;
        if (kind == ToastKind.Info) return;
        string color = kind == ToastKind.Success ? C.Success : C.Warning;
        _badge.Background = UrbeTheme.Brush(color, .18);
        _badge.Child = new Viewbox { Width = 12, Height = 12, Child = new Avalonia.Controls.Shapes.Path
        {
            Data = Geometry.Parse(kind == ToastKind.Success ? UrbeTheme.Icons.Check : "M12 5v8 M12 18v.5"),
            Stroke = UrbeTheme.Brush(color), StrokeThickness = 3, StrokeLineCap = PenLineCap.Round, StrokeJoin = PenLineJoin.Round,
            Width = 24, Height = 24, Stretch = Stretch.None
        } };
        _badge.Transitions = null;
        if (UrbeTokens.Motion.Reduced) { _badge.RenderTransform = null; return; }
        _badge.RenderTransform = Avalonia.Media.Transformation.TransformOperations.Parse("scale(0.4)");
        _badge.Transitions = [new TransformOperationsTransition { Property = RenderTransformProperty, Duration = UrbeTokens.Motion.Slow, Easing = UrbeTokens.Motion.Spring }];
        _badge.RenderTransform = Avalonia.Media.Transformation.TransformOperations.Parse("scale(1)");
    }

    /// <summary>Fades the notice away now.</summary>
    public void Hide()
    {
        _timer?.Stop();
        Transitions = UrbeTokens.Motion.Reduced ? null : _transitions;
        Opacity = 0;
        Margin = new Thickness(0, _hiddenTop, 0, 0);
        IsShown = false;
        IsHitTestVisible = false;
    }
}
