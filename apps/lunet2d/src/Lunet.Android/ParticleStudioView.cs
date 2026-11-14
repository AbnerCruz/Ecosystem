using Android.App;
using Android.Content;
using Android.Graphics;
using Android.Views;
using Android.Widget;
using Lunet.Graphics;
using Lunet.Studio;
using AColor = Android.Graphics.Color;
using LColor = Lunet.Graphics.Color;

namespace Lunet.Android;

/// <summary>
/// Particle Studio (LUNET-504): presets, sliders grouped by Emissão/Movimento/Aparência and a live preview that runs the
/// SAME <see cref="ParticleEmitter"/> the game runs (headless device for the texture; drawn with Canvas from
/// <see cref="ParticleEmitter.Snapshot"/>). Tap the preview to burst; drag to move the emitter.
/// </summary>
internal sealed class ParticleStudioView : LinearLayout
{
    private readonly Action<ParticleDraft> _save;
    private readonly Action _close;
    private readonly Preview _preview;
    private readonly LinearLayout _controls;
    private readonly TextView _title;
    private ParticleDraft _draft;
    private string _group = "Emissão";
    private readonly Texture2D _texture;

    public ParticleStudioView(Context context, ParticleDraft draft, string name, Action<ParticleDraft> save, Action close) : base(context)
    {
        _draft = draft; _save = save; _close = close;
        Orientation = Orientation.Vertical;
        SetBackgroundColor(LunetUi.Background);
        var device = new GraphicsDevice(new HeadlessGraphicsBackend(), 360, 640);
        _texture = Texture2D.CreateCircle(device, 16, LColor.White);

        var bar = LunetUi.Row(context, 4);
        bar.SetPadding(Dp(4), Dp(6), Dp(6), Dp(6));
        bar.AddView(LunetUi.Icon(context, "‹", "Voltar", () => { _preview.Stop(); _close(); }), Square(44));
        _title = LunetUi.Label(context, name, LunetUi.TextLabel, LunetUi.Text, bold: true);
        bar.AddView(_title, LunetUi.Weight());
        bar.AddView(LunetUi.Button(context, "Burst", () => _preview.Burst(), ButtonStyle.Secondary), new LayoutParams(ViewGroup.LayoutParams.WrapContent, Dp(40)));
        bar.AddView(LunetUi.Button(context, "Salvar", () => _save(_draft), ButtonStyle.Primary), new LayoutParams(ViewGroup.LayoutParams.WrapContent, Dp(40)));
        AddView(bar);

        _preview = new Preview(context, this);
        AddView(_preview, new LayoutParams(ViewGroup.LayoutParams.MatchParent, 0, 1.1f));

        var presets = LunetUi.Row(context, 6);
        presets.SetPadding(Dp(8), Dp(6), Dp(8), Dp(2));
        foreach (var preset in ParticlePresets.All)
            presets.AddView(LunetUi.Chip(context, preset.Name, () => { _draft = ParticleDraft.From(preset); Rebuild(); Toast.MakeText(Context, preset.Hint, ToastLength.Short)?.Show(); }));
        var presetScroll = new HorizontalScrollView(context) { HorizontalScrollBarEnabled = false };
        presetScroll.AddView(presets);
        AddView(presetScroll);

        var groups = LunetUi.Row(context, 6);
        groups.SetPadding(Dp(8), Dp(2), Dp(8), Dp(2));
        foreach (var g in new[] { "Emissão", "Movimento", "Aparência", "Cores" })
            groups.AddView(LunetUi.Chip(context, g, () => { _group = g; BuildControls(); }));
        AddView(groups);

        _controls = LunetUi.Column(context, 2);
        _controls.SetPadding(Dp(14), Dp(4), Dp(14), Dp(10));
        var scroll = new ScrollView(context);
        scroll.AddView(_controls);
        AddView(scroll, new LayoutParams(ViewGroup.LayoutParams.MatchParent, 0, 1f));
        Rebuild();
    }

    private int Dp(int value) => LunetUi.Dp(Context!, value);
    private LayoutParams Square(int dp) => new(Dp(dp), Dp(dp));

    private void Rebuild()
    {
        _preview.Reset();
        BuildControls();
    }

    private void Changed()
    {
        try { _preview.Reset(); }
        catch (ArgumentOutOfRangeException ex) { Toast.MakeText(Context, ex.Message, ToastLength.Short)?.Show(); }
    }

