using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Lunet.Client.Shell;
using Lunet.Client.Ui;
using Lunet.Core;
using TileMap = Lunet.Graphics.TileMap;
using GridPoint = Lunet.Pathfinding.GridPoint;
using TileStudioSession = Lunet.Studio.TileStudioSession;
using SpriteDocument = Lunet.Studio.SpriteDocument;

namespace Lunet.Client.Views;

/// <summary>The map canvas: every visible layer drawn from the tileset, the grid, collision cells tinted.</summary>
public sealed class TileCanvas : ZoomCanvas
{
    private TileMap? _map;
    private Bitmap? _tileset;
    private int _layer;

    public TileCanvas() => AutomationProperties.SetName(this, "Mapa");

    protected override (double Width, double Height) ContentSize => _map is null ? (0, 0) : (_map.Width * _map.TileWidth, _map.Height * _map.TileHeight);
    protected override (int X, int Y) Cells => _map is null ? (0, 0) : (_map.Width, _map.Height);

    public void Show(TileMap map, Bitmap? tileset, int layer)
    {
        _map = map;
        _tileset = tileset;
        _layer = layer;
        InvalidateVisual();
    }

    protected override void RenderContent(DrawingContext context, Rect a)
    {
        if (_map is not { } map || a.Width <= 0) return;
        context.FillRectangle(LunetTokens.Brush("#1b1d22"), a);
        var cw = a.Width / map.Width; var ch = a.Height / map.Height;
        var columns = _tileset is null ? 1 : Math.Max(1, (int)(_tileset.PixelSize.Width / map.TileWidth));
        using (context.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = BitmapInterpolationMode.None }))
        {
            for (var layer = 0; layer < map.LayerCount; layer++)
            {
                if (!map.IsLayerVisible(layer)) continue;
                var collision = map.IsCollisionLayer(layer);
                using (context.PushOpacity(layer == _layer ? 1 : 0.55))
                    for (var y = 0; y < map.Height; y++)
                        for (var x = 0; x < map.Width; x++)
                        {
                            var tile = map.GetTile(layer, new GridPoint(x, y));
                            if (tile <= 0) continue;
                            var dest = new Rect(a.X + x * cw, a.Y + y * ch, cw, ch);
                            if (collision) { context.FillRectangle(LunetTokens.Brush("#66ff7b86"), dest); continue; }
                            if (_tileset is null) { context.FillRectangle(LunetTokens.Brush(LunetTokens.Color.Primary), dest); continue; }
                            var i = tile - 1;
                            var src = new Rect(i % columns * map.TileWidth, i / columns * map.TileHeight, map.TileWidth, map.TileHeight);
                            context.DrawImage(_tileset, src, dest);
                        }
            }
        }
        DrawGrid(context, a, minCell: 10);
        context.DrawRectangle(new Pen(LunetTokens.Brush(LunetTokens.Color.BorderStrong), 1), a);
    }
}

/// <summary>
/// The Tile Studio in the shell (A2): paint a map's layers with its tileset on the phone. Tiles, layers and tools at
/// the bottom; pinch and pan from the common canvas; undo per stroke (TileStudioSession); Voltar saves the
/// <c>.tilemap.json</c>. "Editar tileset" opens its source in the Sprite Studio and comes back here.
/// </summary>
public sealed class TileStudioView : UserControl
{
    private readonly LunetShell _shell;
    private readonly string _path;
    private readonly Action _close;
    private readonly StudioAssetWorkspace _storage;
    private readonly TileCanvas _canvas = new();
    private readonly StackPanel _tiles = new() { Orientation = Orientation.Horizontal, Spacing = 6 };
    private readonly StackPanel _layers = new() { Orientation = Orientation.Horizontal, Spacing = 6 };
    private readonly StackPanel _tools = new() { Orientation = Orientation.Horizontal, Spacing = 2, HorizontalAlignment = HorizontalAlignment.Center };
    private readonly TextBlock _info = LunetUi.Text("", LunetTokens.Type.Caption, LunetTokens.Color.TextSecondary);
    private Bitmap? _tileset;
    private bool _dirty;
    private bool _erase;

