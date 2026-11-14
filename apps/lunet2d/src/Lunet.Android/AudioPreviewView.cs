using Android.Content;
using Android.Graphics;
using Android.OS;
using Android.Views;
using Android.Widget;
using Lunet.Studio;
using AColor = Android.Graphics.Color;
using AndroidMediaPlayer = global::Android.Media.MediaPlayer;
using Path = System.IO.Path;

namespace Lunet.Android;

/// <summary>
/// Prévia de áudio reutilizável: forma de onda com cursor de reprodução, tocar/pausar, tocar a partir de um ponto tocado
/// na onda e tempo decorrido. Serve ao Audio Studio (resultado do recorte), à criação de efeitos e ao painel de Assets.
/// O que se ouve é sempre o que será salvo: o estúdio fornece os bytes WAV já com recorte e volume aplicados.
/// </summary>
internal sealed class AudioPreviewView : LinearLayout, IDisposable
{
    private readonly Func<byte[]>? _render;
    private readonly string? _file;
    private readonly string _cacheDirectory;
    private readonly WaveCanvas _wave;
    private readonly Button _toggle;
    private readonly TextView _time;
    private readonly Button _loopButton;
    private readonly Handler _handler = new(Looper.MainLooper!);
    private readonly Java.Lang.Runnable _tick;
    private readonly Java.Lang.Runnable _restart;
    private AndroidMediaPlayer? _player;
    private string? _tempFile;
    private bool _playing, _disposed, _loop, _prepared;
    private double _windowStart, _windowEnd = 1, _windowSeconds;

    /// <param name="peaks">Envelope da onda; nulo quando o formato não permite desenhá-la (MP3/OGG).</param>
    /// <param name="totalSeconds">Duração da fonte inteira, para rotular o tempo.</param>
    /// <param name="render">Gera o WAV a ouvir (recorte/volume atuais). Nulo quando <paramref name="file"/> toca direto.</param>
    public AudioPreviewView(Context context, WavePeak[]? peaks, double totalSeconds, string cacheDirectory,
        Func<byte[]>? render = null, string? file = null, int waveHeightDp = 120) : base(context)
    {
        _render = render;
        _file = file;
        _cacheDirectory = cacheDirectory;
        _windowSeconds = totalSeconds;
        Orientation = Orientation.Vertical;
        _tick = new Java.Lang.Runnable(Tick);
        _restart = new Java.Lang.Runnable(() => { if (!_disposed && _playing) Play(); });

        _wave = new WaveCanvas(context, peaks, Seek);
        AddView(_wave, new LayoutParams(ViewGroup.LayoutParams.MatchParent, LunetUi.Dp(context, waveHeightDp)));

        var controls = LunetUi.Row(context, 8);
        controls.SetPadding(0, LunetUi.Dp(context, 8), 0, 0);
        _toggle = LunetUi.Button(context, "▶ Ouvir", Toggle, ButtonStyle.Primary);
        controls.AddView(_toggle, new LayoutParams(ViewGroup.LayoutParams.WrapContent, LunetUi.Dp(context, 44)));
        controls.AddView(LunetUi.IconByName(context, "loop", "Repetir", () => { }, style: ButtonStyle.Quiet), new LayoutParams(LunetUi.Dp(context, 44), LunetUi.Dp(context, 44)));
        _loopButton = (Button)controls.GetChildAt(1)!;
        _loopButton.Click += (_, _) => SetLooping(!_loop);
        _time = LunetUi.Label(context, "", LunetUi.TextLabel, LunetUi.TextMuted);
        _time.Gravity = GravityFlags.End;
        controls.AddView(_time, LunetUi.Weight());
        AddView(controls);
        UpdateTime(0);
    }

    public bool IsPlaying => _playing;

    /// <summary>Liga/desliga a repetição (o botão acompanha).</summary>
    public void SetLooping(bool loop)
    {
        _loop = loop;
        LunetUi.Style(Context!, _loopButton, _loop ? ButtonStyle.Selected : ButtonStyle.Quiet);
        if (_player is not null) _player.Looping = _loop;
    }

    /// <summary>Região da onda que é tocada (frações 0–1 da fonte) e o rótulo de duração dela.</summary>
    public void SetWindow(double startFraction, double endFraction, double windowSeconds)
    {
        _windowStart = startFraction; _windowEnd = Math.Max(startFraction + 0.0001, endFraction); _windowSeconds = windowSeconds;
        UpdateTime(0);
    }

    public void SetOverlay(float from, float to, bool loop, float loopFrom, float loopTo) => _wave.SetOverlay(from, to, loop, loopFrom, loopTo);

