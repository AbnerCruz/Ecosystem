using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Lunet.Client.Shell;
using Lunet.Client.Ui;
using Lunet.Core;
using SpriteDocument = Lunet.Studio.SpriteDocument;
using SpriteHistory = Lunet.Studio.SpriteHistory;
using SpriteColor = Lunet.Studio.SpriteColor;
using SpriteFloating = Lunet.Studio.SpriteFloating;
using SpriteSelection = Lunet.Studio.SpriteSelection;

namespace Lunet.Client.Views;

/// <summary>The drawing tools of the Sprite Studio.</summary>
public enum SpriteTool { Pencil, Eraser, Fill, Picker, Select }

/// <summary>How the Select tool picks pixels.</summary>
public enum SpriteSelectMode { Rectangle, Lasso, Wand }

/// <summary>
/// The pixel canvas: the current frame scaled to fit (crisp pixels), a grid when zoomed in, the previous frame as a
/// faint onion skin; pinch, pan and painting come from <see cref="ZoomCanvas"/>.
/// </summary>
public sealed class PixelCanvas : ZoomCanvas
{
    private WriteableBitmap? _bitmap, _onion;
    private SpriteDocument _doc = SpriteDocument.Create(16);

    public PixelCanvas() => AutomationProperties.SetName(this, "Tela de pintura");

    protected override (double Width, double Height) ContentSize => (_doc.Width, _doc.Height);
    protected override (int X, int Y) Cells => (_doc.Width, _doc.Height);

    /// <summary>Selection outline (cell edges), the marquee or lasso being drawn, and the floating pixels' corners.</summary>
    public IReadOnlyList<(int X0, int Y0, int X1, int Y1)> Outline { get; set; } = [];
    public (int X0, int Y0, int X1, int Y1)? Marquee { get; set; }
    public IReadOnlyList<(int X, int Y)> LassoPath { get; set; } = [];
    public (float X, float Y)[]? FloatingCorners { get; set; }

    public void Show(SpriteDocument doc, int frame, bool onion, uint[]? pixels = null)
    {
        _doc = doc;
        _bitmap = Bitmap(pixels ?? doc.Frames[frame], _bitmap);
        _onion = onion && frame > 0 ? Bitmap(doc.Frames[frame - 1], _onion) : null;
        InvalidateVisual();
    }

    private WriteableBitmap Bitmap(uint[] pixels, WriteableBitmap? reuse)
    {
        var bitmap = reuse is { } b && b.PixelSize.Width == _doc.Width && b.PixelSize.Height == _doc.Height ? b
            : new WriteableBitmap(new PixelSize(_doc.Width, _doc.Height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Unpremul);
        using var buffer = bitmap.Lock();
        unsafe
        {
            var dst = new Span<uint>((void*)buffer.Address, _doc.Width * _doc.Height);
            pixels.AsSpan().CopyTo(dst); // ARGB in a uint is BGRA in little-endian memory
        }
        return bitmap;
    }

    protected override void RenderContent(DrawingContext context, Rect a)
    {
        if (a.Width <= 0) return;
        // Checkerboard: transparent pixels read as transparent.
        var cell = Math.Max(4, a.Width / _doc.Width / 2);
        using (context.PushClip(a))
        {
            context.FillRectangle(LunetTokens.Brush(LunetTokens.Ink.I4), a);
            for (var y = a.Y; y < a.Bottom; y += cell)
                for (var x = a.X + (((int)((y - a.Y) / cell)) % 2) * cell; x < a.Right; x += cell * 2)
                    context.FillRectangle(LunetTokens.Brush(LunetTokens.Ink.I5), new Rect(x, y, cell, cell));
        }
        if (_onion is not null)
            using (context.PushOpacity(0.25))
                context.DrawImage(_onion, new Rect(0, 0, _doc.Width, _doc.Height), a);
        if (_bitmap is not null)
            using (context.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = BitmapInterpolationMode.None }))
                context.DrawImage(_bitmap, new Rect(0, 0, _doc.Width, _doc.Height), a);
        DrawGrid(context, a);
        context.DrawRectangle(new Pen(LunetTokens.Brush(LunetTokens.Color.BorderStrong), 1), a);
        DrawSelection(context, a);
    }

    private void DrawSelection(DrawingContext context, Rect a)
    {
        double cw = a.Width / _doc.Width, ch = a.Height / _doc.Height;
        Point P(double x, double y) => new(a.X + x * cw, a.Y + y * ch);
        // Marching-ants look without animation: a dark line under a light dashed one reads on any colour.
        var under = new Pen(LunetTokens.Brush("#000000"), 2);
        var over = new Pen(LunetTokens.Brush("#ffffff"), 1, new DashStyle([3, 3], 0));
        void Line(Point p, Point q) { context.DrawLine(under, p, q); context.DrawLine(over, p, q); }
        foreach (var (x0, y0, x1, y1) in Outline) Line(P(x0, y0), P(x1, y1));
        if (Marquee is { } m)
        {
            double l = Math.Min(m.X0, m.X1), t = Math.Min(m.Y0, m.Y1), r = Math.Max(m.X0, m.X1) + 1, b = Math.Max(m.Y0, m.Y1) + 1;
            Line(P(l, t), P(r, t)); Line(P(r, t), P(r, b)); Line(P(r, b), P(l, b)); Line(P(l, b), P(l, t));
        }
        for (var i = 1; i < LassoPath.Count; i++)
            Line(P(LassoPath[i - 1].X + .5, LassoPath[i - 1].Y + .5), P(LassoPath[i].X + .5, LassoPath[i].Y + .5));
        if (FloatingCorners is { } c)
        {
            var accent = new Pen(LunetTokens.Brush(LunetTokens.Color.Primary), 2);
            for (var i = 0; i < 4; i++) context.DrawLine(accent, P(c[i].X, c[i].Y), P(c[(i + 1) % 4].X, c[(i + 1) % 4].Y));
        }
    }
}

