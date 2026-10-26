using Android.App;
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
    private bool _lastSaveSucceeded;
    private readonly PixelCanvas _canvas;
    private readonly TextView _status;
    private readonly LinearLayout _timeline;
    private int _frame;
    private uint _color = 0xff8b5cf6;
    private string _tool = "Lápis";
    private int _zoom = 1;
    private float _panX, _panY;
    private bool _playing, _onion, _grid = true;
    private readonly Action _tick;
    private readonly Button? _play;
    private readonly int _tileSize;

    public SpriteStudioView(Context context, SpriteDocument document, Action<SpriteDocument> save,
        Action<SpriteDocument> export, Action close, int tilesetTileSize = 0) : base(context)
    {
        _document = document; _save = save; _tileSize = tilesetTileSize; Orientation = Orientation.Vertical;
        SetBackgroundColor(AColor.Rgb(17, 20, 29)); SetPadding(12, 12, 12, 12);
        _status = new TextView(context) { TextSize = 14 };
        _status.SetTextColor(AColor.White);
        _canvas = new PixelCanvas(context, this);
        _timeline = new LinearLayout(context) { Orientation = Orientation.Horizontal };
        if (_tileSize > 0)
            Row(("← Mapa", () => { Save(); if (_lastSaveSucceeded) close(); }), ("Salvar", Save));
        else
            Row(("← Código", close), ("Salvar", Save), ("Usar animação", () => { Stop(); Save(); if (_lastSaveSucceeded) export(_document); }));
        Row(("Lápis", () => Tool("Lápis")), ("Borracha", () => Tool("Borracha")), ("Balde", () => Tool("Balde")),
            ("Conta-gotas", () => Tool("Conta-gotas")), ("Linha", () => Tool("Linha")),
            ("Retângulo", () => Tool("Retângulo")), ("Ret. cheio", () => Tool("Retângulo cheio")));
        Row(("Zoom +", () => Zoom(+1)), ("Zoom −", () => Zoom(-1)),
            ("Centralizar", () => { _panX = _panY = 0; _zoom = 1; Refresh(); }),
            ("Mover tela", () => Tool("Mover")), ("Cor ARGB", ChooseColor),
            ("Grade", () => { _grid = !_grid; Refresh(); }));
        var colors = new LinearLayout(context) { Orientation = Orientation.Horizontal };
        foreach (uint color in new uint[] { 0xff171923, 0xffffffff, 0xff8b5cf6, 0xff38bdf8, 0xff34d399, 0xfffacc15, 0xfffb923c, 0xfff472b6, 0xffef4444 })
        {
            var swatch = new Button(context) { Text = "●", TextSize = 24 }; swatch.SetTextColor(new AColor(unchecked((int)color)));
            swatch.Click += (_, _) => { _color = color; Tool("Lápis"); }; colors.AddView(swatch, new LayoutParams(0, (int)(52 * context.Resources!.DisplayMetrics!.Density), 1));
        }
        AddView(colors); AddView(_status);
        AddView(_canvas, new LayoutParams(ViewGroup.LayoutParams.MatchParent, 0, 1));
        if (_tileSize == 0)
        {
            var timelineScroll = new HorizontalScrollView(context); timelineScroll.AddView(_timeline); AddView(timelineScroll);
            Row(("+ Frame", () => Change(() => { _document.AddFrame(); _frame = _document.Frames.Count - 1; })),
                ("Duplicar", () => Change(() => { _document.AddFrame(_frame); _frame = _document.Frames.Count - 1; })),
                ("Excluir frame", () => Change(() => { _document.RemoveFrame(_frame); _frame = Math.Min(_frame, _document.Frames.Count - 1); })));
            var playback = Row(("▶ Play", TogglePlay), ("FPS −", () => Change(() => _document.Fps = Math.Max(1, _document.Fps - 1))),
                ("FPS +", () => Change(() => _document.Fps = Math.Min(60, _document.Fps + 1))),
                ("Onion skin", () => { _onion = !_onion; Refresh(); }));
            _play = (Button)playback.GetChildAt(0)!;
        }
        Row(("↶ Desfazer", () => { Stop(); _document = _history.Undo(_document); AfterEdit(); }),
            ("↷ Refazer", () => { Stop(); _document = _history.Redo(_document); AfterEdit(); }),
            ("Espelhar H", () => Change(() => _document.FlipHorizontal(_frame))),
            ("Espelhar V", () => Change(() => _document.FlipVertical(_frame))));
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
    private void Zoom(int direction)
    {
        Stop();
        _zoom = Math.Clamp(_zoom + direction, 1, 8);
        if (_zoom == 1) _panX = _panY = 0;
        Refresh();
    }
    private void ChooseColor()
    {
        var entry = new EditText(Context) { Text = $"#{_color:X8}", Hint = "#AARRGGBB" };
        entry.SetSingleLine(true);
        new AlertDialog.Builder(Context)!.SetTitle("Cor personalizada · ARGB hexadecimal")!.SetView(entry)!
            .SetNegativeButton("Cancelar", (_, _) => { })!
            .SetPositiveButton("Aplicar", (_, _) =>
            {
                string hex = (entry.Text ?? "").Trim().TrimStart('#');
                if (hex.Length != 8 || !uint.TryParse(hex, System.Globalization.NumberStyles.HexNumber,
                    System.Globalization.CultureInfo.InvariantCulture, out uint selected))
                { _status.Text = "Cor inválida: use 8 dígitos (ex.: #FFFF0000)."; return; }
                _color = selected; Tool("Lápis");
            })!.Show();
    }
    private void Stop() { _playing = false; if (_tick is not null) RemoveCallbacks(_tick); if (_play is not null) _play.Text = "▶ Play"; }
    private void TogglePlay() { if (_playing) { Stop(); return; } _playing = true; _play.Text = "■ Parar"; Post(_tick); }
    private void Change(Action mutation) { Stop(); _history.Checkpoint(_document); mutation(); AfterEdit(); }
    private void AfterEdit() { _frame = Math.Min(_frame, _document.Frames.Count - 1); Refresh(); Save(); }
    public void Suspend() { Stop(); Save(); }
    public void Save()
    {
        _lastSaveSucceeded = false;
        try { _save(_document); _lastSaveSucceeded = true; _status.Text = $"JSON + PNG salvos · {_document.Frames.Count} frames · {_document.Fps} FPS"; }
        catch (Exception ex) { _status.Text = "Não salvo (JSON/PNG): " + ex.Message; }
    }
    private void Refresh()
    {
        _canvas.Invalidate(); _status.Text = $"Frame {_frame + 1}/{_document.Frames.Count} · {_document.Fps} FPS · {_tool} · zoom {_zoom}×";
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
        private int _startX, _startY, _previewX, _previewY;
        private float _touchX, _touchY;
        protected override void OnDraw(Canvas canvas)
        {
            base.OnDraw(canvas); var doc = owner._document;
            _cell = Math.Min(Width / (float)doc.Width, Height / (float)doc.Height) * owner._zoom;
            if (_cell <= 0) return;
            _left = (Width - doc.Width * _cell) / 2 + owner._panX;
            _top = (Height - doc.Height * _cell) / 2 + owner._panY;
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
            if (owner._grid && _cell >= 8)
            {
                _paint.Color = AColor.Argb(45, 255, 255, 255); _paint.StrokeWidth = 1;
                for (int x = 0; x <= doc.Width; x++) canvas.DrawLine(_left + x * _cell, _top, _left + x * _cell, _top + doc.Height * _cell, _paint);
                for (int y = 0; y <= doc.Height; y++) canvas.DrawLine(_left, _top + y * _cell, _left + doc.Width * _cell, _top + y * _cell, _paint);
            }
            // In tileset mode outline tile cells independently of the pixel grid,
            // so a 128px atlas remains legible while painting on a phone.
            if (owner._grid && owner._tileSize > 0)
            {
                _paint.Color = AColor.Argb(190, 255, 210, 85);
                _paint.StrokeWidth = 2;
                for (int x = 0; x <= doc.Width; x += owner._tileSize)
                    canvas.DrawLine(_left + x * _cell, _top, _left + x * _cell, _top + doc.Height * _cell, _paint);
                for (int y = 0; y <= doc.Height; y += owner._tileSize)
                    canvas.DrawLine(_left, _top + y * _cell, _left + doc.Width * _cell, _top + y * _cell, _paint);
            }
            if (_drawing && owner._tool is "Linha" or "Retângulo" or "Retângulo cheio")
            {
                _paint.Color = AColor.Rgb(0, 220, 255);
                _paint.StrokeWidth = 2;
                float x0 = _left + (_startX + .5f) * _cell, y0 = _top + (_startY + .5f) * _cell;
                float x1 = _left + (_previewX + .5f) * _cell, y1 = _top + (_previewY + .5f) * _cell;
                if (owner._tool == "Linha") canvas.DrawLine(x0, y0, x1, y1, _paint);
                else
                {
                    _paint.SetStyle(Paint.Style.Stroke);
                    canvas.DrawRect(Math.Min(x0, x1), Math.Min(y0, y1), Math.Max(x0, x1), Math.Max(y0, y1), _paint);
                    _paint.SetStyle(Paint.Style.Fill);
                }
            }
        }
        private void DrawPixel(Canvas canvas, int x, int y, uint color)
        { _paint.Color = new AColor(unchecked((int)color)); canvas.DrawRect(_left + x * _cell, _top + y * _cell, _left + (x + 1) * _cell, _top + (y + 1) * _cell, _paint); }
        public override bool OnTouchEvent(MotionEvent? e)
        {
            if (e is null || _cell <= 0 || owner._playing) return false;
            var doc = owner._document;
            float touchX = e.GetX(), touchY = e.GetY();
            int x = (int)Math.Floor((touchX - _left) / _cell);
            int y = (int)Math.Floor((touchY - _top) / _cell);
            if (owner._tool == "Mover")
            {
                if (e.ActionMasked == MotionEventActions.Down) { _touchX = touchX; _touchY = touchY; return true; }
                if (e.ActionMasked == MotionEventActions.Move)
                {
                    owner._panX += touchX - _touchX;
                    owner._panY += touchY - _touchY;
                    _touchX = touchX; _touchY = touchY;
                    Invalidate();
                }
                return true;
            }
            if (e.ActionMasked is MotionEventActions.Up or MotionEventActions.Cancel)
            {
                if (_drawing)
                {
                    if (e.ActionMasked == MotionEventActions.Up)
                    {
                        if (owner._tool == "Linha")
                            doc.Stroke(owner._frame, _startX, _startY, x, y, owner._color);
                        else if (owner._tool is "Retângulo" or "Retângulo cheio")
                            doc.Rectangle(owner._frame, _startX, _startY, x, y, owner._color,
                                filled: owner._tool == "Retângulo cheio");
                    }
                    _drawing = false;
                    owner.AfterEdit();
                }
                return true;
            }
            if (e.ActionMasked == MotionEventActions.Down)
            {
                if ((uint)x >= (uint)doc.Width || (uint)y >= (uint)doc.Height) return true;
                if (owner._tool == "Conta-gotas")
                {
                    owner._color = doc.Frames[owner._frame][y * doc.Width + x];
                    owner.Tool("Lápis");
                    return true;
                }
                owner._history.Checkpoint(doc);
                _lastX = _startX = _previewX = x;
                _lastY = _startY = _previewY = y;
                _drawing = true;
                if (owner._tool == "Balde")
                {
                    doc.Fill(owner._frame, x, y, owner._color);
                    _drawing = false;
                    owner.AfterEdit();
                    return true;
                }
            }
            if (_drawing)
            {
                _previewX = x; _previewY = y;
                if (owner._tool is "Lápis" or "Borracha")
                {
                    doc.Stroke(owner._frame, _lastX, _lastY, x, y,
                        owner._tool == "Borracha" ? 0 : owner._color);
                    _lastX = x; _lastY = y;
                }
                Invalidate();
            }
            return true;
        }
        protected override void Dispose(bool disposing) { if (disposing) _paint.Dispose(); base.Dispose(disposing); }
    }
}
