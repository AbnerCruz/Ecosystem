using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace Urbe.Client.Plugins;

/// <summary>
/// "Relógio" (owner, DEC-0045, UC-46: "um relógio/cronômetro/timer nativo"), the first plugin of the native API
/// (ADD-0021): a button in the city opens a floating panel — over the city and the editor — with the time now, a
/// stopwatch and a timer. It uses only what its manifest declares (button, command, panel, notices, its own data).
/// </summary>
public sealed class ClockPlugin : IUrbePlugin
{
    public PluginManifest Manifest { get; } = new("relogio", "Relógio", "1.0", "Hora, cronômetro e temporizador num painel sobre a cidade e o editor.",
        [PluginPermissions.Buttons, PluginPermissions.Commands, PluginPermissions.Panels, PluginPermissions.Notices, PluginPermissions.Data]);

    private readonly DispatcherTimer _tick = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private readonly TextBlock _now = Big(28);
    private readonly TextBlock _watch = Big(22);
    private readonly TextBlock _timer = Big(22);
    private readonly Stopwatch _stopwatch = new();
    private readonly Button _watchToggle = Small("Iniciar");
    private readonly Button _timerToggle = Small("Iniciar");
    private IPluginApi? _api;
    private TimeSpan _timerLength = TimeSpan.FromMinutes(25);
    private DateTime? _timerEnd;
    private TimeSpan? _timerPaused;

    /// <summary>The clock's own "now" (tests move it).</summary>
    public Func<DateTime> Clock { get; set; } = () => DateTime.Now;

    public IPluginPanel? Panel { get; private set; }

    public void Start(IPluginApi api)
    {
        _api = api;
        if (int.TryParse(api.GetData("minutos"), out var minutes) && minutes is > 0 and <= 600) _timerLength = TimeSpan.FromMinutes(minutes);
        var panel = api.Panel("Relógio", Content());
        Panel = panel;
        api.Button("Relógio", UrbeTheme.Icons.Clock, () => { panel.Toggle(); Update(); return Task.CompletedTask; });
        // also in the editor's top bar: the owner looked for it there (UC-46/UC-48: "o plugin do relógio não apareceu no editor")
        api.EditorButton("Relógio", UrbeTheme.Icons.Clock, () => { panel.Toggle(); Update(); return Task.CompletedTask; });
        api.Command("Abrir relógio", () => { panel.Show(); Update(); return Task.CompletedTask; });
        _tick.Tick += (_, _) => Update();
        _tick.Start();
        Update();
    }

    public void Stop()
    {
        _tick.Stop();
        _stopwatch.Reset();
        _timerEnd = null;
        _api = null;
    }

    private Control Content()
    {
        _watchToggle.Click += (_, _) => { if (_stopwatch.IsRunning) _stopwatch.Stop(); else _stopwatch.Start(); Update(); };
        var watchZero = Small("Zerar");
        watchZero.Click += (_, _) => { _stopwatch.Reset(); Update(); };
        _timerToggle.Click += (_, _) => ToggleTimer();
        var timerZero = Small("Zerar");
        timerZero.Click += (_, _) => { _timerEnd = null; _timerPaused = null; Update(); };
        var presets = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        foreach (var m in new[] { 5, 15, 25 })
        {
            var b = Small(m + " min");
            b.Click += (_, _) => SetLength(m);
            presets.Children.Add(b);
        }
        return new StackPanel
        {
            Spacing = 8,
            Children =
            {
                Label("Agora"), _now,
                Label("Cronômetro"), _watch, Row(_watchToggle, watchZero),
                Label("Temporizador"), _timer, presets, Row(_timerToggle, timerZero)
            }
        };
    }

    /// <summary>A timer length (also kept for next time).</summary>
    public void SetLength(int minutes)
    {
        _timerLength = TimeSpan.FromMinutes(minutes);
        _timerEnd = null;
        _timerPaused = null;
        _api?.SetData("minutos", minutes.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Update();
    }

    /// <summary>Starts, pauses or resumes the timer.</summary>
    public void ToggleTimer()
    {
        var now = Clock();
        if (_timerEnd is { } end) { _timerPaused = end - now; _timerEnd = null; }
        else { _timerEnd = now + (_timerPaused ?? _timerLength); _timerPaused = null; }
        Update();
    }

    /// <summary>What the timer shows now (tests).</summary>
    public string TimerText => _timer.Text ?? "";

    /// <summary>Redraws the panel; a timer that reached zero says so once.</summary>
    public void Update()
    {
        var now = Clock();
        _now.Text = now.ToString("HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
        _watch.Text = Format(_stopwatch.Elapsed, tenths: true);
        _watchToggle.Content = _stopwatch.IsRunning ? "Pausar" : _stopwatch.Elapsed > TimeSpan.Zero ? "Continuar" : "Iniciar";
        TimeSpan left;
        if (_timerEnd is { } end)
        {
            left = end - now;
            if (left <= TimeSpan.Zero)
            {
                _timerEnd = null;
                _timerPaused = null;
                left = TimeSpan.Zero;
                _api?.Notice("Temporizador: " + (int)_timerLength.TotalMinutes + " min terminados.");
                Panel?.Show();
            }
        }
        else left = _timerPaused ?? _timerLength;
        _timer.Text = Format(left, tenths: false);
        _timerToggle.Content = _timerEnd is not null ? "Pausar" : _timerPaused is not null ? "Continuar" : "Iniciar";
    }

    private static string Format(TimeSpan t, bool tenths)
    {
        var text = (t.TotalHours >= 1 ? ((int)t.TotalHours).ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" : "") + t.ToString(@"mm\:ss", System.Globalization.CultureInfo.InvariantCulture);
        return tenths ? text + "." + (t.Milliseconds / 100).ToString(System.Globalization.CultureInfo.InvariantCulture) : text;
    }

    private static TextBlock Big(double size) => new() { FontSize = size, FontWeight = FontWeight.Bold, Foreground = UrbeTheme.Brush(UrbeTheme.Text), FontFamily = UrbeTheme.MonoFont };
    private static TextBlock Label(string text) => new() { Text = text, FontSize = 12, FontWeight = FontWeight.SemiBold, Foreground = UrbeTheme.Brush(UrbeTheme.Text3), Margin = new Thickness(0, 4, 0, 0) };
    private static StackPanel Row(params Control[] items) { var r = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 }; foreach (var i in items) r.Children.Add(i); return r; }
    private static Button Small(string text) => new()
    {
        Content = text, Height = 34, Padding = new Thickness(12, 0), CornerRadius = new CornerRadius(10), FontSize = 13, FontWeight = FontWeight.SemiBold,
        Background = UrbeTheme.Brush(UrbeTheme.Surface2), Foreground = UrbeTheme.Brush(UrbeTheme.Text), BorderBrush = UrbeTheme.Brush(UrbeTheme.Line2),
        BorderThickness = new Thickness(1), VerticalContentAlignment = VerticalAlignment.Center
    };
}
