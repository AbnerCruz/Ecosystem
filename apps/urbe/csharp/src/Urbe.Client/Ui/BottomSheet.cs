using Avalonia;
using Avalonia.Animation;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Transformation;
using C = Urbe.Client.UrbeTokens.Color;
using M = Urbe.Client.UrbeTokens.Motion;

namespace Urbe.Client.Ui;

/// <summary>
/// A bottom sheet (UX.md P4, P6, P8): over a scrim, it slides up from the bottom with a spring-like ease, carries a
/// drag handle, follows the finger when dragged down and closes past a third of its height (or a quick flick); the
/// scrim, Escape and the handle (a button too, for who does not swipe) close it. Centred card from 720 dp.
/// </summary>
public sealed class BottomSheet : Grid
{
    private const double Wide = 720;
    private readonly Border _card = new();
    private readonly Button _handle;
    private readonly ContentControl _body = new();
    private Point? _dragFrom;
    private double _drag;
    private DateTime _dragAt;
    private int _openGen;
    private bool _dragged;

    public event Action? Closed;

    public BottomSheet()
    {
        IsVisible = false;
        Background = UrbeTheme.Brush(C.Scrim);
        _card.Background = UrbeTheme.Brush(C.SurfaceRaised);
        _card.BorderBrush = UrbeTheme.Brush(C.BorderStrong);
        _card.BoxShadow = UrbeTokens.Elevation.High;
        _handle = new Button
        {
            // the visible grabber is 40×4; the touch area is 48 dp tall across the middle
            Content = new Border { Width = 40, Height = 4, CornerRadius = new CornerRadius(2), Background = UrbeTheme.Brush(C.BorderStrong) },
            Height = UrbeTokens.MinTouch, Width = 96, Background = Brushes.Transparent, BorderThickness = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Center, HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, -UrbeTokens.Space.Xs, 0, 0)
        };
        _handle.Resources["ButtonBackgroundPointerOver"] = Brushes.Transparent;
        _handle.Resources["ButtonBackgroundPressed"] = Brushes.Transparent;
        AutomationProperties.SetName(_handle, "Fechar");
        ToolTip.SetTip(_handle, "Arraste para baixo ou toque para fechar");
        _handle.Click += (_, _) => { if (!_dragged) _ = CloseAsync(); };
        _card.Child = new DockPanel { Children = { _handle, _body } };
        DockPanel.SetDock(_handle, Dock.Top);
        Children.Add(_card);
        PointerPressed += (_, e) => { if (e.Source == this) _ = CloseAsync(); };
        KeyDown += (_, e) => { if (e.Key == Key.Escape) { e.Handled = true; _ = CloseAsync(); } };
        Focusable = true;
        // swipe down on the handle or the card's top strip
        _card.AddHandler(PointerPressedEvent, OnDragStart, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        _card.AddHandler(PointerMovedEvent, OnDragMove, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        _card.AddHandler(PointerReleasedEvent, OnDragEnd, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        _card.AddHandler(PointerCaptureLostEvent, (_, _) => Settle(), Avalonia.Interactivity.RoutingStrategies.Bubble);
    }

    /// <summary>The card (tests, screenshots).</summary>
    public Border Card => _card;

    /// <summary>The drag handle (a button: tap closes).</summary>
    public Button Handle => _handle;

    public bool IsOpen { get; private set; }

    /// <summary>How far down the sheet is dragged now (tests).</summary>
    public double DragOffset => _drag;

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        Place(e.NewSize.Width, e.NewSize.Height);
    }

    private void Place(double width, double height)
    {
        bool wide = width >= Wide;
        _card.HorizontalAlignment = wide ? HorizontalAlignment.Center : HorizontalAlignment.Stretch;
        _card.VerticalAlignment = wide ? VerticalAlignment.Center : VerticalAlignment.Bottom;
        _card.Width = wide ? Math.Min(480, Math.Max(0, width - 48)) : double.NaN;
        _card.CornerRadius = wide ? new CornerRadius(UrbeTokens.Radius.Xl) : UrbeTokens.Radius.Top(UrbeTokens.Radius.Xl);
        _card.BorderThickness = wide ? new Thickness(1) : new Thickness(0, 1, 0, 0);
        _card.Padding = new Thickness(UrbeTokens.Space.L - 4, UrbeTokens.Space.Xs, UrbeTokens.Space.L - 4, UrbeTokens.Space.L - 4);
        _card.MaxHeight = Math.Max(200, height * .88);
    }

    /// <summary>Opens the sheet with <paramref name="content"/>: visible now, sliding up for <see cref="M.Slow"/>.</summary>
    public void Open(Control content)
    {
        _openGen++;
        _body.Content = content;
        _drag = 0;
        IsVisible = true;
        IsOpen = true;
        Place(Bounds.Width, Bounds.Height);
        double from = Math.Max(240, Bounds.Height * .5);
        if (M.Reduced) { _card.Transitions = null; _card.RenderTransform = null; Transitions = null; Opacity = 1; }
        else
        {
            UrbeMotion.Enter(this, 0, 0, M.Fast);                       // the scrim fades in
            _card.Transitions = null;
            _card.RenderTransform = TransformOperations.Parse(FormattableString.Invariant($"translate(0px, {from}px)"));
            _card.Transitions = [new TransformOperationsTransition { Property = RenderTransformProperty, Duration = M.Slow, Easing = M.Spring }];
            _card.RenderTransform = TransformOperations.Parse("translate(0px, 0px)");
        }
        Focus();
    }

    /// <summary>Closes the sheet: it slides down and fades, then hides (IsOpen is false at once).</summary>
    public async Task CloseAsync()
    {
        if (!IsOpen) return;
        IsOpen = false;
        int gen = ++_openGen;
        if (!M.Reduced)
        {
            double to = Math.Max(_card.Bounds.Height, 240);
            _card.Transitions = [new TransformOperationsTransition { Property = RenderTransformProperty, Duration = M.Normal, Easing = M.EaseIn }];
            _card.RenderTransform = TransformOperations.Parse(FormattableString.Invariant($"translate(0px, {to}px)"));
        }
        await UrbeMotion.HideAsync(this, 0, 0, M.Normal);
        if (gen != _openGen) return;
        _body.Content = null;
        _card.Transitions = null;
        _card.RenderTransform = null;
        Closed?.Invoke();
    }

    // ---------------------------------------------------------------- swipe down to dismiss

    private void OnDragStart(object? sender, PointerPressedEventArgs e)
    {
        var p = e.GetPosition(_card);
        // only the top strip (handle) starts a drag, so the content keeps its own scroll and taps
        if (p.Y > UrbeTokens.MinTouch + UrbeTokens.Space.Xs) return;
        _dragFrom = e.GetPosition(this);
        _drag = 0;
        _dragged = false;
        _dragAt = DateTime.UtcNow;
        _card.Transitions = null;
    }

    private void OnDragMove(object? sender, PointerEventArgs e)
    {
        if (_dragFrom is not { } from) return;
        _drag = Math.Max(0, e.GetPosition(this).Y - from.Y);
        if (_drag > 4 && !_dragged) { _dragged = true; e.Pointer.Capture(_card); }
        _card.RenderTransform = TransformOperations.Parse(FormattableString.Invariant($"translate(0px, {_drag}px)"));
    }

    private void OnDragEnd(object? sender, PointerReleasedEventArgs e)
    {
        if (_dragFrom is null) return;
        _dragFrom = null;
        var ms = Math.Max(1, (DateTime.UtcNow - _dragAt).TotalMilliseconds);
        bool flick = _drag > 24 && _drag / ms > .5;   // > 0.5 dp/ms
        if (_drag > _card.Bounds.Height / 3 || flick)
        {
            e.Handled = true;
            PlatformHaptics.Confirm();
            _ = CloseAsync();
            return;
        }
        if (_drag > 4) e.Handled = true;
        Settle();
    }

    /// <summary>Springs back into place after a short drag.</summary>
    private void Settle()
    {
        _dragFrom = null;
        if (!IsOpen) return;
        _card.Transitions = M.Reduced ? null : [new TransformOperationsTransition { Property = RenderTransformProperty, Duration = M.Normal, Easing = M.Spring }];
        _card.RenderTransform = TransformOperations.Parse("translate(0px, 0px)");
        _drag = 0;
    }
}
