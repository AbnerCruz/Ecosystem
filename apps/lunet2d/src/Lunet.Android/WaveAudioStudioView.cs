using Android.Content;
using Android.Graphics;
using Android.Views;
using Android.Widget;
using Lunet.Studio;
using Path = System.IO.Path;
using AColor = Android.Graphics.Color;
using AndroidMediaPlayer = global::Android.Media.MediaPlayer;

namespace Lunet.Android;

/// <summary>Editor Android touch-first para efeitos WAV PCM, sem alterar o arquivo-fonte.</summary>
internal sealed class WaveAudioStudioView : LinearLayout, IDisposable
{
    private readonly WaveAudioDocument _audio;
    private readonly Action<byte[]> _save;
    private readonly Action _close;
    private readonly string _cacheDirectory;
    private readonly WaveformCanvas _canvas;
    private readonly SeekBar _start;
    private readonly SeekBar _end;
    private readonly SeekBar _gain;
    private readonly SeekBar _loopStart;
    private readonly SeekBar _loopEnd;
    private readonly CheckBox _loop;
    private readonly TextView _details;
    private AndroidMediaPlayer? _player;
    private string? _tempFile;
    private bool _disposed;

    public WaveAudioStudioView(Context context, WaveAudioDocument audio, string cacheDirectory,
        Action<byte[]> save, Action close) : base(context)
    {
        _audio = audio;
        _save = save;
        _close = close;
        _cacheDirectory = cacheDirectory;
        Orientation = Orientation.Vertical;
        SetPadding(Dp(12), Dp(10), Dp(12), Dp(10));
        SetBackgroundColor(AColor.Rgb(17, 20, 29));

        var head = new LinearLayout(context) { Orientation = Orientation.Horizontal };
        var title = new TextView(context) { Text = "Audio Studio", TextSize = 21 };
        title.SetTextColor(AColor.White);
        head.AddView(title, new LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f));
        head.AddView(Button("Fechar", () => { Stop(); _close(); }));
        AddView(head);

        _details = new TextView(context) { TextSize = 13 };
        _details.SetTextColor(AColor.Rgb(190, 205, 220));
        AddView(_details);

