using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Lunet.Client.Shell;
using Lunet.Client.Ui;

namespace Lunet.Client.Views;

/// <summary>
/// The time of each frame, as a bar per frame against the 60 and 30 fps lines (the reference lines Unity, Roblox and
/// PlayCanvas draw), so one hitch is seen, not hidden in an average. Touching a bar shows that frame: update, draw and
/// steps (Roblox's "find the spike, then its cause"). The history keeps filling with the panel closed (Godot monitors).
/// </summary>
public sealed class FrameGraph : Control
{
    private IReadOnlyList<FrameSample> _samples = [];

    /// <summary>The frame the person touched, oldest-first index into the samples; null for none.</summary>
    public int? Selected { get; private set; }

    public event Action? SelectionChanged;

    public FrameGraph()
    {
        MinHeight = 140;
        ClipToBounds = true;
        AutomationProperties.SetName(this, "Gráfico do tempo de cada quadro");
        PointerPressed += (_, e) => Select(e);
        PointerMoved += (_, e) => { if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) Select(e); };
    }

    public void SetSamples(IReadOnlyList<FrameSample> samples)
    {
        _samples = samples;
        if (Selected is { } s && s >= samples.Count) Selected = null;
        InvalidateVisual();
    }

    /// <summary>The ceiling of the scale: room above 33,3 ms, or above the worst frame.</summary>
    public static double Scale(IReadOnlyList<FrameSample> samples) =>
        Math.Max(FrameStats.Budget30 * 1.25, samples.Count == 0 ? 0 : samples.Max(s => s.Total) * 1.1);

    private void Select(PointerEventArgs e)
    {
        if (_samples.Count == 0 || Bounds.Width <= 0) return;
        var x = e.GetPosition(this).X;
        var slot = Bounds.Width / FrameStats.Capacity;
        var first = FrameStats.Capacity - _samples.Count; // newest frame at the right edge
        var index = (int)(x / slot) - first;
        Selected = index >= 0 && index < _samples.Count ? index : null;
        InvalidateVisual();
        SelectionChanged?.Invoke();
    }

    public override void Render(DrawingContext context)
    {
        var w = Bounds.Width; var h = Bounds.Height;
        context.FillRectangle(LunetTokens.Brush(LunetTokens.Color.Stage), new Rect(0, 0, w, h), (float)LunetTokens.Radius.S);
        var scale = Scale(_samples);
        double Y(double ms) => h - ms / scale * h;
        var slot = w / FrameStats.Capacity;
        var first = FrameStats.Capacity - _samples.Count;
        for (var i = 0; i < _samples.Count; i++)
        {
            var t = _samples[i].Total;
            var color = t > FrameStats.Budget30 ? LunetTokens.Color.Danger : t > FrameStats.Budget60 ? LunetTokens.Color.Warning : LunetTokens.Color.Success;
            if (Selected == i) color = LunetTokens.Color.TextPrimary;
            var y = Y(t);
            context.FillRectangle(LunetTokens.Brush(color), new Rect((first + i) * slot, y, Math.Max(1, slot - 0.5), h - y));
        }
        foreach (var (ms, label) in new[] { (FrameStats.Budget60, "60 fps · 16,7 ms"), (FrameStats.Budget30, "30 fps · 33,3 ms") })
        {
            var y = Math.Round(Y(ms)) + 0.5;
            context.DrawLine(new Pen(LunetTokens.Brush(LunetTokens.Color.TextTertiary), 1, new DashStyle([4, 4], 0)), new Point(0, y), new Point(w, y));
            var text = new FormattedText(label, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                new Typeface(LunetTokens.UiFont), LunetTokens.Type.Caption, LunetTokens.Brush(LunetTokens.Color.TextSecondary));
            context.DrawText(text, new Point(LunetTokens.Space.Xs, y - text.Height - 2));
        }
    }
}

/// <summary>The Desempenho panel (M6): the frame graph, its numbers and a copyable report.</summary>
public sealed class PerformanceView : UserControl
{
    public static readonly TimeSpan RefreshInterval = TimeSpan.FromMilliseconds(250);

    private readonly LunetShell _shell;
    private readonly DispatcherTimer _timer = new() { Interval = RefreshInterval };
    private readonly FrameGraph _graph = new();
    private readonly WrapPanel _numbers = new() { Orientation = Orientation.Horizontal };
    private readonly TextBlock _detail = LunetUi.Text("", LunetTokens.Type.Label, LunetTokens.Color.TextSecondary);
    private readonly Control _body;

