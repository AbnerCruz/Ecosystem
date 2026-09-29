using System.Text.Json;
using System.Text.Json.Serialization;

namespace Lunet.Graphics;

/// <summary>Região nomeada de um atlas, em pixels, com pivô opcional (0–1, padrão no centro).</summary>
public readonly record struct AtlasRegion(string Name, RectangleF Bounds, float PivotX = 0.5f, float PivotY = 0.5f);

/// <summary>Uma textura com várias regiões nomeadas. Formato JSON: veja <see cref="Parse"/>.</summary>
public sealed class TextureAtlas
{
    private readonly Dictionary<string, AtlasRegion> _regions;

    public TextureAtlas(Texture2D texture, IEnumerable<AtlasRegion> regions)
    {
        Texture = texture ?? throw new ArgumentNullException(nameof(texture));
        _regions = new Dictionary<string, AtlasRegion>(StringComparer.Ordinal);
        foreach (var region in regions) _regions[region.Name] = region;
    }

    public Texture2D Texture { get; }

    public IEnumerable<string> Names => _regions.Keys;

    public int Count => _regions.Count;

    public bool TryGetRegion(string name, out AtlasRegion region) => _regions.TryGetValue(name, out region);

    public AtlasRegion this[string name] =>
        _regions.TryGetValue(name, out var region) ? region : throw new KeyNotFoundException($"Região \"{name}\" não existe neste atlas.");

    /// <summary>
    /// Lê <c>{ "texture": "Textures/atlas.png", "regions": { "hero": { "x":0, "y":0, "width":16, "height":16, "pivotX":0.5, "pivotY":1 } } }</c>.
    /// Devolve o caminho da textura e as regiões.
    /// </summary>
    public static (string TexturePath, IReadOnlyList<AtlasRegion> Regions) Parse(string json)
    {
        AtlasFile? file;
        try { file = JsonSerializer.Deserialize<AtlasFile>(json, JsonOptions); }
        catch (JsonException ex) { throw new InvalidDataException("Atlas inválido: " + ex.Message, ex); }
        if (file is null || string.IsNullOrWhiteSpace(file.Texture)) throw new InvalidDataException("Atlas sem o campo \"texture\".");
        var regions = new List<AtlasRegion>();
        foreach (var (name, r) in file.Regions ?? [])
        {
            if (r.Width <= 0 || r.Height <= 0) throw new InvalidDataException($"Região \"{name}\" com tamanho inválido.");
            regions.Add(new AtlasRegion(name, new RectangleF(r.X, r.Y, r.Width, r.Height), r.PivotX, r.PivotY));
        }
        return (file.Texture, regions);
    }

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private sealed class AtlasFile
    {
        public string? Texture { get; set; }
        public Dictionary<string, RegionFile>? Regions { get; set; }
    }

    private sealed class RegionFile
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }
        [JsonPropertyName("pivotX")] public float PivotX { get; set; } = 0.5f;
        [JsonPropertyName("pivotY")] public float PivotY { get; set; } = 0.5f;
    }
}
