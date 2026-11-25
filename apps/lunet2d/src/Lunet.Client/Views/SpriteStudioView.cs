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

namespace Lunet.Client.Views;

/// <summary>The drawing tools of the Sprite Studio.</summary>
public enum SpriteTool { Pencil, Eraser, Fill, Picker }

/// <summary>
/// The pixel canvas: the current frame scaled to fit (crisp pixels), a grid when zoomed in, the previous frame as a
/// faint onion skin. One finger draws; it never scrolls the page.
/// </summary>
public sealed class PixelCanvas : Control
{
    private WriteableBitmap? _bitmap, _onion;
    private SpriteDocument _doc = SpriteDocument.Create(16);
    private int _frame;
    private (int X, int Y)? _last;

    public event Action<int, int, bool>? Paint; // x, y, first point of the stroke
    public event Action? StrokeEnded;

    public PixelCanvas()
    {
        ClipToBounds = true;
        Focusable = false;
        AutomationProperties.SetName(this, "Tela de pintura");
        PointerPressed += (_, e) => { e.Pointer.Capture(this); _last = null; Hit(e, first: true); e.Handled = true; };
        PointerMoved += (_, e) => { if (e.Pointer.Captured == this) { Hit(e, first: false); e.Handled = true; } };
        PointerReleased += (_, e) => { if (e.Pointer.Captured == this) { e.Pointer.Capture(null); _last = null; StrokeEnded?.Invoke(); } };
    }

    public void Show(SpriteDocument doc, int frame, bool onion)
    {
        _doc = doc;
        _frame = frame;
        _bitmap = Bitmap(doc.Frames[frame], _bitmap);
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

    /// <summary>The rectangle the sprite occupies (fit, integer scale when it can, centred).</summary>
    public Rect Area
    {
        get
        {
            var scale = Math.Min(Bounds.Width / _doc.Width, Bounds.Height / _doc.Height);
            if (scale >= 2) scale = Math.Floor(scale);
            var w = _doc.Width * scale; var h = _doc.Height * scale;
            return new Rect((Bounds.Width - w) / 2, (Bounds.Height - h) / 2, w, h);
        }
    }

    private void Hit(PointerEventArgs e, bool first)
    {
        var a = Area;
        if (a.Width <= 0) return;
        var p = e.GetPosition(this);
        var x = (int)Math.Floor((p.X - a.X) / a.Width * _doc.Width);
        var y = (int)Math.Floor((p.Y - a.Y) / a.Height * _doc.Height);
        if (x < 0 || y < 0 || x >= _doc.Width || y >= _doc.Height) return;
        if (_last == (x, y)) return;
        _last = (x, y);
        Paint?.Invoke(x, y, first);
    }

    public override void Render(DrawingContext context)
    {
        var a = Area;
        // Checkerboard: transparent pixels read as transparent.
        var cell = Math.Max(4, a.Width / _doc.Width / 2);
        using (context.PushClip(a))
        {
            context.FillRectangle(LunetTokens.Brush("#2a2c33"), a);
            for (var y = a.Y; y < a.Bottom; y += cell)
                for (var x = a.X + (((int)((y - a.Y) / cell)) % 2) * cell; x < a.Right; x += cell * 2)
                    context.FillRectangle(LunetTokens.Brush("#353841"), new Rect(x, y, cell, cell));
        }
        if (_onion is not null)
            using (context.PushOpacity(0.25))
                context.DrawImage(_onion, new Rect(0, 0, _doc.Width, _doc.Height), a);
        if (_bitmap is not null)
        {
            using (context.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = BitmapInterpolationMode.None }))
                context.DrawImage(_bitmap, new Rect(0, 0, _doc.Width, _doc.Height), a);
        }
        var px = a.Width / _doc.Width;
        if (px >= 8)
        {
            var pen = new Pen(LunetTokens.Brush("#22ffffff"), 1);
            for (var i = 1; i < _doc.Width; i++) context.DrawLine(pen, new Point(Math.Round(a.X + i * px) + .5, a.Y), new Point(Math.Round(a.X + i * px) + .5, a.Bottom));
            for (var i = 1; i < _doc.Height; i++) context.DrawLine(pen, new Point(a.X, Math.Round(a.Y + i * px) + .5), new Point(a.Right, Math.Round(a.Y + i * px) + .5));
        }
        context.DrawRectangle(new Pen(LunetTokens.Brush(LunetTokens.Color.BorderStrong), 1), a);
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
    private bool _dirty;
    private bool _playing;