    public PerformanceView(LunetShell shell)
    {
        _shell = shell;
        _graph.SelectionChanged += UpdateDetail;
        _timer.Tick += (_, _) => Refresh();
        AttachedToVisualTree += (_, _) => { Refresh(); _timer.Start(); };
        DetachedFromVisualTree += (_, _) => _timer.Stop();

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = LunetTokens.Space.Xs };
        actions.Children.Add(LunetUi.Button("Copiar relatório", ButtonKind.Secondary, LunetIcons.File, () => _ = CopyReport()));
        actions.Children.Add(LunetUi.Button("Limpar", ButtonKind.Ghost, null, () => { _shell.Game.Frames.Clear(); Refresh(); }));
        var stack = new StackPanel { Spacing = LunetTokens.Space.S, Margin = new Thickness(LunetTokens.Space.M, 0, LunetTokens.Space.M, LunetTokens.Space.M) };
        stack.Children.Add(_numbers);
        stack.Children.Add(_graph);
        stack.Children.Add(_detail);
        stack.Children.Add(actions);
        stack.Children.Add(LunetUi.Text("Tempo de CPU de cada quadro nesta tela, sem esperar a GPU. Toque numa barra para ver o quadro.",
            LunetTokens.Type.Caption, LunetTokens.Color.TextTertiary));
        _body = new ScrollViewer { Content = stack };
    }

    /// <summary>The text "Copiar relatório" puts on the clipboard.</summary>
    public string Report() => _shell.Game.Frames.Report(_shell.State.Project ?? "");

    private async Task CopyReport()
    {
        if (TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
        {
            await Avalonia.Input.Platform.ClipboardExtensions.SetTextAsync(clipboard, Report());
            Haptics.Confirm?.Invoke();
        }
    }

    public void Refresh()
    {
        var frames = _shell.Game.Frames;
        if (frames.Count == 0)
        {
            Content = LunetUi.EmptyState(LunetIcons.Pulse, "Sem quadros medidos",
                _shell.Game.State is GameState.Running or GameState.Paused
                    ? "Os quadros aparecem aqui enquanto o jogo roda."
                    : "Rode o jogo: o tempo de cada quadro aparece aqui, com as linhas de 60 e 30 quadros por segundo.",
                _shell.Game.State is GameState.Running or GameState.Paused ? null : LunetUi.Button("Rodar", ButtonKind.Primary, LunetIcons.Play, () => _shell.RunGame()));
            return;
        }
        if (Content != _body) Content = _body;
        _numbers.Children.Clear();
        _numbers.Children.Add(Number("Média", frames.Average));
        _numbers.Children.Add(Number("95% abaixo de", frames.Percentile95));
        _numbers.Children.Add(Number("Pior", frames.Max));
        _numbers.Children.Add(Number("Acima de 16,7 ms", null, frames.OverBudget.ToString(CultureInfo.CurrentCulture), frames.OverBudget > 0 ? LunetTokens.Color.Warning : LunetTokens.Color.Success));
        _graph.SetSamples(frames.Samples);
        UpdateDetail();
    }

    private void UpdateDetail()
    {
        var samples = _shell.Game.Frames.Samples;
        if (_graph.Selected is { } i && i < samples.Count)
        {
            var s = samples[i];
            _detail.Text = string.Format(CultureInfo.CurrentCulture, "Quadro {0} de {1}: {2:0.00} ms · Update {3:0.00} ms ({4} passo(s)) · Draw {5:0.00} ms · resto {6:0.00} ms",
                i + 1, samples.Count, s.Total, s.Update, s.Steps, s.Draw, Math.Max(0, s.Total - s.Update - s.Draw));
        }
        else _detail.Text = $"Últimos {samples.Count} quadros.";
    }

    private static Control Number(string label, double? ms, string? text = null, string color = LunetTokens.Color.TextPrimary)
    {
        var value = text ?? (ms!.Value.ToString("0.0", CultureInfo.CurrentCulture) + " ms");
        var stack = new StackPanel { Spacing = 0, Margin = new Thickness(0, 0, LunetTokens.Space.L, LunetTokens.Space.Xs) };
        stack.Children.Add(LunetUi.Text(label, LunetTokens.Type.Caption, LunetTokens.Color.TextTertiary));
        var v = LunetUi.Text(value, LunetTokens.Type.Title, color, FontWeight.SemiBold);
        stack.Children.Add(v);
        AutomationProperties.SetName(stack, label + ": " + value);
        return stack;
    }
}
