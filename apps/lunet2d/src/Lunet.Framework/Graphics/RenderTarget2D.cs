namespace Lunet.Graphics;

/// <summary>
/// Textura em que se pode desenhar (minimapa, efeitos, iluminação). Dentro do alvo o espaço de desenho
/// é em pixels do próprio alvo, com (0,0) no canto superior esquerdo.
/// </summary>
public sealed class RenderTarget2D : IDisposable
{
    private readonly GraphicsDevice _device;

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

    public int Width { get; }
    public int Height { get; }
    public bool IsDisposed { get; private set; }

    public void Dispose()
    {
        if (IsDisposed) return;
        IsDisposed = true;
        if (ReferenceEquals(_device.RenderTarget, this)) _device.SetRenderTarget(null);
        _device.Backend.DeleteRenderTarget(Handle);
        Texture.MarkDisposed();
    }
}
