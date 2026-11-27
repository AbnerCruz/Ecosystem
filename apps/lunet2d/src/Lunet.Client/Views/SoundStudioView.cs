using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lunet.Client.Ui;
using Lunet.Core;
using SoundDesigner = Lunet.Studio.Music.SoundDesigner;
using SoundEffect = Lunet.Studio.Music.SoundEffect;
using Waveform = Lunet.Studio.Music.Waveform;

namespace Lunet.Client.Views;

/// <summary>The rendered wave of an effect, drawn as the min/max envelope per column.</summary>
public sealed class WaveView : Control
{
    private float[] _samples = [];

    public WaveView()
    {
        Height = 120;
        AutomationProperties.SetName(this, "Forma da onda");
    }

    public void Show(float[] samples) { _samples = samples; InvalidateVisual(); }

    public override void Render(DrawingContext context)
    {
        var w = Bounds.Width; var h = Bounds.Height;
        context.FillRectangle(LunetTokens.Brush(LunetTokens.Color.Stage), new Rect(0, 0, w, h), (float)LunetTokens.Radius.M);
        var mid = h / 2;
        context.DrawLine(new Pen(LunetTokens.Brush(LunetTokens.Color.Border), 1), new Point(0, mid), new Point(w, mid));
        if (_samples.Length == 0 || w <= 0) return;
        var pen = new Pen(LunetTokens.Brush(LunetTokens.Color.Primary), 1);
        var per = Math.Max(1, _samples.Length / (int)w);
        for (var x = 0; x < (int)w; x++)
        {
            int start = x * _samples.Length / (int)w, end = Math.Min(_samples.Length, start + per);
            float min = 0, max = 0;
            for (var i = start; i < end; i++) { min = Math.Min(min, _samples[i]); max = Math.Max(max, _samples[i]); }
            context.DrawLine(pen, new Point(x + .5, mid - max * mid * 0.9), new Point(x + .5, mid - min * mid * 0.9 + 1));
        }
    }
}

/// <summary>
/// The sound effects Studio in the shell (A4): sfxr's way, on the phone. Pick what the sound is (moeda, pulo,
/// explosão…), generate and vary it until it fits, fine-tune the main knobs, hear it, and Voltar saves the editable
/// <c>.sfx.json</c> with its <c>.wav</c>, together, in the same format as the previous Studio.
/// </summary>
public sealed class SoundStudioView : UserControl
{
    private readonly LunetShell _shell;
    private readonly string _path;
    private readonly Action _close;
    private readonly StudioAssetWorkspace _storage;
    private readonly WaveView _wave = new();
    private readonly StackPanel _knobs = new() { Spacing = 2 };
    private readonly TextBlock _info = LunetUi.Text("", LunetTokens.Type.Caption, LunetTokens.Color.TextSecondary);
    private readonly Random _seeds = new();
    private string _category = "coin";
    private bool _dirty;

    public SoundStudioView(LunetShell shell, string path, Action close, StudioAssetWorkspace? storage = null)
    {
        _shell = shell;
        _path = path;
        _close = close;
        // A game's folder, or the standalone Studio folder (Home → Studios) when given.
        _storage = storage ?? new StudioAssetWorkspace(shell.Workspace.Project!.Files);
        var file = Path.Combine(_storage.RootDirectory, path);
        if (File.Exists(file)) Sound = SoundDesigner.Parse(_storage.ReadText(path));
        else { Sound = SoundDesigner.Generate(_category, 1); _dirty = true; }
        Background = LunetTokens.Brush(LunetTokens.Color.Background);

        var back = LunetUi.IconButton(LunetIcons.Back, "Salvar e voltar", () => Close());
        var title = LunetUi.Title(Path.GetFileName(path).Replace(".sfx.json", "", StringComparison.OrdinalIgnoreCase));
        var bar = new DockPanel { Height = 56, Margin = new Thickness(LunetTokens.Space.Xxs, 0, LunetTokens.Space.Xs, 0) };
        DockPanel.SetDock(back, Dock.Left);
        bar.Children.Add(back);
        bar.Children.Add(new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { title, _info } });

