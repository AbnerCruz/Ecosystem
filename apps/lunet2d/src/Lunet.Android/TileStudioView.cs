using Android.App;
using Android.Content;
using Android.Graphics;
using Android.OS;
using Android.Views;
using Android.Widget;
using Lunet.Studio;
using AColor = Android.Graphics.Color;

namespace Lunet.Android;

/// <summary>
/// Touch-first tilemap editor (LUNET-486) using the exact JSON v1 consumed by the game runtime.
/// The map takes the screen; tiles are picked from a strip below it; layers live in one chip.
/// </summary>
internal sealed class TileStudioView : LinearLayout
{
    private readonly TileStudioSession _edit;
    private readonly Bitmap _tileset;
    private readonly Action<string> _save;
    private readonly Action _editTileset;
    private readonly Action _close;
    private readonly TextView _title;
    private readonly TextView _saveState;
    private readonly MapCanvas _canvas;
    private readonly LinearLayout _tiles;
    private readonly TextView _layerChip;
    private readonly Handler _handler = new(Looper.MainLooper!);
    private readonly Java.Lang.Runnable _saveLater;
    private int _lastTile = 1;
    private bool _drawing;
    private bool _changed;
    private int _lastX = -1, _lastY = -1;

    public TileStudioView(Context context, TileStudioSession edit, Bitmap tileset, Action<string> save, Action editTileset, Action close) : base(context)
    {
        _edit = edit; _tileset = tileset; _save = save; _editTileset = editTileset; _close = close;
        Orientation = Orientation.Vertical;
        SetBackgroundColor(LunetUi.Background);
        _saveLater = new Java.Lang.Runnable(Save);

        var bar = LunetUi.Row(context, 4);
        bar.SetPadding(Dp(4), Dp(6), Dp(6), Dp(6));
        bar.AddView(LunetUi.Icon(context, "‹", "Voltar ao código", () => { Suspend(); _close(); }), Square(44));
        var titles = new LinearLayout(context) { Orientation = Orientation.Vertical };
        _title = LunetUi.Label(context, "", LunetUi.TextLabel, LunetUi.Text, bold: true);
        _title.SetSingleLine(true);
        _saveState = LunetUi.Label(context, "Salvo", LunetUi.TextCaption, LunetUi.TextMuted);
        titles.AddView(_title);
        titles.AddView(_saveState);
        bar.AddView(titles, LunetUi.Weight());
        bar.AddView(LunetUi.Icon(context, "↶", "Desfazer (toque com dois dedos)", Undo), Square(44));
        bar.AddView(LunetUi.Icon(context, "↷", "Refazer (toque com três dedos)", Redo), Square(44));
        bar.AddView(LunetUi.Button(context, "✎ Tileset", () => { Suspend(); _editTileset(); }), new LayoutParams(ViewGroup.LayoutParams.WrapContent, Dp(40)));
        AddView(bar);
        AddView(LunetUi.Divider(context));

        _canvas = new MapCanvas(context, this);
        AddView(_canvas, new LayoutParams(ViewGroup.LayoutParams.MatchParent, 0, 1));

        var layerRow = LunetUi.Row(context, 6);
        layerRow.SetPadding(Dp(8), Dp(6), Dp(8), Dp(2));
        _layerChip = LunetUi.Chip(context, "", ShowLayers);
        layerRow.AddView(_layerChip, LunetUi.Weight());
        layerRow.AddView(LunetUi.Chip(context, "＋ Camada", AddLayer));
        layerRow.AddView(LunetUi.Chip(context, "Ajustar", () => _canvas.Gestures.Reset()));
        layerRow.AddView(LunetUi.Chip(context, "Terreno", ShowTerrainMenu));
        AddView(layerRow);

        var tileRow = LunetUi.Row(context, 4);
        tileRow.SetPadding(Dp(8), Dp(4), Dp(8), Dp(8));
        var tileScroll = new HorizontalScrollView(context) { HorizontalScrollBarEnabled = false };
        _tiles = LunetUi.Row(context, 4);
        tileScroll.AddView(_tiles);
        tileRow.AddView(tileScroll, LunetUi.Weight());
        AddView(tileRow);

        BuildTileStrip();
        Refresh();
    }

    private LayoutParams Square(int dp) => new(Dp(dp), Dp(dp));
    private int Dp(float value) => LunetUi.Dp(Context!, value);

