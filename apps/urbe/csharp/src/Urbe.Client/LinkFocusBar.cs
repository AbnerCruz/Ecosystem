using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Urbe.Client.Ui;
using Urbe.Client.World;
using Urbe.Core;
using C = Urbe.Client.UrbeTokens.Color;

namespace Urbe.Client;

/// <summary>
/// UC-70: the discreet card at the bottom of the links focus. It names the focused note (or the linked one tapped),
/// counts its links — cita · citada por · mútua, each with its arrow icon, not by colour alone (P12) — and offers
/// "Abrir" and "Detalhes" (the 1.8.4 house card), or "Abrir" and "Focar" for a linked note.
/// </summary>
public sealed class LinkFocusBar : Border
{
    private readonly Image _art = new() { Width = 40, Height = 46, Stretch = Stretch.Uniform };
    private readonly TextBlock _title = UrbeTokens.Type.BodyStrong.Apply(new TextBlock());
    private readonly TextBlock _subtitle = UrbeTokens.Type.Footnote.Apply(new TextBlock());
    private readonly Border _subtitleIcon = new();
    private readonly (Border Chip, TextBlock Text)[] _chips;
    private readonly UrbeButton _open = new("Abrir", ButtonKind.Primary, UrbeTheme.Icons.Open);
    private readonly UrbeButton _second = new("Detalhes", ButtonKind.Secondary);
    private readonly UrbeIconButton _close = new(UrbeTheme.Icons.CloseUi, "Sair do destaque", 20);
    private LinkFocus? _focus;
    private LegacyCityBuilding? _peeked;

    public LinkFocusBar()
    {
        RenderOptions.SetBitmapInterpolationMode(_art, BitmapInterpolationMode.None);
        _title.Foreground = UrbeTheme.Brush(C.TextPrimary);
        _title.TextTrimming = TextTrimming.CharacterEllipsis;
        _subtitle.Foreground = UrbeTheme.Brush(C.TextSecondary);
        _subtitle.TextTrimming = TextTrimming.CharacterEllipsis;
        _subtitle.VerticalAlignment = VerticalAlignment.Center;
        _subtitleIcon.VerticalAlignment = VerticalAlignment.Center;
        _subtitleIcon.Margin = new Thickness(0, 0, 6, 0);
        var subtitle = new DockPanel { Children = { _subtitleIcon, _subtitle } };
        DockPanel.SetDock(_subtitleIcon, Dock.Left);
        var text = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(UrbeTokens.Space.S, 0, 0, 0), Children = { _title, subtitle } };
        var artFrame = new Border
        {
            Width = 52, Height = 52, CornerRadius = new CornerRadius(UrbeTokens.Radius.M),
            Background = UrbeTheme.Brush(WorldView.FocusColors.Focus, .14),
            BorderBrush = UrbeTheme.Brush(WorldView.FocusColors.Focus, .45), BorderThickness = new Thickness(1),
            Child = _art
        };
        var head = new DockPanel();
        DockPanel.SetDock(artFrame, Dock.Left);
        DockPanel.SetDock(_close, Dock.Right);
        _close.VerticalAlignment = VerticalAlignment.Top;
        _close.Margin = new Thickness(0, -6, -10, 0);
        head.Children.Add(artFrame);
        head.Children.Add(_close);
        head.Children.Add(text);

        _chips = [Chip(LinkGroup.Outgoing), Chip(LinkGroup.Incoming), Chip(LinkGroup.Mutual)];
        var legend = new WrapPanel { Orientation = Orientation.Horizontal, ItemSpacing = UrbeTokens.Space.Xs, LineSpacing = UrbeTokens.Space.Xs };
        foreach (var (chip, _) in _chips) legend.Children.Add(chip);
        AutomationProperties.SetName(legend, "Ligações");

        var actions = new Grid { ColumnDefinitions = new ColumnDefinitions("*,8,*") };
        _open.HorizontalAlignment = _second.HorizontalAlignment = HorizontalAlignment.Stretch;
        Grid.SetColumn(_second, 2);
        actions.Children.Add(_open);
        actions.Children.Add(_second);

        _open.Click += (_, _) => { if (Target is { } b) OpenRequested?.Invoke(b); };
        _second.Click += (_, _) =>
        {
            if (_peeked is { } p) FocusRequested?.Invoke(p);
            else if (_focus is { } f) DetailsRequested?.Invoke(f.Focus);
        };
        _close.Click += (_, _) => CloseRequested?.Invoke();

