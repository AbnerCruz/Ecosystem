using System.Globalization;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Urbe.Core;

namespace Urbe.Client;

/// <summary>
/// CST-170/209 in the studio: the preview of a video publication (a frame drawn at the scrubber's time, played at
/// its fps), the scenes' timing (duration, cut or fade, objects) and "Exportar MP4" with progress and cancel. The
/// script is still edited in the notes (the studio's note list); only time and presentation are edited here.
/// </summary>
public sealed class PublicationVideoPanel : StackPanel
{
    public const int PreviewResolution = 360;
    private readonly Image _frame = new() { Stretch = Stretch.Uniform, MaxHeight = 420 };
    private readonly Slider _scrubber = new() { Minimum = 0, Maximum = 1, MinHeight = 44 };
    private readonly TextBlock _time = new() { FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
    private readonly Button _play = new() { Content = "▶ Reproduzir", MinHeight = 44, Padding = new Thickness(12, 4) };
    private readonly Ui.UrbeButton _export = new("Exportar MP4", Ui.ButtonKind.Primary, UrbeTheme.Icons.Download) { MinHeight = 44 };
    private readonly Ui.UrbeButton _cancel = new("Cancelar", Ui.ButtonKind.Danger) { MinHeight = 44, IsVisible = false };
    private readonly ProgressBar _progress = new() { Minimum = 0, Maximum = 1, IsVisible = false, Height = 6 };
    private readonly TextBlock _status = new() { FontSize = 12, TextWrapping = TextWrapping.Wrap };
    private readonly StackPanel _scenes = new() { Spacing = 6 };
    private readonly DispatcherTimer _timer = new();
    private readonly System.Diagnostics.Stopwatch _clock = new();
    private VideoFrameRenderer? _renderer;
    private WriteableBitmap? _bitmap;
    private double _playFrom;
    private bool _scrubbing;

    public PublicationVideoPanel()
    {
        Spacing = 10;
        _status.Foreground = UrbeTheme.Brush(UrbeTheme.Text3);
        _time.Foreground = UrbeTheme.Brush(UrbeTheme.Text2);
        AutomationProperties.SetName(_frame, "Prévia do vídeo");
        AutomationProperties.SetName(_scrubber, "Tempo do vídeo");
        AutomationProperties.SetName(_export, "Exportar vídeo MP4");
        AutomationProperties.SetName(_cancel, "Cancelar exportação do vídeo");
        _scrubber.PropertyChanged += (_, e) =>
        {
            if (e.Property != Avalonia.Controls.Primitives.RangeBase.ValueProperty || _scrubbing) return;
            if (!_timer.IsEnabled) ShowAt(_scrubber.Value);
        };
        _play.Click += (_, _) => TogglePlay();
        _export.Click += (_, _) => ExportRequested?.Invoke();
        _cancel.Click += (_, _) => CancelRequested?.Invoke();
        _timer.Tick += (_, _) =>
        {
            if (_renderer is null) { Stop(); return; }
            var t = _playFrom + _clock.Elapsed.TotalSeconds;
            if (t >= _renderer.Timeline.Duration) { Stop(); t = 0; }
            ShowAt(t);
        };
        var controls = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(_play, Dock.Left);
        DockPanel.SetDock(_time, Dock.Right);
        _time.Margin = new Thickness(8, 0, 0, 0);
        _scrubber.Margin = new Thickness(8, 0, 0, 0);
        controls.Children.Add(_play);
        controls.Children.Add(_time);
        controls.Children.Add(_scrubber);
        var exportRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { _export, _cancel } };
        Children.Add(new Border { CornerRadius = new CornerRadius(10), ClipToBounds = true, Background = Brushes.Black, Child = _frame });
        Children.Add(controls);
        Children.Add(exportRow);
        Children.Add(_progress);
        Children.Add(_status);
        Children.Add(new TextBlock { Text = "Cenas", FontSize = 16, FontWeight = FontWeight.SemiBold, Foreground = UrbeTheme.Brush(UrbeTheme.Text), Margin = new Thickness(0, 8, 0, 0) });
        Children.Add(_scenes);
        DetachedFromVisualTree += (_, _) => Stop();
    }

    public event Action? ExportRequested;
    public event Action? CancelRequested;
    /// <summary>The scene, in the project, changed by the timing controls.</summary>
    public event Action<VideoScene>? SceneChanged;
    public event Action<string>? AddElementRequested;

    public VideoTimeline? Timeline => _renderer?.Timeline;
    public double CurrentTime => _scrubber.Value;
    public WriteableBitmap? Frame => _bitmap;
    public bool IsPlaying => _timer.IsEnabled;
    public bool IsExporting => _cancel.IsVisible;

    /// <summary>A new timeline (project or note changed): the frame at the same time is drawn again.</summary>
    public void Show(VideoTimeline timeline, IReadOnlyDictionary<string, byte[]> files, Func<string, string> sceneLabel)
    {
        var at = _scrubber.Value;
        _renderer?.Dispose();
        _renderer = new VideoFrameRenderer(timeline, files, 48L << 20);
        _timer.Interval = TimeSpan.FromSeconds(1.0 / Math.Clamp(timeline.Fps, 1, 30));
        _scrubber.Maximum = Math.Max(0.001, timeline.Duration);
        _scrubbing = true;
        _scrubber.Value = Math.Min(at, timeline.Duration);
        _scrubbing = false;
        ShowAt(_scrubber.Value);
        if (!IsExporting)
            _status.Text = timeline.FrameCount == 0 ? "Adicione notas: cada nota vira uma cena, e o texto dela é o roteiro e a legenda."
                : timeline.Scenes.Count + (timeline.Scenes.Count == 1 ? " cena" : " cenas") + " · " + Clock(timeline.Duration) + " · " + timeline.Fps + " qps";
        _scenes.Children.Clear();
        foreach (var placed in timeline.Scenes) _scenes.Children.Add(SceneRow(placed, sceneLabel(placed.Placement.Id)));
    }