    private void BuildTileStrip()
    {
        _tiles.RemoveAllViews();
        var eraser = new TileSwatch(Context!, this, 0) { ContentDescription = "Borracha" };
        eraser.Click += (_, _) => { _edit.Tile = 0; Refresh(); };
        _tiles.AddView(eraser, Square(46));
        for (int id = 1; id <= _edit.MaxTileId; id++)
        {
            int captured = id;
            var swatch = new TileSwatch(Context!, this, id) { ContentDescription = "Tile " + id };
            swatch.Click += (_, _) => { _edit.Tile = captured; _lastTile = captured; Refresh(); };
            _tiles.AddView(swatch, Square(46));
        }
    }

    private void Undo() { EndGesture(); if (_edit.Undo()) AfterEdit(); }
    private void Redo() { EndGesture(); if (_edit.Redo()) AfterEdit(); }

    private void Edit(Action edit)
    {
        EndGesture();
        try { edit(); } catch (Exception ex) { _saveState.Text = "Falha: " + ex.Message; return; }
        AfterEdit();
    }

    private void AfterEdit()
    {
        _saveState.Text = "Alterações não salvas…";
        _handler.RemoveCallbacks(_saveLater);
        _handler.PostDelayed(_saveLater, 900);
        Refresh();
    }

    private void ShowLayers()
    {
        var map = _edit.Map;
        var items = new List<(string Label, Action Run)>();
        for (int i = 0; i < map.LayerCount; i++)
        {
            int index = i;
            items.Add(((i == _edit.Layer ? "● " : "○ ") + map.GetLayerName(i) +
                (map.IsCollisionLayer(i) ? " · colisão" : "") + (map.IsLayerVisible(i) ? "" : " · oculta"),
                () => { EndGesture(); _edit.SelectLayer(index); Refresh(); }));
        }
        items.Add(("— " + (map.IsCollisionLayer(_edit.Layer) ? "Tornar camada comum" : "Tornar camada de colisão"), () => Edit(() => _edit.ToggleCollision())));
        items.Add(("— " + (map.IsLayerVisible(_edit.Layer) ? "Ocultar camada" : "Mostrar camada"), () => Edit(() => _edit.ToggleVisibility())));
        items.Add(("— Subir camada", () => Edit(() => _edit.MoveLayer(-1))));
        items.Add(("— Descer camada", () => Edit(() => _edit.MoveLayer(1))));
        items.Add(("— Renomear camada…", RenameLayer));
        items.Add(("— Excluir camada…", DeleteLayer));
        new AlertDialog.Builder(Context, global::Android.Resource.Style.ThemeDeviceDefaultDialogAlert)!
            .SetTitle("Camadas · ordem de desenho")!
            .SetItems(items.Select(i => i.Label).ToArray(), (_, e) => items[e.Which].Run())!
            .Show();
    }

    private void AddLayer()
    {
        var input = LunetUi.Input(Context!, "Nome da nova camada", $"camada{_edit.Map.LayerCount + 1}");
        var box = new FrameLayout(Context); box.SetPadding(Dp(20), Dp(8), Dp(20), 0); box.AddView(input);
        new AlertDialog.Builder(Context, global::Android.Resource.Style.ThemeDeviceDefaultDialogAlert)!.SetTitle("Nova camada")!.SetView(box)!
            .SetNegativeButton("Cancelar", (_, _) => { })!
            .SetPositiveButton("Criar", (_, _) => Edit(() => _edit.AddLayer((input.Text ?? "").Trim())))!.Show();
    }

    private void RenameLayer()
    {
        var input = LunetUi.Input(Context!, "Nome", _edit.Map.GetLayerName(_edit.Layer));
        var box = new FrameLayout(Context); box.SetPadding(Dp(20), Dp(8), Dp(20), 0); box.AddView(input);
        new AlertDialog.Builder(Context, global::Android.Resource.Style.ThemeDeviceDefaultDialogAlert)!.SetTitle("Renomear camada")!.SetView(box)!
            .SetNegativeButton("Cancelar", (_, _) => { })!
            .SetPositiveButton("Salvar", (_, _) => Edit(() => _edit.RenameLayer((input.Text ?? "").Trim())))!.Show();
    }

    private void DeleteLayer()
    {
        if (_edit.Map.LayerCount == 1) { _saveState.Text = "O mapa precisa de pelo menos uma camada."; return; }
        new AlertDialog.Builder(Context, global::Android.Resource.Style.ThemeDeviceDefaultDialogAlert)!.SetTitle("Excluir camada e seus tiles?")!
            .SetMessage(_edit.Map.GetLayerName(_edit.Layer))!
            .SetNegativeButton("Cancelar", (_, _) => { })!
            .SetPositiveButton("Excluir", (_, _) => Edit(_edit.RemoveLayer))!.Show();
    }

