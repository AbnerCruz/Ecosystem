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
    private readonly TextBlock _time = new() { FontSize = UrbeTokens.Type.Caption.Size, VerticalAlignment = VerticalAlignment.Center };
    private readonly Button _play = new() { Content = "▶ Reproduzir", MinHeight = 44, Padding = new Thickness(12, 4) };
    private readonly Ui.UrbeButton _export = new("Exportar MP4", Ui.ButtonKind.Primary, UrbeTheme.Icons.Download) { MinHeight = 44 };
    private readonly Ui.UrbeButton _cancel = new("Cancelar", Ui.ButtonKind.Danger) { MinHeight = 44, IsVisible = false };
    private readonly ProgressBar _progress = new() { Minimum = 0, Maximum = 1, IsVisible = false, Height = 6 };
    private readonly TextBlock _status = new() { FontSize = UrbeTokens.Type.Caption.Size, TextWrapping = TextWrapping.Wrap };
    private readonly StackPanel _scenes = new() { Spacing = 6 };
    private readonly DispatcherTimer _timer = new();
    private readonly System.Diagnostics.Stopwatch _clock = new();
    private VideoFrameRenderer? _renderer;
    private WriteableBitmap? _bitmap;
    private double _playFrom;
    private CancellationTokenSource? _startingAudio;
    private IPreviewAudio? _audio;
    public Func<VideoTimeline, double, CancellationToken, Task<IPreviewAudio?>>? StartAudioAsync { get; set; }
    private bool _scrubbing;
    // #956 P2: the object selected on the preview, its outline, the inspector, and a drag under way
    private readonly Canvas _overlay = new() { IsHitTestVisible = false };
    private readonly Border _outline = new() { BorderThickness = new Thickness(2), CornerRadius = new CornerRadius(3), IsVisible = false };
    private readonly Grid _stage = new() { Background = Brushes.Black };
    private readonly StackPanel _inspector = new() { Spacing = 6, IsVisible = false };
    private readonly TextBlock _hint = new() { FontSize = UrbeTokens.Type.Caption.Size, TextWrapping = TextWrapping.Wrap, Text = "Toque num objeto da prévia para movê-lo, mudar o tamanho ou animá-lo." };
    private (string Scene, string Element)? _selected;
    private Point? _dragFrom;
    private Point _dragBy;
    private double _pinch = 1;
    private readonly VideoTimelineStrip _strip = new() { Margin = new Thickness(0, 2, 0, 2) };

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
            Stop();
            ShowAt(_scrubber.Value);
        };
        _play.Click += (_, _) => TogglePlay();
        _export.Click += (_, _) => ExportRequested?.Invoke();
        _cancel.Click += (_, _) => CancelRequested?.Invoke();
        _timer.Tick += (_, _) =>
        {
            if (_renderer is null) { Stop(); return; }
            if (_audio is { Failed: true }) { Stop(); _status.Text = "O áudio da prévia foi interrompido."; return; }
            Advance(_audio is { } audio
                ? audio.Ended ? _renderer.Timeline.Duration : audio.Position
                : _playFrom + _clock.Elapsed.TotalSeconds);
        };
        var controls = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(_play, Dock.Left);
        DockPanel.SetDock(_time, Dock.Right);
        _time.Margin = new Thickness(8, 0, 0, 0);
        _scrubber.Margin = new Thickness(8, 0, 0, 0);
        controls.Children.Add(_play);
        controls.Children.Add(_time);
        controls.Children.Add(_scrubber);
        _strip.Seek += t => { Stop(); ShowAt(t); };
        _strip.SceneResized += scene => SceneChanged?.Invoke(scene);
        var exportRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { _export, _cancel } };
        _outline.BorderBrush = UrbeTheme.Brush(UrbeTheme.Accent);
        _overlay.Children.Add(_outline);
        _stage.Children.Add(_frame);
        _stage.Children.Add(_overlay);
        _stage.PointerPressed += (_, e) => { if (Press(e.GetPosition(_stage))) e.Pointer.Capture(_stage); };
        _stage.PointerMoved += (_, e) => Drag(e.GetPosition(_stage));
        _stage.PointerReleased += (_, _) => Release(commit: true);
        _stage.PointerCaptureLost += (_, _) => Release(commit: true);
        _stage.SizeChanged += (_, _) => DrawOutline();
        // two fingers resize the selected object: the outline follows, the size is written when the fingers leave
        _stage.GestureRecognizers.Add(new Avalonia.Input.PinchGestureRecognizer());
        _stage.AddHandler(Avalonia.Input.InputElement.PinchEvent, (_, e) =>
        {
            if (_selected is null) return;
            _dragFrom = null;
            _dragBy = default;
            _pinch = Math.Clamp(e.Scale, 0.1, 10);
            DrawOutline();
            e.Handled = true;
        });
        _stage.AddHandler(Avalonia.Input.InputElement.PinchEndedEvent, (_, _) => PinchSelected(_pinch));
        _hint.Foreground = UrbeTheme.Brush(UrbeTheme.Text3);
        Children.Add(new Border { CornerRadius = new CornerRadius(10), ClipToBounds = true, Background = Brushes.Black, Child = _stage });
        Children.Add(_hint);
        Children.Add(_inspector);
        Children.Add(controls);
        Children.Add(_strip);
        Children.Add(exportRow);
        Children.Add(_progress);
        Children.Add(_status);
        Children.Add(new TextBlock { Text = "Cenas", FontSize = UrbeTokens.Type.BodyStrong.Size, FontWeight = UrbeTokens.Type.BodyStrong.Weight, Foreground = UrbeTheme.Brush(UrbeTheme.Text), Margin = new Thickness(0, 8, 0, 0) });
        Children.Add(_scenes);
        DetachedFromVisualTree += (_, _) => Stop();
    }

    public event Action? ExportRequested;
    public event Action? CancelRequested;
    /// <summary>The scene, in the project, changed by the timing controls.</summary>
    public event Action<VideoScene>? SceneChanged;
    /// <summary>#1004: the person asked to edit a code element's program.</summary>
    public event Action<VideoScene, VideoElement>? CodeEditRequested;
    public event Action<string>? AddElementRequested;

    public VideoTimeline? Timeline => _renderer?.Timeline;
    public double CurrentTime => _scrubber.Value;
    public WriteableBitmap? Frame => _bitmap;
    public bool IsPlaying => _timer.IsEnabled;
    public bool IsExporting => _cancel.IsVisible;

    /// <summary>A new timeline (project or note changed): the frame at the same time is drawn again.</summary>
    public void Show(VideoTimeline timeline, IReadOnlyDictionary<string, byte[]> files, Func<string, string> sceneLabel)
    {
        // the first look is the first scene formed, not the black of its fade-in
        var at = _renderer is null ? Poster(timeline) : _scrubber.Value;
        Stop();
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
        if (_selected is { } sel && !timeline.Scenes.Any(x => x.Scene.Id == sel.Scene && x.Scene.Elements.Any(e => e.Id == sel.Element))) Select(null);
        else Inspect();
    }

    /// <summary>Playback reached <paramref name="t"/>: past the end it stops and goes back to the poster, not to the black of the fade-in.</summary>
    public void Advance(double t)
    {
        if (_renderer is not { } r) return;
        if (t >= r.Timeline.Duration) { Stop(); t = Poster(r.Timeline); }
        ShowAt(t);
    }

    private static double Poster(VideoTimeline timeline) => timeline.Scenes.Count > 0 ? timeline.Scenes[0].Settled : 0;

    public VideoTimelineStrip Strip => _strip;

    /// <summary>The object selected on the preview (scene and element ids), or null.</summary>
    public (string Scene, string Element)? Selected => _selected;

    /// <summary>Where the frame's picture is drawn inside the stage (letterboxed by Stretch.Uniform).</summary>
    private Rect Picture()
    {
        if (_renderer is not { } r) return default;
        var box = _stage.Bounds.Size;
        double scale = Math.Min(box.Width / r.Timeline.Width, Math.Min(_frame.MaxHeight, box.Height) / r.Timeline.Height);
        double w = r.Timeline.Width * scale, h = r.Timeline.Height * scale;
        return new Rect((box.Width - w) / 2, (box.Height - h) / 2, w, h);
    }

    /// <summary>A tap on the preview (fractions of the stage's picture): selects the object under it, or nothing.</summary>
    public void TapAt(double fx, double fy)
    {
        if (_renderer is not { } r || IsPlaying) return;
        var hit = VideoEditing.HitTest(r.Timeline.Evaluate(CurrentTime), fx, fy, r.Timeline.Width, r.Timeline.Height);
        Select(hit is { } h ? (h.Layer.Scene.Scene.Id, h.Element.Id) : null);
    }

    /// <summary>Selects what is under the finger; true when a drag of it may follow.</summary>
    private bool Press(Point at)
    {
        var picture = Picture();
        if (picture.Width <= 0) return false;
        TapAt((at.X - picture.X) / picture.Width, (at.Y - picture.Y) / picture.Height);
        _dragFrom = _selected is not null ? at : null;
        _dragBy = default;
        return _dragFrom is not null;
    }

    private void Drag(Point at)
    {
        if (_dragFrom is not { } from) return;
        _dragBy = at - from;
        DrawOutline();
    }

    private void Release(bool commit)
    {
        if (_dragFrom is null) return;
        var by = _dragBy;
        _dragFrom = null;
        _dragBy = default;
        var picture = Picture();
        if (!commit || Math.Abs(by.X) + Math.Abs(by.Y) < 4 || picture.Width <= 0 || Current() is not { } c) { DrawOutline(); return; }
        // #1004 slice 4: the finger moved the element on the screen; under a camera that is turned back into the scene
        if (c.Camera is { } cam && _renderer is { } r)
        {
            var shown = cam.Apply(c.State, r.Timeline.Width, r.Timeline.Height);
            var (x, y) = cam.Unapply(shown.X + by.X / picture.Width, shown.Y + by.Y / picture.Height, r.Timeline.Width, r.Timeline.Height);
            MoveSelected(x, y);
        }
        else MoveSelected(c.State.X + by.X / picture.Width, c.State.Y + by.Y / picture.Height);
    }

    /// <summary>Resizes the selected object by <paramref name="factor"/> (the end of a pinch); a tiny change is a wobble, not a resize.</summary>
    public void PinchSelected(double factor)
    {
        _pinch = 1;
        if (Math.Abs(factor - 1) < 0.03 || Current() is not { } c) { DrawOutline(); return; }
        SceneChanged?.Invoke(VideoEditing.Resize(c.Scene.Scene, c.State.Id, factor));
    }

    /// <summary>Moves the selected object's centre to (x, y) at the playhead (a keyframe there when it is animated).</summary>
    public void MoveSelected(double x, double y)
    {
        if (Current() is { } c) SceneChanged?.Invoke(VideoEditing.Move(c.Scene.Scene, c.State.Id, c.Local, x, y, c.State.Parent));
    }

    private (VideoTimelineScene Scene, VideoElementState State, double Local, VideoCameraState? Camera)? Current()
    {
        if (_selected is not { } sel || _renderer is not { } r) return null;
        var scene = r.Timeline.Scenes.FirstOrDefault(x => x.Scene.Id == sel.Scene);
        var layer = r.Timeline.Evaluate(CurrentTime).Layers.FirstOrDefault(l => ReferenceEquals(l.Scene, scene));
        var state = layer?.Elements.FirstOrDefault(e => e.Id == sel.Element);
        return scene is null || state is null ? null : (scene, state, CurrentTime - scene.Start, layer!.Camera);
    }

    private void Select((string Scene, string Element)? which)
    {
        _selected = which;
        Inspect();
    }

    private void DrawOutline()
    {
        var picture = Picture();
        if (Current() is not { } c || picture.Width <= 0 || _renderer is not { } r) { _outline.IsVisible = false; return; }
        var e = c.Camera is { } cam ? cam.Apply(c.State, r.Timeline.Width, r.Timeline.Height) : c.State;
        double w = e.Width * e.Scale * _pinch * picture.Width, h = e.Height * e.Scale * _pinch * picture.Height;
        _outline.Width = Math.Max(8, w);
        _outline.Height = Math.Max(8, h);
        _outline.RenderTransform = e.Rotation == 0 ? null : new RotateTransform(e.Rotation);
        Canvas.SetLeft(_outline, picture.X + e.X * picture.Width - w / 2 + _dragBy.X);
        Canvas.SetTop(_outline, picture.Y + e.Y * picture.Height - h / 2 + _dragBy.Y);
        _outline.IsVisible = true;
    }

    /// <summary>The inspector of the selected object: keyframe at the playhead, size, removal, and its keyframes to jump to.</summary>
    private void Inspect()
    {
        _inspector.Children.Clear();
        var c = Current();
        _inspector.IsVisible = _selected is not null;
        _hint.IsVisible = _selected is null;
        DrawOutline();
        if (_selected is not { } sel || _renderer is not { } r) return;
        var scene = r.Timeline.Scenes.FirstOrDefault(x => x.Scene.Id == sel.Scene);
        var element = scene?.Scene.Elements.FirstOrDefault(e => e.Id == sel.Element);
        if (scene is null || element is null) return;
        Button Action(string text, string name, Action run, bool enabled = true)
        {
            var b = new Button { Content = text, MinWidth = 44, MinHeight = 44, Padding = new Thickness(10, 4), IsEnabled = enabled };
            AutomationProperties.SetName(b, name);
            b.Click += (_, _) => run();
            return b;
        }
        string kind = element.Kind switch { "title" => "Título", "script" => "Roteiro", "shape" => "Forma", "image" => "Imagem", "plot" => "Gráfico", "math" => "Fórmula", "path" => "Traço", "group" => "Grupo", "code" => "Código", _ => "Texto" };
        _inspector.Children.Add(new TextBlock
        {
            Text = kind + (c is null ? " · fora da cena neste instante" : " · " + Clock(c.Value.Local) + " da cena"),
            FontWeight = FontWeight.SemiBold, Foreground = UrbeTheme.Brush(UrbeTheme.Text)
        });
        bool keyed = c is { } now && VideoEditing.HasKeyframe(element, now.Local);
        var actions = new WrapPanel { Orientation = Orientation.Horizontal };
        var buttons = new List<Button>();
        if (element.Kind == "code") buttons.Add(Action("Editar código", "Editar o programa que desenha este objeto", () => CodeEditRequested?.Invoke(scene.Scene, element)));
        foreach (var b in buttons.Concat(new Button[]
        {
            Action(keyed ? "◆ Tirar quadro-chave" : "◇ Quadro-chave aqui", keyed ? "Tirar o quadro-chave deste instante" : "Marcar quadro-chave neste instante",
                () => { if (Current() is { } x) SceneChanged?.Invoke(VideoEditing.ToggleKeyframe(x.Scene.Scene, x.State.Id, x.Local, x.State)); }, c is not null),
            Action("Maior", "Aumentar o objeto", () => SceneChanged?.Invoke(VideoEditing.Resize(scene.Scene, element.Id, 1.15))),
            Action("Menor", "Diminuir o objeto", () => SceneChanged?.Invoke(VideoEditing.Resize(scene.Scene, element.Id, 1 / 1.15))),
            Action("Remover", "Remover o objeto da cena", () => { Select(null); SceneChanged?.Invoke(VideoEditing.Remove(scene.Scene, element.Id)); }),
            Action("Pronto", "Terminar de editar o objeto", () => Select(null))
        }))
        {
            b.Margin = new Thickness(0, 0, 6, 6);
            actions.Children.Add(b);
        }
        _inspector.Children.Add(actions);
        // the curve that arrives at the keyframe under the playhead: how the object gets there
        if (c is { } at && VideoEditing.EasingAt(element, at.Local) is { } easing)
        {
            var curves = new WrapPanel { Orientation = Orientation.Horizontal };
            curves.Children.Add(new TextBlock { Text = "Curva até aqui:", FontSize = UrbeTokens.Type.Caption.Size, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 6), Foreground = UrbeTheme.Brush(UrbeTheme.Text3) });
            foreach (var (label, value) in VideoEditing.Curves)
            {
                var chosen = value == easing;
                var b = Action((chosen ? "● " : "") + label, "Curva " + label + (chosen ? " (atual)" : ""),
                    () => { if (Current() is { } x) SceneChanged?.Invoke(VideoEditing.SetEasing(x.Scene.Scene, x.State.Id, x.Local, value)); });
                b.Margin = new Thickness(0, 0, 6, 6);
                curves.Children.Add(b);
            }
            _inspector.Children.Add(curves);
        }
        var times = element.Keyframes.Select(k => k.Time).Distinct().OrderBy(t => t).ToList();
        if (times.Count > 0)
        {
            var row = new WrapPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(new TextBlock { Text = "Quadros-chave:", FontSize = UrbeTokens.Type.Caption.Size, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 6), Foreground = UrbeTheme.Brush(UrbeTheme.Text3) });
            foreach (var t in times)
            {
                var jump = Action("◆ " + t.ToString("0.0#", CultureInfo.GetCultureInfo("pt-BR")) + " s", "Ir ao quadro-chave de " + t.ToString("0.0#", CultureInfo.GetCultureInfo("pt-BR")) + " s",
                    () => { Stop(); ShowAt(scene.Start + t); Inspect(); });
                jump.Margin = new Thickness(0, 0, 6, 6);
                row.Children.Add(jump);
            }
            _inspector.Children.Add(row);
        }
        else _inspector.Children.Add(new TextBlock { Text = "Arraste o objeto na prévia para movê-lo. Para animar: marque um quadro-chave, mude o tempo, mova o objeto.", FontSize = UrbeTokens.Type.Caption.Size, TextWrapping = TextWrapping.Wrap, Foreground = UrbeTheme.Brush(UrbeTheme.Text3) });
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
        seek.Click += (_, _) => { Stop(); ShowAt(placed.Settled); };
        row.Children.Add(seek);
        return row;
    }

    public void TogglePlay() => _ = TogglePlayAsync();

    private async Task TogglePlayAsync()
    {
        if (_timer.IsEnabled || _startingAudio is not null) { Stop(); return; }
        if (_renderer is not { } renderer || renderer.Timeline.FrameCount == 0) return;
        var timeline = renderer.Timeline;
        _playFrom = _scrubber.Value >= timeline.Duration - 0.01 || Math.Abs(_scrubber.Value - Poster(timeline)) < 1e-6 ? 0 : _scrubber.Value;
        if (timeline.Audio.Count > 0 && StartAudioAsync is { } startAudio)
        {
            using var pending = new CancellationTokenSource();
            _startingAudio = pending;
            _play.Content = "Cancelar prévia";
            try
            {
                var audio = await startAudio(timeline, _playFrom, pending.Token);
                if (!ReferenceEquals(_startingAudio, pending)) { audio?.Dispose(); return; }
                _audio = audio;
                if (audio is null) _status.Text = "A prévia de áudio está indisponível neste aparelho.";
            }
            catch (OperationCanceledException) when (pending.IsCancellationRequested) { return; }
            catch (Exception error)
            {
                if (!ReferenceEquals(_startingAudio, pending)) return;
                _status.Text = "Não foi possível tocar a trilha. " + error.Message;
                _play.Content = "▶ Reproduzir";
                return;
            }
            finally { if (ReferenceEquals(_startingAudio, pending)) _startingAudio = null; }
        }
        _clock.Restart();
        _timer.Start();
        _play.Content = "❚❚ Pausar";
    }

    public void Stop()
    {
        var pending = _startingAudio;
        _startingAudio = null;
        pending?.Cancel();
        _timer.Stop();
        _clock.Stop();
        _audio?.Dispose();
        _audio = null;
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
        if (_selected is not null && !IsPlaying) Inspect();
        else _outline.IsVisible = false;
        // the strip: the playhead, and the selected object's keyframes as diamonds
        var keyed = _selected is { } sel && timeline.Scenes.FirstOrDefault(x => x.Scene.Id == sel.Scene) is { } owner
            ? owner.Scene.Elements.FirstOrDefault(e => e.Id == sel.Element)?.Keyframes.Select(k => owner.Start + k.Time).Distinct().ToList() ?? []
            : [];
        _strip.Show(timeline, time, keyed);
    }

    public void SetProgress(VideoExportProgress? progress, string? message = null)
    {
        _progress.IsVisible = progress is not null;
        _cancel.IsVisible = progress is not null;
        _export.IsEnabled = progress is null;
        if (progress is not null) _progress.Value = progress.Fraction;
        if (message is not null) _status.Text = message;
        else if (progress is not null)
            _status.Text = progress.Phase + " · quadro " + progress.Frame + " de " + progress.FrameCount + " · " + (int)Math.Round(progress.Fraction * 100) + "%" +
                (progress.Left is { } left ? " · falta " + VideoBudget.Words(left) : "");
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