    private void Slider(string label, float value, float min, float max, Action<float> set, string unit = "")
    {
        var text = LunetUi.Label(Context!, "", LunetUi.TextCaption, LunetUi.TextMuted);
        void Show(float v) => text.Text = $"{label}: {v:0.##}{unit}";
        Show(value);
        var bar = new SeekBar(Context) { Max = 1000, Progress = (int)(Math.Clamp((value - min) / (max - min), 0, 1) * 1000) };
        bar.ProgressChanged += (_, e) =>
        {
            if (!e.FromUser) return;
            var v = min + e.Progress / 1000f * (max - min);
            Show(v);
            set(v);
            Changed();
        };
        _controls.AddView(text);
        _controls.AddView(bar);
    }

    private void Choice<T>(string label, T current, (T Value, string Name)[] options, Action<T> set) where T : notnull
    {
        _controls.AddView(LunetUi.Label(Context!, label, LunetUi.TextCaption, LunetUi.TextMuted));
        var row = LunetUi.Row(Context!, 6);
        foreach (var (value, name) in options)
        {
            var v = value;
            row.AddView(LunetUi.Chip(Context!, name, () => { set(v); Changed(); BuildControls(); }, selected: Equals(v, current)));
        }
        var scroll = new HorizontalScrollView(Context) { HorizontalScrollBarEnabled = false };
        scroll.AddView(row);
        _controls.AddView(scroll);
    }

    private void BuildControls()
    {
        _controls.RemoveAllViews();
        var d = _draft;
        switch (_group)
        {
            case "Emissão":
                Slider("Partículas por segundo (0 = só burst)", d.EmissionRate, 0, 400, v => d.EmissionRate = MathF.Round(v));
                Slider("Quantidade no burst", d.Burst, 0, 300, v => d.Burst = (int)v);
                Slider("Máximo de partículas vivas", d.MaxParticles, 10, 2000, v => d.MaxParticles = (int)v);
                Choice("Forma de nascimento", d.Shape, [(ParticleShape.Point, "Ponto"), (ParticleShape.Circle, "Círculo"), (ParticleShape.Rectangle, "Retângulo"), (ParticleShape.Line, "Linha")], v => d.Shape = v);
                if (d.Shape != ParticleShape.Point) Slider(d.Shape == ParticleShape.Circle ? "Raio" : "Largura", d.ShapeWidth, 0, 400, v => d.ShapeWidth = v, " px");
                if (d.Shape == ParticleShape.Rectangle) Slider("Altura", d.ShapeHeight, 0, 400, v => d.ShapeHeight = v, " px");
                break;
            case "Movimento":
                Slider("Velocidade mínima", d.MinSpeed, 0, 600, v => d.MinSpeed = v, " px/s");
                Slider("Velocidade máxima", d.MaxSpeed, 0, 600, v => d.MaxSpeed = v, " px/s");
                Slider("Direção", d.Direction, -180, 180, v => d.Direction = v, "°");
                Slider("Abertura (360 = todas as direções)", d.Spread, 0, 360, v => d.Spread = v, "°");
                Slider("Gravidade horizontal", d.GravityX, -600, 600, v => d.GravityX = v);
                Slider("Gravidade vertical", d.GravityY, -600, 600, v => d.GravityY = v);
                Slider("Resistência do ar", d.Drag, 0, 8, v => d.Drag = v);
                Slider("Giro mínimo", d.MinSpin, -720, 720, v => d.MinSpin = v, "°/s");
                Slider("Giro máximo", d.MaxSpin, -720, 720, v => d.MaxSpin = v, "°/s");
                break;
            case "Aparência":
                Slider("Vida", (float)d.Lifetime, 0.05f, 8, v => d.Lifetime = v, " s");
                Slider("Variação da vida", d.LifetimeVariance, 0, 1, v => d.LifetimeVariance = v);
                Slider("Tamanho inicial", d.StartSize, 0, 80, v => d.StartSize = v, " px");
                Slider("Tamanho final", d.EndSize, 0, 80, v => d.EndSize = v, " px");
                Slider("Variação do tamanho", d.SizeVariance, 0, 1, v => d.SizeVariance = v);
                Slider("Rotação inicial aleatória", d.RotationVariance, 0, 360, v => d.RotationVariance = v, "°");
                Choice("Mistura", d.Additive, [(false, "Normal"), (true, "Brilho (aditiva)")], v => d.Additive = v);
                break;
            default:
                ColorRow("Cor inicial", d.StartColor, c => d.StartColor = c);
                ColorRow("Cor final", d.EndColor, c => d.EndColor = c);
                _controls.AddView(LunetUi.Label(Context!, d.Stops.Count == 0 ? "Gradiente: só início → fim." : $"Gradiente com {d.Stops.Count} cores.", LunetUi.TextCaption, LunetUi.TextMuted));
                var row = LunetUi.Row(Context!, 6);
                row.AddView(LunetUi.Button(Context!, "Fogo", () => { d.Stops = [new(0, LColor.FromHex(0xFFF2A8)), new(0.4, LColor.FromHex(0xFFA000)), new(1, LColor.FromHex(0x601010).WithAlpha(0))]; Changed(); BuildControls(); }, ButtonStyle.Secondary));
                row.AddView(LunetUi.Button(Context!, "Gelo", () => { d.Stops = [new(0, LColor.White), new(0.5, LColor.FromHex(0x9EE7FF)), new(1, LColor.FromHex(0x2050C0).WithAlpha(0))]; Changed(); BuildControls(); }, ButtonStyle.Secondary));
                row.AddView(LunetUi.Button(Context!, "Arco-íris", () => { d.Stops = [new(0, LColor.FromHex(0xFF4D6D)), new(0.25, LColor.FromHex(0xFFD24D)), new(0.5, LColor.FromHex(0x5BE37D)), new(0.75, LColor.FromHex(0x4DD2FF)), new(1, LColor.FromHex(0xB06BFF).WithAlpha(0))]; Changed(); BuildControls(); }, ButtonStyle.Secondary));
                row.AddView(LunetUi.Button(Context!, "Sem gradiente", () => { d.Stops = []; Changed(); BuildControls(); }, ButtonStyle.Quiet));
                var scroll = new HorizontalScrollView(Context) { HorizontalScrollBarEnabled = false };
                scroll.AddView(row);
                _controls.AddView(scroll);
                break;
        }
    }

