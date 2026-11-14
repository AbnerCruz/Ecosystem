using Android.App;
using Android.Content;
using Android.Graphics;
using Android.OS;
using Android.Text;
using Android.Views;
using Android.Widget;
using Lunet.Studio;
using AColor = Android.Graphics.Color;
using APath = Android.Graphics.Path;

namespace Lunet.Android;

/// <summary>
/// Touch-first sprite workspace (LUNET-486). The canvas takes the screen; tools live in one compact bar.
/// Gestures follow mobile pixel-art conventions: one finger draws, pinch zooms, two fingers pan,
/// two-finger tap undoes, three-finger tap redoes. Source stays ordinary project JSON + PNG sheet.
/// </summary>
internal sealed class SpriteStudioView : LinearLayout
{
    private static readonly uint[] DefaultPalette =
    [
        0xff0d1017, 0xff2b3040, 0xff5b6378, 0xff9aa3b5, 0xffe6ecf5, 0xffffffff,
        0xff7c6cff, 0xff4f3fd6, 0xff38bdf8, 0xff0e7490, 0xff3fb950, 0xff166534,
        0xfffacc15, 0xfffb923c, 0xfff87171, 0xffb91c1c, 0xfff472b6, 0xff8b5a2b,
    ];

    private SpriteDocument _document;
    private readonly SpriteHistory _history = new();
    private readonly Action<SpriteDocument> _save;
    private readonly Action<SpriteDocument> _export;
    private readonly Action _close;
    private bool _lastSaveSucceeded = true;
    private bool _dirty = true; // first Save() materializes a new sprite on disk
    private readonly PixelCanvas _canvas;
    private readonly TextView _title;
    private readonly TextView _saveState;
    private readonly LinearLayout _timeline = null!;
    private readonly LinearLayout _palette;
    private readonly View _currentColor;
    private readonly List<(string Tool, Button Button)> _toolButtons = [];
    private readonly Handler _handler = new(Looper.MainLooper!);
    private readonly Java.Lang.Runnable _saveLater;
    private readonly Java.Lang.Runnable _tick;
    private readonly List<uint> _recent = [];
    private int _frame;
    private uint _color = 0xff7c6cff;
    private string _tool = "pencil";
    private int _brush = 1;
    private bool _playing, _onion, _grid = true;
    private Button? _play;
    private readonly int _tileSize;
    private SpriteSelection? _selection;
    private SpriteFloating? _floating;
    private float[] _selectionLines = [];
    private int _wandTolerance;
    private LinearLayout _selectionBar = null!;
    private Button _confirmButton = null!;

