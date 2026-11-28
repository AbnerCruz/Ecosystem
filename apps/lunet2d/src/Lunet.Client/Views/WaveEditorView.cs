using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lunet.Client.Ui;
using Lunet.Core;
using WaveAudioDocument = Lunet.Studio.WaveAudioDocument;

namespace Lunet.Client.Views;

/// <summary>The waveform with the cut (outside dimmed) and the loop (between the two accent lines).</summary>
public sealed class WaveformView : Control
{
    private Lunet.Studio.WavePeak[] _peaks = [];
    private double _duration = 1, _start, _end = 1, _loopStart, _loopEnd;
    private bool _loop;

    public WaveformView() { Height = 160; AutomationProperties.SetName(this, "Forma da onda"); }

    public void Show(WaveAudioDocument audio, double start, double end, bool loop, double loopStart, double loopEnd)
    {
        _peaks = audio.GetPeaks(240);
        _duration = Math.Max(audio.DurationSeconds, 0.0001);
        (_start, _end, _loop, _loopStart, _loopEnd) = (start, end, loop, loopStart, loopEnd);
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        var w = Bounds.Width; var h = Bounds.Height;
        context.FillRectangle(LunetTokens.Brush(LunetTokens.Color.Stage), new Rect(0, 0, w, h));
        if (_peaks.Length == 0 || w <= 0) return;
        var mid = h / 2;
        var bar = w / _peaks.Length;
        for (var i = 0; i < _peaks.Length; i++)
        {
            var t = (i + 0.5) / _peaks.Length * _duration;
            var inside = t >= _start && t <= _end;
            var brush = LunetTokens.Brush(inside ? LunetTokens.Color.Primary : LunetTokens.Color.TextTertiary);
            var top = mid - Math.Max(0.5, _peaks[i].Maximum * mid * 0.95);
            var bottom = mid - _peaks[i].Minimum * mid * 0.95;
            context.FillRectangle(brush, new Rect(i * bar, top, Math.Max(1, bar - 1), Math.Max(1, bottom - top)));
        }
        double X(double s) => s / _duration * w;
        var dim = LunetTokens.Brush("#99000000");
        context.FillRectangle(dim, new Rect(0, 0, X(_start), h));
        context.FillRectangle(dim, new Rect(X(_end), 0, w - X(_end), h));
        var edge = new Pen(LunetTokens.Brush(LunetTokens.Color.TextPrimary), 2);
        context.DrawLine(edge, new Point(X(_start), 0), new Point(X(_start), h));
        context.DrawLine(edge, new Point(X(_end), 0), new Point(X(_end), h));
        if (_loop)
        {
            var loop = new Pen(LunetTokens.Brush(LunetTokens.Color.Warning), 2, new DashStyle([4, 3], 0));
            context.DrawLine(loop, new Point(X(_loopStart), 0), new Point(X(_loopStart), h));
            context.DrawLine(loop, new Point(X(_loopEnd), 0), new Point(X(_loopEnd), h));
        }
    }
}

/// <summary>
/// The WAV editor in the shell (the previous screens' WaveAudioStudioView): cut the start and the end, set the volume
/// (0–200%), optionally write loop points (SMPL) for music that repeats, hear the result and save it as a copy in
/// <c>Content/Audio</c> (the original is never changed). Works on a game's .wav and in Home → Studios.
/// </summary>
public sealed class WaveEditorView : UserControl
{
    private readonly LunetShell _shell;
    private readonly StudioAssetWorkspace _storage;
    private readonly string _name;
    private readonly Action _close;
    private readonly WaveformView _wave = new();
    private readonly StackPanel _controls = new() { Spacing = LunetTokens.Space.Xs };
    private readonly TextBlock _status = LunetUi.Text("", LunetTokens.Type.Caption, LunetTokens.Color.TextSecondary);
    private readonly Dictionary<string, Slider> _sliders = new();
    private ToggleSwitch? _loopSwitch;
    private bool _syncing;