/// <summary>
/// The Sprite Studio in the shell (A1): draw a sprite and its animation frames on the phone, with the tools at the
/// bottom (thumb reach), undo per stroke, and the same files as before — <c>.sprite.json</c> (editable source) and
/// its <c>.png</c> sheet, written together (StudioAssetWorkspace bundle, verified). Saves when you leave.
/// </summary>
public sealed class SpriteStudioView : UserControl
{
    /// <summary>A small, legible starting palette (PICO-8 inspired tones plus black and white).</summary>
    public static readonly uint[] Palette =
    [
        0xFF000000, 0xFFFFFFFF, 0xFF1D2B53, 0xFF7E2553, 0xFF008751, 0xFFAB5236, 0xFF5F574F, 0xFFC2C3C7,
        0xFFFF004D, 0xFFFFA300, 0xFFFFEC27, 0xFF00E436, 0xFF29ADFF, 0xFF83769C, 0xFFFF77A8, 0xFFFFCCAA,
    ];

    private readonly LunetShell _shell;
    private readonly string _path;
    private readonly Action _close;
    private readonly StudioAssetWorkspace _storage;
    private readonly SpriteHistory _history = new();
    private readonly PixelCanvas _canvas = new();
    private readonly StackPanel _tools = new() { Orientation = Orientation.Horizontal, Spacing = 2, HorizontalAlignment = HorizontalAlignment.Center };
    private readonly StackPanel _colors = new() { Orientation = Orientation.Horizontal, Spacing = 6 };
    private readonly StackPanel _frames = new() { Orientation = Orientation.Horizontal, Spacing = 6 };
    private readonly TextBlock _title = LunetUi.Title("");
    private readonly TextBlock _info = LunetUi.Text("", LunetTokens.Type.Caption, LunetTokens.Color.TextSecondary);
    private readonly Avalonia.Threading.DispatcherTimer _playTimer = new();
    private readonly Border _picker = new() { IsVisible = false, Padding = new Thickness(LunetTokens.Space.M, LunetTokens.Space.S) };
    private readonly StackPanel _selectionBar = new() { Orientation = Orientation.Horizontal, Spacing = 2 };
    private readonly ScrollViewer _selectionRow;
    private bool _dirty;
    private bool _playing;
    private SpriteSelection? _selection;
    private SpriteFloating? _floating;
    private SpriteSelection? _lifted;
    private bool _liftedCopy, _selecting, _moving;
    private (int X, int Y) _selStart, _selEnd, _moveLast;
    private readonly List<(int X, int Y)> _lasso = [];