    public SpriteStudioView(Context context, SpriteDocument document, Action<SpriteDocument> save,
        Action<SpriteDocument> export, Action close, int tilesetTileSize = 0) : base(context)
    {
        _document = document; _save = save; _export = export; _close = close; _tileSize = tilesetTileSize;
        Orientation = Orientation.Vertical;
        SetBackgroundColor(LunetUi.Background);
        _saveLater = new Java.Lang.Runnable(Save);
        _tick = new Java.Lang.Runnable(Tick);

        // Top bar: back · title · undo · redo · play · use
        var bar = LunetUi.Row(context, 4);
        bar.SetPadding(Dp(4), Dp(6), Dp(6), Dp(6));
        bar.AddView(LunetUi.Icon(context, "‹", _tileSize > 0 ? "Voltar ao mapa" : "Voltar ao código", Close), Square(44));
        var titles = new LinearLayout(context) { Orientation = Orientation.Vertical };
        _title = LunetUi.Label(context, "", LunetUi.TextLabel, LunetUi.Text, bold: true);
        _title.SetSingleLine(true);
        _saveState = LunetUi.Label(context, "Salvo", LunetUi.TextCaption, LunetUi.TextMuted);
        titles.AddView(_title);
        titles.AddView(_saveState);
        bar.AddView(titles, LunetUi.Weight());
        bar.AddView(LunetUi.Icon(context, "↶", "Desfazer (toque com dois dedos)", Undo), Square(44));
        bar.AddView(LunetUi.Icon(context, "↷", "Refazer (toque com três dedos)", Redo), Square(44));
        if (_tileSize == 0)
        {
            _play = LunetUi.Icon(context, "▶", "Reproduzir animação", TogglePlay);
            bar.AddView(_play, Square(44));
            bar.AddView(LunetUi.Button(context, "Usar", () => { Stop(); Save(); if (_lastSaveSucceeded) _export(_document); }, ButtonStyle.Primary),
                new LayoutParams(ViewGroup.LayoutParams.WrapContent, Dp(40)));
        }
        AddView(bar);
        AddView(LunetUi.Divider(context));

        _canvas = new PixelCanvas(context, this);
        AddView(_canvas, new LayoutParams(ViewGroup.LayoutParams.MatchParent, 0, 1));

        if (_tileSize == 0)
        {
            var timelineBox = LunetUi.Row(context, 6);
            timelineBox.SetPadding(Dp(8), Dp(6), Dp(8), Dp(2));
            var timelineScroll = new HorizontalScrollView(context) { HorizontalScrollBarEnabled = false };
            _timeline = LunetUi.Row(context, 6);
            timelineScroll.AddView(_timeline);
            timelineBox.AddView(timelineScroll, LunetUi.Weight());
            timelineBox.AddView(LunetUi.Icon(context, "＋", "Novo frame", () => Structural(() => { _document.AddFrame(); _frame = _document.Frames.Count - 1; })), Square(40));
            timelineBox.AddView(LunetUi.Icon(context, "⧉", "Duplicar frame", () => Structural(() => { _document.AddFrame(_frame); _frame = _document.Frames.Count - 1; })), Square(40));
            timelineBox.AddView(LunetUi.Icon(context, "⋯", "Opções da animação", ShowAnimationMenu), Square(40));
            AddView(timelineBox);
        }

        // Palette: current color (tap = picker) + recent + document + default colors.
        var paletteRow = LunetUi.Row(context, 6);
        paletteRow.SetPadding(Dp(8), Dp(4), Dp(8), Dp(4));
        _currentColor = new View(context) { Clickable = true, ContentDescription = "Cor atual: escolher cor" };
        _currentColor.Click += (_, _) => ShowColorPicker();
        paletteRow.AddView(_currentColor, Square(40));
        var paletteScroll = new HorizontalScrollView(context) { HorizontalScrollBarEnabled = false };
        _palette = LunetUi.Row(context, 4);
        paletteScroll.AddView(_palette);
        paletteRow.AddView(paletteScroll, LunetUi.Weight());
        AddView(paletteRow);

        // Selection actions: appear only while something is selected or floating.
        _selectionBar = LunetUi.Row(context, 2);
        _selectionBar.SetPadding(Dp(8), Dp(2), Dp(8), Dp(2));
        _selectionBar.SetBackgroundColor(LunetUi.SurfaceRaised);
        _selectionBar.Visibility = ViewStates.Gone;
        _confirmButton = LunetUi.IconByName(context, "confirm", "Aplicar a seleção", CommitFloating, accent: true);
        _selectionBar.AddView(_confirmButton, Square(44));
        _selectionBar.AddView(LunetUi.IconByName(context, "cancel", "Cancelar (desfaz o movimento) ou limpar a seleção", CancelSelection), Square(44));
        _selectionBar.AddView(new View(context), LunetUi.Weight());
        _selectionBar.AddView(LunetUi.IconByName(context, "rotate-90", "Girar 90°", () => OnFloating(f => f.Rotate90(true))), Square(44));
        _selectionBar.AddView(LunetUi.IconByName(context, "flip-horizontal", "Espelhar horizontalmente", () => OnFloating(f => f.FlipHorizontal())), Square(44));
        _selectionBar.AddView(LunetUi.IconByName(context, "flip-vertical", "Espelhar verticalmente", () => OnFloating(f => f.FlipVertical())), Square(44));
        _selectionBar.AddView(LunetUi.IconByName(context, "duplicate", "Duplicar (carimbar uma cópia)", DuplicateSelection), Square(44));
        _selectionBar.AddView(LunetUi.IconByName(context, "trash", "Apagar os pixels selecionados", DeleteSelection), Square(44));
        AddView(_selectionBar);

        // Tools: one scrolling row, plus "more" pinned at the end.
        var toolRow = LunetUi.Row(context, 0);
        toolRow.SetPadding(Dp(4), Dp(2), Dp(4), Dp(8));
        var toolScroll = new HorizontalScrollView(context) { HorizontalScrollBarEnabled = false };
        var tools = LunetUi.Row(context, 2);
        toolScroll.AddView(tools);
        void ToolButton(string id, string icon, string name)
        {
            var button = LunetUi.IconByName(context, icon, name, () => Tool(id));
            tools.AddView(button, new LayoutParams(Dp(48), Dp(46)));
            _toolButtons.Add((id, button));
        }
        ToolButton("pencil", "pencil", "Lápis");
        ToolButton("eraser", "eraser", "Borracha");
        ToolButton("fill", "bucket", "Balde de tinta");
        ToolButton("picker", "picker", "Conta-gotas");
        ToolButton("line", "line", "Linha");
        ToolButton("rect", "rect", "Retângulo");
        ToolButton("rectfill", "rect-filled", "Retângulo cheio");
        ToolButton("select", "select", "Selecionar área (mover, girar, redimensionar)");
        ToolButton("lasso", "lasso", "Laço: selecionar à mão livre");
        ToolButton("wand", "wand", "Varinha: selecionar por cor");
        var brush = LunetUi.Icon(context, "1px", "Tamanho do pincel", () => { });
        brush.TextSize = 12;
        brush.Click += (_, _) => { _brush = _brush switch { 1 => 2, 2 => 3, 3 => 5, _ => 1 }; brush.Text = _brush + "px"; Refresh(); };
        tools.AddView(brush, new LayoutParams(Dp(48), Dp(46)));
        toolRow.AddView(toolScroll, LunetUi.Weight());
        toolRow.AddView(LunetUi.IconByName(context, "more-vertical", "Mais ferramentas", ShowMoreMenu), new LayoutParams(Dp(48), Dp(46)));
        AddView(toolRow);

        Refresh();
        RefreshPalette();
    }

    private LayoutParams Square(int dp) => new(Dp(dp), Dp(dp));
    private int Dp(float value) => LunetUi.Dp(Context!, value);

    // ---------- Commands ----------

    private void Close()
    {
        Stop();
        Save();
        if (_lastSaveSucceeded) _close();
    }

    private void Tool(string value) { Stop(); _tool = value; Refresh(); }

    private void Undo() { if (_floating is not null) { CancelFloating(); return; } Stop(); if (!_history.CanUndo) return; _document = _history.Undo(_document); AfterEdit(); }
    private void Redo() { if (_floating is not null) return; Stop(); if (!_history.CanRedo) return; _document = _history.Redo(_document); AfterEdit(); }

    private void Structural(Action mutation)
    {
        Stop();
        _history.Checkpoint(_document);
        try { mutation(); }
        catch (Exception ex) { _history.Rollback(_document); _saveState.Text = ex.Message; return; }
        AfterEdit();
    }

    private void FrameEdit(Action mutation)
    {
        Stop();
        _history.CheckpointFrame(_document, _frame);
        mutation();
        AfterEdit();
    }

    private void Stop()
    {
        CommitFloating();
        _selection = null;
        _playing = false;
        _handler.RemoveCallbacks(_tick);
        if (_play is not null) LunetUi.SetLabel(_play, "▶");
    }

    // ---------- Selection ----------

    internal bool IsSelectionTool => _tool is "select" or "lasso" or "wand";

    private void SetSelection(SpriteSelection? selection)
    {
        _selection = selection is { IsEmpty: false } ? selection : null;
        _selectionLines = _selection is null ? [] : _selection.OutlineSegments()
            .SelectMany(e => new float[] { e.X0, e.Y0, e.X1, e.Y1 }).ToArray();
        Refresh();
    }

    /// <summary>Levanta os pixels selecionados para a camada flutuante. Um ponto de desfazer por levantamento.</summary>
    private void EnsureFloating(bool copy = false)
    {
        if (_floating is not null || _selection is null) return;
        _history.CheckpointFrame(_document, _frame);
        _floating = SpriteFloating.Lift(_document, _frame, _selection, copy);
        _selection = null;
        _selectionLines = [];
        Refresh();
    }

