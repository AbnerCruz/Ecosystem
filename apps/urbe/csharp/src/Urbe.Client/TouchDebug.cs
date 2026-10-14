using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Urbe.Client;

/// <summary>
/// "Diagnóstico de toques" (src/ui/touch-debug.js): for each touch, where the finger fell, which control was there,
/// whether the touch was cancelled and where the click went. A green dot = the click reached the control touched; red =
/// it did not. "Copiar relatório" makes a text for whoever is fixing it. On or off is kept on this device.
/// </summary>
public sealed class TouchDebug : Grid
{
    private const int MaxLog = 60;
    private sealed class Touch
    {
        public DateTime T0;
        public Point At;
        public string Type = "";
        public string Target = "", Control = "";
        public Button? Element;
        public double Moved;
        public bool Cancelled;
        public string? ClickTarget;
        public bool ClickSame;
        public double Duration;
    }

    private readonly List<(string Time, string Line, bool Ok, Point At, string Target)> _log = [];
    private readonly Canvas _dots = new() { IsHitTestVisible = false };
    private readonly Border _panel = new();
    private readonly StackPanel _lines = new() { Margin = new Thickness(10, 6, 10, 8), Spacing = 2 };
    private readonly TextBlock _flash = new() { FontSize = 12, Foreground = new SolidColorBrush(Color.Parse("#7fdca6")), VerticalAlignment = VerticalAlignment.Center };
    private readonly Control _host;
    private Touch? _cur;

    public TouchDebug(Control host)
    {
        _host = host;
        IsVisible = false;
        IsHitTestVisible = true;
        Background = null;
        Children.Add(_dots);
        var copy = HeadButton("Copiar relatório");
        copy.Click += async (_, _) => await CopyAsync();
        var min = HeadButton("–");
        ToolTip.SetTip(min, "Minimizar");
        min.Click += (_, _) => _lines.IsVisible = !_lines.IsVisible;
        var off = HeadButton("×");
        ToolTip.SetTip(off, "Desligar");
        off.Click += (_, _) => TurnedOff?.Invoke();
        var head = new DockPanel { Margin = new Thickness(10, 6, 6, 6) };
        foreach (var b in new[] { off, min, copy }) { DockPanel.SetDock(b, Dock.Right); b.Margin = new Thickness(6, 0, 0, 0); head.Children.Add(b); }
        DockPanel.SetDock(_flash, Dock.Right);
        head.Children.Add(_flash);
        head.Children.Add(new TextBlock { Text = "Diagnóstico de toques", FontSize = 12.5, FontWeight = FontWeight.SemiBold, Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center });
        _panel.Child = new StackPanel { Children = { head, new Border { Height = 1, Background = new SolidColorBrush(Color.Parse("#2a2c33")) }, _lines } };
        _panel.Background = new SolidColorBrush(Color.Parse("#F00C0D10"));
        _panel.BorderBrush = new SolidColorBrush(Color.Parse("#3a3d46"));
        _panel.BorderThickness = new Thickness(1);
        _panel.CornerRadius = new CornerRadius(12);
        _panel.Margin = new Thickness(8);
        _panel.VerticalAlignment = VerticalAlignment.Top;
        _panel.BoxShadow = BoxShadows.Parse("0 10 30 0 #80000000");
        Children.Add(_panel);
        Render();
        // captured before everything (tunnel) and seen after everything (handled too)
        host.AddHandler(PointerPressedEvent, Pressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        host.AddHandler(PointerMovedEvent, Moved, RoutingStrategies.Tunnel, handledEventsToo: true);
        host.AddHandler(PointerReleasedEvent, Released, RoutingStrategies.Tunnel, handledEventsToo: true);
        host.AddHandler(PointerCaptureLostEvent, CaptureLost, RoutingStrategies.Bubble | RoutingStrategies.Direct, handledEventsToo: true);
        host.AddHandler(Button.ClickEvent, Clicked, RoutingStrategies.Bubble, handledEventsToo: true);
    }

    /// <summary>The × of the panel.</summary>
    public event Action? TurnedOff;

    /// <summary>The lines registered (tests): "✓ Button “Buscar” · 80ms".</summary>
    public IReadOnlyList<string> Lines => _log.Select(l => l.Line).ToList();

    /// <summary>The app's version for the report.</summary>
    public string Version { get; set; } = "";

    private static Button HeadButton(string text) => new()
    {
        Content = text, MinHeight = 32, Padding = new Thickness(10, 0), CornerRadius = new CornerRadius(8), FontSize = 12, FontWeight = FontWeight.SemiBold,
        Background = new SolidColorBrush(Color.Parse("#1e2026")), Foreground = Brushes.White, BorderBrush = new SolidColorBrush(Color.Parse("#3a3d46")), BorderThickness = new Thickness(1),
        VerticalContentAlignment = VerticalAlignment.Center
    };

    private bool OnPanel(object? source) => source is Visual v && (v == _panel || _panel.IsVisualAncestorOf(v));

    /// <summary>desc(el): type, name and a label (automation name, tooltip or text) of up to 24 characters.</summary>
    private static string Describe(object? o)
    {
        if (o is not Control c) return o?.GetType().Name ?? "—";
        var s = c.GetType().Name + (string.IsNullOrEmpty(c.Name) ? "" : "#" + c.Name);
        var label = Avalonia.Automation.AutomationProperties.GetName(c);
        if (string.IsNullOrWhiteSpace(label)) label = ToolTip.GetTip(c) as string;
        if (string.IsNullOrWhiteSpace(label))
            label = c is ContentControl { Content: string t } ? t
                : c.GetVisualDescendants().OfType<TextBlock>().Select(x => x.Text).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
        label = System.Text.RegularExpressions.Regex.Replace((label ?? "").Trim(), @"\s+", " ");
        if (label.Length > 24) label = label[..24];
        return s + (label.Length > 0 ? " “" + label + "”" : "");
    }

    private void Pressed(object? sender, PointerPressedEventArgs e)
    {
        if (!IsVisible || OnPanel(e.Source)) return;
        var control = (e.Source as Visual)?.GetSelfAndVisualAncestors().OfType<Button>().FirstOrDefault();
        _cur = new Touch
        {
            T0 = DateTime.UtcNow, At = e.GetPosition(this), Type = e.Pointer.Type.ToString().ToLowerInvariant(), Target = Describe(e.Source),
            Control = control is not null ? Describe(control) : "(nenhum controle)", Element = control
        };
    }

    private void Moved(object? sender, PointerEventArgs e)
    {
        if (_cur is not { } c) return;
        var p = e.GetPosition(this);
        c.Moved = Math.Max(c.Moved, Math.Round(Math.Sqrt(Math.Pow(p.X - c.At.X, 2) + Math.Pow(p.Y - c.At.Y, 2))));
    }

    private void CaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        if (_cur is { } c && c.Duration == 0) c.Cancelled = true;
    }

