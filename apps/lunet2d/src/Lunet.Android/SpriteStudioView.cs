using Android.Content;
using Android.Graphics;
using Android.Views;
using Android.Widget;
using Lunet.Studio;
using AColor = Android.Graphics.Color;

namespace Lunet.Android;

/// <summary>Native touch-first sprite workspace; its source remains ordinary project JSON.</summary>
internal sealed class SpriteStudioView : LinearLayout
{
    private SpriteDocument _document;
    private readonly SpriteHistory _history = new();
    private readonly Action<SpriteDocument> _save;
    private readonly PixelCanvas _canvas;
    private readonly TextView _status;
    private readonly LinearLayout _timeline;
    private int _frame;
    private uint _color = 0xff8b5cf6;
    private string _tool = "Lápis";
    private bool _playing, _onion;
    private readonly Action _tick;
    private readonly Button _play;

    public SpriteStudioView(Context context, SpriteDocument document, Action<SpriteDocument> save,
        Action<SpriteDocument> export, Action close) : base(context)
    {
        _document = document; _save = save; Orientation = Orientation.Vertical;
        SetBackgroundColor(AColor.Rgb(17, 20, 29)); SetPadding(12, 12, 12, 12);
        _status = new TextView(context) { TextSize = 14 };
        _status.SetTextColor(AColor.White);
        _canvas = new PixelCanvas(context, this);
        _timeline = new LinearLayout(context) { Orientation = Orientation.Horizontal };
        Row(("← Código", close), ("Salvar", Save), ("Exportar PNG", () => { Stop(); Save(); export(_document); }));
        Row(("Lápis", () => Tool("Lápis")), ("Borracha", () => Tool("Borracha")), ("Balde", () => Tool("Balde")), ("Conta-gotas", () => Tool("Conta-gotas")));
        var colors = new LinearLayout(context) { Orientation = Orientation.Horizontal };
        foreach (uint color in new uint[] { 0xff171923, 0xffffffff, 0xff8b5cf6, 0xff38bdf8, 0xff34d399, 0xfffacc15, 0xfffb923c, 0xfff472b6, 0xffef4444 })
        {
            var swatch = new Button(context) { Text = "●", TextSize = 24 }; swatch.SetTextColor(new AColor(unchecked((int)color)));
            swatch.Click += (_, _) => { _color = color; Tool("Lápis"); }; colors.AddView(swatch, new LayoutParams(0, (int)(52 * context.Resources!.DisplayMetrics!.Density), 1));
        }
        AddView(colors); AddView(_status);
        AddView(_canvas, new LayoutParams(ViewGroup.LayoutParams.MatchParent, 0, 1));
        var timelineScroll = new HorizontalScrollView(context); timelineScroll.AddView(_timeline); AddView(timelineScroll);
        Row(("+ Frame", () => Change(() => { _document.AddFrame(); _frame = _document.Frames.Count - 1; })),
            ("Duplicar", () => Change(() => { _document.AddFrame(_frame); _frame = _document.Frames.Count - 1; })),
            ("Excluir frame", () => Change(() => { _document.RemoveFrame(_frame); _frame = Math.Min(_frame, _document.Frames.Count - 1); })));
        var playback = Row(("▶ Play", TogglePlay), ("FPS −", () => Change(() => _document.Fps = Math.Max(1, _document.Fps - 1))),
            ("FPS +", () => Change(() => _document.Fps = Math.Min(60, _document.Fps + 1))),
            ("Onion skin", () => { _onion = !_onion; Refresh(); }));
        _play = (Button)playback.GetChildAt(0)!;
        Row(("↶ Desfazer", () => { Stop(); _document = _history.Undo(_document); AfterEdit(); }),
            ("↷ Refazer", () => { Stop(); _document = _history.Redo(_document); AfterEdit(); }),
            ("Espelhar", () => Change(() => _document.FlipHorizontal(_frame))));
        _tick = () => { if (!_playing) return; _frame = (_frame + 1) % _document.Frames.Count; _canvas.Invalidate(); PostDelayed(_tick!, 1000 / _document.Fps); };
        Refresh();
    }
    private LinearLayout Row(params (string label, Action action)[] actions)
    {
        var row = new LinearLayout(Context) { Orientation = Orientation.Horizontal };
        foreach (var (label, action) in actions)
        {
            var button = new Button(Context) { Text = label, TextSize = 12 }; button.SetAllCaps(false);
            button.Click += (_, _) => { try { action(); } catch (Exception ex) { _status.Text = "Falha: " + ex.Message; } };
            row.AddView(button, new LayoutParams(ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent));
        }
        var scroll = new HorizontalScrollView(Context); scroll.AddView(row); AddView(scroll); return row;
    }
    private void Tool(string value) { Stop(); _tool = value; Refresh(); }
    private void Stop() { _playing = false; if (_tick is not null) RemoveCallbacks(_tick); if (_play is not null) _play.Text = "▶ Play"; }
    private void TogglePlay() { if (_playing) { Stop(); return; } _playing = true; _play.Text = "■ Parar"; Post(_tick); }
    private void Change(Action mutation) { Stop(); _history.Checkpoint(_document); mutation(); AfterEdit(); }
    private void AfterEdit() { _frame = Math.Min(_frame, _document.Frames.Count - 1); Refresh(); Save(); }
    public void Save()
    {
        try { _save(_document); _status.Text = $"Salvo · {_document.Width}×{_document.Height} · {_document.Fps} FPS · {_tool}"; }
        catch (Exception ex) { _status.Text = "Não salvo: " + ex.Message; }
    }
    private void Refresh()
    {
        _canvas.Invalidate(); _status.Text = $"Frame {_frame + 1}/{_document.Frames.Count} · {_document.Fps} FPS · {_tool}";
        _timeline.RemoveAllViews();
        for (int i = 0; i < _document.Frames.Count; i++)
        {
            int frame = i; var button = new Button(Context) { Text = (i == _frame ? "● " : "") + (i + 1), TextSize = 12 };
            button.Click += (_, _) => { Stop(); _frame = frame; Refresh(); }; _timeline.AddView(button);
        }
    }
    protected override void OnDetachedFromWindow() { Stop(); base.OnDetachedFromWindow(); }

