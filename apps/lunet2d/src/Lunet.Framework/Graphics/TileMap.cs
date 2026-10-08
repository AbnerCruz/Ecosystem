using System.Numerics;
using System.Text.Json;
using Lunet.Pathfinding;

namespace Lunet.Graphics;

/// <summary>Mapa de tiles em camadas, com células vazias (0), tiles 1-based e camada de colisão opcional.</summary>
/// <remarks>É conteúdo declarativo do jogo, sem estado de GPU, edição visual ou dependência do Tile Studio.
/// O mapa é imutável após Parse. Desenho em viewport e consultas não alocam por quadro.</remarks>
/// <example><code>
/// var map = content.LoadTileMap("Data/level.tilemap.json");
/// var tiles = content.LoadTexture(map.TexturePath, TextureFilter.Point);
/// var grid = new Lunet.Pathfinding.GridPathfinder(map.Width, map.Height);
/// map.CopyCollisionTo(grid);
/// batch.Begin();
/// map.Draw(batch, tiles, new RectangleF(0, 0, 360, 640), Vector2.Zero, Color.White);
/// batch.End();
/// </code></example>
public sealed class TileMap
{
    private const int MaxCells = 1_048_576;
    private const int MaxLayers = 32;
    private readonly TileLayer[] _layers;
    private readonly int _maximumTile;

    private TileMap(string texturePath, int width, int height, int tileWidth, int tileHeight, TileLayer[] layers, int maximumTile)
    {
        TexturePath = texturePath;
        Width = width;
        Height = height;
        TileWidth = tileWidth;
        TileHeight = tileHeight;
        _layers = layers;
        _maximumTile = maximumTile;
    }

    /// <summary>Caminho relativo dentro de Content para a imagem tileset.</summary>
    public string TexturePath { get; }
    /// <summary>Número de colunas no mapa.</summary>
    public int Width { get; }
    /// <summary>Número de linhas no mapa.</summary>
    public int Height { get; }
    /// <summary>Largura de cada tile no mundo e na imagem, em pixels.</summary>
    public int TileWidth { get; }
    /// <summary>Altura de cada tile no mundo e na imagem, em pixels.</summary>
    public int TileHeight { get; }
    /// <summary>Quantidade de camadas na ordem de desenho.</summary>
    public int LayerCount => _layers.Length;

    /// <summary>Nome estável da camada por índice.</summary>
    /// <param name="layer">Índice de 0 a LayerCount-1.</param>
    /// <returns>Nome do layer no JSON.</returns>
    public string GetLayerName(int layer) => _layers[layer].Name;

    /// <summary>Indica se a camada é desenhada (a colisão não depende de visibilidade).</summary>
    /// <param name="layer">Índice da camada.</param>
    /// <returns>Verdadeiro se desenhável.</returns>
    public bool IsLayerVisible(int layer) => _layers[layer].Visible;

    /// <summary>Indica se qualquer tile não vazio da camada bloqueia passagem.</summary>
    /// <param name="layer">Índice da camada.</param>
    /// <returns>Verdadeiro se participa da colisão.</returns>
    public bool IsCollisionLayer(int layer) => _layers[layer].Collision;

    /// <summary>Identificador do tile (0 vazio, demais começando em 1). Fora da grade lança exceção.</summary>
    /// <param name="layer">Índice da camada.</param>
    /// <param name="cell">Coordenada da célula.</param>
    /// <returns>ID do tile.</returns>
    public int GetTile(int layer, GridPoint cell) => _layers[layer].Tiles[Index(cell)];

    /// <summary>Fora do mapa também é bloqueado. Dentro, qualquer tile em camada collision bloqueia.</summary>
    /// <param name="cell">Célula da grade.</param>
    /// <returns>Verdadeiro quando a célula bloqueia o caminho.</returns>
    public bool IsBlocked(GridPoint cell)
    {
        if ((uint)cell.X >= (uint)Width || (uint)cell.Y >= (uint)Height) return true;
        int index = cell.Y * Width + cell.X;
        foreach (var layer in _layers)
            if (layer.Collision && layer.Tiles[index] != 0) return true;
        return false;
    }