        Child = new StackPanel { Spacing = UrbeTokens.Space.S, Children = { head, legend, actions } };
        Background = UrbeTheme.Brush(C.Surface, .94);
        BorderBrush = UrbeTheme.Brush(WorldView.FocusColors.Focus, .18);
        BorderThickness = new Thickness(1);
        BoxShadow = UrbeTokens.Elevation.High;
        CornerRadius = new CornerRadius(UrbeTokens.Radius.Xl);
        Padding = new Thickness(UrbeTokens.Space.M, UrbeTokens.Space.M, UrbeTokens.Space.M, UrbeTokens.Space.M);
        Margin = new Thickness(UrbeTokens.Space.S, 0, UrbeTokens.Space.S, UrbeTokens.Space.S);
        VerticalAlignment = VerticalAlignment.Bottom;
        HorizontalAlignment = HorizontalAlignment.Stretch;
        MaxWidth = 460;
        IsVisible = false;
        AutomationProperties.SetName(this, "Ligações da nota");
    }

    /// <summary>"Abrir": the note shown (the linked one tapped, else the focus).</summary>
    public event Action<LegacyCityBuilding>? OpenRequested;
    /// <summary>"Detalhes": the 1.8.4 house card of the focus.</summary>
    public event Action<LegacyCityBuilding>? DetailsRequested;
    /// <summary>"Focar": the linked note becomes the focus.</summary>
    public event Action<LegacyCityBuilding>? FocusRequested;
    /// <summary>✕: leave the focus.</summary>
    public event Action? CloseRequested;

    /// <summary>The note the card is about now.</summary>
    public LegacyCityBuilding? Target => _peeked ?? _focus?.Focus;

    /// <summary>The legend as read ("cita 1", "citada por 2", "mútua 0"), for tests and screen readers.</summary>
    public IReadOnlyList<string> Legend => _chips.Select(c => c.Text.Text ?? "").ToList();

    public string Title => _title.Text ?? "";
    public string Subtitle => _subtitle.Text ?? "";
    public string SecondAction => _second.Content is string s ? s : (string?)AutomationProperties.GetName(_second) ?? "";

    /// <summary>Shows the focus (null hides the card) with the art of the building.</summary>
    public void Show(LinkFocus? focus, LegacyCityBuilding? peeked, Func<LegacyCityBuilding, Bitmap?> art)
    {
        bool was = IsVisible;
        _focus = focus;
        _peeked = focus is null ? null : peeked;
        if (focus is null) { IsVisible = false; return; }
        var target = _peeked ?? focus.Focus;
        _title.Text = target.Name;
        _art.Source = art(target);
        var group = focus.GroupOf(target) ?? LinkGroup.Focus;
        string focusName = focus.Focus.Name;
        _subtitle.Text = group switch
        {
            LinkGroup.Outgoing => "Citada por " + focusName,
            LinkGroup.Incoming => "Cita " + focusName,
            LinkGroup.Mutual => "Ligação mútua com " + focusName,
            _ => Total(focus) == 0 ? "Nenhuma ligação ainda" : "Toque numa ligada para ver o nome"
        };
        _subtitleIcon.Child = group == LinkGroup.Focus ? null : Badge(group, 18);
        _subtitleIcon.IsVisible = group != LinkGroup.Focus;
        SetChip(0, "cita", focus.Outgoing.Count);
        SetChip(1, "citada por", focus.Incoming.Count);
        SetChip(2, "mútua", focus.Mutual.Count);
        string second = _peeked is null ? "Detalhes" : "Focar";
        _second.Content = second;
        AutomationProperties.SetName(_second, second);
        AutomationProperties.SetName(_open, "Abrir " + target.Name);
        IsVisible = true;
        if (!was) UrbeMotion.Enter(this, dy: 16);
    }

    private static int Total(LinkFocus f) => f.Outgoing.Count + f.Incoming.Count + f.Mutual.Count;

    private void SetChip(int i, string label, int n)
    {
        var (chip, text) = _chips[i];
        text.Text = label + " " + n;
        chip.Opacity = n == 0 ? .5 : 1;
        AutomationProperties.SetName(chip, label + " " + n);
    }

    private static (Border, TextBlock) Chip(LinkGroup g)
    {
        var text = UrbeTokens.Type.Label.Apply(new TextBlock { VerticalAlignment = VerticalAlignment.Center, Foreground = UrbeTheme.Brush(C.TextPrimary) });
        var chip = new Border
        {
            Background = UrbeTheme.Brush(WorldView.FocusColors.Of(g), .12),
            BorderBrush = UrbeTheme.Brush(WorldView.FocusColors.Of(g), .35), BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(UrbeTokens.Radius.Pill),
            Padding = new Thickness(4, 4, 12, 4),
            Child = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { Badge(g, 22), text } }
        };
        return (chip, text);
    }

    /// <summary>The round arrow badge of a group, as drawn over the buildings in the city.</summary>
    public static Control Badge(LinkGroup g, double size) => new Border
    {
        Width = size, Height = size, CornerRadius = new CornerRadius(size / 2),
        Background = UrbeTheme.Brush(WorldView.FocusColors.Of(g)),
        Child = new Viewbox
        {
            Width = size * .72, Height = size * .72,
            Child = new Avalonia.Controls.Shapes.Path
            {
                Data = Geometry.Parse(WorldView.FocusIcons.Of(g)), Stroke = UrbeTheme.Brush("#1a1209"), StrokeThickness = 2.6,
                StrokeLineCap = PenLineCap.Round, StrokeJoin = PenLineJoin.Round, Width = 24, Height = 24, Stretch = Stretch.None
            }
        }
    };
}
