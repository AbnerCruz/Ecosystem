using Lunet.Graphics;

namespace Lunet.Content;

/// <summary>Carrega e guarda em cache o conteúdo do jogo. Texturas são liberadas em <see cref="Dispose"/>.</summary>
public sealed class ContentManager : IDisposable
{
    private readonly IContentSource _source;
    private readonly GraphicsDevice _device;
    private readonly Dictionary<string, Texture2D> _textures = new(StringComparer.Ordinal);

    public ContentManager(IContentSource source, GraphicsDevice device)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _device = device ?? throw new ArgumentNullException(nameof(device));
    }

    /// <summary>Carrega um PNG (ex.: <c>"Textures/hero.png"</c>). Chamadas repetidas devolvem a mesma textura.</summary>
    public Texture2D LoadTexture(string path)
    {
        if (_textures.TryGetValue(path, out var cached) && !cached.IsDisposed) return cached;
        using var stream = _source.Open(path);
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        DecodedImage image;
        try { image = PngDecoder.Decode(memory.ToArray()); }
        catch (Exception ex) when (ex is InvalidDataException or NotSupportedException)
        {
            throw new InvalidDataException($"Não foi possível carregar \"{path}\": {ex.Message}", ex);
        }
        var texture = Texture2D.FromPixels(_device, image.Width, image.Height, image.Rgba);
        _textures[path] = texture;
        return texture;
    }

    public string ReadText(string path)
    {
        using var reader = new StreamReader(_source.Open(path));
        return reader.ReadToEnd();
    }

    public bool Exists(string path) => _source.Exists(path);

    public void Dispose()
    {
        foreach (var texture in _textures.Values) texture.Dispose();
        _textures.Clear();
    }
}