    /// <summary>Redesenha só a tela durante um arrasto; evita recriar paleta e linha do tempo a cada movimento.</summary>
    internal void RefreshFloatingOnly() => _canvas.Invalidate();

    private void OnFloating(Action<SpriteFloating> change)
    {
        EnsureFloating();
        if (_floating is null) return;
        change(_floating);
        Refresh();
    }

    private void CommitFloating()
    {
        if (_floating is not { } floating) return;
        _floating = null;
        floating.Commit(_document, _frame);
        _selection = null;
        AfterEdit();
    }

    private void CancelFloating()
    {
        if (_floating is null) return;
        _floating = null;
        _document = _history.Rollback(_document);
        Refresh();
    }

    private void CancelSelection()
    {
        if (_floating is not null) CancelFloating();
        else SetSelection(null);
    }

    private void DuplicateSelection()
    {
        if (_floating is { } floating) { floating.Stamp(_document, _frame); Refresh(); return; }
        EnsureFloating(copy: true);
        _floating?.MoveBy(2, 2);
        Refresh();
    }

    private void DeleteSelection()
    {
        if (_floating is not null) { CancelFloating(); _selection = null; _selectionLines = []; }
        else if (_selection is { } selection)
        {
            FrameEdit(() => selection.Erase(_document, _frame));
            return;
        }
        Refresh();
    }

    private void SelectAll() { Stop(); Tool("select"); SetSelection(SpriteSelection.All(_document.Width, _document.Height)); }

    private void TogglePlay()
    {
        if (_playing) { Stop(); Refresh(); return; }
        Stop();
        _playing = true;
        if (_play is not null) LunetUi.SetLabel(_play, "■");
        _handler.Post(_tick);
    }

    private void Tick()
    {
        if (!_playing) return;
        _frame = (_frame + 1) % _document.Frames.Count;
        _canvas.Invalidate();
        _title.Text = $"Frame {_frame + 1}/{_document.Frames.Count} · {_document.Fps} FPS";
        _handler.PostDelayed(_tick, 1000 / Math.Max(1, _document.Fps));
    }

    private void AfterEdit()
    {
        _frame = Math.Clamp(_frame, 0, _document.Frames.Count - 1);
        _dirty = true;
        _saveState.Text = "Alterações não salvas…";
        _handler.RemoveCallbacks(_saveLater);
        _handler.PostDelayed(_saveLater, 900); // debounce: one write per pause, not per stroke
        Refresh();
    }

    private void UseColor(uint color)
    {
        _color = color;
        _recent.Remove(color);
        _recent.Insert(0, color);
        if (_recent.Count > 8) _recent.RemoveAt(_recent.Count - 1);
        if (_tool is "eraser" or "picker") _tool = "pencil";
        Refresh();
        RefreshPalette();
    }

    public void Suspend() { Stop(); Save(); }

    public void Save()
    {
        _handler.RemoveCallbacks(_saveLater);
        if (!_dirty && _lastSaveSucceeded) { _saveState.Text = "Salvo"; return; }
        try
        {
            _save(_document);
            _lastSaveSucceeded = true;
            _dirty = false;
            _saveState.Text = "Salvo · JSON + PNG";
        }
        catch (Exception ex)
        {
            _lastSaveSucceeded = false;
            _saveState.Text = "Não salvo: " + ex.Message;
        }
    }

    // ---------- Chrome refresh ----------

    private void Refresh()
    {
        _canvas.Invalidate();
        var name = _tool switch
        {
            "pencil" => "Lápis", "eraser" => "Borracha", "fill" => "Balde", "picker" => "Conta-gotas",
            "line" => "Linha", "rect" => "Retângulo", "select" => "Seleção", "lasso" => "Laço", "wand" => "Varinha",
            _ => "Retângulo cheio",
        };
        _title.Text = $"{_document.Width}×{_document.Height} · frame {_frame + 1}/{_document.Frames.Count} · {name}" +
                      (_tool is "pencil" or "eraser" or "line" ? $" {_brush}px" : "");
        if (_floating is not null)
            _title.Text = "Arraste para mover · cantos e bordas redimensionam · círculo gira";
        else if (_selection is not null)
            _title.Text = $"{_selection.Count} pixel(s) selecionado(s) · arraste para mover";
        _selectionBar.Visibility = _floating is not null || _selection is not null ? ViewStates.Visible : ViewStates.Gone;
        _confirmButton.Visibility = _floating is not null ? ViewStates.Visible : ViewStates.Gone;
        foreach (var (tool, button) in _toolButtons)
            LunetUi.Style(Context!, button, tool == _tool ? ButtonStyle.Selected : ButtonStyle.Quiet);
        _currentColor.Background = Swatch(_color, true);
        if (_tileSize == 0) RefreshTimeline();
    }

    private global::Android.Graphics.Drawables.Drawable Swatch(uint color, bool selected)
    {
        var layers = new global::Android.Graphics.Drawables.LayerDrawable([
            LunetUi.Shape(Context!, LunetUi.SurfaceHigh, LunetUi.RadiusSmall),
            LunetUi.Shape(Context!, new AColor(unchecked((int)color)), LunetUi.RadiusSmall, selected ? LunetUi.Text : LunetUi.Border),
        ]);
        return layers;
    }

    private void RefreshPalette()
    {
        _palette.RemoveAllViews();
        var colors = _recent.Concat(SpriteColor.Extract(_document, 12)).Concat(DefaultPalette).Distinct().Take(40);
        foreach (var color in colors)
        {
            var swatch = new View(Context) { Clickable = true, ContentDescription = "Cor " + SpriteColor.ToHex(color) };
            swatch.Background = Swatch(color, color == _color);
            var captured = color;
            swatch.Click += (_, _) => UseColor(captured);
            _palette.AddView(swatch, Square(32));
        }
    }

    private void RefreshTimeline()
    {
        _timeline.RemoveAllViews();
        for (int i = 0; i < _document.Frames.Count; i++)
        {
            int index = i;
            var thumb = new FrameThumb(Context!, this, index) { Clickable = true, ContentDescription = $"Frame {i + 1}" };
            thumb.Click += (_, _) => { Stop(); _frame = index; Refresh(); };
            thumb.LongClick += (_, e) => { _frame = index; ShowFrameMenu(); e.Handled = true; };
            _timeline.AddView(thumb, Square(44));
        }
    }