    public SpriteStudioView(LunetShell shell, string path, Action close, StudioAssetWorkspace? storage = null)
    {
        _shell = shell;
        _path = path;
        _close = close;
        // A game's folder, or the standalone Studio folder (Home → Studios) when given.
        _storage = storage ?? new StudioAssetWorkspace(shell.Workspace.Project!.Directory);
        Document = File.Exists(Path.Combine(_storage.RootDirectory, path))
            ? SpriteDocument.Parse(_storage.ReadText(path)) : SpriteDocument.Create(16);
        Background = LunetTokens.Brush(LunetTokens.Color.Background);
        if (PaletteView.Load(shell) is { Colors.Count: > 0 } palette) GamePalette = palette.Argb;
        _canvas.Paint += Paint;
        _canvas.StrokeEnded += () => { if (Tool == SpriteTool.Select) EndSelectStroke(); else Refresh(frames: true); };
        // Two fingers mean zoom: the dot the first finger left is taken back (a selection being drawn is dropped).
        _canvas.StrokeCancelled += () =>
        {
            if (Tool == SpriteTool.Select) { _selecting = false; _moving = false; ShowCanvas(); return; }
            Document = _history.Rollback(Document); ShowCanvas();
        };
        _playTimer.Tick += (_, _) => { Frame = (Frame + 1) % Document.Frames.Count; ShowCanvas(); };

        var back = LunetUi.IconButton(LunetIcons.Back, "Salvar e voltar", () => Close());
        _title.Text = Path.GetFileName(path).Replace(".sprite.json", "", StringComparison.OrdinalIgnoreCase);
        var titles = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { _title, _info } };
        var play = LunetUi.IconButton(LunetIcons.Play, "Prévia da animação", TogglePlay);
        play.Name = "PlayAnimation";
        var fit = LunetUi.IconButton(LunetIcons.Fit, "Ver o sprite inteiro (dois dedos aproximam)", () => _canvas.ResetView());
        fit.Name = "FitView";
        DockPanel.SetDock(fit, Dock.Right);
        var bar = new DockPanel { Height = 56, Margin = new Thickness(LunetTokens.Space.Xxs, 0, LunetTokens.Space.Xs, 0) };
        DockPanel.SetDock(back, Dock.Left);
        DockPanel.SetDock(play, Dock.Right);
        bar.Children.Add(back);
        bar.Children.Add(play);
        bar.Children.Add(fit);
        bar.Children.Add(titles);