        var categories = new WrapPanel { Orientation = Orientation.Horizontal };
        foreach (var (id, name) in SoundDesigner.Categories)
        {
            var b = LunetUi.Button(name, ButtonKind.Secondary, null, () => { _category = id; Generate(); });
            b.Margin = new Thickness(0, 0, LunetTokens.Space.Xs, LunetTokens.Space.Xs);
            b.MinHeight = 40;
            b.Name = "Category-" + id;
            categories.Children.Add(b);
        }
        var page = new StackPanel { Spacing = LunetTokens.Space.S, Margin = new Thickness(LunetTokens.Space.M, 0, LunetTokens.Space.M, LunetTokens.Space.M) };
        page.Children.Add(_wave);
        page.Children.Add(Section("Que som é?"));
        page.Children.Add(categories);
        page.Children.Add(Section("Ajustar"));
        page.Children.Add(_knobs);

        // Hear, generate again and vary: the three actions of the loop, at the bottom for the thumb.
        var play = LunetUi.Button("Tocar", ButtonKind.Primary, LunetIcons.Play, Play);
        play.Name = "PlaySound";
        play.MinHeight = 52;
        var again = LunetUi.Button("Gerar de novo", ButtonKind.Secondary, null, Generate);
        again.Name = "Regenerate";
        var vary = LunetUi.Button("Variar", ButtonKind.Secondary, null, Vary);
        vary.Name = "Vary";
        var actions = new Avalonia.Controls.Primitives.UniformGrid { Columns = 3, Margin = new Thickness(LunetTokens.Space.S), Children = { again, vary, play } };
        foreach (var child in actions.Children.OfType<Button>()) { child.Margin = new Thickness(LunetTokens.Space.Xxs); child.HorizontalAlignment = HorizontalAlignment.Stretch; }
        var bottom = new Border { Child = actions, Background = LunetTokens.Brush(LunetTokens.Color.Surface), BorderBrush = LunetTokens.Brush(LunetTokens.Color.Border), BorderThickness = new Thickness(0, 1, 0, 0) };