    // ---------- Menus ----------

    private void ShowFrameMenu() => ShowMenu("Frame " + (_frame + 1),
    [
        ("Duplicar", () => Structural(() => { _document.AddFrame(_frame); _frame = _document.Frames.Count - 1; })),
        ("Mover para a esquerda", () => Structural(() => MoveFrame(-1))),
        ("Mover para a direita", () => Structural(() => MoveFrame(1))),
        ("Excluir", () => Structural(() => _document.RemoveFrame(_frame))),
    ]);

    private void MoveFrame(int delta)
    {
        int target = Math.Clamp(_frame + delta, 0, _document.Frames.Count - 1);
        if (target == _frame) return;
        (_document.Frames[_frame], _document.Frames[target]) = (_document.Frames[target], _document.Frames[_frame]);
        _frame = target;
    }

    private void ShowAnimationMenu() => ShowMenu("Animação",
    [
        ($"Velocidade: {_document.Fps} FPS (mais rápido)", () => Structural(() => _document.Fps = Math.Min(60, _document.Fps + 2))),
        ("Mais devagar", () => Structural(() => _document.Fps = Math.Max(1, _document.Fps - 2))),
        ((_onion ? "Desligar" : "Ligar") + " papel vegetal (onion skin)", () => { _onion = !_onion; Refresh(); }),
        ("Excluir frame atual", () => Structural(() => _document.RemoveFrame(_frame))),
    ]);

    private void ShowMoreMenu() => ShowMenu("Ferramentas",
    [
        ("Selecionar tudo", SelectAll),
        ($"Varinha: tolerância {_wandTolerance} (alternar 0 / 24 / 64)", () => { _wandTolerance = _wandTolerance switch { 0 => 24, 24 => 64, _ => 0 }; Refresh(); }),
        ("Espelhar imagem horizontalmente", () => FrameEdit(() => _document.FlipHorizontal(_frame))),
        ("Espelhar imagem verticalmente", () => FrameEdit(() => _document.FlipVertical(_frame))),
        ("Limpar frame", () => FrameEdit(() => Array.Clear(_document.Frames[_frame]))),
        ((_grid ? "Esconder" : "Mostrar") + " grade", () => { _grid = !_grid; Refresh(); }),
        ("Centralizar e ajustar à tela", () => { _canvas.ResetView(); }),
        ("Escolher cor…", ShowColorPicker),
        ("Gestos: pinça = zoom · 2 dedos = mover · toque com 2 = desfazer · com 3 = refazer", () => { }),
    ]);

    private void ShowMenu(string title, (string Label, Action Run)[] items)
    {
        new AlertDialog.Builder(Context, global::Android.Resource.Style.ThemeDeviceDefaultDialogAlert)!
            .SetTitle(title)!
            .SetItems(items.Select(i => i.Label).ToArray(), (_, e) => items[e.Which].Run())!
            .Show();
    }

    private void ShowColorPicker()
    {
        var context = Context!;
        var (h, s, v, a) = SpriteColor.ToHsv(_color);
        var form = LunetUi.Column(context, 10);
        form.SetPadding(Dp(20), Dp(10), Dp(20), Dp(6));
        var preview = new View(context);
        form.AddView(preview, new LayoutParams(ViewGroup.LayoutParams.MatchParent, Dp(56)));
        var hex = LunetUi.Input(context, "#RRGGBB ou #AARRGGBB", SpriteColor.ToHex(_color));
        bool syncing = false;
        uint Current() => SpriteColor.FromHsv(h, s, v, a);
        void Paint()
        {
            preview.Background = Swatch(Current(), false);
            syncing = true;
            hex.Text = SpriteColor.ToHex(Current());
            syncing = false;
        }
        SeekBar Slider(string label, int max, int value, Action<int> set)
        {
            form.AddView(LunetUi.Label(context, label, LunetUi.TextCaption, LunetUi.TextMuted));
            var bar = new SeekBar(context) { Max = max, Progress = value };
            bar.ProgressChanged += (_, e) => { if (e.FromUser) { set(e.Progress); Paint(); } };
            form.AddView(bar);
            return bar;
        }
        var hue = Slider("Matiz", 360, (int)h, x => h = x);
        var sat = Slider("Saturação", 100, (int)(s * 100), x => s = x / 100f);
        var val = Slider("Brilho", 100, (int)(v * 100), x => v = x / 100f);
        var alpha = Slider("Opacidade", 255, a, x => a = x);
        form.AddView(hex);
        hex.TextChanged += (_, _) =>
        {
            if (syncing || SpriteColor.ParseHex(hex.Text) is not { } parsed) return;
            (h, s, v, a) = SpriteColor.ToHsv(parsed);
            hue.Progress = (int)h; sat.Progress = (int)(s * 100); val.Progress = (int)(v * 100); alpha.Progress = a;
            preview.Background = Swatch(parsed, false);
        };
        Paint();
        new AlertDialog.Builder(context, global::Android.Resource.Style.ThemeDeviceDefaultDialogAlert)!
            .SetTitle("Cor")!
            .SetView(form)!
            .SetNegativeButton("Cancelar", (_, _) => { })!
            .SetPositiveButton("Usar", (_, _) => UseColor(Current()))!
            .Show();
    }

    protected override void OnDetachedFromWindow() { Stop(); _handler.RemoveCallbacks(_saveLater); base.OnDetachedFromWindow(); }

    // ---------- Rendering helpers ----------

    private static void Blit(Bitmap bitmap, uint[] pixels, int[] buffer)
    {
        Buffer.BlockCopy(pixels, 0, buffer, 0, pixels.Length * 4);
        bitmap.SetPixels(buffer, 0, bitmap.Width, 0, 0, bitmap.Width, bitmap.Height);
    }

    private sealed class FrameThumb(Context context, SpriteStudioView owner, int index) : View(context)
    {
        private Bitmap? _bitmap;
        private readonly Paint _paint = new() { FilterBitmap = false };

