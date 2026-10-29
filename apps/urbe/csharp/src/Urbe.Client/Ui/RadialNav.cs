using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Urbe.Client.Ui;

/// <summary>
/// CST-043: the phone's navigation, one round button in the bottom right corner in place of the tab bar (FeaturesCheck:
/// "substituir as abas por um menu radial no canto inferior direito"). A tap opens the items on a quarter arc, each
/// with its name above it (where the finger does not cover it); a press-drag-release picks the item the drag points to
/// without the menu stopping open (a marking menu). Voltar, Esc or a touch outside closes it.
/// </summary>
public sealed class RadialNav : Panel
{
    public sealed record Entry(string Label, string Icon, string Tip, Action Invoke);

    public const double TriggerSize = 56, Gutter = 16, Radius = 120, ItemWidth = 76, DeadZone = 24;
    private const double LabelHeight = 18, LabelGap = 4;

    private readonly List<(Entry Entry, Button Button, Border Circle, Point At)> _items = [];
    private readonly Border _badge, _glyph = new() { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    private string? _active;
    private int _highlighted = -1;
    private bool _dragging, _swallowClick, _keyboard;
    private Point _center;

    public Button Trigger { get; } = new();
    public Border Scrim { get; } = new() { IsVisible = false };
    public bool IsOpen { get; private set; }
    public IReadOnlyList<Button> Items => _items.Select(i => i.Button).ToList();
    public string? Highlighted => _highlighted < 0 ? null : _items[_highlighted].Entry.Label;
    public event Action<bool>? OpenChanged;

    public RadialNav(IReadOnlyList<Entry> entries)
    {
        Scrim.Background = UrbeTheme.Brush(UrbeTheme.Bg, .62);
        Scrim.PointerPressed += (_, e) => { Close(); e.Handled = true; };
        Children.Add(Scrim);

        var places = RadialLayout.Positions(entries.Count, Radius);
        for (int i = 0; i < entries.Count; i++) AddItem(entries[i], places[i]);

        Trigger.Width = Trigger.Height = TriggerSize;
        Trigger.Padding = new Thickness(0);
        Trigger.CornerRadius = new CornerRadius(TriggerSize / 2);
        Trigger.Background = UrbeTheme.Brush(UrbeTheme.Surface2, .96);
        Trigger.BorderBrush = UrbeTheme.Brush(UrbeTheme.Line2);
        Trigger.BorderThickness = new Thickness(1);
        Trigger.HorizontalAlignment = HorizontalAlignment.Right;
        Trigger.VerticalAlignment = VerticalAlignment.Bottom;
        Trigger.HorizontalContentAlignment = HorizontalAlignment.Center;
        Trigger.VerticalContentAlignment = VerticalAlignment.Center;
        Trigger.Margin = new Thickness(0, 0, Gutter, Gutter);
        _badge = new Border
        {
            Width = 10, Height = 10, CornerRadius = new CornerRadius(5), IsVisible = false,
            Background = UrbeTheme.Brush(UrbeTheme.Accent),
            HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 9, 9, 0), IsHitTestVisible = false
        };
        Trigger.Content = new Grid { Width = TriggerSize, Height = TriggerSize, Children = { _glyph, _badge } };
        Trigger.Click += (_, _) =>
        {
            if (_swallowClick) { _swallowClick = false; return; }
            if (IsOpen) Close(); else Open(focus: true);
        };
        Children.Add(UrbeMotion.Pressable(Trigger, haptic: true));
        PaintTrigger();

        // the marking gesture: the trigger keeps the pointer from the press to the release
        AddHandler(PointerPressedEvent, OnPress, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(PointerMovedEvent, OnMove, RoutingStrategies.Bubble, handledEventsToo: true);
        AddHandler(PointerReleasedEvent, OnRelease, RoutingStrategies.Tunnel, handledEventsToo: true);
        KeyDown += (_, e) => { if (e.Key == Key.Escape && IsOpen) { Close(); Trigger.Focus(NavigationMethod.Tab); e.Handled = true; } };
        // the focus ring only for whoever opened it from the keyboard
        Trigger.AddHandler(KeyDownEvent, (_, e) => _keyboard = e.Key is Key.Enter or Key.Space, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    private void AddItem(Entry entry, Point at)
    {
        var circle = new Border
        {
            Width = UrbeTokens.MinTouch, Height = UrbeTokens.MinTouch,
            CornerRadius = new CornerRadius(UrbeTokens.MinTouch / 2),
            BorderThickness = new Thickness(1),
            BoxShadow = UrbeTokens.Elevation.Mid,
            HorizontalAlignment = HorizontalAlignment.Center,
            RenderTransformOrigin = RelativePoint.Center
        };
        var label = new Border
        {
            Height = LabelHeight,
            Padding = new Thickness(6, 0),
            CornerRadius = new CornerRadius(LabelHeight / 2),
            Background = UrbeTheme.Brush(UrbeTheme.Surface2, .96),
            HorizontalAlignment = HorizontalAlignment.Center,
            Child = new TextBlock
            {
                Text = entry.Label, FontSize = UrbeTokens.Type.Caption.Size, FontWeight = FontWeight.SemiBold,
                Foreground = UrbeTheme.Brush(UrbeTheme.Text), VerticalAlignment = VerticalAlignment.Center
            }
        };
        var button = new Button
        {
            Width = ItemWidth,
            Padding = new Thickness(0),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            IsVisible = false,
            // the circle's centre on the arc: the trigger's centre is Gutter + TriggerSize/2 from the corner
            Margin = new Thickness(0, 0, Gutter + TriggerSize / 2 - at.X - ItemWidth / 2, Gutter + TriggerSize / 2 - at.Y - UrbeTokens.MinTouch / 2),
            Content = new StackPanel { Spacing = LabelGap, Children = { label, circle } }
        };
        Avalonia.Automation.AutomationProperties.SetName(button, entry.Label);
        ToolTip.SetTip(button, entry.Tip);
        int index = _items.Count;
        button.Click += (_, _) => Choose(index);
        _items.Add((entry, button, circle, at));
        Children.Add(UrbeMotion.Pressable(button));
        Paint(index);
    }

    /// <summary>The round part of an item (its touch target is at least 48 dp).</summary>
    public Border CircleOf(Button item) => _items.First(i => i.Button == item).Circle;

    public bool IsActive(string label) => _active == label;

    /// <summary>The destination on screen (Notas, Grafos or Assistente), marked in the menu.</summary>
    public void SetActive(string label)
    {
        if (_active == label) return;
        _active = label;
        for (int i = 0; i < _items.Count; i++) Paint(i);
    }

    /// <summary>The Assistente waits for an answer: a dot on the button, and its name says so.</summary>
    public void SetAttention(bool attention)
    {
        _badge.IsVisible = attention;
        PaintTrigger();
    }

    public void Open() => Open(focus: true);

    private void Open(bool focus)
    {
        if (IsOpen) return;
        IsOpen = true;
        Scrim.IsVisible = true;
        UrbeMotion.Enter(Scrim, 0, 0, UrbeTokens.Motion.Fast);
        foreach (var (_, button, _, at) in _items)
        {
            button.IsVisible = true;
            // out of the trigger, along the arc
            UrbeMotion.Enter(button, -at.X * .45, -at.Y * .45, UrbeTokens.Motion.Fast);
        }
        PaintTrigger();
        if (focus && _items.Count > 0) _items[0].Button.Focus(_keyboard ? NavigationMethod.Tab : NavigationMethod.Unspecified);
        OpenChanged?.Invoke(true);
    }

    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;
        _dragging = false;
        Highlight(-1);
        Scrim.IsVisible = false;
        foreach (var item in _items) item.Button.IsVisible = false;
        PaintTrigger();
        OpenChanged?.Invoke(false);
    }

    /// <summary>Android Voltar: an open menu closes first.</summary>
    public bool HandleBack()
    {
        if (!IsOpen) return false;
        Close();
        return true;
    }

    private void Choose(int index)
    {
        Close();
        _items[index].Entry.Invoke();
    }

    private void OnPress(object? sender, PointerPressedEventArgs e)
    {
        _swallowClick = false;
        _dragging = false;
        _keyboard = false;
        if (!IsFromTrigger(e.Source)) return;
        _center = Trigger.TranslatePoint(new Point(TriggerSize / 2, TriggerSize / 2), this) ?? default;
        e.Pointer.Capture(Trigger);
    }

    private void OnMove(object? sender, PointerEventArgs e)
    {
        if (e.Pointer.Captured != Trigger) return;
        var p = e.GetPosition(this) - _center;
        if (!_dragging)
        {
            if (p.X * p.X + p.Y * p.Y < DeadZone * DeadZone) return;
            _dragging = true;
            Open(focus: false);
        }
        // far past the arc is a way out: nothing is picked
        Highlight(RadialLayout.Pick(p.X, p.Y, _items.Count, DeadZone, Radius * 2));
    }

    private void OnRelease(object? sender, PointerReleasedEventArgs e)
    {
        if (!_dragging) return;
        _dragging = false;
        _swallowClick = true;   // the release over the trigger must not open the menu again
        int pick = _highlighted;
        e.Handled = true;
        e.Pointer.Capture(null);
        if (pick >= 0) Choose(pick); else Close();
    }

    private bool IsFromTrigger(object? source) =>
        source is Visual v && (v == Trigger || Trigger.IsVisualAncestorOf(v));

    private void Highlight(int index)
    {
        if (index == _highlighted) return;
        int old = _highlighted;
        _highlighted = index;
        if (old >= 0) Paint(old);
        if (index >= 0) { Paint(index); PlatformHaptics.Tick(); }
    }

    private void Paint(int index)
    {
        var (entry, _, circle, _) = _items[index];
        bool active = entry.Label == _active, hot = index == _highlighted;
        circle.Background = UrbeTheme.Brush(hot ? UrbeTheme.Accent : UrbeTheme.Surface2, hot ? 1 : .98);
        circle.BorderBrush = UrbeTheme.Brush(active || hot ? UrbeTheme.Accent : UrbeTheme.Line2);
        circle.RenderTransform = hot ? new ScaleTransform(1.12, 1.12) : null;
        circle.Child = UrbeTheme.Icon(entry.Icon, 21, hot ? UrbeTheme.Bg : active ? UrbeTheme.Accent : UrbeTheme.Text2, 1.8);
    }

    private void PaintTrigger()
    {
        _glyph.Child = UrbeTheme.Icon(IsOpen ? UrbeTheme.Icons.CloseUi : UrbeTheme.Icons.Radial, 22, UrbeTheme.Accent, 2);
        Trigger.BorderBrush = UrbeTheme.Brush(_badge.IsVisible ? UrbeTheme.Accent : UrbeTheme.Line2);
        Avalonia.Automation.AutomationProperties.SetName(Trigger, IsOpen ? "Fechar menu"
            : _badge.IsVisible ? "Menu: o Assistente aguarda sua resposta"
            : "Menu: " + Join(_items.Select(i => i.Entry.Label).ToList()));
        ToolTip.SetTip(Trigger, IsOpen ? "Fechar" : "Navegar");
    }

    private static string Join(IReadOnlyList<string> labels) =>
        labels.Count <= 1 ? string.Concat(labels) : string.Join(", ", labels.Take(labels.Count - 1)) + " e " + labels[^1];
}
