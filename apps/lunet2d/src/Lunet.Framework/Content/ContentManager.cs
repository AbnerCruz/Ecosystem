using Lunet.Audio;
using System.Text.Json;
using Lunet.Graphics;

namespace Lunet.Content;

/// <summary>Carrega e guarda em cache o conteúdo do jogo. Texturas são liberadas em <see cref="Dispose"/>.</summary>
public sealed class ContentManager : IDisposable
{
    private readonly IContentSource _source;
    private readonly GraphicsDevice _device;
    private readonly Dictionary<string, Texture2D> _textures = new(StringComparer.Ordinal);

    private readonly IAudioBackend _audio;
    private readonly Dictionary<string, Music> _music = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TextureAtlas> _atlases = new(StringComparer.Ordinal);
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true, IncludeFields = true, ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
    private readonly Dictionary<string, SoundEffect> _sounds = new(StringComparer.Ordinal);

    /// <summary>Textos traduzidos (<c>Data/strings.&lt;idioma&gt;.json</c>).</summary>
    public Localization Localization { get; }

    /// <summary>Mistura de áudio do jogo (barramentos, música, fades).</summary>
    public AudioMixer Audio { get; }

    /// <summary>Cria o gerenciador de conteúdo.</summary>
    /// <param name="source">De onde ler os arquivos.</param>
    /// <param name="device">Dispositivo gráfico para criar texturas.</param>
    /// <param name="audio">Motor de áudio</param>
    public ContentManager(IContentSource source, GraphicsDevice device, IAudioBackend? audio = null)
    {
        _audio = audio ?? new NullAudioBackend();
        Audio = new AudioMixer(_audio);
        Localization = new Localization(source);
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _device = device ?? throw new ArgumentNullException(nameof(device));
    }

    /// <summary>Carrega um PNG (ex.: <c>"Textures/hero.png"</c>). Chamadas repetidas devolvem a mesma textura.</summary>
    public Texture2D LoadTexture(string path, TextureFilter filter = TextureFilter.Linear)
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
        var texture = Texture2D.FromPixels(_device, image.Width, image.Height, image.Rgba, filter);
        _textures[path] = texture;
        return texture;
    }

    /// <summary>Carrega um efeito sonoro (ex.: <c>"Audio/jump.wav"</c>). Cacheado.</summary>
    public SoundEffect LoadSound(string path)
    {
        if (_sounds.TryGetValue(path, out var cached) && !cached.IsDisposed) return cached;
        using var stream = _source.Open(path);
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        var sound = new SoundEffect(_audio, Audio, _audio.LoadSound(memory.ToArray(), path), path);
        _sounds[path] = sound;
        return sound;
    }

    /// <summary>Carrega uma música longa (ex.: <c>"Audio/theme.ogg"</c>) para tocar com <c>Audio.PlayMusic</c>. Cacheada.</summary>
    public Music LoadMusic(string path)
    {
        if (_music.TryGetValue(path, out var cached) && !cached.IsDisposed) return cached;
        using var stream = _source.Open(path);
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        var music = new Music(_audio, _audio.LoadMusic(memory.ToArray(), path), path);
        _music[path] = music;
        return music;
    }

    /// <summary>Lê um JSON e o converte para <typeparamref name="T"/> (campos públicos e propriedades; comentários e vírgula final são aceitos).</summary>
    public T LoadJson<T>(string path)
    {
        var text = ReadText(path);
        try { return JsonSerializer.Deserialize<T>(text, JsonOptions) ?? throw new InvalidDataException($"\"{path}\" está vazio."); }
        catch (JsonException ex) { throw new InvalidDataException($"Não foi possível ler \"{path}\": {ex.Message}", ex); }
    }

    /// <summary>Carrega um atlas de texturas (JSON com a textura e as regiões nomeadas). Cacheado.</summary>
    public TextureAtlas LoadAtlas(string path, TextureFilter filter = TextureFilter.Point)
    {
        if (_atlases.TryGetValue(path, out var cached) && !cached.Texture.IsDisposed) return cached;
        var (texturePath, regions) = TextureAtlas.Parse(ReadText(path));
        var atlas = new TextureAtlas(LoadTexture(texturePath, filter), regions);
        _atlases[path] = atlas;
        return atlas;
    }

    /// <summary>Libera uma textura carregada; a próxima <see cref="LoadTexture"/> a lê de novo (útil após editar o arquivo).</summary>
    public void UnloadTexture(string path)
    {
        if (_textures.Remove(path, out var texture)) texture.Dispose();
        foreach (var key in _atlases.Where(a => a.Value.Texture == texture).Select(a => a.Key).ToList()) _atlases.Remove(key);
    }

    /// <summary>Lê um arquivo de texto (UTF-8).</summary>
    /// <param name="path">Caminho relativo a Content.</param>
    /// <returns>O conteúdo do arquivo.</returns>
    public string ReadText(string path)
    {
        using var reader = new StreamReader(_source.Open(path));
        return reader.ReadToEnd();
    }

    /// <summary>Diz se o arquivo existe em Content.</summary>
    /// <param name="path">Caminho relativo a Content, com barras normais.</param>
    /// <returns>Verdadeiro se existe.</returns>
    public bool Exists(string path) => _source.Exists(path);

    /// <summary>Libera todas as texturas, sons e músicas carregadas.</summary>
    public void Dispose()
    {
        foreach (var texture in _textures.Values) texture.Dispose();
        _textures.Clear();
        _atlases.Clear();
        Audio.StopMusic();
        foreach (var music in _music.Values) music.Dispose();
        _music.Clear();
        foreach (var sound in _sounds.Values) sound.Dispose();
        _sounds.Clear();
    }
}