    public TileStudioView(LunetShell shell, string path, Action close)
    {
        _shell = shell;
        _path = path;
        _close = close;
        _storage = new StudioAssetWorkspace(shell.Workspace.Project!.Directory);
        var map = TileMap.Parse(_storage.ReadText(path));
        _tileset = LoadTileset(map);
        var tiles = _tileset is null ? 1 : Math.Max(1, (int)(_tileset.PixelSize.Width / map.TileWidth) * (int)(_tileset.PixelSize.Height / map.TileHeight));
        Session = new TileStudioSession(map, tiles);
        Background = LunetTokens.Brush(LunetTokens.Color.Background);
        _canvas.Paint += (x, y, first) => Paint(x, y, first);
        _canvas.StrokeEnded += () => { if (Session.EndStroke()) _dirty = true; Refresh(); };
        _canvas.StrokeCancelled += () => { if (Session.EndStroke()) Session.Undo(); Refresh(); };

        var back = LunetUi.IconButton(LunetIcons.Back, "Salvar e voltar", () => Close());
        var title = LunetUi.Title(Path.GetFileName(path).Replace(".tilemap.json", "", StringComparison.OrdinalIgnoreCase));
        var titles = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { title, _info } };
        var fit = LunetUi.IconButton(LunetIcons.Fit, "Ver o mapa inteiro (dois dedos aproximam)", () => _canvas.ResetView());
        var bar = new DockPanel { Height = 56, Margin = new Thickness(LunetTokens.Space.Xxs, 0, LunetTokens.Space.Xs, 0) };
        DockPanel.SetDock(back, Dock.Left);
        DockPanel.SetDock(fit, Dock.Right);
        bar.Children.Add(back);
        bar.Children.Add(fit);
        bar.Children.Add(titles);