        var framesRow = new ScrollViewer { Content = _frames, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Hidden, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled, Margin = new Thickness(LunetTokens.Space.S, LunetTokens.Space.Xs) };
        _selectionRow = new ScrollViewer { Content = _selectionBar, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Hidden, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled, Margin = new Thickness(LunetTokens.Space.S, 0), IsVisible = false };
        var colorsRow = new ScrollViewer { Content = _colors, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Hidden, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled, Margin = new Thickness(LunetTokens.Space.S, LunetTokens.Space.Xs) };
        // Everything the thumb needs is at the bottom: frames, colours, then tools.
        var bottom = new StackPanel
        {
            Background = LunetTokens.Brush(LunetTokens.Color.Surface),
            Children = { _picker, framesRow, colorsRow, _selectionRow, new Border { Child = _tools, Padding = new Thickness(0, 0, 0, LunetTokens.Space.Xs) } },
        };
        var root = new DockPanel();
        DockPanel.SetDock(bar, Dock.Top);
        DockPanel.SetDock(bottom, Dock.Bottom);
        root.Children.Add(bar);
        root.Children.Add(bottom);
        root.Children.Add(new Border { Child = _canvas, Margin = new Thickness(LunetTokens.Space.S) });
        Content = root;
        Refresh(frames: true);
    }

    public SpriteDocument Document { get; private set; }

    /// <summary>The colours offered: the game's palette when it has one, else <see cref="Palette"/>.</summary>
    public IReadOnlyList<uint> GamePalette { get; private set; } = Palette;

    /// <summary>The canvas (zoom and pan).</summary>
    public PixelCanvas Canvas => _canvas;
    public int Frame { get; private set; }
    public SpriteTool Tool { get; private set; } = SpriteTool.Pencil;
    public uint Color { get; private set; } = 0xFF000000;

    public void SelectTool(SpriteTool tool)
    {
        if (tool != SpriteTool.Select) { CommitFloating(); _selection = null; }
        Tool = tool;
        Refresh(frames: false);
    }

    public void SelectColor(uint color) { Color = color; Tool = Tool is SpriteTool.Eraser or SpriteTool.Picker or SpriteTool.Select ? SpriteTool.Pencil : Tool; CommitFloating(); _selection = null; Refresh(frames: false); }

    // ---------- Selection ----------

    public SpriteSelectMode SelectionMode { get; private set; } = SpriteSelectMode.Rectangle;
    public SpriteSelection? Selection => _selection;
    public SpriteFloating? Floating => _floating;

    public void SetSelectionMode(SpriteSelectMode mode) { SelectionMode = mode; Tool = SpriteTool.Select; Refresh(frames: false); }

    public void SelectAll()
    {
        CommitFloating();
        Tool = SpriteTool.Select;
        SetSelection(SpriteSelection.All(Document.Width, Document.Height));
    }

    public void SetSelection(SpriteSelection? selection)
    {
        _selection = selection is { IsEmpty: false } ? selection : null;
        Refresh(frames: false);
    }

    /// <summary>Lifts the selected pixels so they can move (one undo step per lift).</summary>
    private void EnsureFloating(bool copy = false)
    {
        if (_floating is not null || _selection is null) return;
        _history.CheckpointFrame(Document, Frame);
        _floating = SpriteFloating.Lift(Document, Frame, _selection, copy);
        _lifted = _selection;
        _liftedCopy = copy;
        _selection = null;
    }

    /// <summary>Writes the floating pixels where they are now.</summary>
    public void CommitFloating()
    {
        if (_floating is not { } floating) return;
        _floating = null;
        floating.Commit(Document, Frame);
        _dirty = true;
        Refresh(frames: true);
    }

    /// <summary>Puts the pixels back (undoes the move) or, with nothing lifted, clears the selection.</summary>
    public void CancelSelection()
    {
        if (_floating is not null) { _floating = null; Document = _history.Rollback(Document); }
        _selection = null;
        Refresh(frames: true);
    }

    public void RotateSelection() => OnFloating(f => f.Rotate90(true));
    public void FlipSelection(bool horizontal) => OnFloating(f => { if (horizontal) f.FlipHorizontal(); else f.FlipVertical(); });

    /// <summary>Moves the selection by whole pixels (lifting it first).</summary>
    public void MoveSelection(int dx, int dy) => OnFloating(f => f.MoveBy(dx, dy));

    private void OnFloating(Action<SpriteFloating> change)
    {
        EnsureFloating();
        if (_floating is null) return;
        change(_floating);
        Refresh(frames: false);
    }

    /// <summary>Stamps a copy: the lifted pixels stay floating, a copy is written where they are.</summary>
    public void DuplicateSelection()
    {
        if (_floating is { } floating) { floating.Stamp(Document, Frame); _dirty = true; Refresh(frames: true); return; }
        EnsureFloating(copy: true);
        _floating?.MoveBy(2, 2);
        Refresh(frames: false);
    }

    /// <summary>Erases the selected pixels (or drops the lifted ones).</summary>
    public void DeleteSelection()
    {
        if (_floating is not null)
        {
            _floating = null;
            Document = _history.Rollback(Document);
            if (!_liftedCopy && _lifted is { } lifted) { _history.CheckpointFrame(Document, Frame); lifted.Erase(Document, Frame); _dirty = true; }
        }
        else if (_selection is { } selection)
        {
            _history.CheckpointFrame(Document, Frame);
            selection.Erase(Document, Frame);
            _dirty = true;
        }
        _selection = null;
        Refresh(frames: true);
    }

    private bool InsideFloating(int x, int y)
    {
        var c = _floating!.Corners();
        return x + .5 >= c.Min(p => p.X) && x + .5 <= c.Max(p => p.X) && y + .5 >= c.Min(p => p.Y) && y + .5 <= c.Max(p => p.Y);
    }

    private void SelectTouch(int x, int y, bool first)
    {
        if (first)
        {
            _selecting = _moving = false;
            if (_floating is not null)
            {
                if (InsideFloating(x, y)) { _moving = true; _moveLast = (x, y); return; }
                CommitFloating();
            }
            if (_selection is { } selection && selection.Contains(x, y))
            {
                EnsureFloating();
                _moving = true; _moveLast = (x, y);
                Refresh(frames: false);
                return;
            }
            _selection = null;
            if (SelectionMode == SpriteSelectMode.Wand) { SetSelection(SpriteSelection.FromColor(Document, Frame, x, y)); return; }
            _selecting = true;
            _selStart = _selEnd = (x, y);
            _lasso.Clear();
            _lasso.Add((x, y));
            ShowCanvas();
            return;
        }
        if (_moving && _floating is { } floating)
        {
            floating.MoveBy(x - _moveLast.X, y - _moveLast.Y);
            _moveLast = (x, y);
            ShowCanvas();
        }
        else if (_selecting)
        {
            _selEnd = (x, y);
            if (SelectionMode == SpriteSelectMode.Lasso) _lasso.Add((x, y));
            ShowCanvas();
        }
    }

    private void EndSelectStroke()
    {
        var wasSelecting = _selecting;
        _selecting = _moving = false;
        if (!wasSelecting) { Refresh(frames: false); return; }
        SetSelection(SelectionMode switch
        {
            SpriteSelectMode.Rectangle when _selStart == _selEnd => null, // a plain tap clears
            SpriteSelectMode.Rectangle => SpriteSelection.FromRectangle(Document.Width, Document.Height, _selStart.X, _selStart.Y, _selEnd.X, _selEnd.Y),
            _ => SpriteSelection.FromPolygon(Document.Width, Document.Height, _lasso),
        });
    }

    /// <summary>The selection drag as the canvas would send it (tests use the same path as a finger).</summary>
    public void DragSelect(int x0, int y0, int x1, int y1)
    {
        Paint(x0, y0, first: true);
        if ((x1, y1) != (x0, y0)) Paint(x1, y1, first: false);
        if (Tool == SpriteTool.Select) EndSelectStroke();
    }

    /// <summary>One touch of a tool at a pixel (the canvas calls it; tests too). <paramref name="first"/> starts an undo step.</summary>
    public void Paint(int x, int y, bool first)
    {
        if (Tool == SpriteTool.Select) { SelectTouch(x, y, first); return; }
        if (Tool == SpriteTool.Picker)
        {
            var picked = Document.Frames[Frame][y * Document.Width + x];
            if ((picked >> 24) != 0) Color = picked;
            Tool = SpriteTool.Pencil;
            Refresh(frames: false);
            return;
        }
        if (first) _history.CheckpointFrame(Document, Frame);
        var color = Tool == SpriteTool.Eraser ? 0u : Color;
        if (Tool == SpriteTool.Fill) { if (first) Document.Fill(Frame, x, y, color); }
        else
        {
            var (px, py) = first || _lastPaint is null ? (x, y) : _lastPaint.Value;
            Document.Stroke(Frame, px, py, x, y, color);
        }
        _lastPaint = (x, y);
        _dirty = true;
        ShowCanvas();
    }

    private (int X, int Y)? _lastPaint;

    public void Undo() { if (_floating is not null) { CancelSelection(); return; } if (_history.CanUndo) { Document = _history.Undo(Document); Frame = Math.Min(Frame, Document.Frames.Count - 1); _dirty = true; Refresh(frames: true); } }
    public void Redo() { if (_floating is not null) return; if (_history.CanRedo) { Document = _history.Redo(Document); Frame = Math.Min(Frame, Document.Frames.Count - 1); _dirty = true; Refresh(frames: true); } }

    public void AddFrame(bool duplicate)
    {
        CommitFloating(); _selection = null;
        _history.Checkpoint(Document);
        Document.AddFrame(duplicate ? Frame : -1);
        Frame = Document.Frames.Count - 1;
        _dirty = true;
        Refresh(frames: true);
    }

    public void RemoveFrame()
    {
        if (Document.Frames.Count <= 1) return;
        CommitFloating(); _selection = null;
        _history.Checkpoint(Document);
        Document.RemoveFrame(Frame);
        Frame = Math.Min(Frame, Document.Frames.Count - 1);
        _dirty = true;
        Refresh(frames: true);
    }

    public void SelectFrame(int frame) { CommitFloating(); _selection = null; Frame = Math.Clamp(frame, 0, Document.Frames.Count - 1); Refresh(frames: true); }

    /// <summary>Writes the source and its sprite sheet together (or neither). No-op when nothing changed.</summary>
    public void Save()
    {
        CommitFloating();
        if (!_dirty) return;
        var sheetPath = _path[..^".sprite.json".Length] + ".png";
        var png = PngEncoder.Rgba(Document.Width * Document.Frames.Count, Document.Height, Document.SheetRgba());
        _storage.WriteBundle((_path, System.Text.Encoding.UTF8.GetBytes(Document.ToJson())), (sheetPath, png));
        _dirty = false;
    }

    /// <summary>Saves and leaves; a failed save keeps you here with the reason (nothing is lost).</summary>
    public bool Close()
    {
        try { Save(); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException)
        {
            _info.Text = "Não foi possível salvar: " + e.Message;
            _info.Foreground = LunetTokens.Brush(LunetTokens.Color.Danger);
            return true;
        }
        _playTimer.Stop();
        _close();
        return true;
    }

    private void TogglePlay()
    {
        _playing = !_playing;
        _playTimer.Interval = TimeSpan.FromSeconds(1.0 / Math.Max(1, Document.Fps));
        if (_playing) _playTimer.Start(); else { _playTimer.Stop(); Refresh(frames: true); }
    }

    private void ShowCanvas()
    {
        _canvas.Outline = _selection?.OutlineSegments() ?? [];
        _canvas.Marquee = _selecting && SelectionMode == SpriteSelectMode.Rectangle ? (_selStart.X, _selStart.Y, _selEnd.X, _selEnd.Y) : null;
        _canvas.LassoPath = _selecting && SelectionMode == SpriteSelectMode.Lasso ? _lasso.ToArray() : [];
        _canvas.FloatingCorners = _floating?.Corners();
        _canvas.Show(Document, Frame, onion: !_playing, _floating?.Compose());
    }

    private void Refresh(bool frames)
    {
        ShowCanvas();
        _info.Text = $"{Document.Width}×{Document.Height} · quadro {Frame + 1} de {Document.Frames.Count} · {Document.Fps} FPS";
        _info.Foreground = LunetTokens.Brush(LunetTokens.Color.TextSecondary);

        _tools.Children.Clear();
        void ToolKey(SpriteTool tool, string icon, string label)
        {
            var selected = tool == Tool;
            var b = new Button
            {
                Width = 52, Height = 52, Padding = new Thickness(0), CornerRadius = new CornerRadius(LunetTokens.Radius.M),
                Background = LunetTokens.Brush(selected ? LunetTokens.Color.PrimarySoft : "#00000000"), BorderThickness = new Thickness(0),
                Content = LunetIcons.Create(icon, selected ? LunetTokens.Color.Primary : LunetTokens.Color.TextPrimary, 22),
                HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center, Name = "Tool-" + tool,
            };
            AutomationProperties.SetName(b, label + (selected ? ", escolhido" : ""));
            ToolTip.SetTip(b, label);
            b.Click += (_, _) => SelectTool(tool);
            _tools.Children.Add(b);
        }
        ToolKey(SpriteTool.Pencil, LunetIcons.Edit, "Lápis");
        ToolKey(SpriteTool.Eraser, LunetIcons.Eraser, "Borracha");
        ToolKey(SpriteTool.Fill, LunetIcons.Bucket, "Balde");
        ToolKey(SpriteTool.Picker, LunetIcons.Dropper, "Conta-gotas");
        ToolKey(SpriteTool.Select, LunetIcons.Select, "Selecionar");
        _tools.Children.Add(new Border { Width = 1, Height = 28, Margin = new Thickness(6, 0), Background = LunetTokens.Brush(LunetTokens.Color.BorderStrong), VerticalAlignment = VerticalAlignment.Center });
        _tools.Children.Add(Action("↶", "Desfazer", Undo, _history.CanUndo || _floating is not null));
        _tools.Children.Add(Action("↷", "Refazer", Redo, _history.CanRedo));

        BuildSelectionBar();

        _colors.Children.Clear();
        // The first swatch is the colour in use: a tap opens the picker (hue, saturation, brightness, alpha, hex).
        var current = new Button
        {
            Width = 44, Height = 36, Padding = new Thickness(0), CornerRadius = new CornerRadius(LunetTokens.Radius.M),
            Background = new Avalonia.Media.Immutable.ImmutableSolidColorBrush(Avalonia.Media.Color.FromUInt32(Color)),
            BorderBrush = LunetTokens.Brush(LunetTokens.Color.TextPrimary), BorderThickness = new Thickness(2), Name = "ColorPicker",
            Content = LunetIcons.Create(LunetIcons.Edit, ((Color >> 16 & 0xff) * 0.3 + (Color >> 8 & 0xff) * 0.59 + (Color & 0xff) * 0.11) > 140 ? "#000000" : "#ffffff", 16),
            HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center,
        };
        AutomationProperties.SetName(current, "Escolher cor, atual " + SpriteColor.ToHex(Color));
        current.Click += (_, _) => TogglePicker();
        _colors.Children.Add(current);
        _colors.Children.Add(new Border { Width = 1, Height = 24, Margin = new Thickness(2, 0), Background = LunetTokens.Brush(LunetTokens.Color.BorderStrong), VerticalAlignment = VerticalAlignment.Center });
        // The game's palette (Assets/palette.json) when it has one; else the starter colours.
        foreach (var c in GamePalette)
        {
            var selected = c == Color && Tool is SpriteTool.Pencil or SpriteTool.Fill;
            var swatch = new Button
            {
                Width = 36, Height = 36, Padding = new Thickness(0), CornerRadius = new CornerRadius(LunetTokens.Radius.Pill),
                Background = new Avalonia.Media.Immutable.ImmutableSolidColorBrush(Avalonia.Media.Color.FromUInt32(c)),
                BorderBrush = LunetTokens.Brush(selected ? LunetTokens.Color.TextPrimary : LunetTokens.Color.BorderStrong),
                BorderThickness = new Thickness(selected ? 3 : 1),
            };
            AutomationProperties.SetName(swatch, "Cor " + SpriteColor.ToHex(c));
            var color = c;
            swatch.Click += (_, _) => SelectColor(color);
            _colors.Children.Add(swatch);
        }

        if (!frames) return;
        _frames.Children.Clear();
        for (var i = 0; i < Document.Frames.Count; i++)
        {
            var index = i;
            var thumb = new PixelCanvas { Width = 44, Height = 44, IsHitTestVisible = false };
            thumb.Show(Document, i, onion: false);
            var b = new Button
            {
                Content = thumb, Width = 52, Height = 52, Padding = new Thickness(3), CornerRadius = new CornerRadius(LunetTokens.Radius.S),
                Background = LunetTokens.Brush(LunetTokens.Color.SurfaceRaised),
                BorderBrush = LunetTokens.Brush(i == Frame ? LunetTokens.Color.Primary : LunetTokens.Color.Border), BorderThickness = new Thickness(i == Frame ? 2 : 1),
            };
            AutomationProperties.SetName(b, $"Quadro {i + 1}");
            b.Click += (_, _) => SelectFrame(index);
            _frames.Children.Add(b);
        }
        _frames.Children.Add(Action("+", "Novo quadro vazio", () => AddFrame(duplicate: false), true));
        _frames.Children.Add(Action("⧉", "Duplicar este quadro", () => AddFrame(duplicate: true), true));
        _frames.Children.Add(Action("✕", "Apagar este quadro", RemoveFrame, Document.Frames.Count > 1));
    }

    /// <summary>Opens or closes the colour picker above the palette.</summary>
    public void TogglePicker()
    {
        _picker.IsVisible = !_picker.IsVisible;
        if (_picker.IsVisible) BuildPicker();
    }

    public bool PickerOpen => _picker.IsVisible;

    private void BuildPicker()
    {
        var (h, sat, v, a) = SpriteColor.ToHsv(Color);
        var updating = false;
        var hex = new TextBox { Text = SpriteColor.ToHex(Color), Width = 120, MinHeight = LunetTokens.MinTouch, FontFamily = LunetTokens.MonoFont, VerticalContentAlignment = VerticalAlignment.Center, Name = "ColorHex" };
        AutomationProperties.SetName(hex, "Cor em hexadecimal");
        Slider Make(string label, double max, double value, string name)
        {
            var slider = new Slider { Minimum = 0, Maximum = max, Value = value, MinHeight = 40, Name = name };
            AutomationProperties.SetName(slider, label);
            return slider;
        }
        var hue = Make("Matiz", 360, h, "Hue");
        var saturation = Make("Saturação", 100, sat * 100, "Saturation");
        var value = Make("Brilho", 100, v * 100, "Brightness");
        var alpha = Make("Opacidade", 255, a, "Alpha");
        void FromSliders()
        {
            if (updating) return;
            Color = SpriteColor.FromHsv((float)hue.Value, (float)(saturation.Value / 100), (float)(value.Value / 100), (int)alpha.Value);
            updating = true; hex.Text = SpriteColor.ToHex(Color); updating = false;
            if (Tool is SpriteTool.Eraser or SpriteTool.Picker) Tool = SpriteTool.Pencil;
            Refresh(frames: false);
        }
        foreach (var slider in new[] { hue, saturation, value, alpha }) slider.ValueChanged += (_, _) => FromSliders();
        hex.TextChanged += (_, _) =>
        {
            if (updating || SpriteColor.ParseHex(hex.Text) is not { } parsed) return;
            Color = parsed;
            var (nh, ns, nv, na) = SpriteColor.ToHsv(parsed);
            updating = true; hue.Value = nh; saturation.Value = ns * 100; value.Value = nv * 100; alpha.Value = na; updating = false;
            Refresh(frames: false);
        };
        Control Row(string label, Slider slider)
        {
            var row = new DockPanel();
            var t = LunetUi.Text(label, LunetTokens.Type.Caption, LunetTokens.Color.TextSecondary);
            t.Width = 72; t.VerticalAlignment = VerticalAlignment.Center;
            DockPanel.SetDock(t, Dock.Left);
            row.Children.Add(t);
            row.Children.Add(slider);
            return row;
        }
        var close = LunetUi.Button("Pronto", ButtonKind.Ghost, null, TogglePicker);
        var top = new DockPanel();
        DockPanel.SetDock(close, Dock.Right);
        top.Children.Add(close);
        top.Children.Add(hex);
        _picker.Child = new StackPanel { Spacing = 2, Children = { top, Row("Matiz", hue), Row("Saturação", saturation), Row("Brilho", value), Row("Opacidade", alpha) } };
        _picker.Background = LunetTokens.Brush(LunetTokens.Color.SurfaceRaised);
    }

    /// <summary>Above the tools while Select is chosen: how to pick, then what to do with what is picked.</summary>
    private void BuildSelectionBar()
    {
        _selectionBar.Children.Clear();
        _selectionRow.IsVisible = Tool == SpriteTool.Select;
        if (!_selectionRow.IsVisible) return;
        Button Key(string icon, string label, Action act, string name, bool on = false, bool iconOnly = false)
        {
            var b = new Button
            {
                Height = 44, MinWidth = 44, Padding = new Thickness(LunetTokens.Space.S, 0), CornerRadius = new CornerRadius(LunetTokens.Radius.M),
                Background = LunetTokens.Brush(on ? LunetTokens.Color.PrimarySoft : "#00000000"), BorderThickness = new Thickness(0), Name = name,
                Content = new StackPanel
                {
                    Orientation = Orientation.Horizontal, Spacing = 6,
                    Children = { LunetIcons.Create(icon, on ? LunetTokens.Color.Primary : LunetTokens.Color.TextPrimary, 20) },
                },
                VerticalContentAlignment = VerticalAlignment.Center,
            };
            if (!iconOnly) ((StackPanel)b.Content!).Children.Add(LunetUi.Text(label, LunetTokens.Type.Label, on ? LunetTokens.Color.Primary : LunetTokens.Color.TextPrimary));
            AutomationProperties.SetName(b, label);
            ToolTip.SetTip(b, label);
            b.Click += (_, _) => act();
            _selectionBar.Children.Add(b);
            return b;
        }
        if (_selection is null && _floating is null)
        {
            Key(LunetIcons.Select, "Retângulo", () => SetSelectionMode(SpriteSelectMode.Rectangle), "Mode-Rectangle", SelectionMode == SpriteSelectMode.Rectangle);
            Key(LunetIcons.Lasso, "Laço", () => SetSelectionMode(SpriteSelectMode.Lasso), "Mode-Lasso", SelectionMode == SpriteSelectMode.Lasso);
            Key(LunetIcons.Wand, "Mesma cor", () => SetSelectionMode(SpriteSelectMode.Wand), "Mode-Wand", SelectionMode == SpriteSelectMode.Wand);
            Key(LunetIcons.Fit, "Tudo", SelectAll, "SelectAll");
            return;
        }
        if (_floating is not null) Key(LunetIcons.Check, "Aplicar", CommitFloating, "CommitSelection", on: true);
        Key(LunetIcons.Close, _floating is not null ? "Desfazer" : "Limpar", CancelSelection, "CancelSelection");
        Key(LunetIcons.Rotate, "Girar", RotateSelection, "RotateSelection");
        Key(LunetIcons.FlipH, "Espelhar na horizontal", () => FlipSelection(horizontal: true), "FlipH", iconOnly: true);
        Key(LunetIcons.FlipV, "Espelhar na vertical", () => FlipSelection(horizontal: false), "FlipV", iconOnly: true);
        Key(LunetIcons.Copy, "Duplicar", DuplicateSelection, "DuplicateSelection");
        Key(LunetIcons.Trash, "Apagar", DeleteSelection, "DeleteSelection");
    }

    private static Button Action(string face, string label, Action act, bool enabled)
    {
        var b = new Button
        {
            // Never the theme's grey disabled fill: a dimmed glyph says "not now" and the tap does nothing.
            Width = 52, Height = 52, Padding = new Thickness(0), CornerRadius = new CornerRadius(LunetTokens.Radius.M),
            Background = LunetTokens.Brush("#00000000"), BorderThickness = new Thickness(0),
            Content = new TextBlock { Text = face, FontSize = 20, Foreground = LunetTokens.Brush(enabled ? LunetTokens.Color.TextPrimary : LunetTokens.Color.TextTertiary), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
            HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center,
        };
        AutomationProperties.SetName(b, label + (enabled ? "" : " (indisponível)"));
        ToolTip.SetTip(b, label);
        b.Click += (_, _) => { if (enabled) act(); };
        return b;
    }
}
