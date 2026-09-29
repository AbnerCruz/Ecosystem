using System.Numerics;

namespace Lunet.Graphics;

/// <summary>
/// Dispositivo gráfico do jogo. Desenha numa resolução virtual fixa, centralizada na tela com barras (letterbox).
/// </summary>
public sealed class GraphicsDevice
{
    private int _surfaceWidth = 1;
    private int _surfaceHeight = 1;
    private float _scale = 1f;
    private float _offsetX;
    private float _offsetY;

    public GraphicsDevice(IGraphicsBackend backend, int virtualWidth, int virtualHeight)
    {
        Backend = backend ?? throw new ArgumentNullException(nameof(backend));
        SetVirtualResolution(virtualWidth, virtualHeight);
    }

    internal IGraphicsBackend Backend { get; }

    public int VirtualWidth { get; private set; }
    public int VirtualHeight { get; private set; }
    public int SurfaceWidth => _surfaceWidth;
    public int SurfaceHeight => _surfaceHeight;

    /// <summary>Projeção ortográfica: (0,0) no canto superior esquerdo virtual até (W,H) no inferior direito.</summary>
    public Matrix4x4 Projection { get; private set; }

    public void SetVirtualResolution(int width, int height)
    {
        if (width < 1 || height < 1) throw new ArgumentOutOfRangeException(nameof(width), "A resolução virtual deve ser positiva.");
        VirtualWidth = width;
        VirtualHeight = height;
        Projection = Matrix4x4.CreateOrthographicOffCenter(0, width, height, 0, -1, 1);
        Recompute();
    }

    /// <summary>Informa o tamanho real da superfície em pixels e ajusta o viewport.</summary>
    public void Resize(int surfaceWidth, int surfaceHeight)
    {
        _surfaceWidth = Math.Max(1, surfaceWidth);
        _surfaceHeight = Math.Max(1, surfaceHeight);
        Recompute();
    }

    private void Recompute()
    {
        _scale = MathF.Min((float)_surfaceWidth / VirtualWidth, (float)_surfaceHeight / VirtualHeight);
        var width = VirtualWidth * _scale;
        var height = VirtualHeight * _scale;
        _offsetX = (_surfaceWidth - width) * 0.5f;
        _offsetY = (_surfaceHeight - height) * 0.5f;
    }

    /// <summary>Limpa toda a superfície com preto e o viewport virtual com <paramref name="color"/>.</summary>
    public void Clear(Color color)
    {
        Backend.SetViewport(0, 0, _surfaceWidth, _surfaceHeight);
        Backend.Clear(Color.Black);
        Backend.SetViewport((int)MathF.Round(_offsetX), (int)MathF.Round(_offsetY),
            (int)MathF.Round(VirtualWidth * _scale), (int)MathF.Round(VirtualHeight * _scale));
        Backend.Clear(color);
    }

    /// <summary>Converte pixels da superfície (ex.: toque) para coordenadas virtuais.</summary>
    public Vector2 SurfaceToVirtual(Vector2 surfacePoint) =>
        new((surfacePoint.X - _offsetX) / _scale, (surfacePoint.Y - _offsetY) / _scale);
}
