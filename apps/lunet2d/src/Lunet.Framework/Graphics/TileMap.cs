using System.Numerics;
using System.Text.Json;
using System.Text;
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

    /// <summary>Consulta colisão de AABB em pixels de mundo; paredes invisíveis também contam e fora do mapa é sólido.</summary>
    /// <param name="bounds">Retângulo mundial finito de largura/altura não negativas (contato de borda não conta).</param>
    /// <param name="origin">Deslocamento do canto superior esquerdo do mapa no mundo.</param>
    /// <returns>Verdadeiro se a área não vazia invade célula bloqueada ou ultrapassa os limites do mapa.</returns>
    /// <remarks>Para mover um personagem, consulte o retângulo desejado antes de atualizar a posição.
    /// Não resolve colisão, aceleração ou movimento contínuo; chamadas não alocam memória por quadro.</remarks>
    public bool OverlapsCollision(RectangleF bounds, Vector2 origin)
    {
        if (!Valid(origin) || !float.IsFinite(bounds.X) || !float.IsFinite(bounds.Y)
            || !float.IsFinite(bounds.Width) || !float.IsFinite(bounds.Height)
            || bounds.Width < 0 || bounds.Height < 0)
            throw new ArgumentOutOfRangeException(nameof(bounds));
        if (bounds.Width == 0 || bounds.Height == 0) return false;

        // Double preserva limites quando as coordenadas float são muito grandes.
        double minX = ((double)bounds.X - origin.X) / TileWidth;
        double minY = ((double)bounds.Y - origin.Y) / TileHeight;
        double maxX = ((double)bounds.X + bounds.Width - origin.X) / TileWidth;
        double maxY = ((double)bounds.Y + bounds.Height - origin.Y) / TileHeight;
        if (minX < 0 || minY < 0 || maxX > Width || maxY > Height) return true;

        int left = (int)Math.Floor(minX), top = (int)Math.Floor(minY);
        int right = (int)Math.Ceiling(maxX), bottom = (int)Math.Ceiling(maxY);
        foreach (var layer in _layers)
        {
            if (!layer.Collision) continue;
            for (int y = top; y < bottom; y++)
            for (int x = left; x < right; x++)
                if (layer.Tiles[y * Width + x] != 0) return true;
        }
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

    /// <summary>Cria mapa vazio compatível com JSON v1, com uma camada "ground".</summary>
    /// <param name="texturePath">Caminho seguro do tileset relativo a Content.</param>
    /// <param name="width">Largura em células.</param>
    /// <param name="height">Altura em células.</param>
    /// <param name="tileWidth">Largura de cada tile em pixels.</param>
    /// <param name="tileHeight">Altura de cada tile em pixels.</param>
    /// <returns>Novo mapa vazio, pronto para exportação ou edição.</returns>
    public static TileMap Create(string texturePath, int width, int height, int tileWidth, int tileHeight)
    {
        if (!ValidPath(texturePath)) throw new ArgumentException("Caminho de tileset inválido.", nameof(texturePath));
        if (width <= 0 || height <= 0 || (long)width * height > MaxCells)
            throw new ArgumentOutOfRangeException(nameof(width), "Dimensões do mapa excedem o limite.");
        if (tileWidth is < 1 or > 4096 || tileHeight is < 1 or > 4096)
            throw new ArgumentOutOfRangeException(nameof(tileWidth), "Tamanho de tile fora dos limites.");
        return new TileMap(texturePath, width, height, tileWidth, tileHeight,
            [new TileLayer("ground", true, false, new int[width * height])], 0);
    }

    /// <summary>Devolve uma nova versão do mapa com uma célula alterada, sem modificar a anterior.</summary>
    /// <param name="layer">Índice da camada de destino.</param>
    /// <param name="cell">Célula dentro dos limites do mapa.</param>
    /// <param name="tileId">ID do tile: 0 vazio, 1 ou superior presente no tileset.</param>
    /// <returns>Nova versão imutável do mapa, ou esta instância quando nada mudou.</returns>
    /// <remarks>É uma operação de autoria que copia a camada editada e pode alocar; nunca chame a cada frame.</remarks>
    public TileMap WithTile(int layer, GridPoint cell, int tileId)
    {
        CheckLayer(layer);
        int index = Index(cell);
        if (tileId < 0) throw new ArgumentOutOfRangeException(nameof(tileId));
        int previous = _layers[layer].Tiles[index];
        if (previous == tileId) return this;
        var layers = (TileLayer[])_layers.Clone();
        var tiles = (int[])layers[layer].Tiles.Clone();
        tiles[index] = tileId;
        layers[layer] = layers[layer] with { Tiles = tiles };
        // Pincel do editor não deve varrer todas as camadas a cada célula alterada.
        // Só é necessário recalcular se o maior ID conhecido foi reduzido.
        return previous == _maximumTile && tileId < previous
            ? CopyWith(layers)
            : new TileMap(TexturePath, Width, Height, TileWidth, TileHeight, layers, Math.Max(_maximumTile, tileId));
    }

    /// <summary>Devolve novo mapa com visibilidade e colisão atualizadas para uma camada.</summary>
    /// <param name="layer">Índice da camada.</param>
    /// <param name="visible">Visibilidade do desenho; independente da colisão.</param>
    /// <param name="collision">Células não vazias desta camada impedem passagem.</param>
    /// <returns>Nova versão imutável do mapa.</returns>
    public TileMap WithLayerSettings(int layer, bool visible, bool collision)
    {
        CheckLayer(layer);
        var old = _layers[layer];
        if (old.Visible == visible && old.Collision == collision) return this;
        var layers = (TileLayer[])_layers.Clone();
        layers[layer] = old with { Visible = visible, Collision = collision };
        return new TileMap(TexturePath, Width, Height, TileWidth, TileHeight, layers, _maximumTile);
    }

    /// <summary>Renomeia uma camada sem perder células, ordem, visibilidade ou colisão.</summary>
    /// <param name="layer">Índice da camada.</param>
    /// <param name="name">Novo nome não vazio e único no mapa.</param>
    /// <returns>Mapa com o nome alterado.</returns>
    public TileMap RenameLayer(int layer, string name)
    {
        CheckLayer(layer);
        CheckName(name, layer);
        if (_layers[layer].Name == name) return this;
        var layers = (TileLayer[])_layers.Clone();
        layers[layer] = layers[layer] with { Name = name };
        return new TileMap(TexturePath, Width, Height, TileWidth, TileHeight, layers, _maximumTile);
    }

    /// <summary>Acrescenta camada vazia no topo (até 32), sem alterar as camadas existentes.</summary>
    /// <param name="name">Nome exclusivo da nova camada.</param>
    /// <param name="visible">Se os tiles devem aparecer no desenho.</param>
    /// <param name="collision">Se tiles não vazios bloqueiam passagem.</param>
    /// <returns>Mapa com a camada acrescentada.</returns>
    public TileMap AddLayer(string name, bool visible = true, bool collision = false)
    {
        if (_layers.Length >= MaxLayers) throw new InvalidOperationException("Limite de 32 camadas.");
        CheckName(name);
        var layers = new TileLayer[_layers.Length + 1];
        Array.Copy(_layers, layers, _layers.Length);
        layers[^1] = new TileLayer(name, visible, collision, new int[Width * Height]);
        return new TileMap(TexturePath, Width, Height, TileWidth, TileHeight, layers, _maximumTile);
    }

    /// <summary>Remove uma camada; o mapa sempre preserva ao menos uma camada.</summary>
    /// <param name="layer">Índice da camada a remover.</param>
    /// <returns>Mapa sem a camada selecionada.</returns>
    public TileMap RemoveLayer(int layer)
    {
        CheckLayer(layer);
        if (_layers.Length == 1) throw new InvalidOperationException("Um tilemap precisa de uma camada.");
        var layers = new TileLayer[_layers.Length - 1];
        if (layer > 0) Array.Copy(_layers, 0, layers, 0, layer);
        if (layer < layers.Length) Array.Copy(_layers, layer + 1, layers, layer, layers.Length - layer);
        return CopyWith(layers);
    }

    /// <summary>Move a camada da posição atual para outra posição na ordem de desenho.</summary>
    /// <param name="from">Índice atual.</param>
    /// <param name="to">Novo índice.</param>
    /// <returns>Mapa com a ordem de desenho atualizada.</returns>
    public TileMap MoveLayer(int from, int to)
    {
        CheckLayer(from);
        CheckLayer(to);
        if (from == to) return this;
        var layers = (TileLayer[])_layers.Clone();
        var moved = layers[from];
        if (from < to) Array.Copy(layers, from + 1, layers, from, to - from);
        else Array.Copy(layers, to, layers, to + 1, from - to);
        layers[to] = moved;
        return new TileMap(TexturePath, Width, Height, TileWidth, TileHeight, layers, _maximumTile);
    }

    private void CheckLayer(int layer)
    {
        if ((uint)layer >= (uint)_layers.Length) throw new ArgumentOutOfRangeException(nameof(layer));
    }

    private void CheckName(string? name, int exclude = -1)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Nome de camada vazio.", nameof(name));
        for (int i = 0; i < _layers.Length; i++)
            if (i != exclude && string.Equals(_layers[i].Name, name, StringComparison.Ordinal))
                throw new ArgumentException("Nome de camada duplicado.", nameof(name));
    }

    private TileMap CopyWith(TileLayer[] layers)
    {
        int maximum = 0;
        foreach (var layer in layers)
            foreach (int tile in layer.Tiles)
                if (tile > maximum) maximum = tile;
        return new TileMap(TexturePath, Width, Height, TileWidth, TileHeight, layers, maximum);
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

    /// <summary>Exporta o mapa no mesmo contrato JSON v1 aceito por Parse, preservando camadas, visibilidade e colisão.</summary>
    /// <returns>JSON UTF-8 legível, com nomes de propriedades canônicos e IDs de tiles sem alteração.</returns>
    /// <remarks>Aloca memória para exportação; não chamar no loop do jogo. Não grava arquivos nem modifica o mapa.</remarks>
    public string ToJson()
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteNumber("version", 1);
            writer.WriteString("texture", TexturePath);
            writer.WriteNumber("width", Width);
            writer.WriteNumber("height", Height);
            writer.WriteNumber("tileWidth", TileWidth);
            writer.WriteNumber("tileHeight", TileHeight);
            writer.WriteStartArray("layers");
            foreach (var layer in _layers)
            {
                writer.WriteStartObject();
                writer.WriteString("name", layer.Name);
                writer.WriteBoolean("visible", layer.Visible);
                writer.WriteBoolean("collision", layer.Collision);
                writer.WriteStartArray("tiles");
                foreach (var tile in layer.Tiles) writer.WriteNumberValue(tile);
                writer.WriteEndArray();
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.GetBuffer(), 0, checked((int)stream.Length));
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