    public SpriteStudioView(LunetShell shell, string path, Action close)
    {
        _shell = shell;
        _path = path;
        _close = close;
        _storage = new StudioAssetWorkspace(shell.Workspace.Project!.Directory);
        Document = File.Exists(Path.Combine(shell.Workspace.Project.Directory, path))
            ? SpriteDocument.Parse(_storage.ReadText(path)) : SpriteDocument.Create(16);
        Background = LunetTokens.Brush(LunetTokens.Color.Background);
        _canvas.Paint += Paint;
        _canvas.StrokeEnded += () => { Refresh(frames: true); };
        _playTimer.Tick += (_, _) => { Frame = (Frame + 1) % Document.Frames.Count; ShowCanvas(); };

        var back = LunetUi.IconButton(LunetIcons.Back, "Salvar e voltar", () => Close());
        _title.Text = Path.GetFileName(path).Replace(".sprite.json", "", StringComparison.OrdinalIgnoreCase);
        var titles = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { _title, _info } };
        var play = LunetUi.IconButton(LunetIcons.Play, "Prévia da animação", TogglePlay);
        play.Name = "PlayAnimation";
        var bar = new DockPanel { Height = 56, Margin = new Thickness(LunetTokens.Space.Xxs, 0, LunetTokens.Space.Xs, 0) };
        DockPanel.SetDock(back, Dock.Left);
        DockPanel.SetDock(play, Dock.Right);
        bar.Children.Add(back);
        bar.Children.Add(play);
        bar.Children.Add(titles);

        var framesRow = new ScrollViewer { Content = _frames, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Hidden, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled, Margin = new Thickness(LunetTokens.Space.S, LunetTokens.Space.Xs) };
        var colorsRow = new ScrollViewer { Content = _colors, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Hidden, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled, Margin = new Thickness(LunetTokens.Space.S, LunetTokens.Space.Xs) };
        // Everything the thumb needs is at the bottom: frames, colours, then tools.
        var bottom = new StackPanel
        {
            Background = LunetTokens.Brush(LunetTokens.Color.Surface),
            Children = { framesRow, colorsRow, new Border { Child = _tools, Padding = new Thickness(0, 0, 0, LunetTokens.Space.Xs) } },
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
    public int Frame { get; private set; }
    public SpriteTool Tool { get; private set; } = SpriteTool.Pencil;
    public uint Color { get; private set; } = 0xFF000000;

    public void SelectTool(SpriteTool tool) { Tool = tool; Refresh(frames: false); }
    public void SelectColor(uint color) { Color = color; Tool = Tool is SpriteTool.Eraser or SpriteTool.Picker ? SpriteTool.Pencil : Tool; Refresh(frames: false); }

    /// <summary>One touch of a tool at a pixel (the canvas calls it; tests too). <paramref name="first"/> starts an undo step.</summary>
    public void Paint(int x, int y, bool first)
    {
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

    public void Undo() { if (_history.CanUndo) { Document = _history.Undo(Document); Frame = Math.Min(Frame, Document.Frames.Count - 1); _dirty = true; Refresh(frames: true); } }
    public void Redo() { if (_history.CanRedo) { Document = _history.Redo(Document); Frame = Math.Min(Frame, Document.Frames.Count - 1); _dirty = true; Refresh(frames: true); } }

    public void AddFrame(bool duplicate)
    {
        _history.Checkpoint(Document);
        Document.AddFrame(duplicate ? Frame : -1);
        Frame = Document.Frames.Count - 1;
        _dirty = true;
        Refresh(frames: true);
    }

    public void RemoveFrame()
    {
        if (Document.Frames.Count <= 1) return;
        _history.Checkpoint(Document);
        Document.RemoveFrame(Frame);
        Frame = Math.Min(Frame, Document.Frames.Count - 1);
        _dirty = true;
        Refresh(frames: true);
    }

    public void SelectFrame(int frame) { Frame = Math.Clamp(frame, 0, Document.Frames.Count - 1); Refresh(frames: true); }

    /// <summary>Writes the source and its sprite sheet together (or neither). No-op when nothing changed.</summary>
    public void Save()
    {
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

    private void ShowCanvas() => _canvas.Show(Document, Frame, onion: !_playing);

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
        _tools.Children.Add(new Border { Width = 1, Height = 28, Margin = new Thickness(6, 0), Background = LunetTokens.Brush(LunetTokens.Color.BorderStrong), VerticalAlignment = VerticalAlignment.Center });
        _tools.Children.Add(Action("↶", "Desfazer", Undo, _history.CanUndo));
        _tools.Children.Add(Action("↷", "Refazer", Redo, _history.CanRedo));

        _colors.Children.Clear();
        foreach (var c in Palette.Contains(Color) ? Palette : Palette.Prepend(Color))
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
