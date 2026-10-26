using Android.App;
using Android.Content;
using Android.Graphics;
using Android.Views;
using Android.Widget;
using Lunet.Studio;
using AColor = Android.Graphics.Color;

namespace Lunet.Android;

/// <summary>Touch-first tilemap editor using the exact JSON v1 consumed by the game runtime.</summary>
internal sealed class TileStudioView : LinearLayout
{
    private readonly TileStudioSession _edit;
    private readonly Bitmap _tileset;
    private readonly Action<string> _save;
    private readonly Action _editTileset;
    private readonly TextView _status;
    private readonly MapCanvas _canvas;
    private int _lastTile = 1;
    private bool _drawing;
    private bool _changed;
    private int _lastX = -1, _lastY = -1;

    public TileStudioView(Context context, TileStudioSession edit, Bitmap tileset, Action<string> save, Action editTileset, Action close) : base(context)
    {
        _edit = edit; _tileset = tileset; _save = save; _editTileset = editTileset;
        Orientation = Orientation.Vertical;
        SetBackgroundColor(AColor.Rgb(17, 20, 29));
        SetPadding(10, 8, 10, 8);
        _status = new TextView(context) { TextSize = 13 };
        _status.SetTextColor(AColor.White);
        _canvas = new MapCanvas(context, this);

        Row(("← Código", () => { Suspend(); close(); }), ("Salvar", Save), ("Escolher tile", ShowPalette),
            ("Editar tileset", _editTileset), ("Borracha", () => { if (_edit.Tile == 0) _edit.Tile = _lastTile; else { _lastTile = _edit.Tile; _edit.Tile = 0; } Refresh(); }));
        Row(("Camada", PickLayer), ("+ Camada", AddLayer), ("Colisão", () => Edit(() => _edit.ToggleCollision())),
            ("Visível", () => Edit(() => _edit.ToggleVisibility())), ("↑", () => Edit(() => _edit.MoveLayer(-1))),
            ("↓", () => Edit(() => _edit.MoveLayer(1))), ("Renomear", RenameLayer),
            ("Excluir", DeleteLayer), ("Desfazer", () => Edit(() => _edit.Undo())),
            ("Refazer", () => Edit(() => _edit.Redo())));
        AddView(_status);
        AddView(_canvas, new LayoutParams(ViewGroup.LayoutParams.MatchParent, 0, 1));
        Refresh();
    }

    private void Row(params (string text, Action action)[] items)
    {
        var line = new LinearLayout(Context) { Orientation = Orientation.Horizontal };
        foreach (var (text, action) in items)
        {
            var button = new Button(Context) { Text = text, TextSize = 12 };
            button.SetAllCaps(false);
            button.Click += (_, _) => { try { action(); } catch (Exception ex) { Error(ex); } };
            line.AddView(button);
        }
        var scroll = new HorizontalScrollView(Context) { HorizontalScrollBarEnabled = false };
        scroll.AddView(line);
        AddView(scroll);
    }

    private void Edit(Action edit)
    {
        EndGesture();
        edit();
        Refresh();
        Save();
    }

    private void PickLayer()
    {
        var labels = Enumerable.Range(0, _edit.Map.LayerCount)
            .Select(i => $"{i + 1}. {_edit.Map.GetLayerName(i)}{(_edit.Map.IsCollisionLayer(i) ? " · colisão" : "")}").ToArray();
        new AlertDialog.Builder(Context)!.SetTitle("Camadas · ordem de desenho")!
            .SetItems(labels, (_, e) => { EndGesture(); _edit.SelectLayer(e.Which); Refresh(); })!
            .SetNegativeButton("Cancelar", (_, _) => { })!.Show();
    }

    private void AddLayer()
    {
        var input = new EditText(Context) { Hint = "Nome da nova camada" };
        input.SetSingleLine(true);
        input.Text = $"camada{_edit.Map.LayerCount + 1}";
        new AlertDialog.Builder(Context)!.SetTitle("Nova camada")!.SetView(input)!
            .SetNegativeButton("Cancelar", (_, _) => { })!
            .SetPositiveButton("Criar", (_, _) =>
            {
                try { Edit(() => _edit.AddLayer((input.Text ?? "").Trim())); }
                catch (Exception ex) { Error(ex); }
            })!.Show();
    }