        protected override void OnDraw(Canvas canvas)
        {
            var doc = owner._document;
            if (index >= doc.Frames.Count) return;
            bool current = index == owner._frame;
            canvas.DrawColor(current ? LunetUi.AccentSoft : LunetUi.Surface);
            if (_bitmap is null || _bitmap.Width != doc.Width || _bitmap.Height != doc.Height)
                _bitmap = Bitmap.CreateBitmap(doc.Width, doc.Height, Bitmap.Config.Argb8888!)!;
            Blit(_bitmap, doc.Frames[index], new int[doc.Width * doc.Height]);
            int pad = (int)(4 * Resources!.DisplayMetrics!.Density);
            float scale = Math.Min((Width - 2 * pad) / (float)doc.Width, (Height - 2 * pad) / (float)doc.Height);
            float w = doc.Width * scale, h = doc.Height * scale;
            canvas.DrawBitmap(_bitmap, null, new RectF((Width - w) / 2, (Height - h) / 2, (Width + w) / 2, (Height + h) / 2), _paint);
            var border = new Paint { Color = current ? LunetUi.Accent : LunetUi.Border, StrokeWidth = current ? 3 : 1 };
            border.SetStyle(Paint.Style.Stroke);
            canvas.DrawRect(1, 1, Width - 1, Height - 1, border);
        }
    }

    /// <summary>Bitmap-backed canvas: one scaled draw per frame instead of one rect per pixel.</summary>
    private sealed class PixelCanvas : View
    {
        private readonly SpriteStudioView _owner;
        private readonly ScaleGestureDetector _scale;
        private readonly Paint _pixels = new() { FilterBitmap = false };
        private readonly Paint _onionPaint = new() { FilterBitmap = false, Alpha = 70 };
        private readonly Paint _line = new(PaintFlags.AntiAlias);
        private Bitmap? _frameBitmap, _onionBitmap, _checker;
        private int[] _buffer = [];
        private float _zoom = 1, _panX, _panY, _cell, _left, _top;
        private bool _drawing, _multiTouch, _scaled;
        private int _lastX, _lastY, _startX, _startY, _previewX, _previewY, _maxPointers;
        private long _downTime;
        private float _midX, _midY, _travel;
        // Selection gestures
        private enum Grab { None, Move, Corner, EdgeX, EdgeY, Rotate }
        private Grab _grab;
        private bool _selecting;
        private readonly List<(int X, int Y)> _lasso = [];
        private float _grabFx, _grabFy, _startScaleX = 1, _startScaleY = 1;
        private int _appliedDx, _appliedDy;
        private readonly Paint _ants = new() { StrokeWidth = 2 };
        private readonly Paint _handlePaint = new(PaintFlags.AntiAlias);

        public PixelCanvas(Context context, SpriteStudioView owner) : base(context)
        {
            _owner = owner;
            _scale = new ScaleGestureDetector(context, new ScaleListener(this));
            ContentDescription = "Tela de desenho do sprite";
        }

        public void ResetView() { _zoom = 1; _panX = _panY = 0; Invalidate(); }

        private void EnsureBitmaps(SpriteDocument doc)
        {
            if (_frameBitmap is { } b && b.Width == doc.Width && b.Height == doc.Height) return;
            _frameBitmap = Bitmap.CreateBitmap(doc.Width, doc.Height, Bitmap.Config.Argb8888!)!;
            _onionBitmap = Bitmap.CreateBitmap(doc.Width, doc.Height, Bitmap.Config.Argb8888!)!;
            _checker = Bitmap.CreateBitmap(doc.Width, doc.Height, Bitmap.Config.Argb8888!)!;
            var checker = new int[doc.Width * doc.Height];
            for (int y = 0; y < doc.Height; y++)
                for (int x = 0; x < doc.Width; x++)
                    checker[y * doc.Width + x] = ((x / 4 + y / 4) % 2 == 0) ? AColor.Rgb(40, 45, 58).ToArgb() : AColor.Rgb(52, 58, 72).ToArgb();
            _checker.SetPixels(checker, 0, doc.Width, 0, 0, doc.Width, doc.Height);
            _buffer = new int[doc.Width * doc.Height];
        }

        protected override void OnDraw(Canvas canvas)
        {
            base.OnDraw(canvas);
            var doc = _owner._document;
            EnsureBitmaps(doc);
            float margin = 12 * Resources!.DisplayMetrics!.Density;
            _cell = Math.Min((Width - 2 * margin) / doc.Width, (Height - 2 * margin) / doc.Height) * _zoom;
            if (_cell <= 0) return;
            _left = (Width - doc.Width * _cell) / 2 + _panX;
            _top = (Height - doc.Height * _cell) / 2 + _panY;
            var dest = new RectF(_left, _top, _left + doc.Width * _cell, _top + doc.Height * _cell);
            canvas.DrawBitmap(_checker!, null, dest, _pixels);
            int frame = Math.Min(_owner._frame, doc.Frames.Count - 1);
            if (_owner._onion && !_owner._playing && frame > 0)
            {
                Blit(_onionBitmap!, doc.Frames[frame - 1], _buffer);
                canvas.DrawBitmap(_onionBitmap!, null, dest, _onionPaint);
            }
            Blit(_frameBitmap!, _owner._floating?.Compose() ?? doc.Frames[frame], _buffer);
            canvas.DrawBitmap(_frameBitmap!, null, dest, _pixels);

            if (_owner._grid && _cell >= 8)
            {
                _line.Color = AColor.Argb(38, 255, 255, 255);
                _line.StrokeWidth = 1;
                var lines = new float[(doc.Width + doc.Height + 2) * 4];
                int i = 0;
                for (int x = 0; x <= doc.Width; x++) { lines[i++] = _left + x * _cell; lines[i++] = _top; lines[i++] = _left + x * _cell; lines[i++] = dest.Bottom; }
                for (int y = 0; y <= doc.Height; y++) { lines[i++] = _left; lines[i++] = _top + y * _cell; lines[i++] = dest.Right; lines[i++] = _top + y * _cell; }
                canvas.DrawLines(lines, _line);
            }
            if (_owner._grid && _owner._tileSize > 0)
            {
                _line.Color = AColor.Argb(190, 255, 210, 85);
                _line.StrokeWidth = 2;
                for (int x = 0; x <= doc.Width; x += _owner._tileSize) canvas.DrawLine(_left + x * _cell, _top, _left + x * _cell, dest.Bottom, _line);
                for (int y = 0; y <= doc.Height; y += _owner._tileSize) canvas.DrawLine(_left, _top + y * _cell, dest.Right, _top + y * _cell, _line);
            }
            _line.Color = LunetUi.Border;
            _line.StrokeWidth = 2;
            _line.SetStyle(Paint.Style.Stroke);
            canvas.DrawRect(dest, _line);
            if (_drawing && _owner._tool is "line" or "rect" or "rectfill")
            {
                _line.Color = LunetUi.AccentText;
                float x0 = _left + (_startX + .5f) * _cell, y0 = _top + (_startY + .5f) * _cell;
                float x1 = _left + (_previewX + .5f) * _cell, y1 = _top + (_previewY + .5f) * _cell;
                if (_owner._tool == "line") canvas.DrawLine(x0, y0, x1, y1, _line);
                else canvas.DrawRect(Math.Min(x0, x1), Math.Min(y0, y1), Math.Max(x0, x1), Math.Max(y0, y1), _line);
            }
            _line.SetStyle(Paint.Style.Fill);
            DrawSelectionOverlay(canvas);
        }

