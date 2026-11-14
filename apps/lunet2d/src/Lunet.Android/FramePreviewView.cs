using Android.Content;
using Android.Graphics;
using Android.OS;
using Android.Views;
using Lunet.Studio;
using AColor = Android.Graphics.Color;

namespace Lunet.Android;

/// <summary>
/// Prévia de imagem ou sprite animado para o painel de Assets: fundo xadrez para mostrar transparência, ampliação sem
/// suavização (pixel art continua nítida) e, em sprites com vários frames, animação na velocidade do próprio arquivo.
/// Toque para pausar/continuar.
/// </summary>
internal sealed class FramePreviewView : View
{
    private readonly List<Bitmap> _frames;
    private readonly int _fps;
    private readonly Paint _bitmapPaint = new() { FilterBitmap = false };
    private readonly Paint _fill = new();
    private readonly Handler _handler = new(Looper.MainLooper!);
    private readonly Java.Lang.Runnable _tick;
    private int _index;
    private bool _playing;

    public FramePreviewView(Context context, IEnumerable<Bitmap> frames, int fps = 8) : base(context)
    {
        _frames = frames.ToList();
        _fps = Math.Clamp(fps, 1, 60);
        _tick = new Java.Lang.Runnable(Tick);
        Clickable = true;
        ContentDescription = _frames.Count > 1 ? "Prévia animada. Toque para pausar ou continuar." : "Prévia da imagem";
        Click += (_, _) => { if (_frames.Count > 1) SetPlaying(!_playing); };
    }

    public int FrameCount => _frames.Count;

    /// <summary>Converte um sprite do Studio em bitmaps (um por frame), sem criar arquivos.</summary>
    public static FramePreviewView FromSprite(Context context, SpriteDocument document)
    {
        var bitmaps = document.Frames.Select(pixels =>
        {
            var bitmap = Bitmap.CreateBitmap(document.Width, document.Height, Bitmap.Config.Argb8888!)!;
            var buffer = new int[pixels.Length];
            Buffer.BlockCopy(pixels, 0, buffer, 0, pixels.Length * 4);
            bitmap.SetPixels(buffer, 0, document.Width, 0, 0, document.Width, document.Height);
            return bitmap;
        });
        var view = new FramePreviewView(context, bitmaps, document.Fps);
        return view;
    }

    protected override void OnAttachedToWindow()
    {
        base.OnAttachedToWindow();
        if (_frames.Count > 1) SetPlaying(true);
    }

    protected override void OnDetachedFromWindow()
    {
        SetPlaying(false);
        base.OnDetachedFromWindow();
    }

    /// <summary>Libera os bitmaps quando o painel fecha.</summary>
    public void Release()
    {
        SetPlaying(false);
        foreach (var frame in _frames) frame.Dispose();
        _frames.Clear();
    }

    private void SetPlaying(bool playing)
    {
        _playing = playing;
        _handler.RemoveCallbacks(_tick);
        if (playing) _handler.PostDelayed(_tick, 1000 / _fps);
        Invalidate();
    }

    private void Tick()
    {
        if (!_playing || _frames.Count == 0) return;
        _index = (_index + 1) % _frames.Count;
        Invalidate();
        _handler.PostDelayed(_tick, 1000 / _fps);
    }

    protected override void OnDraw(Canvas? canvas)
    {
        base.OnDraw(canvas);
        if (canvas is null || _frames.Count == 0) return;
        float dp = Resources!.DisplayMetrics!.Density;
        _fill.Color = LunetUi.Surface;
        canvas.DrawRoundRect(0, 0, Width, Height, 12 * dp, 12 * dp, _fill);
        var bitmap = _frames[Math.Min(_index, _frames.Count - 1)];
        float margin = 12 * dp;
        float scale = Math.Min((Width - 2 * margin) / bitmap.Width, (Height - 2 * margin) / bitmap.Height);
        // Ampliações pequenas em passos inteiros mantêm cada pixel do mesmo tamanho.
        if (scale >= 1) scale = MathF.Floor(scale);
        float w = bitmap.Width * scale, h = bitmap.Height * scale;
        float left = (Width - w) / 2f, top = (Height - h) / 2f;
        var dest = new RectF(left, top, left + w, top + h);
        DrawChecker(canvas, dest, dp);
        canvas.DrawBitmap(bitmap, null, dest, _bitmapPaint);
        if (_frames.Count > 1)
        {
            _fill.Color = AColor.Argb(170, 0, 0, 0);
            canvas.DrawRoundRect(Width - 76 * dp, 8 * dp, Width - 8 * dp, 30 * dp, 8 * dp, 8 * dp, _fill);
            _fill.Color = LunetUi.Text;
            _fill.TextSize = 12 * dp;
            _fill.TextAlign = Paint.Align.Center;
            canvas.DrawText($"{_index + 1}/{_frames.Count}" + (_playing ? "" : " · pausado"), Width - 42 * dp, 23 * dp, _fill);
        }
    }

    private void DrawChecker(Canvas canvas, RectF dest, float dp)
    {
        int save = canvas.Save();
        canvas.ClipRect(dest);
        float cell = 8 * dp;
        for (float y = dest.Top, row = 0; y < dest.Bottom; y += cell, row++)
        for (float x = dest.Left, col = 0; x < dest.Right; x += cell, col++)
        {
            _fill.Color = (row + col) % 2 == 0 ? AColor.Rgb(40, 45, 58) : AColor.Rgb(52, 58, 72);
            canvas.DrawRect(x, y, x + cell, y + cell, _fill);
        }
        canvas.RestoreToCount(save);
    }
}