    /// <summary>Autotile brush (LUNET-506): the selected tile is the first of the terrain's 16 or 47 tiles.</summary>
    private void ShowTerrainMenu()
    {
        var items = new (string Label, Action Run)[]
        {
            ($"Terreno simples (16 tiles a partir do tile {_edit.Tile})", () => SetTerrain(Lunet.Studio.AutotileLayout.Sides16)),
            ($"Terreno suave com cantos (47 tiles a partir do tile {_edit.Tile})", () => SetTerrain(Lunet.Studio.AutotileLayout.Blob47)),
            (_edit.EraseTerrain ? "Pintar terreno" : "Apagar terreno", () => { _edit.EraseTerrain = !_edit.EraseTerrain; Refresh(); }),
            ("Pincel normal (um tile)", () => { _edit.Terrain = null; Refresh(); }),
        };
        new global::Android.App.AlertDialog.Builder(Context, global::Android.Resource.Style.ThemeDeviceDefaultDialogAlert)!
            .SetTitle("Autotile")!
            .SetItems(items.Select(i => i.Label).ToArray(), (_, e) => items[e.Which].Run())!
            .Show();
    }

    private void SetTerrain(Lunet.Studio.AutotileLayout layout)
    {
        var set = new Lunet.Studio.AutotileSet("terreno", Math.Max(1, _edit.Tile), layout);
        if (set.BaseTile + set.Count - 1 > _edit.MaxTileId)
        {
            Toast.MakeText(Context, $"O tileset precisa de {set.Count} tiles a partir do tile {set.BaseTile}.", ToastLength.Long)?.Show();
            return;
        }
        _edit.Terrain = set;
        _edit.EraseTerrain = false;
        Refresh();
    }

    private void Refresh()
    {
        var map = _edit.Map;
        _title.Text = $"Mapa {map.Width}×{map.Height} · " + (_edit.Terrain is { } t
            ? (_edit.EraseTerrain ? "apagando terreno" : $"terreno {t.Count} tiles desde {t.BaseTile}")
            : _edit.Tile == 0 ? "borracha" : "tile " + _edit.Tile);
        _layerChip.Text = "Camada: " + map.GetLayerName(_edit.Layer) + (map.IsCollisionLayer(_edit.Layer) ? " · colisão" : "") + "  ▾";
        for (int i = 0; i < _tiles.ChildCount; i++) _tiles.GetChildAt(i)?.Invalidate();
        _canvas.Invalidate();
    }

    public void Save()
    {
        _handler.RemoveCallbacks(_saveLater);
        EndGesture();
        try
        {
            _save(_edit.Map.ToJson());
            _saveState.Text = "Salvo · JSON do mapa";
        }
        catch (Exception ex) { _saveState.Text = "Não salvo: " + ex.Message; }
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
        catch (Exception ex) { _saveState.Text = "Falha: " + ex.Message; }
        _canvas.Invalidate();
    }

    private void EndGesture()
    {
        if (!_drawing) return;
        _drawing = false;
        _edit.EndStroke();
        if (_changed) { _changed = false; AfterEdit(); }
    }

    private void CancelGesture()
    {
        if (!_drawing) return;
        bool changed = _changed;
        _drawing = false;
        _changed = false;
        _edit.EndStroke();
        if (changed) _edit.Undo();
        _canvas.Invalidate();
    }

    private void DrawTile(Canvas canvas, int id, RectF dest, Paint paint)
    {
        var map = _edit.Map;
        int columns = Math.Max(1, _tileset.Width / map.TileWidth);
        int index = id - 1;
        var src = new Rect((index % columns) * map.TileWidth, (index / columns) * map.TileHeight,
            (index % columns + 1) * map.TileWidth, (index / columns + 1) * map.TileHeight);
        canvas.DrawBitmap(_tileset, src, dest, paint);
    }

    private sealed class TileSwatch(Context context, TileStudioView owner, int id) : View(context)
    {
        private readonly Paint _paint = new() { FilterBitmap = false };

        protected override void OnDraw(Canvas canvas)
        {
            bool selected = owner._edit.Tile == id;
            canvas.DrawColor(selected ? LunetUi.AccentSoft : LunetUi.Surface);
            float pad = 5 * Resources!.DisplayMetrics!.Density;
            var dest = new RectF(pad, pad, Width - pad, Height - pad);
            if (id == 0)
            {
                var text = new Paint(PaintFlags.AntiAlias) { Color = LunetUi.TextMuted, TextSize = 18 * Resources.DisplayMetrics.Density, TextAlign = Paint.Align.Center };
                canvas.DrawText("⌫", Width / 2f, Height / 2f + text.TextSize / 3, text);
            }
            else owner.DrawTile(canvas, id, dest, _paint);
            var border = new Paint { Color = selected ? LunetUi.Accent : LunetUi.Border, StrokeWidth = selected ? 3 : 1 };
            border.SetStyle(Paint.Style.Stroke);
            canvas.DrawRect(1, 1, Width - 1, Height - 1, border);
        }
    }