        private float ScreenX(float x) => _left + x * _cell;
        private float ScreenY(float y) => _top + y * _cell;

        private void DrawSelectionOverlay(Canvas canvas)
        {
            float dp = Resources!.DisplayMetrics!.Density;
            _ants.SetStyle(Paint.Style.Stroke);
            if (_owner._selection is not null && _owner._selectionLines.Length > 0)
            {
                var lines = _owner._selectionLines;
                var screen = new float[lines.Length];
                for (int i = 0; i < lines.Length; i += 2) { screen[i] = ScreenX(lines[i]); screen[i + 1] = ScreenY(lines[i + 1]); }
                _ants.SetPathEffect(null); _ants.Color = AColor.Argb(200, 0, 0, 0); _ants.StrokeWidth = 4;
                canvas.DrawLines(screen, _ants);
                _ants.SetPathEffect(new DashPathEffect([8 * dp, 6 * dp], 0)); _ants.Color = AColor.White; _ants.StrokeWidth = 2;
                canvas.DrawLines(screen, _ants);
                _ants.SetPathEffect(null);
            }
            if (_owner._floating is { } floating)
            {
                var corners = floating.Corners();
                var outline = new APath();
                outline.MoveTo(ScreenX(corners[0].X), ScreenY(corners[0].Y));
                for (int i = 1; i < 4; i++) outline.LineTo(ScreenX(corners[i].X), ScreenY(corners[i].Y));
                outline.Close();
                _ants.Color = LunetUi.Accent; _ants.StrokeWidth = 2 * dp;
                canvas.DrawPath(outline, _ants);
                foreach (var (hx, hy, _, _) in Handles(floating))
                {
                    _handlePaint.SetStyle(Paint.Style.Fill); _handlePaint.Color = AColor.White;
                    canvas.DrawCircle(hx, hy, 6 * dp, _handlePaint);
                    _handlePaint.SetStyle(Paint.Style.Stroke); _handlePaint.Color = LunetUi.Accent; _handlePaint.StrokeWidth = 2 * dp;
                    canvas.DrawCircle(hx, hy, 6 * dp, _handlePaint);
                }
                var rot = Handles(floating).First(h => h.Grab == Grab.Rotate);
                var topMid = Handles(floating).First(h => h.Grab == Grab.EdgeY && h.Index == 0);
                _ants.StrokeWidth = 1.5f * dp;
                canvas.DrawLine(topMid.X, topMid.Y, rot.X, rot.Y, _ants);
            }
            if (_selecting && _owner._tool == "select")
            {
                _ants.Color = AColor.White; _ants.StrokeWidth = 2;
                float x0 = ScreenX(Math.Min(_startX, _previewX)), y0 = ScreenY(Math.Min(_startY, _previewY));
                float x1 = ScreenX(Math.Max(_startX, _previewX) + 1), y1 = ScreenY(Math.Max(_startY, _previewY) + 1);
                _ants.SetPathEffect(new DashPathEffect([8 * dp, 6 * dp], 0));
                canvas.DrawRect(x0, y0, x1, y1, _ants);
                _ants.SetPathEffect(null);
            }
            if (_selecting && _owner._tool == "lasso" && _lasso.Count > 1)
            {
                _ants.Color = AColor.White; _ants.StrokeWidth = 2;
                var path = new APath();
                path.MoveTo(ScreenX(_lasso[0].X + .5f), ScreenY(_lasso[0].Y + .5f));
                foreach (var (lx, ly) in _lasso.Skip(1)) path.LineTo(ScreenX(lx + .5f), ScreenY(ly + .5f));
                path.Close();
                _ants.SetPathEffect(new DashPathEffect([8 * dp, 6 * dp], 0));
                canvas.DrawPath(path, _ants);
                _ants.SetPathEffect(null);
            }
            _ants.SetStyle(Paint.Style.Fill);
        }

        /// <summary>Alças do objeto flutuante em coordenadas de tela: 4 cantos, 4 meios de borda e o círculo de rotação.</summary>
        private List<(float X, float Y, Grab Grab, int Index)> Handles(SpriteFloating floating)
        {
            var c = floating.Corners();
            var list = new List<(float, float, Grab, int)>();
            for (int i = 0; i < 4; i++) list.Add((ScreenX(c[i].X), ScreenY(c[i].Y), Grab.Corner, i));
            // Edge midpoints: 0 = top, 1 = right, 2 = bottom, 3 = left.
            for (int i = 0; i < 4; i++)
            {
                var a = c[i]; var b = c[(i + 1) % 4];
                list.Add((ScreenX((a.X + b.X) / 2), ScreenY((a.Y + b.Y) / 2), i % 2 == 0 ? Grab.EdgeY : Grab.EdgeX, i));
            }
            double rad = floating.RotationDegrees * Math.PI / 180.0;
            float offset = 30 * Resources!.DisplayMetrics!.Density;
            var top = list.First(h => h.Item3 == Grab.EdgeY && h.Item4 == 0);
            list.Add((top.Item1 + (float)Math.Sin(rad) * offset, top.Item2 - (float)Math.Cos(rad) * offset, Grab.Rotate, 0));
            return list;
        }

