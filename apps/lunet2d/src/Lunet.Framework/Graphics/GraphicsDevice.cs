using System.Numerics;

namespace Lunet.Graphics;

/// <summary>
/// Dispositivo gráfico do jogo. Desenha numa resolução virtual fixa, centralizada na tela com barras (letterbox),
/// ou num <see cref="RenderTarget2D"/> (espaço em pixels do alvo).
/// </summary>
public sealed class GraphicsDevice
{
    private int _surfaceWidth = 1;
    private int _surfaceHeight = 1;
    private float _scale = 1f;
    private float _offsetX;
    private float _offsetY;
    private bool _pixelPerfect;
    private RectI _insets;
    private Texture2D? _white;

    /// <summary>Cria o dispositivo gráfico.</summary>
    /// <param name="backend">API gráfica.</param>
    /// <param name="virtualWidth">Largura virtual.</param>
    /// <param name="virtualHeight">Altura virtual.</param>
    public GraphicsDevice(IGraphicsBackend backend, int virtualWidth, int virtualHeight)
    {
        Backend = backend ?? throw new ArgumentNullException(nameof(backend));
        SetVirtualResolution(virtualWidth, virtualHeight);
    }

    internal IGraphicsBackend Backend { get; }

    /// <summary>Largura da resolução virtual.</summary>
    public int VirtualWidth { get; private set; }
    /// <summary>Altura da resolução virtual.</summary>
    public int VirtualHeight { get; private set; }
    /// <summary>Largura real da tela, em pixels.</summary>
    public int SurfaceWidth => _surfaceWidth;
    /// <summary>Altura real da tela, em pixels.</summary>
    public int SurfaceHeight => _surfaceHeight;

    /// <summary>Alvo de desenho atual; nulo = a tela.</summary>
    public RenderTarget2D? RenderTarget { get; private set; }

    /// <summary>Tamanho do espaço de desenho atual: a resolução virtual na tela, ou o tamanho do alvo.</summary>
    public Vector2 ViewSize => RenderTarget is { } rt ? new Vector2(rt.Width, rt.Height) : new Vector2(VirtualWidth, VirtualHeight);

    /// <summary>Projeção ortográfica: (0,0) no canto superior esquerdo até o tamanho de <see cref="ViewSize"/> no inferior direito.</summary>
    public Matrix4x4 Projection { get; private set; }

    /// <summary>Área da superfície ocupada pela resolução virtual (sem as barras), em pixels.</summary>
    public Viewport Viewport => new((int)MathF.Round(_offsetX), (int)MathF.Round(_offsetY),
        (int)MathF.Round(VirtualWidth * _scale), (int)MathF.Round(VirtualHeight * _scale));

    /// <summary>Densidade de pixels do aparelho (1 = 160 dpi). Informada pelo host.</summary>
    public float Density { get; private set; } = 1f;

    /// <summary>Quando verdadeiro, a escala é sempre um número inteiro (≥ 1 se couber), mantendo pixel art nítida.</summary>
    public bool PixelPerfect
    {
        get => _pixelPerfect;
        set
        {
            _pixelPerfect = value;
            Recompute();
        }
    }

    /// <summary>Retângulo, em coordenadas virtuais, livre de recortes de tela, cantos arredondados e barras do sistema.</summary>
    public RectangleF SafeArea { get; private set; }

    /// <summary>Pixels da superfície por unidade virtual.</summary>
    public float Scale => _scale;

    /// <summary>Uma textura branca de 1×1, útil para retângulos e linhas.</summary>
    public Texture2D WhiteTexture => _white ??= Texture2D.CreateSolid(this, 1, 1, Color.White);

    /// <summary>Troca a resolução virtual em que o jogo desenha.</summary>
    /// <param name="width">Nova largura.</param>
    /// <param name="height">Nova altura.</param>
    public void SetVirtualResolution(int width, int height)
    {
        if (width < 1 || height < 1) throw new ArgumentOutOfRangeException(nameof(width), "A resolução virtual deve ser positiva.");
        VirtualWidth = width;
        VirtualHeight = height;
        Recompute();
    }

    /// <summary>Informa o tamanho real da superfície em pixels e ajusta o viewport.</summary>
    public void Resize(int surfaceWidth, int surfaceHeight)
    {
        _surfaceWidth = Math.Max(1, surfaceWidth);
        _surfaceHeight = Math.Max(1, surfaceHeight);
        Recompute();
    }

    /// <summary>Informa densidade e recuos seguros (em pixels da superfície: esquerda, topo, direita, base).</summary>
    public void SetDisplay(float density, int insetLeft, int insetTop, int insetRight, int insetBottom)
    {
        Density = density > 0 ? density : 1f;
        _insets = new RectI(Math.Max(0, insetLeft), Math.Max(0, insetTop), Math.Max(0, insetRight), Math.Max(0, insetBottom));
        Recompute();
    }