    public void SetPeaks(WavePeak[] peaks) => _wave.SetPeaks(peaks);

    /// <summary>O som mudou: se estiver tocando, recomeça com a nova versão para o ouvido acompanhar o ajuste.</summary>
    public void ContentChanged()
    {
        if (!_playing) return;
        _handler.RemoveCallbacks(_restart);
        _handler.PostDelayed(_restart, 350);
    }

    private void Toggle()
    {
        if (_playing) Pause(); else Play();
    }

    public void Play(double startFraction = 0)
    {
        if (_disposed) return;
        Release();
        string path;
        try
        {
            if (_render is not null)
            {
                Directory.CreateDirectory(_cacheDirectory);
                _tempFile = Path.Combine(_cacheDirectory, "preview-" + Guid.NewGuid().ToString("N") + ".wav");
                File.WriteAllBytes(_tempFile, _render());
                path = _tempFile;
            }
            else path = _file ?? throw new InvalidOperationException("Sem áudio para tocar.");
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or InvalidOperationException or UnauthorizedAccessException)
        {
            Toast.MakeText(Context, "Não foi possível preparar o som: " + ex.Message, ToastLength.Long)?.Show();
            return;
        }
        try
        {
            var player = new AndroidMediaPlayer();
            _player = player;
            _prepared = false;
            player.SetDataSource(path);
            player.Looping = _loop;
            player.Prepared += (_, _) =>
            {
                if (_disposed || !ReferenceEquals(_player, player)) return;
                _prepared = true;
                if (startFraction > 0) player.SeekTo((int)(player.Duration * startFraction));
                player.Start();
                _handler.Post(_tick);
            };
            player.Completion += (_, _) => { if (ReferenceEquals(_player, player) && !_loop) Finished(); };
            player.Error += (_, e) =>
            {
                e.Handled = true;
                Toast.MakeText(Context, "O aparelho não conseguiu reproduzir este arquivo.", ToastLength.Long)?.Show();
                Stop();
            };
            player.PrepareAsync();
            _playing = true;
            LunetUi.SetLabel(_toggle, "⏸ Pausar");
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or InvalidOperationException or Java.Lang.Exception)
        {
            Toast.MakeText(Context, "Não foi possível reproduzir: " + ex.Message, ToastLength.Long)?.Show();
            Stop();
        }
    }

    private void Pause()
    {
        // Pausar mantém a posição; tocar de novo recomeça do início do trecho.
        Stop();
    }

    public void Stop()
    {
        _handler.RemoveCallbacks(_tick);
        _handler.RemoveCallbacks(_restart);
        _playing = false;
        Release();
        _wave.SetPlayhead(-1);
        UpdateTime(0);
        if (!_disposed) LunetUi.SetLabel(_toggle, "▶ Ouvir");
    }

    private void Finished()
    {
        _handler.RemoveCallbacks(_tick);
        _playing = false;
        _wave.SetPlayhead(-1);
        UpdateTime(0);
        LunetUi.SetLabel(_toggle, "▶ Ouvir");
    }

    private void Release()
    {
        var player = _player;
        _player = null;
        _prepared = false;
        if (player is not null)
        {
            try { player.Release(); } catch (Java.Lang.IllegalStateException) { /* já liberado */ }
            player.Dispose();
        }
        if (_tempFile is { } path)
        {
            _tempFile = null;
            try { File.Delete(path); } catch (IOException) { /* só cache; nunca apaga arquivos do projeto */ }
        }
    }

    private void Tick()
    {
        if (_disposed || _player is not { } player || !_prepared) return;
        try
        {
            double duration = Math.Max(1, player.Duration);
            double progress = Math.Clamp(player.CurrentPosition / duration, 0, 1);
            _wave.SetPlayhead((float)(_windowStart + progress * (_windowEnd - _windowStart)));
            UpdateTime(progress * _windowSeconds);
        }
        catch (Java.Lang.IllegalStateException) { return; }
        _handler.PostDelayed(_tick, 33);
    }

    /// <summary>Tocar a partir do ponto tocado na onda (dentro da região audível).</summary>
    private void Seek(float fraction)
    {
        double inside = Math.Clamp((fraction - _windowStart) / (_windowEnd - _windowStart), 0, 0.999);
        if (_playing && _player is { } player && _prepared)
        {
            try { player.SeekTo((int)(player.Duration * inside)); return; }
            catch (Java.Lang.IllegalStateException) { /* cai para novo Play */ }
        }
        Play(inside);
    }

    private void UpdateTime(double seconds) =>
        _time.Text = $"{Format(seconds)} / {Format(_windowSeconds)}";