    private void ColorRow(string label, LColor color, Action<LColor> set)
    {
        var row = LunetUi.Row(Context!, 10);
        var swatch = new View(Context) { Background = LunetUi.Shape(Context!, AColor.Argb(color.A, color.R, color.G, color.B), LunetUi.RadiusSmall, LunetUi.Border) };
        row.AddView(swatch, new LayoutParams(Dp(36), Dp(36)));
        row.AddView(LunetUi.Label(Context!, label + $"  #{color.R:X2}{color.G:X2}{color.B:X2} · {color.A * 100 / 255}%", LunetUi.TextBody, LunetUi.Text), LunetUi.Weight());
        row.Clickable = true;
        row.Click += (_, _) =>
        {
            var input = LunetUi.Input(Context!, "#RRGGBB ou #RRGGBBAA", $"#{color.R:X2}{color.G:X2}{color.B:X2}{color.A:X2}");
            var frame = new FrameLayout(Context);
            frame.SetPadding(Dp(20), Dp(8), Dp(20), Dp(4));
            frame.AddView(input);
            new AlertDialog.Builder(Context, global::Android.Resource.Style.ThemeDeviceDefaultDialogAlert)!
                .SetTitle(label)!.SetView(frame)!
                .SetPositiveButton("OK", (_, _) =>
                {
                    var hex = (input.Text ?? "").Trim().TrimStart('#');
                    if (hex.Length is 6 or 8 && uint.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out var v))
                    {
                        set(hex.Length == 6 ? LColor.FromHex(v) : new LColor((byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v));
                        Changed();
                        BuildControls();
                    }
                })!.SetNegativeButton("Cancelar", (_, _) => { })!.Show();
        };
        _controls.AddView(row);
    }

    /// <summary>The live preview: the real emitter, ticked by Choreographer-like posts, drawn with Canvas.</summary>
    private sealed class Preview : View
    {
        private readonly ParticleStudioView _owner;
        private readonly Paint _paint = new(PaintFlags.AntiAlias);
        private ParticleEmitter? _emitter;
        private ParticleSnapshot[] _snapshot = [];
        private long _last;
        private bool _running;
        private System.Numerics.Vector2 _origin;
        private int _background;
        private float Density => Resources?.DisplayMetrics?.Density ?? 1;

