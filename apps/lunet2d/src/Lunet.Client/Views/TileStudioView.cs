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
using AutotileLayout = Lunet.Studio.AutotileLayout;
using AutotileSet = Lunet.Studio.AutotileSet;

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
        context.FillRectangle(LunetTokens.Brush(LunetTokens.Ink.I3), a);
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
                            if (collision) { context.FillRectangle(LunetTokens.Brush("#66FF6B7A"), dest); continue; }
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
    private readonly StackPanel _context = new() { Orientation = Orientation.Horizontal, Spacing = 4 };
    private readonly ScrollViewer _contextRow;
    private bool _terrainTool, _layerMenu, _confirmRemove;
    private string? _message;
    private readonly TextBlock _info = LunetUi.Text("", LunetTokens.Type.Caption, LunetTokens.Color.TextSecondary);
    private Bitmap? _tileset;
    private bool _dirty;
    private bool _erase;

    public TileStudioView(LunetShell shell, string path, Action close, StudioAssetWorkspace? storage = null)
    {
        _shell = shell;
        _path = path;
        _close = close;
        // A game's folder, or the standalone Studio folder (Home → Studios) when given.
        _storage = storage ?? new StudioAssetWorkspace(shell.Workspace.Project!.Files);
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
        _contextRow = Row(_context);
        var bottom = new StackPanel
        {
            Background = LunetTokens.Brush(LunetTokens.Color.Surface),
            Children = { Row(_layers), _contextRow, Row(_tiles), new Border { Child = _tools, Padding = new Thickness(0, 0, 0, LunetTokens.Space.Xs) } },
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

    /// <summary>Where "Tileset" goes outside a game (Home → Studios); null: the workspace's Sprite Studio.</summary>
    public Action<string>? EditTileset { get; set; }
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

    public void SelectTile(int tile)
    {
        Session.Tile = tile;
        _erase = false;
        // With the terrain tool the chosen tile is the terrain's first one.
        if (_terrainTool && Session.Terrain is { } terrain) _message = SetTerrain(terrain.Layout);
        Refresh();
    }

    public void SetErase(bool erase) { _erase = erase; _terrainTool = false; Session.Terrain = null; Refresh(); }

    // ---------- Terrain (autotile, LUNET-506) ----------

    public bool TerrainTool => _terrainTool;

    /// <summary>The terrain tool: painting picks the edge and corner tiles from the neighbours.</summary>
    public void UseTerrain()
    {
        _terrainTool = true;
        _erase = false;
        _message = Session.Terrain is null ? SetTerrain(AutotileLayout.Sides16) : null;
        Refresh();
    }

    /// <summary>The terrain starts at the chosen tile; null when the tileset has its 16 or 47 tiles, else the reason.</summary>
    public string? SetTerrain(AutotileLayout layout)
    {
        var set = new AutotileSet("terreno", Math.Max(1, Session.Tile), layout);
        if (set.BaseTile + set.Count - 1 > Session.MaxTileId)
        {
            Session.Terrain = null;
            return $"O terreno precisa de {set.Count} tiles a partir do tile {set.BaseTile}; o tileset tem {Session.MaxTileId}. Desenhe mais tiles no tileset.";
        }
        Session.Terrain = set;
        _message = null;
        Refresh();
        return null;
    }

    public void SetTerrainErase(bool erase) { Session.EraseTerrain = erase; Refresh(); }

    // ---------- Layers ----------

    public bool LayerMenuOpen => _layerMenu;
    public void ToggleLayerMenu() { _layerMenu = !_layerMenu; _confirmRemove = false; Refresh(); }
    private void LayerChange(Action change) { change(); _dirty = true; Refresh(); }
    public void RenameLayer(string name) { if (name.Trim().Length > 0) LayerChange(() => Session.RenameLayer(name.Trim())); }
    public void MoveLayer(int direction) => LayerChange(() => Session.MoveLayer(direction));

    /// <summary>Removes the current layer and its tiles (the map keeps at least one).</summary>
    public bool RemoveLayer()
    {
        if (Session.Map.LayerCount <= 1) { _message = "O mapa precisa de pelo menos uma camada."; Refresh(); return false; }
        _confirmRemove = false;
        LayerChange(Session.RemoveLayer);
        return true;
    }
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
    public static string Create(LunetShell shell, int cells = 16, int tile = 16) =>
        Create(new StudioAssetWorkspace(shell.Workspace.Project!.Files), shell.Workspace.Files(), cells, tile);

    /// <summary>The same, in any Studio folder (a game's, or the standalone Studios').</summary>
    public static string Create(StudioAssetWorkspace storage, IReadOnlyCollection<string> files, int cells = 16, int tile = 16)
    {
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
            var chip = LunetUi.Button(label, selected ? ButtonKind.Selected : ButtonKind.Secondary, null, () => { Session.SelectLayer(index); Refresh(); });
            chip.MinHeight = 40;
            chip.Name = "Layer-" + i;
            _layers.Children.Add(chip);
        }
        _layers.Children.Add(Small("+", "Nova camada", () => { Session.AddLayer("Camada " + (map.LayerCount + 1)); Session.SelectLayer(Session.Map.LayerCount - 1); _dirty = true; Refresh(); }));
        var more = Small("⋮", "Opções da camada", ToggleLayerMenu);
        more.Name = "LayerMenu";
        _layers.Children.Add(more);
        if (_message is not null) { _info.Text = _message; _info.Foreground = LunetTokens.Brush(LunetTokens.Color.Danger); }
        BuildContext();

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
        Tool(LunetIcons.Edit, "Pincel de tile", !_erase && !_terrainTool, () => SetErase(false), "Brush");
        Tool(LunetIcons.Map, "Terreno (autotile)", _terrainTool, UseTerrain, "Terrain");
        Tool(LunetIcons.Eraser, "Borracha", _erase, () => SetErase(true), "Erase");
        _tools.Children.Add(new Border { Width = 1, Height = 28, Margin = new Thickness(6, 0), Background = LunetTokens.Brush(LunetTokens.Color.BorderStrong), VerticalAlignment = VerticalAlignment.Center });
        _tools.Children.Add(Small("↶", "Desfazer", Undo));
        _tools.Children.Add(Small("↷", "Refazer", Redo));
        if (TilesetSource is { } tileset)
        {
            var edit = LunetUi.Button("Tileset", ButtonKind.Ghost, LunetIcons.Art, () =>
            {
                Save();
                if (EditTileset is { } edit) edit(tileset);
                else _shell.WorkspaceView.OpenSprite(tileset, reopen: () => _shell.WorkspaceView.OpenMap(_path));
            });
            edit.Name = "EditTileset";
            _tools.Children.Add(edit);
        }
    }

    /// <summary>The row above the tiles: the layer's options, or the terrain's, depending on what is open.</summary>
    private void BuildContext()
    {
        _context.Children.Clear();
        Button Chip(string label, Action act, string name, bool on = false)
        {
            var b = LunetUi.Button(label, on ? ButtonKind.Selected : ButtonKind.Secondary, null, act);
            b.MinHeight = 40;
            b.Name = name;
            _context.Children.Add(b);
            return b;
        }
        var map = Session.Map;
        if (_layerMenu)
        {
            var name = new TextBox { Text = map.GetLayerName(Session.Layer), Width = 140, MinHeight = 40, VerticalContentAlignment = VerticalAlignment.Center, Name = "LayerName" };
            AutomationProperties.SetName(name, "Nome da camada");
            name.KeyDown += (_, e) => { if (e.Key == Avalonia.Input.Key.Enter) { RenameLayer(name.Text ?? ""); e.Handled = true; } };
            name.LostFocus += (_, _) => { if ((name.Text ?? "").Trim() is { Length: > 0 } n && n != Session.Map.GetLayerName(Session.Layer)) RenameLayer(n); };
            _context.Children.Add(name);
            Chip("← Antes", () => MoveLayer(-1), "LayerUp");
            Chip("Depois →", () => MoveLayer(1), "LayerDown");
            Chip(map.IsLayerVisible(Session.Layer) ? "Esconder" : "Mostrar", () => LayerChange(Session.ToggleVisibility), "LayerVisible");
            Chip("Colisão", () => LayerChange(Session.ToggleCollision), "LayerCollision", map.IsCollisionLayer(Session.Layer));
            var remove = Chip(_confirmRemove ? "Apagar mesmo?" : "Apagar camada", () =>
            {
                if (!_confirmRemove) { _confirmRemove = true; Refresh(); }
                else RemoveLayer();
            }, "LayerRemove");
            if (_confirmRemove) remove.Foreground = LunetTokens.Brush(LunetTokens.Color.Danger);
        }
        else if (_terrainTool)
        {
            Chip("Simples · 16", () => _message = SetTerrain(AutotileLayout.Sides16), "TerrainSides", Session.Terrain?.Layout == AutotileLayout.Sides16);
            Chip("Suave · 47", () => _message = SetTerrain(AutotileLayout.Blob47), "TerrainBlob", Session.Terrain?.Layout == AutotileLayout.Blob47);
            Chip(Session.EraseTerrain ? "Apagando" : "Pintando", () => SetTerrainErase(!Session.EraseTerrain), "TerrainErase", Session.EraseTerrain);
            if (Session.Terrain is { } t)
            {
                var hint = LunetUi.Text($"a partir do tile {t.BaseTile}", LunetTokens.Type.Caption, LunetTokens.Color.TextSecondary);
                hint.VerticalAlignment = VerticalAlignment.Center;
                _context.Children.Add(hint);
            }
        }
        _contextRow.IsVisible = _context.Children.Count > 0;
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