        _canvas = new WaveformCanvas(context, audio.GetPeaks(320));
        AddView(_canvas, new LayoutParams(ViewGroup.LayoutParams.MatchParent, Dp(160)));
        var controls = new LinearLayout(context) { Orientation = Orientation.Horizontal };
        controls.AddView(Button("▶ Ouvir", Play), new LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f));
        controls.AddView(Button("■ Parar", Stop), new LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f));
        controls.AddView(Button("Salvar cópia WAV", Save), new LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1.8f));
        AddView(controls);

        var scroll = new ScrollView(context);
        var settings = new LinearLayout(context) { Orientation = Orientation.Vertical };
        settings.SetPadding(Dp(4), Dp(12), Dp(4), Dp(12));
        scroll.AddView(settings);
        AddView(scroll, new LayoutParams(ViewGroup.LayoutParams.MatchParent, 0, 1f));

        _start = Slider(settings, "Início do recorte", 0);
        _end = Slider(settings, "Fim do recorte", 1000);
        _gain = Slider(settings, "Volume (0–200%)", 100, maximum: 200);
        _loop = new CheckBox(context) { Text = "Gravar pontos de loop no WAV (SMPL)", Checked = false };
        settings.AddView(_loop);
        _loopStart = Slider(settings, "Início do loop", 0);
        _loopEnd = Slider(settings, "Fim do loop", 1000);
        settings.AddView(new TextView(context)
        {
            Text = "O original é preservado. A cópia é salva em Content/Audio. Reprodução em loop usa a região selecionada; pontos SMPL ficam gravados para ferramentas compatíveis.",
            TextSize = 12
        });

        _start.ProgressChanged += (_, _) => Refresh();
        _end.ProgressChanged += (_, _) => Refresh();
        _gain.ProgressChanged += (_, _) => Refresh();
        _loopStart.ProgressChanged += (_, _) => Refresh();
        _loopEnd.ProgressChanged += (_, _) => Refresh();
        _loop.CheckedChange += (_, _) => Refresh();
        Refresh();
    }

    private int Dp(int value) => (int)(value * Context!.Resources!.DisplayMetrics!.Density);
    private Button Button(string label, Action action)
    {
        var result = new Button(Context) { Text = label, TextSize = 12 };
        result.SetAllCaps(false);
        result.Click += (_, _) =>
        {
            try { action(); }
            catch (Exception ex) when (ex is IOException or ArgumentException or InvalidOperationException or UnauthorizedAccessException)
            { Toast.MakeText(Context, ex.Message, ToastLength.Long)?.Show(); }
        };
        return result;
    }

    private SeekBar Slider(LinearLayout parent, string name, int progress, int maximum = 1000)
    {
        parent.AddView(new TextView(Context) { Text = name, TextSize = 13 });
        var slider = new SeekBar(Context) { Max = maximum, Progress = progress };
        parent.AddView(slider);
        return slider;
    }

    private static string Time(double seconds) => TimeSpan.FromSeconds(seconds).ToString(@"mm\:ss\.fff");

    private void Refresh()
    {
        if (_disposed) return;
        int start = _start.Progress, end = _end.Progress;
        if (end <= start)
        {
            if (start == 1000) { _start.Progress = 999; start = 999; }
            _end.Progress = start + 1;
            end = _end.Progress;
        }
        int ls = Math.Clamp(_loopStart.Progress, start, end - 1);
        int le = Math.Clamp(_loopEnd.Progress, ls + 1, end);
        if (ls != _loopStart.Progress) _loopStart.Progress = ls;
        if (le != _loopEnd.Progress) _loopEnd.Progress = le;
        _loopStart.Enabled = _loop.Checked;
        _loopEnd.Enabled = _loop.Checked;
        double duration = _audio.DurationSeconds;
        _details.Text = $"{_audio.SampleRate} Hz · {_audio.Channels} canal(is) · {Time(duration)}\n" +
            $"Recorte {Time(duration * start / 1000.0)}–{Time(duration * end / 1000.0)} · {_gain.Progress}% volume" +
            (_loop.Checked ? $" · loop {Time(duration * ls / 1000.0)}–{Time(duration * le / 1000.0)}" : "");
        _canvas.SetSelection(start / 1000f, end / 1000f, _loop.Checked, ls / 1000f, le / 1000f);
    }

    private byte[] Output(bool includeLoop)
    {
        double duration = _audio.DurationSeconds;
        double start = duration * _start.Progress / 1000.0;
        double end = duration * _end.Progress / 1000.0;
        double? ls = includeLoop && _loop.Checked ? duration * _loopStart.Progress / 1000.0 : null;
        double? le = includeLoop && _loop.Checked ? duration * _loopEnd.Progress / 1000.0 : null;
        return _audio.Render(start, end, _gain.Progress / 100f, ls, le);
    }

    private void Save()
    {
        if (_disposed) return;
        _save(Output(includeLoop: true));
        Toast.MakeText(Context, "Cópia WAV salva em Content/Audio.", ToastLength.Short)?.Show();
    }

    private void Play()
    {
        if (_disposed) return;
        Stop();
        // Preview do trecho; em modo loop, repetimos somente o intervalo marcado.
        byte[] data = _loop.Checked
            ? _audio.Render(_audio.DurationSeconds * _loopStart.Progress / 1000.0,
                _audio.DurationSeconds * _loopEnd.Progress / 1000.0, _gain.Progress / 100f)
            : Output(includeLoop: false);
        Directory.CreateDirectory(_cacheDirectory);
        _tempFile = Path.Combine(_cacheDirectory, "preview-" + Guid.NewGuid().ToString("N") + ".wav");
        File.WriteAllBytes(_tempFile, data);
        var player = new AndroidMediaPlayer();
        _player = player;
        player.SetDataSource(_tempFile);
        player.Looping = _loop.Checked;
        player.Prepared += (_, _) => { if (!_disposed && ReferenceEquals(_player, player)) player.Start(); };
        player.PrepareAsync();
    }

    private void Stop()
    {
        var player = _player;
        _player = null;
        if (player is not null)
        {
            player.Release();
            player.Dispose();
        }
        if (_tempFile is { } path)
        {
            _tempFile = null;
            try { File.Delete(path); }
            catch (IOException) { /* cache apenas; nunca apaga arquivos do projeto */ }
        }
    }

    /// <summary>Interrompe reprodução quando o aplicativo perde foco.</summary>
    public void Suspend() => Stop();

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
    }

    private sealed class WaveformCanvas : View
    {
        private readonly WavePeak[] _peaks;
        private readonly Paint _paint = new(PaintFlags.AntiAlias);
        private float _from, _to = 1, _loopFrom, _loopTo = 1;
        private bool _loop;

        public WaveformCanvas(Context context, WavePeak[] peaks) : base(context) { _peaks = peaks; }
        public void SetSelection(float from, float to, bool loop, float loopFrom, float loopTo)
        { _from = from; _to = to; _loop = loop; _loopFrom = loopFrom; _loopTo = loopTo; Invalidate(); }

        protected override void OnDraw(Canvas? canvas)
        {
            base.OnDraw(canvas);
            if (canvas is null) return;
            float width = Width, height = Height, mid = height / 2f;
            canvas.DrawColor(AColor.Rgb(24, 30, 43));
            _paint.Color = AColor.Rgb(47, 57, 77);
            canvas.DrawRect(0, 0, width * _from, height, _paint);
            canvas.DrawRect(width * _to, 0, width, height, _paint);
            _paint.Color = AColor.Rgb(44, 184, 166);
            for (int i = 0; i < _peaks.Length; i++)
            {
                float x = (i + .5f) * width / _peaks.Length;
                canvas.DrawLine(x, mid - _peaks[i].Maximum * mid * .88f,
                    x, mid - _peaks[i].Minimum * mid * .88f, _paint);
            }
            _paint.Color = AColor.Rgb(255, 200, 88);
            canvas.DrawLine(width * _from, 0, width * _from, height, _paint);
            canvas.DrawLine(width * _to, 0, width * _to, height, _paint);
            if (_loop)
            {
                _paint.Color = AColor.Rgb(190, 140, 255);
                canvas.DrawLine(width * _loopFrom, 0, width * _loopFrom, height, _paint);
                canvas.DrawLine(width * _loopTo, 0, width * _loopTo, height, _paint);
            }
        }
    }
}