        public override bool OnTouchEvent(MotionEvent? e)
        {
            if (e is null || _cell <= 0) return false;
            _scale.OnTouchEvent(e);
            var action = e.ActionMasked;
            if (action == MotionEventActions.Down)
            {
                Parent?.RequestDisallowInterceptTouchEvent(true);
                _multiTouch = false; _scaled = false; _maxPointers = 1; _travel = 0;
                _downTime = e.EventTime;
            }
            if (action == MotionEventActions.PointerDown)
            {
                _maxPointers = Math.Max(_maxPointers, e.PointerCount);
                if (!_multiTouch)
                {
                    _multiTouch = true;
                    if (_drawing) { _owner._document = _owner._history.Rollback(_owner._document); _drawing = false; }
                    _selecting = false; _grab = Grab.None;
                }
                Midpoint(e, out _midX, out _midY);
                return true;
            }
            if (_multiTouch)
            {
                if (action == MotionEventActions.Move && e.PointerCount >= 2)
                {
                    Midpoint(e, out var mx, out var my);
                    _panX += mx - _midX; _panY += my - _midY;
                    _travel += Math.Abs(mx - _midX) + Math.Abs(my - _midY);
                    _midX = mx; _midY = my;
                    Invalidate();
                }
                else if (action == MotionEventActions.PointerUp)
                {
                    // Re-anchor on the remaining pointers to avoid a jump.
                    int remaining = e.ActionIndex == 0 ? 1 : 0;
                    _midX = e.GetX(remaining); _midY = e.GetY(remaining);
                }
                else if (action is MotionEventActions.Up or MotionEventActions.Cancel)
                {
                    bool tap = action == MotionEventActions.Up && !_scaled && e.EventTime - _downTime < 320 &&
                               _travel < 24 * Resources!.DisplayMetrics!.Density;
                    if (tap && _maxPointers == 2) _owner.Undo();
                    else if (tap && _maxPointers >= 3) _owner.Redo();
                    _multiTouch = false;
                }
                return true;
            }
            if (_owner._playing) return true;
            return Draw(e);
        }

        private bool Draw(MotionEvent e)
        {
            var doc = _owner._document;
            int x = (int)Math.Floor((e.GetX() - _left) / _cell);
            int y = (int)Math.Floor((e.GetY() - _top) / _cell);
            var tool = _owner._tool;
            if (_owner.IsSelectionTool) return SelectionTouch(e, doc);
            switch (e.ActionMasked)
            {
                case MotionEventActions.Down:
                    if ((uint)x >= (uint)doc.Width || (uint)y >= (uint)doc.Height) return true;
                    if (tool == "picker")
                    {
                        var picked = doc.Frames[_owner._frame][y * doc.Width + x];
                        if (picked >> 24 != 0) _owner.UseColor(picked);
                        return true;
                    }
                    _owner.Stop();
                    _owner._history.CheckpointFrame(doc, _owner._frame);
                    _lastX = _startX = _previewX = x;
                    _lastY = _startY = _previewY = y;
                    if (tool == "fill") { doc.Fill(_owner._frame, x, y, _owner._color); _owner.AfterEdit(); return true; }
                    _drawing = true;
                    if (tool is "pencil" or "eraser")  PaintAt(doc, x, y);
                    Invalidate();
                    return true;
                case MotionEventActions.Move when _drawing:
                    _previewX = x; _previewY = y;
                    if (tool is "pencil" or "eraser")
                    {
                        for (int i = 0; i < e.HistorySize; i++)
                             PaintAt(doc, (int)Math.Floor((e.GetHistoricalX(i) - _left) / _cell), (int)Math.Floor((e.GetHistoricalY(i) - _top) / _cell));
                         PaintAt(doc, x, y);
                    }
                    Invalidate();
                    return true;
                case MotionEventActions.Up when _drawing:
                    if (tool == "line") doc.Brush(_owner._frame, _startX, _startY, x, y, _owner._color, _owner._brush);
                    else if (tool is "rect" or "rectfill") doc.Rectangle(_owner._frame, _startX, _startY, x, y, _owner._color, tool == "rectfill");
                    _drawing = false;
                    _owner.AfterEdit();
                    return true;
                case MotionEventActions.Cancel when _drawing:
                    _owner._document = _owner._history.Rollback(doc);
                    _drawing = false;
                    Invalidate();
                    return true;
            }
            return true;
        }

        private bool SelectionTouch(MotionEvent e, SpriteDocument doc)
        {
            float fx = (e.GetX() - _left) / _cell, fy = (e.GetY() - _top) / _cell;
            int x = Math.Clamp((int)Math.Floor(fx), 0, doc.Width - 1), y = Math.Clamp((int)Math.Floor(fy), 0, doc.Height - 1);
            var tool = _owner._tool;
            switch (e.ActionMasked)
            {
                case MotionEventActions.Down:
                    _grab = Grab.None; _selecting = false;
                    if (_owner._floating is { } active)
                    {
                        _grab = HitHandle(active, e.GetX(), e.GetY());
                        if (_grab == Grab.None && InsideFloating(active, fx, fy)) _grab = Grab.Move;
                        if (_grab != Grab.None) { BeginGrab(active, fx, fy, e.GetX(), e.GetY()); return true; }
                        _owner.CommitFloating();
                    }
                    if (_owner._selection is { } selection && selection.Contains((int)Math.Floor(fx), (int)Math.Floor(fy)))
                    {
                        _owner.EnsureFloating();
                        if (_owner._floating is { } lifted) { _grab = Grab.Move; BeginGrab(lifted, fx, fy, e.GetX(), e.GetY()); }
                        return true;
                    }
                    _owner._selection = null;
                    _startX = _previewX = x; _startY = _previewY = y;
                    _lasso.Clear(); _lasso.Add((x, y));
                    _selecting = true;
                    Invalidate();
                    return true;
                case MotionEventActions.Move when _grab != Grab.None && _owner._floating is { } dragged:
                    DragGrab(dragged, fx, fy);
                    _owner.RefreshFloatingOnly();
                    return true;
                case MotionEventActions.Move when _selecting:
                    _previewX = x; _previewY = y;
                    if (tool == "lasso" && (_lasso[^1].X != x || _lasso[^1].Y != y)) _lasso.Add((x, y));
                    Invalidate();
                    return true;
                case MotionEventActions.Up when _grab != Grab.None:
                    _grab = Grab.None;
                    return true;
                case MotionEventActions.Up when _selecting:
                    _selecting = false;
                    SpriteSelection? result = tool switch
                    {
                        "select" when _startX == x && _startY == y => null, // a plain tap clears
                        "select" => SpriteSelection.FromRectangle(doc.Width, doc.Height, _startX, _startY, x, y),
                        "lasso" => SpriteSelection.FromPolygon(doc.Width, doc.Height, _lasso),
                        _ => SpriteSelection.FromColor(doc, _owner._frame, x, y, _owner._wandTolerance),
                    };
                    _owner.SetSelection(result);
                    return true;
                case MotionEventActions.Cancel:
                    _grab = Grab.None; _selecting = false; Invalidate();
                    return true;
            }
            return true;
        }