    private void RenameLayer()
    {
        var input = new EditText(Context) { Text = _edit.Map.GetLayerName(_edit.Layer) };
        input.SetSingleLine(true);
        new AlertDialog.Builder(Context)!.SetTitle("Renomear camada")!.SetView(input)!
            .SetNegativeButton("Cancelar", (_, _) => { })!
            .SetPositiveButton("Salvar", (_, _) =>
            {
                try { Edit(() => _edit.RenameLayer((input.Text ?? "").Trim())); }
                catch (Exception ex) { Error(ex); }
            })!.Show();
    }

    private void DeleteLayer()
    {
        if (_edit.Map.LayerCount == 1) { _status.Text = "O mapa precisa de pelo menos uma camada."; return; }
        new AlertDialog.Builder(Context)!.SetTitle("Excluir camada e seus tiles?")!
            .SetMessage(_edit.Map.GetLayerName(_edit.Layer))!
            .SetNegativeButton("Cancelar", (_, _) => { })!
            .SetPositiveButton("Excluir", (_, _) => { try { Edit(_edit.RemoveLayer); } catch (Exception ex) { Error(ex); } })!
            .Show();
    }

    private void ShowPalette()
    {
        var palette = new PaletteCanvas(Context, this);
        var horizontal = new HorizontalScrollView(Context);
        horizontal.AddView(palette);
        var vertical = new ScrollView(Context);
        vertical.AddView(horizontal);
        var dialog = new Dialog(Context);
        palette.Picked = id =>
        {
            _edit.Tile = id;
            _lastTile = id;
            dialog.Dismiss();
            Refresh();
        };
        dialog.SetTitle("Escolher tile");
        dialog.SetContentView(vertical);
        dialog.Show();
        dialog.Window?.SetLayout(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent);
    }

    private void Refresh()
    {
        var map = _edit.Map;
        _status.Text = $"Mapa {map.Width}×{map.Height} · Camada: {map.GetLayerName(_edit.Layer)}" +
            $"{(map.IsCollisionLayer(_edit.Layer) ? " [colisão]" : "")} · Tile {_edit.Tile}" +
            (_edit.CanUndo ? " · ↶" : "");
        _canvas.Invalidate();
    }

    private void Error(Exception ex) => _status.Text = "Falha: " + ex.Message;

    public void Save()
    {
        EndGesture();
        try
        {
            _save(_edit.Map.ToJson());
            _status.Text = $"Salvo · {_edit.Map.Width}×{_edit.Map.Height} · {_edit.Map.GetLayerName(_edit.Layer)} · tile {_edit.Tile}";
        }
        catch (Exception ex) { _status.Text = "Não salvo: " + ex.Message; }
    }

    public void Suspend() => Save();

    private void BeginGesture()
    {
        _edit.BeginStroke();
        _drawing = true;
        _changed = false;
        _lastX = _lastY = -1;
    }

    private void PaintCell(int x, int y)
    {
        if (!_drawing || (x == _lastX && y == _lastY)) return;
        _lastX = x;
        _lastY = y;
        try { _changed |= _edit.Paint(x, y); }
        catch (Exception ex) { Error(ex); }
        _canvas.Invalidate();
    }

    private void EndGesture()
    {
        if (!_drawing) return;
        _drawing = false;
        _edit.EndStroke();
        if (_changed)
        {
            _changed = false;
            try { _save(_edit.Map.ToJson()); Refresh(); }
            catch (Exception ex) { _status.Text = "Não salvo: " + ex.Message; }
        }
    }

    private sealed class MapCanvas(Context context, TileStudioView owner) : View(context)
    {
        private readonly Paint _paint = new() { AntiAlias = false };
        private float _cell, _left, _top;