    public WaveEditorView(LunetShell shell, StudioAssetWorkspace storage, WaveAudioDocument audio, string name, Action close)
    {
        _shell = shell;
        _storage = storage;
        _name = name;
        _close = close;
        Audio = audio;
        End = audio.DurationSeconds;
        LoopEnd = audio.DurationSeconds;
        Background = LunetTokens.Brush(LunetTokens.Color.Background);
        var back = LunetUi.IconButton(LunetIcons.Back, "Voltar", () => Close());
        var bar = new DockPanel { Height = 56, Margin = new Thickness(LunetTokens.Space.Xxs, 0, LunetTokens.Space.M, 0) };
        DockPanel.SetDock(back, Dock.Left);
        bar.Children.Add(back);
        bar.Children.Add(new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { LunetUi.Title(name), _status } });
        var play = LunetUi.Button("Ouvir", ButtonKind.Secondary, LunetIcons.Play, () => Play());
        play.Name = "WavePlay";
        var save = LunetUi.Button("Salvar cópia", ButtonKind.Primary, LunetIcons.Check, () => SaveCopy());
        save.Name = "WaveSave";
        var actions = new Avalonia.Controls.Primitives.UniformGrid { Columns = 2, Margin = new Thickness(LunetTokens.Space.S), Children = { play, save } };
        foreach (var b in new[] { play, save }) { b.MinHeight = 56; b.HorizontalAlignment = HorizontalAlignment.Stretch; b.Margin = new Thickness(LunetTokens.Space.Xxs); }
        var root = new DockPanel();
        DockPanel.SetDock(bar, Dock.Top);
        DockPanel.SetDock(actions, Dock.Bottom);
        root.Children.Add(bar);
        root.Children.Add(actions);
        root.Children.Add(new ScrollViewer
        {
            Content = new StackPanel
            {
                Margin = new Thickness(LunetTokens.Space.M, 0, LunetTokens.Space.M, LunetTokens.Space.M), Spacing = LunetTokens.Space.S,
                Children = { new Border { Child = _wave, CornerRadius = new CornerRadius(LunetTokens.Radius.M), ClipToBounds = true }, _controls },
            },
        });
        Content = root;
        Refresh();
    }

    public WaveAudioDocument Audio { get; }
    public double Start { get; private set; }
    public double End { get; private set; }
    public float Gain { get; private set; } = 1;
    public bool Loop { get; private set; }
    public double LoopStart { get; private set; }
    public double LoopEnd { get; private set; }

    public bool Close() { _close(); return true; }

    public void SetCut(double start, double end)
    {
        var d = Audio.DurationSeconds;
        Start = Math.Clamp(Math.Min(start, end), 0, d);
        End = Math.Clamp(Math.Max(start, end), 0, d);
        if (End - Start < 0.01) End = Math.Min(d, Start + 0.01);
        LoopStart = Math.Clamp(LoopStart, Start, End);
        LoopEnd = Math.Clamp(LoopEnd, Start, End);
        Show();
    }

    public void SetGain(float gain) { Gain = Math.Clamp(gain, 0, 2); Show(); }

    public void SetLoop(bool on, double start, double end)
    {
        var changed = Loop != on;
        Loop = on;
        LoopStart = Math.Clamp(Math.Min(start, end), Start, End);
        LoopEnd = Math.Clamp(Math.Max(start, end), Start, End);
        if (changed) Refresh(); else Show();
    }

    /// <summary>The edited WAV (cut, volume and loop markers); the original samples are untouched.</summary>
    public byte[] Render() => Audio.Render(Start, End, Gain, Loop ? LoopStart : null, Loop ? LoopEnd : null);

    public void Play()
    {
        try { _shell.PlaySound?.Invoke(Render()); }
        catch (ArgumentOutOfRangeException e) { Say(e.Message, LunetTokens.Color.Danger); }
    }

    /// <summary>Writes the result as a new .wav in Content/Audio and returns its path.</summary>
    public string? SaveCopy()
    {
        try
        {
            var wav = Render();
            var stem = new string(_name.Select(c => char.IsAsciiLetterOrDigit(c) ? char.ToLowerInvariant(c) : '-').ToArray()).Trim('-');
            if (stem.Length == 0) stem = "audio";
            var n = 1;
            string path;
            do path = $"Content/Audio/{stem}-editado{(n == 1 ? "" : "-" + n)}.wav"; while (_storage.Exists(path) && n++ < 1000);
            _storage.WriteBytes(path, wav);
            Say($"Cópia salva em {path}.", LunetTokens.Color.Success);
            return path;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
        {
            Say("Não foi possível salvar: " + e.Message, LunetTokens.Color.Danger);
            return null;
        }
    }

    private void Say(string text, string color) { _status.Text = text; _status.Foreground = LunetTokens.Brush(color); }

    private void Show()
    {
        // The controls follow the values (a change from code, or another slider clamping this one) without a rebuild.
        if (!_syncing)
        {
            _syncing = true;
            foreach (var (name, value) in new[] { ("WaveStart", Start), ("WaveEnd", End), ("WaveGain", (double)Gain), ("WaveLoopStart", LoopStart), ("WaveLoopEnd", LoopEnd) })
                if (_sliders.TryGetValue(name, out var slider) && Math.Abs(slider.Value - value) > 1e-6) slider.Value = value;
            if (_loopSwitch is not null) _loopSwitch.IsChecked = Loop;
            _syncing = false;
        }
        _wave.Show(Audio, Start, End, Loop, LoopStart, LoopEnd);
        if (!_status.Text!.StartsWith("Cópia", StringComparison.Ordinal) && !_status.Text.StartsWith("Não", StringComparison.Ordinal))
            Say($"{End - Start:0.00} s de {Audio.DurationSeconds:0.00} s · {Audio.SampleRate} Hz · {(Audio.Channels == 1 ? "mono" : "estéreo")}", LunetTokens.Color.TextSecondary);
    }

    private void Refresh()
    {
        _controls.Children.Clear();
        _sliders.Clear();
        var d = Audio.DurationSeconds;
        Control Row(string label, double min, double max, double value, Action<double> set, string name)
        {
            var slider = new Slider { Minimum = min, Maximum = max, Value = value, MinHeight = 40, Name = name };
            AutomationProperties.SetName(slider, label);
            slider.ValueChanged += (_, e) => { if (!_syncing) set(e.NewValue); };
            _sliders[name] = slider;
            var t = LunetUi.Text(label, LunetTokens.Type.Caption, LunetTokens.Color.TextSecondary);
            t.Width = 96; t.VerticalAlignment = VerticalAlignment.Center;
            var row = new DockPanel();
            DockPanel.SetDock(t, Dock.Left);
            row.Children.Add(t);
            row.Children.Add(slider);
            return row;
        }
        _controls.Children.Add(Row("Início", 0, d, Start, v => SetCut(v, End), "WaveStart"));
        _controls.Children.Add(Row("Fim", 0, d, End, v => SetCut(Start, v), "WaveEnd"));
        _controls.Children.Add(Row("Volume", 0, 2, Gain, v => SetGain((float)v), "WaveGain"));
        var loop = new ToggleSwitch { MinHeight = LunetTokens.MinTouch, IsChecked = Loop, OnContent = "Repetir em loop (marcadores no WAV)", OffContent = "Repetir em loop (marcadores no WAV)", Name = "WaveLoop" };
        loop.IsCheckedChanged += (_, _) => { if (!_syncing && loop.IsChecked != Loop) SetLoop(loop.IsChecked == true, Loop ? LoopStart : Start, Loop ? LoopEnd : End); };
        _loopSwitch = loop;
        _controls.Children.Add(loop);
        if (Loop)
        {
            _controls.Children.Add(Row("Loop começa", 0, d, LoopStart, v => SetLoop(true, v, LoopEnd), "WaveLoopStart"));
            _controls.Children.Add(Row("Loop termina", 0, d, LoopEnd, v => SetLoop(true, LoopStart, v), "WaveLoopEnd"));
        }
        Show();
    }
}