        private readonly Java.Lang.Runnable _tick;

        public Preview(Context context, ParticleStudioView owner) : base(context)
        {
            _owner = owner;
            _tick = new Java.Lang.Runnable(Tick);
            Clickable = true;
            LongClickable = true;
            LongClick += (_, _) => { _background = (_background + 1) % 3; Invalidate(); };
        }

        public void Reset()
        {
            var settings = _owner._draft.Build();
            _emitter = new ParticleEmitter(_owner._texture, settings, Math.Clamp(_owner._draft.MaxParticles, 1, 10_000), (ulong)Environment.TickCount64)
            { EmissionRate = _owner._draft.EmissionRate, Position = _origin };
            _snapshot = new ParticleSnapshot[_emitter.Capacity];
            if (_owner._draft.EmissionRate > 0) _emitter.Start();
            if (_owner._draft.Burst > 0) _emitter.Burst(_owner._draft.Burst, _origin);
            if (!_running) { _running = true; _last = Java.Lang.JavaSystem.NanoTime(); PostOnAnimation(_tick); }
        }

        public void Burst() => _emitter?.Burst(Math.Max(1, _owner._draft.Burst), _origin);

        public void Stop() => _running = false;

        private void Tick()
        {
            if (!_running || !IsAttachedToWindow) { _running = false; return; }
            long now = Java.Lang.JavaSystem.NanoTime();
            double dt = Math.Min(0.05, (now - _last) / 1e9);
            _last = now;
            _emitter?.Update(dt);
            Invalidate();
            PostOnAnimation(_tick);
        }

        protected override void OnSizeChanged(int w, int h, int oldw, int oldh)
        {
            base.OnSizeChanged(w, h, oldw, oldh);
            _origin = new System.Numerics.Vector2(w / Density / 2f, h / Density * 0.6f);
            if (_emitter is not null) _emitter.Position = _origin;
        }

        public override bool OnTouchEvent(MotionEvent? e)
        {
            if (e is null || _emitter is null) return false;
            var point = new System.Numerics.Vector2(e.GetX() / Density, e.GetY() / Density); // game units, like a 1x virtual screen
            switch (e.ActionMasked)
            {
                case MotionEventActions.Down: _origin = point; _emitter.Position = point; Burst(); break;
                case MotionEventActions.Move: _origin = point; _emitter.Position = point; break;
            }
            return base.OnTouchEvent(e) || true;
        }

        protected override void OnDraw(Canvas canvas)
        {
            canvas.DrawColor(_background switch { 0 => AColor.Rgb(14, 16, 24), 1 => AColor.Rgb(235, 238, 245), _ => AColor.Rgb(46, 92, 60) });
            if (_emitter is null) return;
            int count = _emitter.Snapshot(_snapshot);
            canvas.Save();
            canvas.Scale(Density, Density);
            _paint.BlendMode = _emitter.Settings.Additive ? global::Android.Graphics.BlendMode.Plus : global::Android.Graphics.BlendMode.SrcOver;
            for (int i = 0; i < count; i++)
            {
                ref readonly var p = ref _snapshot[i];
                _paint.Color = AColor.Argb(p.Color.A, p.Color.R, p.Color.G, p.Color.B);
                if (p.Rotation == 0) canvas.DrawCircle(p.Position.X, p.Position.Y, p.Size / 2, _paint);
                else
                {
                    canvas.Save();
                    canvas.Rotate(p.Rotation * 180 / MathF.PI, p.Position.X, p.Position.Y);
                    canvas.DrawRect(p.Position.X - p.Size / 2, p.Position.Y - p.Size / 4, p.Position.X + p.Size / 2, p.Position.Y + p.Size / 4, _paint);
                    canvas.Restore();
                }
            }
            _paint.BlendMode = global::Android.Graphics.BlendMode.SrcOver;
            _paint.Color = AColor.Argb(120, 255, 255, 255);
            canvas.DrawCircle(_origin.X, _origin.Y, 3, _paint);
            canvas.Restore();
            _paint.Color = AColor.Argb(160, 200, 200, 210);
            _paint.TextSize = 28;
            canvas.DrawText($"{count} partículas · toque = burst · arraste = mover · segure = fundo", 16, Height - 16, _paint);
        }
    }
}