    private void Recompute()
    {
        var fit = MathF.Min((float)_surfaceWidth / VirtualWidth, (float)_surfaceHeight / VirtualHeight);
        _scale = _pixelPerfect && fit >= 1f ? MathF.Floor(fit) : fit;
        var width = VirtualWidth * _scale;
        var height = VirtualHeight * _scale;
        _offsetX = (_surfaceWidth - width) * 0.5f;
        _offsetY = (_surfaceHeight - height) * 0.5f;

        // Recuos: pixels da superfície → coordenadas virtuais, limitados à área virtual.
        var left = Math.Clamp((_insets.X - _offsetX) / _scale, 0f, VirtualWidth);
        var top = Math.Clamp((_insets.Y - _offsetY) / _scale, 0f, VirtualHeight);
        var right = Math.Clamp((_insets.Width - (_surfaceWidth - _offsetX - width)) / _scale, 0f, VirtualWidth);
        var bottom = Math.Clamp((_insets.Height - (_surfaceHeight - _offsetY - height)) / _scale, 0f, VirtualHeight);
        SafeArea = new RectangleF(left, top, Math.Max(0f, VirtualWidth - left - right), Math.Max(0f, VirtualHeight - top - bottom));

        UpdateProjection();
    }

    private void UpdateProjection()
    {
        Projection = RenderTarget is { } rt
            // Dentro de um alvo o eixo Y é invertido: assim a textura resultante sai "em pé" ao ser amostrada.
            ? Matrix4x4.CreateOrthographicOffCenter(0, rt.Width, 0, rt.Height, -1, 1)
            : Matrix4x4.CreateOrthographicOffCenter(0, VirtualWidth, VirtualHeight, 0, -1, 1);
    }

    internal void ReleaseResources()
    {
        _white?.Dispose();
        _white = null;
    }

    /// <summary>Passa a desenhar no alvo (ou volta à tela com <c>null</c>).</summary>
    public void SetRenderTarget(RenderTarget2D? target)
    {
        if (target is { IsDisposed: true }) throw new ObjectDisposedException(nameof(RenderTarget2D));
        RenderTarget = target;
        Backend.SetRenderTarget(target?.Handle ?? 0, target?.Width ?? _surfaceWidth, target?.Height ?? _surfaceHeight);
        if (target is null) Backend.SetViewport(0, 0, _surfaceWidth, _surfaceHeight);
        else Backend.SetViewport(0, 0, target.Width, target.Height);
        UpdateProjection();
    }

    /// <summary>Limpa o alvo atual com <paramref name="color"/>. Na tela, as barras laterais ficam pretas.</summary>
    public void Clear(Color color)
    {
        if (RenderTarget is { } rt)
        {
            Backend.SetViewport(0, 0, rt.Width, rt.Height);
            Backend.Clear(color);
            return;
        }
        Backend.SetViewport(0, 0, _surfaceWidth, _surfaceHeight);
        Backend.Clear(Color.Black);
        var v = Viewport;
        Backend.SetViewport(v.X, v.Y, v.Width, v.Height);
        Backend.Clear(color);
    }

    /// <summary>Converte pixels da superfície (ex.: toque) para coordenadas virtuais.</summary>
    public Vector2 SurfaceToVirtual(Vector2 surfacePoint) =>
        new((surfacePoint.X - _offsetX) / _scale, (surfacePoint.Y - _offsetY) / _scale);

    /// <summary>Converte coordenadas virtuais para pixels da superfície.</summary>
    public Vector2 VirtualToSurface(Vector2 virtualPoint) =>
        new(virtualPoint.X * _scale + _offsetX, virtualPoint.Y * _scale + _offsetY);

    /// <summary>Retângulo de recorte do GL (origem embaixo à esquerda, pixels do alvo) para uma área do espaço de desenho atual.</summary>
    internal RectI ToScissor(RectangleF area)
    {
        if (RenderTarget is { } rt)
        {
            // Projeção invertida no alvo: (0,0) do desenho já é a origem do GL.
            var x0 = Math.Clamp((int)MathF.Floor(area.X), 0, rt.Width);
            var y0 = Math.Clamp((int)MathF.Floor(area.Y), 0, rt.Height);
            var x1 = Math.Clamp((int)MathF.Ceiling(area.Right), 0, rt.Width);
            var y1 = Math.Clamp((int)MathF.Ceiling(area.Bottom), 0, rt.Height);
            return new RectI(x0, y0, Math.Max(0, x1 - x0), Math.Max(0, y1 - y0));
        }
        var topLeft = VirtualToSurface(area.Position);
        var bottomRight = VirtualToSurface(new Vector2(area.Right, area.Bottom));
        var sx0 = Math.Clamp((int)MathF.Floor(topLeft.X), 0, _surfaceWidth);
        var sx1 = Math.Clamp((int)MathF.Ceiling(bottomRight.X), 0, _surfaceWidth);
        var sy0 = Math.Clamp((int)MathF.Floor(topLeft.Y), 0, _surfaceHeight);
        var sy1 = Math.Clamp((int)MathF.Ceiling(bottomRight.Y), 0, _surfaceHeight);
        return new RectI(sx0, _surfaceHeight - sy1, Math.Max(0, sx1 - sx0), Math.Max(0, sy1 - sy0));
    }
}
