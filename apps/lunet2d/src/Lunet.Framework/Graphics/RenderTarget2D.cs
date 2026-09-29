namespace Lunet.Graphics;

/// <summary>
/// Textura em que se pode desenhar (minimapa, efeitos, iluminação). Dentro do alvo o espaço de desenho
/// é em pixels do próprio alvo, com (0,0) no canto superior esquerdo.
/// </summary>
public sealed class RenderTarget2D : IDisposable
{
    private readonly GraphicsDevice _device;

    /// <summary>Cria um alvo de desenho.</summary>
    /// <param name="device">Dispositivo gráfico.</param>
    /// <param name="width">Largura em pixels (1 a 4096).</param>
    /// <param name="height">Altura em pixels (1 a 4096).</param>
    /// <param name="filter">Filtro da textura resultante.</param>
    public RenderTarget2D(GraphicsDevice device, int width, int height, TextureFilter filter = TextureFilter.Linear)
    {
        _device = device ?? throw new ArgumentNullException(nameof(device));
        if (width < 1 || height < 1 || width > 4096 || height > 4096) throw new ArgumentOutOfRangeException(nameof(width), "Tamanho entre 1 e 4096.");
        Handle = device.Backend.CreateRenderTarget(width, height, filter, out var textureHandle);
        Texture = new Texture2D(device, textureHandle, width, height, filter, ownsHandle: false);
        Width = width;
        Height = height;
    }

    internal int Handle { get; }

    /// <summary>Conteúdo desenhado, para usar com <see cref="SpriteBatch"/>.</summary>
    public Texture2D Texture { get; }

    /// <summary>Largura, em pixels.</summary>
    public int Width { get; }
    /// <summary>Altura, em pixels.</summary>
    public int Height { get; }
    /// <summary>Verdadeiro depois de liberado.</summary>
    public bool IsDisposed { get; private set; }

    /// <summary>Libera o alvo e a textura.</summary>
    public void Dispose()
    {
        if (IsDisposed) return;
        IsDisposed = true;
        if (ReferenceEquals(_device.RenderTarget, this)) _device.SetRenderTarget(null);
        _device.Backend.DeleteRenderTarget(Handle);
        Texture.MarkDisposed();
    }
}