    /// <summary>Substitui os custos da grade A* (bloqueado = 0; demais freeCost), sem alocar.</summary>
    /// <param name="grid">Grade com as mesmas dimensões do mapa.</param>
    /// <param name="freeCost">Custo finito e positivo das células livres.</param>
    public void CopyCollisionTo(GridPathfinder grid, float freeCost = 1f)
    {
        ArgumentNullException.ThrowIfNull(grid);
        if (grid.Width != Width || grid.Height != Height) throw new ArgumentException("Dimensões do grid A* não conferem.", nameof(grid));
        if (!float.IsFinite(freeCost) || freeCost <= 0) throw new ArgumentOutOfRangeException(nameof(freeCost));
        for (int y = 0; y < Height; y++)
        for (int x = 0; x < Width; x++)
        {
            var cell = new GridPoint(x, y);
            grid.SetCost(cell, IsBlocked(cell) ? 0 : freeCost);
        }
    }

    /// <summary>Transforma posição do mundo em célula sem limitar à grade; aplica floor para coordenadas negativas.</summary>
    /// <param name="world">Posição em pixels do mundo.</param>
    /// <param name="origin">Posição do canto superior esquerdo do mapa.</param>
    /// <returns>Coordenadas inteiras da célula, possivelmente fora da grade.</returns>
    public GridPoint WorldToCell(Vector2 world, Vector2 origin)
    {
        if (!Valid(world) || !Valid(origin)) throw new ArgumentOutOfRangeException(nameof(world));
        double x = Math.Floor(((double)world.X - origin.X) / TileWidth);
        double y = Math.Floor(((double)world.Y - origin.Y) / TileHeight);
        if (x < int.MinValue || x > int.MaxValue || y < int.MinValue || y > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(world));
        return new GridPoint((int)x, (int)y);
    }

    /// <summary>Retângulo de uma célula em coordenadas de mundo.</summary>
    /// <param name="cell">Coordenada válida da célula.</param>
    /// <param name="origin">Deslocamento do mapa no mundo.</param>
    /// <returns>Limites da célula.</returns>
    public RectangleF CellBounds(GridPoint cell, Vector2 origin)
    {
        Index(cell);
        if (!Valid(origin)) throw new ArgumentOutOfRangeException(nameof(origin));
        return new RectangleF(origin.X + cell.X * TileWidth, origin.Y + cell.Y * TileHeight, TileWidth, TileHeight);
    }

    /// <summary>Desenha apenas tiles dentro da viewport mundial, com camadas em ordem e um único tileset.</summary>
    /// <param name="batch">SpriteBatch já iniciado (opcionalmente com Camera2D).</param>
    /// <param name="texture">Imagem tileset apontada por TexturePath, em grade regular.</param>
    /// <param name="viewport">AABB da área visível em coordenadas mundiais; para câmera girada, use AABB conservador.</param>
    /// <param name="origin">Deslocamento em pixels do canto superior esquerdo do mapa.</param>
    /// <param name="tint">Cor que multiplica os pixels desenhados.</param>
    public void Draw(SpriteBatch batch, Texture2D texture, RectangleF viewport, Vector2 origin, Color tint)
    {
        ArgumentNullException.ThrowIfNull(batch);
        ArgumentNullException.ThrowIfNull(texture);
        if (texture.IsDisposed) throw new ObjectDisposedException(nameof(Texture2D));
        if (texture.Width % TileWidth != 0 || texture.Height % TileHeight != 0 ||
            (long)(texture.Width / TileWidth) * (texture.Height / TileHeight) < _maximumTile)
            throw new ArgumentException("Tileset não contém todos os IDs ou não é múltiplo do tile.", nameof(texture));
        if (!Valid(origin) || !float.IsFinite(viewport.X) || !float.IsFinite(viewport.Y) ||
            !float.IsFinite(viewport.Width) || !float.IsFinite(viewport.Height) ||
            viewport.Width < 0 || viewport.Height < 0)
            throw new ArgumentOutOfRangeException(nameof(viewport));
        if (viewport.Width == 0 || viewport.Height == 0) return;
        int columns = texture.Width / TileWidth;
        int left = Bound(Math.Floor(((double)viewport.X - origin.X) / TileWidth), Width);
        int top = Bound(Math.Floor(((double)viewport.Y - origin.Y) / TileHeight), Height);
        int right = Bound(Math.Ceiling(((double)viewport.X + viewport.Width - origin.X) / TileWidth), Width);
        int bottom = Bound(Math.Ceiling(((double)viewport.Y + viewport.Height - origin.Y) / TileHeight), Height);
        for (int l = 0; l < _layers.Length; l++)
        {
            var layer = _layers[l];
            if (!layer.Visible) continue;
            for (int y = top; y < bottom; y++)
            for (int x = left; x < right; x++)
            {
                int id = layer.Tiles[y * Width + x];
                if (id == 0) continue;
                int index = id - 1;
                var src = new RectangleF((index % columns) * TileWidth, (index / columns) * TileHeight, TileWidth, TileHeight);
                var dest = new RectangleF(origin.X + x * TileWidth, origin.Y + y * TileHeight, TileWidth, TileHeight);
                batch.Draw(texture, dest, src, tint, 0, Vector2.Zero);
            }
        }
    }