    private sealed class MapCanvas : View
    {
        private readonly TileStudioView _owner;
        private readonly Paint _paint = new() { FilterBitmap = false };
        private readonly Paint _grid = new() { StrokeWidth = 1, Color = AColor.Argb(46, 255, 255, 255) };
        private float _cell, _left, _top;
        public CanvasGestures Gestures { get; }

        public MapCanvas(Context context, TileStudioView owner) : base(context)
        {
            _owner = owner;
            Gestures = new CanvasGestures(context, this) { MinZoom = 0.5f, MaxZoom = 12f };
            Gestures.CancelTool += owner.CancelGesture;
            Gestures.Undo += owner.Undo;
            Gestures.Redo += owner.Redo;
            ContentDescription = "Mapa de tiles";
        }

        protected override void OnDraw(Canvas canvas)
        {
            base.OnDraw(canvas);
            var map = _owner._edit.Map;
            float margin = 10 * Resources!.DisplayMetrics!.Density;
            _cell = Math.Min((Width - 2 * margin) / map.Width, (Height - 2 * margin) / map.Height) * Gestures.Zoom;
            if (_cell <= 0) return;
            _left = (Width - map.Width * _cell) / 2 + Gestures.PanX;
            _top = (Height - map.Height * _cell) / 2 + Gestures.PanY;
            // Only the visible cells are drawn: big maps stay fluid while zoomed in.
            int x0 = Math.Max(0, (int)Math.Floor(-_left / _cell)), y0 = Math.Max(0, (int)Math.Floor(-_top / _cell));
            int x1 = Math.Min(map.Width - 1, (int)Math.Ceiling((Width - _left) / _cell)), y1 = Math.Min(map.Height - 1, (int)Math.Ceiling((Height - _top) / _cell));
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                float left = _left + x * _cell, top = _top + y * _cell;
                _paint.Color = (x + y) % 2 == 0 ? AColor.Rgb(30, 35, 46) : AColor.Rgb(36, 41, 53);
                canvas.DrawRect(left, top, left + _cell, top + _cell, _paint);
                for (int l = 0; l < map.LayerCount; l++)
                {
                    if (!map.IsLayerVisible(l)) continue;
                    int id = map.GetTile(l, new Lunet.Pathfinding.GridPoint(x, y));
                    if (id == 0) continue;
                    _paint.Color = AColor.White;
                    _paint.Alpha = map.IsCollisionLayer(l) && l != _owner._edit.Layer ? 120 : 255;
                    _owner.DrawTile(canvas, id, new RectF(left, top, left + _cell, top + _cell), _paint);
                    _paint.Alpha = 255;
                }
                if (map.IsCollisionLayer(_owner._edit.Layer) && map.GetTile(_owner._edit.Layer, new Lunet.Pathfinding.GridPoint(x, y)) != 0)
                {
                    _paint.Color = AColor.Argb(70, 248, 81, 73);
                    canvas.DrawRect(left, top, left + _cell, top + _cell, _paint);
                }
            }
            if (_cell >= 10)
            {
                for (int x = x0; x <= x1 + 1; x++) canvas.DrawLine(_left + x * _cell, _top + y0 * _cell, _left + x * _cell, _top + (y1 + 1) * _cell, _grid);
                for (int y = y0; y <= y1 + 1; y++) canvas.DrawLine(_left + x0 * _cell, _top + y * _cell, _left + (x1 + 1) * _cell, _top + y * _cell, _grid);
            }
        }

        public override bool OnTouchEvent(MotionEvent? e)
        {
            if (e is null || _cell <= 0) return false;
            if (Gestures.Handle(e)) return true;
            if (e.ActionMasked is MotionEventActions.Up or MotionEventActions.Cancel)
            {
                _owner.EndGesture();
                return true;
            }
            if (e.ActionMasked == MotionEventActions.Down) _owner.BeginGesture();
            if (e.ActionMasked is MotionEventActions.Down or MotionEventActions.Move)
                _owner.PaintCell((int)Math.Floor((e.GetX() - _left) / _cell), (int)Math.Floor((e.GetY() - _top) / _cell));
            return true;
        }
    }
}