    private sealed class PixelCanvas(Context context, SpriteStudioView owner) : View(context)
    {
        private readonly Paint _paint = new();
        private float _cell, _left, _top;
        private int _lastX, _lastY;
        private bool _drawing;
        protected override void OnDraw(Canvas canvas)
        {
            base.OnDraw(canvas); var doc = owner._document;
            _cell = Math.Min(Width / (float)doc.Width, Height / (float)doc.Height); if (_cell <= 0) return;
            _left = (Width - doc.Width * _cell) / 2; _top = (Height - doc.Height * _cell) / 2;
            for (int y = 0; y < doc.Height; y++) for (int x = 0; x < doc.Width; x++)
            {
                _paint.Color = ((x / 4 + y / 4) % 2 == 0) ? AColor.Rgb(44, 48, 61) : AColor.Rgb(56, 60, 74);
                canvas.DrawRect(_left + x * _cell, _top + y * _cell, _left + (x + 1) * _cell, _top + (y + 1) * _cell, _paint);
                if (owner._onion && !owner._playing && owner._frame > 0)
                {
                    uint c = doc.Frames[owner._frame - 1][y * doc.Width + x];
                    DrawPixel(canvas, x, y, (c & 0xffffff) | ((c >> 24) / 4 << 24));
                }
                DrawPixel(canvas, x, y, doc.Frames[owner._frame][y * doc.Width + x]);
            }
            if (_cell >= 8)
            {
                _paint.Color = AColor.Argb(45, 255, 255, 255); _paint.StrokeWidth = 1;
                for (int x = 0; x <= doc.Width; x++) canvas.DrawLine(_left + x * _cell, _top, _left + x * _cell, _top + doc.Height * _cell, _paint);
                for (int y = 0; y <= doc.Height; y++) canvas.DrawLine(_left, _top + y * _cell, _left + doc.Width * _cell, _top + y * _cell, _paint);
            }
        }
        private void DrawPixel(Canvas canvas, int x, int y, uint color)
        { _paint.Color = new AColor(unchecked((int)color)); canvas.DrawRect(_left + x * _cell, _top + y * _cell, _left + (x + 1) * _cell, _top + (y + 1) * _cell, _paint); }
        public override bool OnTouchEvent(MotionEvent? e)
        {
            if (e is null || _cell <= 0 || owner._playing) return false;
            var doc = owner._document;
            int x = (int)Math.Floor((e.GetX() - _left) / _cell), y = (int)Math.Floor((e.GetY() - _top) / _cell);
            if (e.ActionMasked is MotionEventActions.Up or MotionEventActions.Cancel)
            { if (_drawing) { _drawing = false; owner.AfterEdit(); } return true; }
            if ((uint)x >= doc.Width || (uint)y >= doc.Height) return true;
            if (e.ActionMasked == MotionEventActions.Down)
            {
                if (owner._tool == "Conta-gotas") { owner._color = doc.Frames[owner._frame][y * doc.Width + x]; owner.Tool("Lápis"); return true; }
                owner._history.Checkpoint(doc); _lastX = x; _lastY = y; _drawing = true;
                if (owner._tool == "Balde") { doc.Fill(owner._frame, x, y, owner._color); _drawing = false; owner.AfterEdit(); return true; }
            }
            if (_drawing)
            { doc.Stroke(owner._frame, _lastX, _lastY, x, y, owner._tool == "Borracha" ? 0 : owner._color); _lastX = x; _lastY = y; Invalidate(); }
            return true;
        }
        protected override void Dispose(bool disposing) { if (disposing) _paint.Dispose(); base.Dispose(disposing); }
    }
}