        ScrollViewer Row(Control content) => new()
        {
            Content = content, Margin = new Thickness(LunetTokens.Space.S, LunetTokens.Space.Xs),
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Hidden,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
        };
        var bottom = new StackPanel
        {
            Background = LunetTokens.Brush(LunetTokens.Color.Surface),
            Children = { Row(_layers), Row(_tiles), new Border { Child = _tools, Padding = new Thickness(0, 0, 0, LunetTokens.Space.Xs) } },
        };
        var root = new DockPanel();
        DockPanel.SetDock(bar, Dock.Top);
        DockPanel.SetDock(bottom, Dock.Bottom);
        root.Children.Add(bar);
        root.Children.Add(bottom);
        root.Children.Add(new Border { Child = _canvas, Margin = new Thickness(LunetTokens.Space.S) });
        Content = root;
        Refresh();
    }

    public TileStudioSession Session { get; }
    public TileCanvas Canvas => _canvas;
    public bool Erasing => _erase;

    /// <summary>The source of the tileset (the editable sprite next to its PNG), when the map has one.</summary>
    public string? TilesetSource
    {
        get
        {
            var png = "Content/" + Session.Map.TexturePath;
            var source = png.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? png[..^4] + ".sprite.json" : null;
            return source is not null && File.Exists(Path.Combine(_storage.RootDirectory, source)) ? source : null;
        }
    }

    private Bitmap? LoadTileset(TileMap map)
    {
        try
        {
            var file = Path.Combine(_storage.RootDirectory, "Content", map.TexturePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(file)) return null;
            using var stream = File.OpenRead(file);
            return new Bitmap(stream);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException) { return null; }
    }

    /// <summary>Paints (or erases) one cell of the current layer; <paramref name="first"/> starts the undo step.</summary>
    public void Paint(int x, int y, bool first)
    {
        if (first) Session.BeginStroke();
        var tile = Session.Tile;
        if (_erase) Session.Tile = 0;
        Session.Paint(x, y);
        Session.Tile = tile;
        _canvas.Show(Session.Map, _tileset, Session.Layer);
    }

    public void SelectTile(int tile) { Session.Tile = tile; _erase = false; Refresh(); }
    public void SetErase(bool erase) { _erase = erase; Refresh(); }
    public void Undo() { if (Session.Undo()) { _dirty = true; Refresh(); } }
    public void Redo() { if (Session.Redo()) { _dirty = true; Refresh(); } }

    public void Save()
    {
        if (!_dirty) return;
        _storage.WriteText(_path, Session.Map.ToJson());
        _dirty = false;
    }

    /// <summary>Saves and leaves; a failed save keeps you here with the reason.</summary>
    public bool Close()
    {
        try { Save(); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException)
        {
            _info.Text = "Não foi possível salvar: " + e.Message;
            _info.Foreground = LunetTokens.Brush(LunetTokens.Color.Danger);
            return true;
        }
        _close();
        return true;
    }

    /// <summary>Creates a map with an editable tileset of four starting tiles (as the previous Studio does).</summary>
    public static string Create(LunetShell shell, int cells = 16, int tile = 16)
    {
        var storage = new StudioAssetWorkspace(shell.Workspace.Project!.Directory);
        var files = shell.Workspace.Files();
        var n = 1;
        while (files.Contains($"Content/Data/mapa-{n}.tilemap.json")) n++;
        var name = $"tiles-mapa-{n}";
        var source = $"Content/Textures/{name}.sprite.json";
        var png = $"Content/Textures/{name}.png";
        var mapPath = $"Content/Data/mapa-{n}.tilemap.json";
        var atlasSize = Math.Min(128, tile * 4);
        var atlas = SpriteDocument.Create(atlasSize);
        var columns = atlasSize / tile;
        uint[] colors = [0xff438956, 0xffb68b53, 0xff7b8796, 0xff3c8fbd];
        for (var i = 0; i < colors.Length; i++)
        {
            int x = i % columns * tile, y = i / columns * tile;
            atlas.Rectangle(0, x, y, x + tile - 1, y + tile - 1, colors[i], filled: true);
        }
        var map = TileMap.Create($"Textures/{name}.png", cells, cells, tile, tile);
        storage.WriteBundle((source, System.Text.Encoding.UTF8.GetBytes(atlas.ToJson())),
            (png, PngEncoder.Rgba(atlas.Width, atlas.Height, atlas.SheetRgba())),
            (mapPath, System.Text.Encoding.UTF8.GetBytes(map.ToJson())));
        return mapPath;
    }

    private void Refresh()
    {
        _canvas.Show(Session.Map, _tileset, Session.Layer);
        var map = Session.Map;
        _info.Text = $"{map.Width}×{map.Height} · tiles de {map.TileWidth} px · camada {Session.Layer + 1} de {map.LayerCount}";
        _info.Foreground = LunetTokens.Brush(LunetTokens.Color.TextSecondary);

        _layers.Children.Clear();
        for (var i = 0; i < map.LayerCount; i++)
        {
            var index = i;
            var selected = i == Session.Layer;
            var label = (map.IsLayerVisible(i) ? "" : "◌ ") + map.GetLayerName(i) + (map.IsCollisionLayer(i) ? " · colisão" : "");
            var chip = LunetUi.Button(label, selected ? ButtonKind.Primary : ButtonKind.Secondary, null, () => { Session.SelectLayer(index); Refresh(); });
            chip.MinHeight = 40;
            chip.Name = "Layer-" + i;
            _layers.Children.Add(chip);
        }
        _layers.Children.Add(Small("+", "Nova camada", () => { Session.AddLayer("Camada " + (map.LayerCount + 1)); Session.SelectLayer(Session.Map.LayerCount - 1); _dirty = true; Refresh(); }));
        _layers.Children.Add(Small(map.IsLayerVisible(Session.Layer) ? "👁" : "◌", "Mostrar ou esconder a camada", () => { Session.ToggleVisibility(); _dirty = true; Refresh(); }));
        _layers.Children.Add(Small("▦", "Camada de colisão (liga/desliga)", () => { Session.ToggleCollision(); _dirty = true; Refresh(); }));

        _tiles.Children.Clear();
        var columns = _tileset is null ? 1 : Math.Max(1, (int)(_tileset.PixelSize.Width / map.TileWidth));
        for (var t = 1; t <= Session.MaxTileId; t++)
        {
            var tile = t;
            Control face;
            if (_tileset is not null)
            {
                var i = t - 1;
                var crop = new CroppedBitmap(_tileset, new PixelRect(i % columns * map.TileWidth, i / columns * map.TileHeight, map.TileWidth, map.TileHeight));
                var img = new Image { Source = crop, Width = 36, Height = 36, Stretch = Stretch.Uniform };
                RenderOptions.SetBitmapInterpolationMode(img, BitmapInterpolationMode.None);
                face = img;
            }
            else face = LunetUi.Text(t.ToString(), LunetTokens.Type.Label);
            var selected = !_erase && Session.Tile == t;
            var b = new Button
            {
                Content = face, Width = 46, Height = 46, Padding = new Thickness(3), CornerRadius = new CornerRadius(LunetTokens.Radius.S),
                Background = LunetTokens.Brush(LunetTokens.Color.SurfaceRaised),
                BorderBrush = LunetTokens.Brush(selected ? LunetTokens.Color.Primary : LunetTokens.Color.Border), BorderThickness = new Thickness(selected ? 2 : 1),
                Name = "Tile-" + t,
            };
            AutomationProperties.SetName(b, "Tile " + t + (selected ? ", escolhido" : ""));
            b.Click += (_, _) => SelectTile(tile);
            _tiles.Children.Add(b);
        }

        _tools.Children.Clear();
        void Tool(string icon, string label, bool selected, Action act, string name)
        {
            var b = new Button
            {
                Width = 52, Height = 52, Padding = new Thickness(0), CornerRadius = new CornerRadius(LunetTokens.Radius.M),
                Background = LunetTokens.Brush(selected ? LunetTokens.Color.PrimarySoft : "#00000000"), BorderThickness = new Thickness(0),
                Content = LunetIcons.Create(icon, selected ? LunetTokens.Color.Primary : LunetTokens.Color.TextPrimary, 22),
                HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center, Name = name,
            };
            AutomationProperties.SetName(b, label);
            ToolTip.SetTip(b, label);
            b.Click += (_, _) => act();
            _tools.Children.Add(b);
        }
        Tool(LunetIcons.Edit, "Pincel de tile", !_erase, () => SetErase(false), "Brush");
        Tool(LunetIcons.Eraser, "Borracha", _erase, () => SetErase(true), "Erase");
        _tools.Children.Add(new Border { Width = 1, Height = 28, Margin = new Thickness(6, 0), Background = LunetTokens.Brush(LunetTokens.Color.BorderStrong), VerticalAlignment = VerticalAlignment.Center });
        _tools.Children.Add(Small("↶", "Desfazer", Undo));
        _tools.Children.Add(Small("↷", "Refazer", Redo));
        if (TilesetSource is { } tileset)
        {
            var edit = LunetUi.Button("Tileset", ButtonKind.Ghost, LunetIcons.Art, () =>
            {
                Save();
                _shell.WorkspaceView.OpenSprite(tileset, reopen: () => _shell.WorkspaceView.OpenMap(_path));
            });
            edit.Name = "EditTileset";
            _tools.Children.Add(edit);
        }
    }

    private static Button Small(string face, string label, Action act)
    {
        var b = new Button
        {
            Width = 48, Height = 48, Padding = new Thickness(0), CornerRadius = new CornerRadius(LunetTokens.Radius.M),
            Background = LunetTokens.Brush("#00000000"), BorderThickness = new Thickness(0),
            Content = new TextBlock { Text = face, FontSize = 18, Foreground = LunetTokens.Brush(LunetTokens.Color.TextPrimary), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
            HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center,
        };
        AutomationProperties.SetName(b, label);
        ToolTip.SetTip(b, label);
        b.Click += (_, _) => act();
        return b;
    }
}
