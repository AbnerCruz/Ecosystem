using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Lunet.Client.Ui;
using Lunet.Core;
using LColor = Lunet.Graphics.Color;
using ParticleDraft = Lunet.Studio.ParticleDraft;
using ParticlePresets = Lunet.Studio.ParticlePresets;

namespace Lunet.Client.Views;

/// <summary>
/// The particles Studio in the shell (A5): the effect runs live with the game's own emitter (ParticleEmitter on the
/// software renderer — what the game will draw), presets to start from, the main knobs, colours, and a tap on the
/// preview bursts it there. Voltar saves the <c>.particles.json</c> the game loads.
/// </summary>
public sealed class ParticleStudioView : UserControl
{
    private const int Size = 240;
    private readonly LunetShell _shell;
    private readonly string _path;
    private readonly Action _close;
    private readonly StudioAssetWorkspace _storage;
    private readonly Lunet.Runtime.SoftwareGraphicsBackend _backend = new(Size, Size);
    private readonly Lunet.Graphics.GraphicsDevice _device;
    private readonly Lunet.Graphics.SpriteBatch _batch;
    private readonly Lunet.Graphics.Texture2D _dot;
    private readonly WriteableBitmap _frame = new(new PixelSize(Size, Size), new Vector(96, 96), PixelFormat.Rgba8888, AlphaFormat.Unpremul);
    private readonly Image _image = new() { Stretch = Stretch.Uniform };
    private readonly Avalonia.Threading.DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(33) };
    private readonly StackPanel _knobs = new() { Spacing = 2 };
    private readonly TextBlock _info = LunetUi.Text("", LunetTokens.Type.Caption, LunetTokens.Color.TextSecondary);
    private Lunet.Graphics.ParticleEmitter? _emitter;
    private bool _dirty;

    public ParticleStudioView(LunetShell shell, string path, Action close)
    {
        _shell = shell;
        _path = path;
        _close = close;
        _storage = new StudioAssetWorkspace(shell.Workspace.Project!.Directory);
        _device = new Lunet.Graphics.GraphicsDevice(_backend, Size, Size);
        _device.Resize(Size, Size);
        _batch = new Lunet.Graphics.SpriteBatch(_device);
        _dot = Lunet.Graphics.Texture2D.CreateCircle(_device, 8, LColor.White);
        if (File.Exists(Path.Combine(_storage.RootDirectory, path)))
        {
            var json = _storage.ReadText(path);
            Draft = ParticleDraft.From(Lunet.Graphics.ParticleSettings.FromJson(json));
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("EmissionRate", out var rate)) Draft.EmissionRate = rate.GetSingle();
            if (doc.RootElement.TryGetProperty("MaxParticles", out var max)) Draft.MaxParticles = max.GetInt32();
        }
        else { Draft = ParticleDraft.From(ParticlePresets.All[0]); _dirty = true; }
        Background = LunetTokens.Brush(LunetTokens.Color.Background);
        RenderOptions.SetBitmapInterpolationMode(_image, BitmapInterpolationMode.LowQuality);
        _image.Source = _frame;
        _image.PointerPressed += (_, e) =>
        {
            // A tap bursts the effect where the finger is (in the preview's own pixels).
            var p = e.GetPosition(_image);
            var scale = Math.Min(_image.Bounds.Width, _image.Bounds.Height) / Size;
            var ox = (_image.Bounds.Width - Size * scale) / 2; var oy = (_image.Bounds.Height - Size * scale) / 2;
            Burst(new System.Numerics.Vector2((float)((p.X - ox) / scale), (float)((p.Y - oy) / scale)));
        };
        _timer.Tick += (_, _) => Step(0.033);
        AttachedToVisualTree += (_, _) => _timer.Start();
        DetachedFromVisualTree += (_, _) => _timer.Stop();

        var back = LunetUi.IconButton(LunetIcons.Back, "Salvar e voltar", () => Close());
        var title = LunetUi.Title(Path.GetFileName(path).Replace(".particles.json", "", StringComparison.OrdinalIgnoreCase));
        var bar = new DockPanel { Height = 56, Margin = new Thickness(LunetTokens.Space.Xxs, 0, LunetTokens.Space.Xs, 0) };
        DockPanel.SetDock(back, Dock.Left);
        bar.Children.Add(back);
        bar.Children.Add(new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { title, _info } });

        var presets = new WrapPanel { Orientation = Orientation.Horizontal };
        foreach (var preset in ParticlePresets.All)
        {
            var p = preset;
            var b = LunetUi.Button(preset.Name, ButtonKind.Secondary, null, () => { Draft = ParticleDraft.From(p); Changed(); });
            b.MinHeight = 40;
            b.Margin = new Thickness(0, 0, LunetTokens.Space.Xs, LunetTokens.Space.Xs);
            b.Name = "Preset-" + preset.Id;
            presets.Children.Add(b);
        }
        var stage = new Border
        {
            Child = _image, Height = 260, Background = LunetTokens.Brush(LunetTokens.Color.Stage), CornerRadius = new CornerRadius(LunetTokens.Radius.L), ClipToBounds = true,
        };
        var page = new StackPanel { Spacing = LunetTokens.Space.S, Margin = new Thickness(LunetTokens.Space.M, 0, LunetTokens.Space.M, LunetTokens.Space.L) };
        page.Children.Add(stage);
        page.Children.Add(LunetUi.Text("Toque na prévia para soltar uma explosão ali.", LunetTokens.Type.Caption, LunetTokens.Color.TextTertiary));
        page.Children.Add(Section("Começar de"));
        page.Children.Add(presets);
        page.Children.Add(Section("Ajustar"));
        page.Children.Add(_knobs);
        var burst = LunetUi.Button("Explosão", ButtonKind.Primary, LunetIcons.Play, () => Burst(new System.Numerics.Vector2(Size / 2f, Size / 2f)));
        burst.Name = "Burst";
        burst.MinHeight = 52;
        burst.HorizontalAlignment = HorizontalAlignment.Stretch;
        var bottom = new Border
        {
            Child = burst, Padding = new Thickness(LunetTokens.Space.S), Background = LunetTokens.Brush(LunetTokens.Color.Surface),
            BorderBrush = LunetTokens.Brush(LunetTokens.Color.Border), BorderThickness = new Thickness(0, 1, 0, 0),
        };
        var root = new DockPanel();
        DockPanel.SetDock(bar, Dock.Top);
        DockPanel.SetDock(bottom, Dock.Bottom);
        root.Children.Add(bar);
        root.Children.Add(bottom);
        root.Children.Add(new ScrollViewer { Content = page });
        Content = root;
        Changed(dirty: _dirty);
    }

    public ParticleDraft Draft { get; private set; }

    /// <summary>Particles alive in the preview now.</summary>
    public int Alive => _emitter?.Count ?? 0;

    /// <summary>Advances and draws the preview (the timer calls it; tests call it directly).</summary>
    public void Step(double seconds)
    {
        if (_emitter is null) return;
        _emitter.Update(seconds);
        _device.Clear(new LColor(12, 13, 16));
        _batch.Begin();
        _emitter.Draw(_batch);
        _batch.End();
        using (var buffer = _frame.Lock())
            unsafe { _backend.CopyScreenRgba(new Span<byte>((void*)buffer.Address, buffer.RowBytes * Size)); }
        _image.InvalidateVisual();
    }

    public void Burst(System.Numerics.Vector2 at) => _emitter?.Burst(Math.Max(1, Draft.Burst), at);

    private void Changed(bool dirty = true)
    {
        try
        {
            var settings = Draft.Build();
            _emitter = new Lunet.Graphics.ParticleEmitter(_dot, settings, Math.Clamp(Draft.MaxParticles, 1, 10_000)) { Position = new System.Numerics.Vector2(Size / 2f, Size * 0.6f), EmissionRate = Draft.EmissionRate };
            if (Draft.EmissionRate > 0) _emitter.Start(); else Burst(new System.Numerics.Vector2(Size / 2f, Size / 2f));
            _info.Text = $"{Draft.EmissionRate:0}/s · até {Draft.MaxParticles} · vida {Draft.Lifetime:0.0} s";
            _info.Foreground = LunetTokens.Brush(LunetTokens.Color.TextSecondary);
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException or InvalidDataException)
        {
            _info.Text = e.Message;
            _info.Foreground = LunetTokens.Brush(LunetTokens.Color.Danger);
        }
        if (dirty) _dirty = true;
        BuildKnobs();
    }

    public void Save()
    {
        if (!_dirty) return;
        _storage.WriteText(_path, Draft.ToJson());
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
        _timer.Stop();
        _close();
        return true;
    }

    public static string NewPath(LunetShell shell)
    {
        var files = shell.Workspace.Files();
        var n = 1;
        while (files.Contains($"Content/Effects/efeito-{n}.particles.json")) n++;
        return $"Content/Effects/efeito-{n}.particles.json";
    }

    private void BuildKnobs()
    {
        _knobs.Children.Clear();
        Knob("Quantidade por segundo", 0, 400, Draft.EmissionRate, v => Draft.EmissionRate = v, "Rate");
        Knob("Explosão (partículas)", 1, 300, Draft.Burst, v => Draft.Burst = (int)v, "BurstCount");
        Knob("Velocidade", 0, 600, Draft.MaxSpeed, v => { Draft.MaxSpeed = v; Draft.MinSpeed = Math.Min(Draft.MinSpeed, v); }, "Speed");
        Knob("Direção (graus)", -180, 180, Draft.Direction, v => Draft.Direction = v, "Direction");
        Knob("Abertura (graus)", 0, 360, Draft.Spread, v => Draft.Spread = v, "Spread");
        Knob("Gravidade", -600, 600, Draft.GravityY, v => Draft.GravityY = v, "Gravity");
        Knob("Vida (s)", 0.05, 5, Draft.Lifetime, v => Draft.Lifetime = v, "Lifetime");
        Knob("Tamanho no início", 0, 48, Draft.StartSize, v => Draft.StartSize = v, "StartSize");
        Knob("Tamanho no fim", 0, 48, Draft.EndSize, v => Draft.EndSize = v, "EndSize");
        Colors("Cor no início", Draft.StartColor, c => Draft.StartColor = c);
        Colors("Cor no fim", Draft.EndColor, c => Draft.EndColor = c);
        var glow = new ToggleSwitch { IsChecked = Draft.Additive, OnContent = null, OffContent = null, Name = "Additive" };
        AutomationProperties.SetName(glow, "Brilho somado (fogo, magia)");
        glow.IsCheckedChanged += (_, _) => { Draft.Additive = glow.IsChecked == true; Changed(); };
        var row = new DockPanel { MinHeight = 48 };
        DockPanel.SetDock(glow, Dock.Right);
        row.Children.Add(glow);
        row.Children.Add(LunetUi.Text("Brilho somado (fogo, magia)", LunetTokens.Type.Callout));
        _knobs.Children.Add(row);
    }

    private void Knob(string label, double min, double max, double value, Action<float> set, string name)
    {
        var slider = new Slider { Minimum = min, Maximum = max, Value = Math.Clamp(value, min, max), MinHeight = 40, Name = name };
        AutomationProperties.SetName(slider, label);
        slider.AddHandler(PointerReleasedEvent, (_, _) => { set((float)slider.Value); Changed(); },
            Avalonia.Interactivity.RoutingStrategies.Tunnel | Avalonia.Interactivity.RoutingStrategies.Bubble, handledEventsToo: true);
        var t = LunetUi.Text(label, LunetTokens.Type.Caption, LunetTokens.Color.TextSecondary);
        t.Width = 128; t.VerticalAlignment = VerticalAlignment.Center;
        var row = new DockPanel();
        DockPanel.SetDock(t, Dock.Left);
        row.Children.Add(t);
        row.Children.Add(slider);
        _knobs.Children.Add(row);
    }

    private void Colors(string label, LColor current, Action<LColor> set)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Margin = new Thickness(0, LunetTokens.Space.Xxs) };
        var t = LunetUi.Text(label, LunetTokens.Type.Caption, LunetTokens.Color.TextSecondary);
        t.Width = 128; t.VerticalAlignment = VerticalAlignment.Center;
        row.Children.Add(t);
        foreach (var argb in SpriteStudioView.Palette.Take(12).Append(0x00FFFFFFu))
        {
            var c = new LColor((byte)(argb >> 16), (byte)(argb >> 8), (byte)argb, (byte)(argb >> 24));
            var selected = c.R == current.R && c.G == current.G && c.B == current.B && c.A == current.A;
            var swatch = new Button
            {
                Width = 28, Height = 28, Padding = new Thickness(0), CornerRadius = new CornerRadius(14),
                Background = new Avalonia.Media.Immutable.ImmutableSolidColorBrush(Avalonia.Media.Color.FromArgb(c.A == 0 ? (byte)40 : c.A, c.R, c.G, c.B)),
                BorderBrush = LunetTokens.Brush(selected ? LunetTokens.Color.TextPrimary : LunetTokens.Color.BorderStrong), BorderThickness = new Thickness(selected ? 3 : 1),
            };
            AutomationProperties.SetName(swatch, c.A == 0 ? "Transparente" : $"Cor #{c.R:X2}{c.G:X2}{c.B:X2}");
            swatch.Click += (_, _) => { set(c); Changed(); };
            row.Children.Add(swatch);
        }
        _knobs.Children.Add(new ScrollViewer { Content = row, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Hidden, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled });
    }

    private static TextBlock Section(string text)
    {
        var t = LunetUi.Text(text.ToUpperInvariant(), LunetTokens.Type.Caption, LunetTokens.Color.TextTertiary, FontWeight.SemiBold);
        t.Margin = new Thickness(0, LunetTokens.Space.S, 0, 0);
        return t;
    }
}