    private Control SceneRow(VideoTimelineScene placed, string label)
    {
        var scene = placed.Scene;
        var row = new DockPanel { LastChildFill = true };
        Button Small(string text, string name, Action run)
        {
            var b = new Button { Content = text, MinWidth = 44, MinHeight = 44, Padding = new Thickness(8, 4) };
            AutomationProperties.SetName(b, name);
            b.Click += (_, _) => run();
            return b;
        }
        var minus = Small("−", "Encurtar cena " + label, () => SceneChanged?.Invoke(scene with { Duration = Math.Max(0.5, scene.Duration - 0.5) }));
        var plus = Small("+", "Alongar cena " + label, () => SceneChanged?.Invoke(scene with { Duration = Math.Min(3600, scene.Duration + 0.5) }));
        var fade = scene.Transition.Kind == "fade";
        var transition = Small(fade ? "Fade" : "Corte", "Transição da cena " + label,
            () => SceneChanged?.Invoke(scene with { Transition = scene.Transition with { Kind = fade ? "cut" : "fade" } }));
        var add = Small("Objeto", "Adicionar objeto à cena " + label, () => AddElementRequested?.Invoke(scene.Id));
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { minus, plus, transition, add } };
        DockPanel.SetDock(buttons, Dock.Right);
        row.Children.Add(buttons);
        var seek = new Button
        {
            Background = Brushes.Transparent, BorderThickness = new Thickness(0), MinHeight = 44,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Content = new StackPanel
            {
                Children =
                {
                    new TextBlock { Text = label, Foreground = UrbeTheme.Brush(UrbeTheme.Text), TextTrimming = TextTrimming.CharacterEllipsis },
                    new TextBlock { Text = Clock(placed.Start) + " · " + placed.Duration.ToString("0.#", CultureInfo.GetCultureInfo("pt-BR")) + " s · " +
                        scene.Elements.Count + (scene.Elements.Count == 1 ? " objeto" : " objetos"), FontSize = 11, Foreground = UrbeTheme.Brush(UrbeTheme.Text3) }
                }
            }
        };
        AutomationProperties.SetName(seek, "Ir para a cena " + label);
        seek.Click += (_, _) => { Stop(); _scrubber.Value = placed.Start; ShowAt(placed.Start); };
        row.Children.Add(seek);
        return row;
    }

    public void TogglePlay()
    {
        if (_timer.IsEnabled) { Stop(); return; }
        if (_renderer is null || _renderer.Timeline.FrameCount == 0) return;
        _playFrom = _scrubber.Value >= _renderer.Timeline.Duration - 0.01 ? 0 : _scrubber.Value;
        _clock.Restart();
        _timer.Start();
        _play.Content = "❚❚ Pausar";
    }

    public void Stop()
    {
        _timer.Stop();
        _clock.Stop();
        _play.Content = "▶ Reproduzir";
    }

    /// <summary>Draws the frame at <paramref name="time"/> into the preview (the same renderer as the export).</summary>
    public void ShowAt(double time)
    {
        if (_renderer is not { } renderer) return;
        var timeline = renderer.Timeline;
        _scrubbing = true;
        _scrubber.Value = Math.Clamp(time, 0, _scrubber.Maximum);
        _scrubbing = false;
        _time.Text = Clock(time) + " / " + Clock(timeline.Duration);
        if (timeline.FrameCount == 0) { _frame.Source = null; return; }
        using var skia = renderer.Render(time);
        if (_bitmap is null || _bitmap.PixelSize.Width != skia.Width || _bitmap.PixelSize.Height != skia.Height)
            _bitmap = new WriteableBitmap(new PixelSize(skia.Width, skia.Height), new Vector(96, 96), PixelFormat.Rgba8888, AlphaFormat.Premul);
        using (var target = _bitmap.Lock())
        {
            var row = new byte[skia.RowBytes];
            for (int y = 0; y < skia.Height; y++)
            {
                Marshal.Copy(skia.GetPixels() + y * skia.RowBytes, row, 0, row.Length);
                Marshal.Copy(row, 0, target.Address + y * target.RowBytes, Math.Min(row.Length, target.RowBytes));
            }
        }
        _frame.Source = null;
        _frame.Source = _bitmap;
        _frame.InvalidateVisual();
    }

    public void SetProgress(VideoExportProgress? progress, string? message = null)
    {
        _progress.IsVisible = progress is not null;
        _cancel.IsVisible = progress is not null;
        _export.IsEnabled = progress is null;
        if (progress is not null) _progress.Value = progress.Fraction;
        if (message is not null) _status.Text = message;
        else if (progress is not null)
            _status.Text = progress.Phase + " · quadro " + progress.Frame + " de " + progress.FrameCount + " · " + (int)Math.Round(progress.Fraction * 100) + "%";
    }

    public static string Clock(double seconds)
    {
        var t = TimeSpan.FromSeconds(Math.Max(0, seconds));
        return (int)t.TotalMinutes + ":" + t.Seconds.ToString("00", CultureInfo.InvariantCulture) + "." + (t.Milliseconds / 100).ToString(CultureInfo.InvariantCulture);
    }

    public void Release()
    {
        Stop();
        _renderer?.Dispose();
        _renderer = null;
    }
}