        private Grab HitHandle(SpriteFloating floating, float px, float py)
        {
            float radius = 26 * Resources!.DisplayMetrics!.Density;
            Grab best = Grab.None; float bestDistance = radius;
            _gripIndex = 0;
            foreach (var (hx, hy, grab, index) in Handles(floating))
            {
                float d = MathF.Sqrt((hx - px) * (hx - px) + (hy - py) * (hy - py));
                if (d >= bestDistance) continue;
                bestDistance = d; best = grab; _gripIndex = index;
            }
            return best;
        }

        private int _gripIndex;

        private static bool InsideFloating(SpriteFloating floating, float fx, float fy)
        {
            var (lx, ly) = ToLocal(floating, fx, fy);
            return Math.Abs(lx) <= floating.Width * floating.ScaleX / 2f && Math.Abs(ly) <= floating.Height * floating.ScaleY / 2f;
        }

        private static (float X, float Y) ToLocal(SpriteFloating floating, float fx, float fy)
        {
            double rad = floating.RotationDegrees * Math.PI / 180.0;
            float cos = (float)Math.Cos(rad), sin = (float)Math.Sin(rad);
            float dx = fx - floating.CenterX, dy = fy - floating.CenterY;
            return (dx * cos + dy * sin, -dx * sin + dy * cos);
        }

        private void BeginGrab(SpriteFloating floating, float fx, float fy, float px, float py)
        {
            _grabFx = fx; _grabFy = fy;
            _appliedDx = _appliedDy = 0;
            _startScaleX = floating.ScaleX; _startScaleY = floating.ScaleY;
        }

        private void DragGrab(SpriteFloating floating, float fx, float fy)
        {
            switch (_grab)
            {
                case Grab.Move:
                    // Whole pixels only, so moved art never blurs between pixel cells.
                    int dx = (int)Math.Round(fx - _grabFx), dy = (int)Math.Round(fy - _grabFy);
                    floating.MoveBy(dx - _appliedDx, dy - _appliedDy);
                    _appliedDx = dx; _appliedDy = dy;
                    break;
                case Grab.Corner:
                {
                    var (lx, ly) = ToLocal(floating, fx, fy);
                    float k = (Math.Abs(lx) / (floating.Width * _startScaleX / 2f) + Math.Abs(ly) / (floating.Height * _startScaleY / 2f)) / 2f;
                    floating.SetScale(SnapScale(floating.Width, _startScaleX * k), SnapScale(floating.Height, _startScaleY * k));
                    break;
                }
                case Grab.EdgeX:
                {
                    var (lx, _) = ToLocal(floating, fx, fy);
                    floating.SetScale(SnapScale(floating.Width, Math.Abs(lx) * 2f / floating.Width), floating.ScaleY);
                    break;
                }
                case Grab.EdgeY:
                {
                    var (_, ly) = ToLocal(floating, fx, fy);
                    floating.SetScale(floating.ScaleX, SnapScale(floating.Height, Math.Abs(ly) * 2f / floating.Height));
                    break;
                }
                case Grab.Rotate:
                {
                    double degrees = Math.Atan2(fy - floating.CenterY, fx - floating.CenterX) * 180.0 / Math.PI + 90.0;
                    double snapped = Math.Round(degrees / 15.0) * 15.0;
                    floating.RotationDegrees = (float)(Math.Abs(degrees - snapped) < 4 ? snapped : degrees);
                    break;
                }
            }
        }

        /// <summary>Escala que resulta num tamanho inteiro de pixels (mínimo 1), mantendo a arte nítida.</summary>
        private static float SnapScale(int size, float scale) => Math.Max(1, MathF.Round(size * scale)) / size;

        private void PaintAt(SpriteDocument doc, int x, int y)
        {
            doc.Brush(_owner._frame, _lastX, _lastY, x, y, _owner._tool == "eraser" ? 0 : _owner._color, _owner._brush);
            _lastX = x; _lastY = y;
        }

        private static void Midpoint(MotionEvent e, out float x, out float y)
        {
            x = 0; y = 0;
            for (int i = 0; i < e.PointerCount; i++) { x += e.GetX(i); y += e.GetY(i); }
            x /= e.PointerCount; y /= e.PointerCount;
        }

        private sealed class ScaleListener(PixelCanvas canvas) : ScaleGestureDetector.SimpleOnScaleGestureListener
        {
            public override bool OnScale(ScaleGestureDetector detector)
            {
                float previous = canvas._zoom;
                canvas._zoom = Math.Clamp(canvas._zoom * detector.ScaleFactor, 0.5f, 40f);
                float ratio = canvas._zoom / previous;
                // Keep the pixel under the fingers in place.
                float cx = canvas.Width / 2f, cy = canvas.Height / 2f;
                canvas._panX = (canvas._panX + cx - detector.FocusX) * ratio - cx + detector.FocusX;
                canvas._panY = (canvas._panY + cy - detector.FocusY) * ratio - cy + detector.FocusY;
                if (Math.Abs(detector.ScaleFactor - 1) > 0.01f) canvas._scaled = true;
                canvas.Invalidate();
                return true;
            }
        }
    }
}