        var root = new DockPanel();
        DockPanel.SetDock(bar, Dock.Top);
        DockPanel.SetDock(bottom, Dock.Bottom);
        root.Children.Add(bar);
        root.Children.Add(bottom);
        root.Children.Add(new ScrollViewer { Content = page });
        Content = root;
        Refresh();
    }

    public SoundEffect Sound { get; private set; }

    public void Generate() { Set(SoundDesigner.Generate(_category, _seeds.Next())); }
    public void Vary() { Set(SoundDesigner.Mutate(Sound, _seeds.Next())); }

    /// <summary>Plays the effect through the head (Android); silent where there is no player.</summary>
    public void Play() => _shell.PlaySound?.Invoke(SoundDesigner.RenderWav(Sound));

    private void Set(SoundEffect effect)
    {
        SoundDesigner.Validate(effect);
        Sound = effect;
        _dirty = true;
        Refresh();
        Play();
    }

    public void Save()
    {
        if (!_dirty) return;
        var wav = _path[..^".sfx.json".Length] + ".wav";
        _storage.WriteBundle((_path, System.Text.Encoding.UTF8.GetBytes(SoundDesigner.ToJson(Sound))), (wav, SoundDesigner.RenderWav(Sound)));
        _dirty = false;
    }

    public bool Close()
    {
        try { Save(); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException)
        {
            _info.Text = "Não foi possível salvar: " + e.Message;
            _info.Foreground = LunetTokens.Brush(LunetTokens.Color.Danger);
            return true;
        }
        _close();
        return true;
    }

    /// <summary>A new effect in Content/Audio (where the game loads sounds from).</summary>
    public static string NewPath(LunetShell shell)
    {
        var files = shell.Workspace.Files();
        var n = 1;
        while (files.Contains($"Content/Audio/efeito-{n}.sfx.json")) n++;
        return $"Content/Audio/efeito-{n}.sfx.json";
    }

    private void Refresh()
    {
        var samples = SoundDesigner.Render(Sound);
        _wave.Show(samples);
        var waveName = Sound.Wave switch
        {
            Waveform.Square or Waveform.Pulse25 or Waveform.Pulse12 => "Quadrada", Waveform.Triangle => "Triângulo", Waveform.Saw => "Serra",
            Waveform.Sine => "Seno", Waveform.Noise => "Ruído", _ => "FM",
        };
        _info.Text = $"{waveName} · {samples.Length / (double)SoundDesigner.SampleRate:0.00} s · {Sound.StartHz:0} Hz";
        _info.Foreground = LunetTokens.Brush(LunetTokens.Color.TextSecondary);
        _knobs.Children.Clear();
        var waves = new WrapPanel { Orientation = Orientation.Horizontal };
        foreach (var w in new[] { (Waveform.Square, "Quadrada"), (Waveform.Triangle, "Triângulo"), (Waveform.Saw, "Serra"), (Waveform.Sine, "Seno"), (Waveform.Noise, "Ruído") })
        {
            var (wave, label) = w;
            var b = LunetUi.Button(label, Sound.Wave == wave ? ButtonKind.Selected : ButtonKind.Ghost, null, () => Set(Sound with { Wave = wave }));
            b.MinHeight = 40;
            b.Margin = new Thickness(0, 0, LunetTokens.Space.Xxs, LunetTokens.Space.Xxs);
            waves.Children.Add(b);
        }
        _knobs.Children.Add(waves);
        Knob("Altura (Hz)", 40, 2000, Sound.StartHz, v => Sound with { StartHz = v }, "Pitch");
        Knob("Deslizar", -1, 1, Sound.Slide, v => Sound with { Slide = v }, "Slide");
        Knob("Sustentar (s)", 0, 1, Sound.Sustain, v => Sound with { Sustain = v }, "Sustain");
        Knob("Sumir (s)", 0, 1.5, Sound.Decay, v => Sound with { Decay = v }, "Decay");
        Knob("Vibrato", 0, 1, Sound.VibratoDepth, v => Sound with { VibratoDepth = v }, "Vibrato");
        Knob("Volume", 0, 1, Sound.Volume, v => Sound with { Volume = v }, "Volume");
    }

    private void Knob(string label, double min, double max, double value, Func<float, SoundEffect> change, string name)
    {
        var slider = new Slider { Minimum = min, Maximum = max, Value = Math.Clamp(value, min, max), MinHeight = 40, Name = name };
        AutomationProperties.SetName(slider, label);
        // Applied when the finger lifts (no sound per pixel of drag).
        slider.AddHandler(PointerReleasedEvent, (_, _) => Apply(), Avalonia.Interactivity.RoutingStrategies.Tunnel | Avalonia.Interactivity.RoutingStrategies.Bubble, handledEventsToo: true);
        slider.KeyUp += (_, _) => Apply();
        void Apply()
        {
            try { Set(change((float)slider.Value)); }
            catch (Exception e) when (e is ArgumentException or InvalidDataException) { _info.Text = e.Message; }
        }
        var t = LunetUi.Text(label, LunetTokens.Type.Caption, LunetTokens.Color.TextSecondary);
        t.Width = 104; t.VerticalAlignment = VerticalAlignment.Center;
        var row = new DockPanel();
        DockPanel.SetDock(t, Dock.Left);
        row.Children.Add(t);
        row.Children.Add(slider);
        _knobs.Children.Add(row);
    }

    private static TextBlock Section(string text) => LunetUi.Section(text);
}