    private void Released(object? sender, PointerReleasedEventArgs e)
    {
        if (_cur is not { } c) return;
        c.Duration = Math.Max(1, Math.Round((DateTime.UtcNow - c.T0).TotalMilliseconds));
        DispatcherTimer.RunOnce(() => Finish(c), TimeSpan.FromMilliseconds(450));
    }

    private void Clicked(object? sender, RoutedEventArgs e)
    {
        if (!IsVisible || _cur is not { } c || OnPanel(e.Source)) return;
        c.ClickTarget = Describe(e.Source);
        c.ClickSame = c.Element is not null && (ReferenceEquals(c.Element, e.Source) || e.Source is Visual v && c.Element.IsVisualAncestorOf(v));
    }

    /// <summary>finish(c): the line of the touch and its dot.</summary>
    private void Finish(Touch c)
    {
        if (!IsVisible) return;
        if (ReferenceEquals(_cur, c)) _cur = null;
        bool ok = c.Element is null || (c.ClickTarget is not null && c.ClickSame);
        var line = (ok ? "✓ " : "✗ ") + c.Control + " · " + c.Duration + "ms" + (c.Moved > 0 ? " · deslizou " + c.Moved + "px" : "") + (c.Cancelled ? " · CANCELADO pelo sistema" : "") +
                   (c.ClickTarget is not null ? (c.ClickSame ? "" : " · clique foi para " + c.ClickTarget) : c.Element is not null ? " · SEM CLIQUE" : "");
        _log.Add((DateTime.Now.ToString("HH:mm:ss"), line, ok, c.At, c.Target));
        if (_log.Count > MaxLog) _log.RemoveAt(0);
        Dot(c.At, ok);
        Render();
    }

    private void Dot(Point at, bool ok)
    {
        var color = Color.Parse(ok ? "#3ddc84" : "#ff4d5e");
        var d = new Ellipse { Width = 26, Height = 26, Stroke = new SolidColorBrush(color), StrokeThickness = 3, Fill = new SolidColorBrush(color, ok ? .25 : .3) };
        Canvas.SetLeft(d, at.X - 13);
        Canvas.SetTop(d, at.Y - 13);
        _dots.Children.Add(d);
        DispatcherTimer.RunOnce(() => _dots.Children.Remove(d), TimeSpan.FromMilliseconds(ok ? 900 : 4000));
    }

    private void Render()
    {
        _lines.Children.Clear();
        var last = _log.TakeLast(6).Reverse().ToList();
        if (last.Count == 0) _lines.Children.Add(Line("Toque nos botões que não funcionam.", "#e8e8ee"));
        foreach (var l in last) _lines.Children.Add(Line(l.Line, l.Ok ? "#9fe0b9" : "#ff8e98"));
    }

    private static TextBlock Line(string text, string color) => new()
    {
        Text = text, FontSize = 12, FontFamily = UrbeTheme.MonoFont, TextWrapping = TextWrapping.Wrap, Foreground = new SolidColorBrush(Color.Parse(color))
    };

    /// <summary>report(): the app, the device and every touch registered.</summary>
    public string Report()
    {
        var top = TopLevel.GetTopLevel(_host);
        var size = top?.ClientSize ?? _host.Bounds.Size;
        return string.Join("\n",
            "Relatório de toques — Urbe " + Version,
            "Aparelho: " + RuntimeInformation.OSDescription + " · " + RuntimeInformation.ProcessArchitecture,
            "Tela: " + Math.Round(size.Width) + "×" + Math.Round(size.Height) + " @" + (top?.RenderScaling ?? 1).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "x",
            "",
            _log.Count > 0 ? string.Join("\n", _log.Select(l => l.Time + "  " + l.Line + "  @" + Math.Round(l.At.X) + "," + Math.Round(l.At.Y) + (l.Line.Contains(l.Target, StringComparison.Ordinal) ? "" : "  [alvo: " + l.Target + "]")))
                : "(nenhum toque registrado)");
    }

    private async Task CopyAsync()
    {
        var text = Report();
        if (TopLevel.GetTopLevel(_host)?.Clipboard is { } clipboard)
        {
            await clipboard.SetTextAsync(text);
            Flash("Relatório copiado. Cole na conversa.");
        }
        else Flash("Não deu para copiar.");
    }

    private void Flash(string m)
    {
        _flash.Text = m;
        DispatcherTimer.RunOnce(() => _flash.Text = "", TimeSpan.FromMilliseconds(2500));
    }
}
