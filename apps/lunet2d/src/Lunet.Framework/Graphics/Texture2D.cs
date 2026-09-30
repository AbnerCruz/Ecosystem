namespace Lunet.Graphics;

/// <summary>Imagem na GPU. Cores RGBA de 8 bits, origem no canto superior esquerdo.</summary>
/// <example>
/// <code>
/// var square = Texture2D.CreateSolid(device, 16, 16, Color.Red);
/// var ball = Texture2D.CreateCircle(device, 24, Color.Yellow);
/// </code>
/// </example>
public sealed class Texture2D : IDisposable
{
    private readonly GraphicsDevice _device;

    private readonly bool _ownsHandle;

    internal Texture2D(GraphicsDevice device, int handle, int width, int height, TextureFilter filter, bool ownsHandle = true)
    {
        _ownsHandle = ownsHandle;
        Filter = filter;
        _device = device;
        Handle = handle;
        Width = width;
        Height = height;
    }

    /// <summary>Largura, em pixels.</summary>
    public int Width { get; }
    /// <summary>Altura, em pixels.</summary>
    public int Height { get; }
    /// <summary>Filtro usado ao criar a textura.</summary>
    public TextureFilter Filter { get; }
    /// <summary>Verdadeiro depois de liberada.</summary>
    public bool IsDisposed { get; private set; }
    internal int Handle { get; }

    /// <summary>Cria uma textura a partir de pixels RGBA (largura × altura × 4 bytes).</summary>
    /// <param name="device">Dispositivo gráfico do jogo.</param>
    /// <param name="width">Largura, em pixels.</param>
    /// <param name="height">Altura, em pixels.</param>
    /// <param name="rgba">Pixels em RGBA de 8 bits por canal, linha a linha de cima para baixo.</param>
    /// <param name="filter">Filtro de amostragem: `Point` (pixels nítidos) ou `Linear` (suave).</param>
    /// <returns>A textura criada.</returns>
    public static Texture2D FromPixels(GraphicsDevice device, int width, int height, ReadOnlySpan<byte> rgba, TextureFilter filter = TextureFilter.Linear)
    {
        ArgumentNullException.ThrowIfNull(device);
        if (width < 1 || height < 1) throw new ArgumentOutOfRangeException(nameof(width), "Dimensões devem ser positivas.");
        if (rgba.Length != checked(width * height * 4)) throw new ArgumentException("Esperados largura×altura×4 bytes.", nameof(rgba));
        return new Texture2D(device, device.Backend.CreateTexture(width, height, rgba, filter), width, height, filter);
    }

    /// <summary>Cria uma textura de uma cor só.</summary>
    /// <param name="device">Dispositivo gráfico.</param>
    /// <param name="width">Largura.</param>
    /// <param name="height">Altura.</param>
    /// <param name="color">Cor.</param>
    /// <returns>A textura.</returns>
    public static Texture2D CreateSolid(GraphicsDevice device, int width, int height, Color color)
    {
        var pixels = new byte[checked(width * height * 4)];
        for (var i = 0; i < pixels.Length; i += 4)
        {
            pixels[i] = color.R; pixels[i + 1] = color.G; pixels[i + 2] = color.B; pixels[i + 3] = color.A;
        }
        return FromPixels(device, width, height, pixels);
    }

    /// <summary>Disco preenchido (borda com meio pixel de suavização de alfa).</summary>
    /// <param name="device">Dispositivo gráfico do jogo.</param>
    /// <param name="diameter">Diâmetro do círculo, em pixels.</param>
    /// <param name="color">Cor de tinta (multiplica as cores da imagem).</param>
    /// <returns>Uma textura quadrada com um círculo preenchido.</returns>
    public static Texture2D CreateCircle(GraphicsDevice device, int diameter, Color color)
    {
        var pixels = new byte[checked(diameter * diameter * 4)];
        var radius = diameter / 2f;
        for (var y = 0; y < diameter; y++)
        for (var x = 0; x < diameter; x++)
        {
            var dx = x + 0.5f - radius;
            var dy = y + 0.5f - radius;
            var distance = MathF.Sqrt(dx * dx + dy * dy);
            var coverage = Math.Clamp(radius - distance + 0.5f, 0f, 1f);
            var i = (y * diameter + x) * 4;
            pixels[i] = color.R; pixels[i + 1] = color.G; pixels[i + 2] = color.B;
            pixels[i + 3] = (byte)(color.A * coverage);
        }
        return FromPixels(device, diameter, diameter, pixels);
    }

    /// <summary>Libera a textura na GPU.</summary>
    public void Dispose()
    {
        if (IsDisposed) return;
        IsDisposed = true;
        if (_ownsHandle) _device.Backend.DeleteTexture(Handle);
    }

    internal void MarkDisposed() => IsDisposed = true;
}