    private static string Format(double seconds) => TimeSpan.FromSeconds(Math.Max(0, seconds)).ToString(@"m\:ss\.f");

    /// <summary>Interrompe quando o app perde foco.</summary>
    public void Suspend() => Stop();

    public void Dispose()
    {
        if (_disposed) return;
        Stop();
        _disposed = true;
    }

    protected override void OnDetachedFromWindow() { Stop(); base.OnDetachedFromWindow(); }

    private sealed class WaveCanvas : View
    {
        private WavePeak[]? _peaks;
        private readonly Action<float> _seek;
        private readonly Paint _paint = new(PaintFlags.AntiAlias);
        private float _from, _to = 1, _loopFrom, _loopTo = 1, _head = -1;
        private bool _loop;

        public WaveCanvas(Context context, WavePeak[]? peaks, Action<float> seek) : base(context)
        {
            _peaks = peaks; _seek = seek;
            ContentDescription = "Forma de onda. Toque para ouvir a partir deste ponto.";
        }

        public void SetPeaks(WavePeak[] peaks) { _peaks = peaks; Invalidate(); }
        public void SetOverlay(float from, float to, bool loop, float loopFrom, float loopTo)
        { _from = from; _to = to; _loop = loop; _loopFrom = loopFrom; _loopTo = loopTo; Invalidate(); }
        public void SetPlayhead(float fraction) { _head = fraction; Invalidate(); }

        public override bool OnTouchEvent(MotionEvent? e)
        {
            if (e is null || Width == 0) return false;
            if (e.ActionMasked is MotionEventActions.Down or MotionEventActions.Move)
            {
                Parent?.RequestDisallowInterceptTouchEvent(true);
                if (e.ActionMasked == MotionEventActions.Down) _seek(Math.Clamp(e.GetX() / Width, 0, 1));
                return true;
            }
            return base.OnTouchEvent(e);
        }

        protected override void OnDraw(Canvas? canvas)
        {
            base.OnDraw(canvas);
            if (canvas is null) return;
            float width = Width, height = Height, mid = height / 2f, dp = Resources!.DisplayMetrics!.Density;
            _paint.SetStyle(Paint.Style.Fill);
            _paint.Color = LunetUi.Surface;
            canvas.DrawRoundRect(0, 0, width, height, 10 * dp, 10 * dp, _paint);
            if (_peaks is null)
            {
                _paint.Color = LunetUi.TextFaint;
                _paint.TextSize = 12 * dp;
                _paint.TextAlign = Paint.Align.Center;
                canvas.DrawText("Este formato toca, mas não mostra a onda.", width / 2, mid, _paint);
                DrawHead(canvas, width, height, dp);
                return;
            }
            // Fora do recorte: escurecido.
            _paint.Color = AColor.Argb(110, 0, 0, 0);
            canvas.DrawRect(0, 0, width * _from, height, _paint);
            canvas.DrawRect(width * _to, 0, width, height, _paint);
            _paint.StrokeWidth = Math.Max(1f, width / _peaks.Length * .7f);
            _paint.Color = AColor.Rgb(44, 184, 166);
            for (int i = 0; i < _peaks.Length; i++)
            {
                float x = (i + .5f) * width / _peaks.Length;
                float top = mid - Math.Max(_peaks[i].Maximum, 0.01f) * mid * .88f, bottom = mid - Math.Min(_peaks[i].Minimum, -0.01f) * mid * .88f;
                canvas.DrawLine(x, top, x, bottom, _paint);
            }
            _paint.StrokeWidth = 2 * dp;
            _paint.Color = AColor.Rgb(255, 200, 88);
            canvas.DrawLine(width * _from, 0, width * _from, height, _paint);
            canvas.DrawLine(width * _to, 0, width * _to, height, _paint);
            if (_loop)
            {
                _paint.Color = AColor.Rgb(190, 140, 255);
                canvas.DrawLine(width * _loopFrom, 0, width * _loopFrom, height, _paint);
                canvas.DrawLine(width * _loopTo, 0, width * _loopTo, height, _paint);
            }
            DrawHead(canvas, width, height, dp);
        }

        private void DrawHead(Canvas canvas, float width, float height, float dp)
        {
            if (_head < 0) return;
            float x = Math.Clamp(_head, 0, 1) * width;
            _paint.Color = AColor.White;
            _paint.StrokeWidth = 2 * dp;
            canvas.DrawLine(x, 0, x, height, _paint);
            _paint.SetStyle(Paint.Style.Fill);
            canvas.DrawCircle(x, 5 * dp, 5 * dp, _paint);
        }
    }
}