    /// <summary>Lê mapa JSON v1. Cada camada usa array row-major de Width × Height; zero significa vazio.</summary>
    /// <param name="json">Fonte JSON UTF-8 legível e versionada.</param>
    /// <returns>Mapa sem dependências de GPU.</returns>
    public static TileMap Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        MapFile? source;
        try { source = JsonSerializer.Deserialize<MapFile>(json, JsonOptions); }
        catch (JsonException ex) { throw new InvalidDataException("Tilemap JSON inválido: " + ex.Message, ex); }
        if (source is null || source.Version != 1) throw new InvalidDataException("Tilemap requer version 1.");
        if (!ValidPath(source.Texture)) throw new InvalidDataException("Caminho de tileset inválido.");
        if (source.Width <= 0 || source.Height <= 0 || (long)source.Width * source.Height > MaxCells)
            throw new InvalidDataException("Dimensões do tilemap excedem os limites.");
        if (source.TileWidth is < 1 or > 4096 || source.TileHeight is < 1 or > 4096)
            throw new InvalidDataException("Dimensões de tile inválidas.");
        if (source.Layers is null || source.Layers.Length == 0 || source.Layers.Length > MaxLayers)
            throw new InvalidDataException("Quantidade de camadas inválida.");

        int count = source.Width * source.Height;
        int maximum = 0;
        var names = new HashSet<string>(StringComparer.Ordinal);
        var layers = new TileLayer[source.Layers.Length];
        for (int i = 0; i < layers.Length; i++)
        {
            var item = source.Layers[i];
            if (item is null || string.IsNullOrWhiteSpace(item.Name) || !names.Add(item.Name))
                throw new InvalidDataException("Camada com nome vazio ou duplicado.");
            if (item.Tiles is null || item.Tiles.Length != count)
                throw new InvalidDataException("Camada " + item.Name + " não tem Width × Height tiles.");
            foreach (int tile in item.Tiles)
            {
                if (tile < 0) throw new InvalidDataException("ID de tile negativo na camada " + item.Name);
                if (tile > maximum) maximum = tile;
            }
            layers[i] = new TileLayer(item.Name, item.Visible, item.Collision, item.Tiles);
        }
        return new TileMap(source.Texture!, source.Width, source.Height, source.TileWidth, source.TileHeight, layers, maximum);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true, AllowTrailingCommas = true, ReadCommentHandling = JsonCommentHandling.Skip
    };

    private int Index(GridPoint cell)
    {
        if ((uint)cell.X >= (uint)Width || (uint)cell.Y >= (uint)Height)
            throw new ArgumentOutOfRangeException(nameof(cell));
        return cell.Y * Width + cell.X;
    }

    private static int Bound(double value, int limit) => (int)Math.Clamp(value, 0d, limit);
    private static bool Valid(Vector2 value) => float.IsFinite(value.X) && float.IsFinite(value.Y);
    private static bool ValidPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Contains('\\') || path.Contains(':') || path.StartsWith('/'))
            return false;
        foreach (var segment in path.Split('/'))
            if (segment.Length == 0 || segment is "." or "..") return false;
        return true;
    }

    private sealed record TileLayer(string Name, bool Visible, bool Collision, int[] Tiles);
    private sealed class MapFile
    {
        public int Version { get; set; }
        public string? Texture { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int TileWidth { get; set; }
        public int TileHeight { get; set; }
        public LayerFile?[]? Layers { get; set; }
    }
    private sealed class LayerFile
    {
        public string? Name { get; set; }
        public bool Visible { get; set; } = true;
        public bool Collision { get; set; }
        public int[]? Tiles { get; set; }
    }
}