        protected override void OnDraw(Canvas canvas)
        {
            base.OnDraw(canvas);
            var map = owner._edit.Map;
            _cell = Math.Min(Width / (float)map.Width, Height / (float)map.Height);
            if (_cell <= 0) return;
            _left = (Width - map.Width * _cell) / 2;
            _top = (Height - map.Height * _cell) / 2;
            int columns = owner._tileset.Width / map.TileWidth;
            for (int y = 0; y < map.Height; y++)
            for (int x = 0; x < map.Width; x++)
            {
                float left = _left + x * _cell, top = _top + y * _cell;
                _paint.Color = (x + y) % 2 == 0 ? AColor.Rgb(42, 46, 55) : AColor.Rgb(49, 53, 63);
                canvas.DrawRect(left, top, left + _cell, top + _cell, _paint);
                for (int l = 0; l < map.LayerCount; l++)
                {
                    if (!map.IsLayerVisible(l)) continue;
                    int id = map.GetTile(l, new Lunet.Pathfinding.GridPoint(x, y));
                    if (id == 0) continue;
                    int index = id - 1;
                    var src = new Rect((index % columns) * map.TileWidth, (index / columns) * map.TileHeight,
                        (index % columns + 1) * map.TileWidth, (index / columns + 1) * map.TileHeight);
                    _paint.Color = AColor.White;
                    canvas.DrawBitmap(owner._tileset, src, new RectF(left, top, left + _cell, top + _cell), _paint);
                }
            }
            if (_cell >= 10)
            {
                _paint.StrokeWidth = 1;
                _paint.Color = AColor.Argb(50, 255, 255, 255);
                for (int x = 0; x <= map.Width; x++)
                    canvas.DrawLine(_left + x * _cell, _top, _left + x * _cell, _top + map.Height * _cell, _paint);
                for (int y = 0; y <= map.Height; y++)
                    canvas.DrawLine(_left, _top + y * _cell, _left + map.Width * _cell, _top + y * _cell, _paint);
            }
        }

        public override bool OnTouchEvent(MotionEvent? e)
        {
            if (e is null || _cell <= 0) return false;
            if (e.ActionMasked is MotionEventActions.Up or MotionEventActions.Cancel)
            {
                owner.EndGesture();
                return true;
            }
            if (e.ActionMasked == MotionEventActions.Down) owner.BeginGesture();
            if (e.ActionMasked is MotionEventActions.Down or MotionEventActions.Move)
                owner.PaintCell((int)Math.Floor((e.GetX() - _left) / _cell), (int)Math.Floor((e.GetY() - _top) / _cell));
            return true;
        }

        protected override void Dispose(bool disposing) { if (disposing) _paint.Dispose(); base.Dispose(disposing); }
    }

    private sealed class PaletteCanvas : View
    {
        private readonly TileStudioView _owner;
        private readonly int _cols, _rows, _size;
        private readonly Paint _paint = new();
        public Action<int>? Picked { get; set; }

        public PaletteCanvas(Context context, TileStudioView owner) : base(context)
        {
            _owner = owner;
            _cols = owner._tileset.Width / owner._edit.Map.TileWidth;
            _rows = owner._tileset.Height / owner._edit.Map.TileHeight;
            _size = (int)(48 * context.Resources!.DisplayMetrics!.Density);
            LayoutParameters = new ViewGroup.LayoutParams(_cols * _size, _rows * _size);
        }

        protected override void OnDraw(Canvas canvas)
        {
            base.OnDraw(canvas);
            var map = _owner._edit.Map;
            for (int y = 0; y < _rows; y++)
            for (int x = 0; x < _cols; x++)
            {
                var src = new Rect(x * map.TileWidth, y * map.TileHeight, (x + 1) * map.TileWidth, (y + 1) * map.TileHeight);
                var dst = new RectF(x * _size, y * _size, (x + 1) * _size, (y + 1) * _size);
                _paint.Color = AColor.White;
                canvas.DrawBitmap(_owner._tileset, src, dst, _paint);
                _paint.Color = AColor.Argb(90, 0, 0, 0);
                _paint.SetStyle(Paint.Style.Stroke);
                canvas.DrawRect(dst, _paint);
                _paint.SetStyle(Paint.Style.Fill);
            }
        }

        public override bool OnTouchEvent(MotionEvent? e)
        {
            if (e?.ActionMasked != MotionEventActions.Up) return true;
            int x = (int)(e.GetX() / _size), y = (int)(e.GetY() / _size);
            if ((uint)x < (uint)_cols && (uint)y < (uint)_rows) Picked?.Invoke(y * _cols + x + 1);
            return true;
        }

        protected override void Dispose(bool disposing) { if (disposing) _paint.Dispose(); base.Dispose(disposing); }
    }
}
