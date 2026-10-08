using System.Numerics;

namespace Lunet.Graphics;

/// <summary>Agrupa quads com a mesma textura e os envia ao backend. Sem alocações por quadro.</summary>
/// <example>
/// <code>
/// batch.Begin();
/// batch.Draw(texture, position, Color.White);
/// batch.DrawString(font, "Pontos: 10", new Vector2(8, 8), Color.White);
/// batch.End();
/// </code>
/// </example>
public sealed class SpriteBatch
{
    /// <summary>Máximo de quads por chamada de desenho ao backend.</summary>
    public const int MaxQuads = 2048;
    private readonly GraphicsDevice _device;
    private readonly SpriteVertex[] _vertices = new SpriteVertex[MaxQuads * 4];
    private int _quads;
    private Texture2D? _texture;
    private bool _begun;
    private DrawState _state = DrawState.Default;
    private Shader? _shader;
    private Matrix3x2 _view = Matrix3x2.Identity;
    private bool _hasView;

    /// <summary>Cria um lote de sprites.</summary>
    /// <param name="device">Dispositivo gráfico.</param>
    public SpriteBatch(GraphicsDevice device) => _device = device ?? throw new ArgumentNullException(nameof(device));

    /// <summary>O dispositivo em que este lote desenha.</summary>
    public GraphicsDevice GraphicsDevice => _device;

    /// <summary>Começa um lote.</summary>
    /// <param name="blend">Mistura; nulo = <see cref="BlendState.Alpha"/>.</param>
    /// <param name="sampler">Filtro e repetição de textura; nulo = os da própria textura.</param>
    /// <param name="shader">Shader de fragmento; nulo = o padrão.</param>
    /// <param name="clip">Área (no espaço de desenho atual) fora da qual nada é desenhado.</param>
    public void Begin(BlendState? blend = null, SamplerState? sampler = null, Shader? shader = null, RectangleF? clip = null)
    {
        if (_begun) throw new InvalidOperationException("End() deve ser chamado antes de outro Begin().");
        if (shader is { IsDisposed: true }) throw new ObjectDisposedException(nameof(Shader));
        _begun = true;
        _shader = shader;
        _view = Matrix3x2.Identity;
        _hasView = false;
        _state = new DrawState(
            (blend ?? BlendState.Alpha).Mode,
            sampler,
            clip is { } area ? _device.ToScissor(area) : null,
            shader?.Handle ?? 0,
            null);
    }

    /// <summary>Começa um lote em coordenadas do mundo usando uma câmera.</summary>
    /// <param name="camera">Câmera a capturar; mudanças nela afetam somente o próximo Begin.</param>
    /// <param name="blend">Mistura; nulo usa Alpha.</param>
    /// <param name="sampler">Filtro e repetição; nulo usa o estado da textura.</param>
    /// <param name="shader">Shader de fragmento; nulo usa o padrão.</param>
    /// <param name="clip">Recorte em coordenadas virtuais da vista, independente da câmera.</param>
    /// <remarks>Usa GraphicsDevice.ViewSize, inclusive em render targets. Transformação aplicada a sprites,
    /// texto e DebugDraw sem mudar o backend. Para HUD, encerre o lote e use Begin() sem câmera.</remarks>
    public void Begin(Camera2D camera, BlendState? blend = null, SamplerState? sampler = null, Shader? shader = null, RectangleF? clip = null)
    {
        ArgumentNullException.ThrowIfNull(camera);
        var view = camera.GetViewMatrix(_device.ViewSize);
        Begin(blend, sampler, shader, clip);
        _view = view;
        _hasView = view != Matrix3x2.Identity;
    }

    /// <summary>Começa um lote usando o estado do material.</summary>
    /// <param name="material">Shader, mistura e amostragem.</param>
    public void Begin(Material material)
    {
        ArgumentNullException.ThrowIfNull(material);
        Begin(material.Blend, material.Sampler, material.Shader);
    }

    /// <summary>Desenha a textura inteira com o canto superior esquerdo na posição.</summary>
    /// <param name="texture">Textura.</param>
    /// <param name="position">Onde desenhar.</param>
    /// <param name="color">Cor de multiplicação.</param>
    public void Draw(Texture2D texture, Vector2 position, Color color) =>
        Draw(texture, new RectangleF(position.X, position.Y, texture.Width, texture.Height), null, color, 0f, Vector2.Zero);

    /// <summary>Desenha um recorte (<paramref name="source"/>, em pixels) na posição dada, sem escala.</summary>
    /// <param name="texture">Textura a desenhar.</param>
    /// <param name="position">Posição, em coordenadas virtuais.</param>
    /// <param name="source">Trecho da textura a desenhar, em pixels; nulo desenha a textura toda.</param>
    /// <param name="color">Cor de tinta (multiplica as cores da imagem).</param>
    public void Draw(Texture2D texture, Vector2 position, RectangleF source, Color color) =>
        Draw(texture, new RectangleF(position.X, position.Y, source.Width, source.Height), source, color, 0f, Vector2.Zero);

    /// <summary>Desenha <paramref name="source"/> (em pixels; nulo = textura inteira) em <paramref name="destination"/>.</summary>
    /// <param name="rotation">Radianos, em torno de <paramref name="origin"/> (em pixels do retângulo de origem).</param>
    /// <param name="texture">Textura a desenhar.</param>
    /// <param name="destination">Retângulo de destino, em coordenadas virtuais.</param>
    /// <param name="source">Trecho da textura a desenhar, em pixels; nulo desenha a textura toda.</param>
    /// <param name="color">Cor de tinta (multiplica as cores da imagem).</param>
    /// <param name="origin">Ponto de rotação, em pixels da textura.</param>
    public void Draw(Texture2D texture, RectangleF destination, RectangleF? source, Color color, float rotation, Vector2 origin)
    {
        if (!_begun) throw new InvalidOperationException("Chame Begin() antes de Draw().");
        ArgumentNullException.ThrowIfNull(texture);
        if (texture.IsDisposed) throw new ObjectDisposedException(nameof(Texture2D));
        if (_texture is not null && !ReferenceEquals(_texture, texture)) Flush();
        if (_quads == MaxQuads) Flush();
        _texture = texture;

        var src = source ?? new RectangleF(0, 0, texture.Width, texture.Height);
        var u0 = src.X / texture.Width;
        var v0 = src.Y / texture.Height;
        var u1 = src.Right / texture.Width;
        var v1 = src.Bottom / texture.Height;

        var scaleX = src.Width == 0 ? 0 : destination.Width / src.Width;
        var scaleY = src.Height == 0 ? 0 : destination.Height / src.Height;
        var ox = origin.X * scaleX;
        var oy = origin.Y * scaleY;
        var cos = MathF.Cos(rotation);
        var sin = MathF.Sin(rotation);
        var packed = color.PackedRgba;

        var i = _quads * 4;
        Corner(ref _vertices[i], 0, 0, u0, v0);
        Corner(ref _vertices[i + 1], destination.Width, 0, u1, v0);
        Corner(ref _vertices[i + 2], destination.Width, destination.Height, u1, v1);
        Corner(ref _vertices[i + 3], 0, destination.Height, u0, v1);
        _quads++;

        void Corner(ref SpriteVertex vertex, float x, float y, float u, float v)
        {
            x -= ox;
            y -= oy;
            vertex.Position = new Vector2(
                destination.X + ox + x * cos - y * sin,
                destination.Y + oy + x * sin + y * cos);
            if (_hasView) vertex.Position = Vector2.Transform(vertex.Position, _view);
            vertex.TexCoord = new Vector2(u, v);
            vertex.Color = packed;
        }
    }

    /// <summary>Desenha um painel nine-slice, sem alocação, no lote atual.</summary>
    /// <param name="slice">Região e bordas da textura.</param>
    /// <param name="destination">Destino finito com tamanho não negativo; tamanho zero não desenha.</param>
    /// <param name="color">Cor multiplicada em todas as partes.</param>
    /// <param name="borderScale">Escala positiva e finita das bordas; 1 preserva pixels da imagem.</param>
    /// <remarks>Se o destino for menor que a soma das bordas, comprime-as proporcionalmente por eixo
    /// e elimina o centro nesse eixo. Usa câmera, clip, blend e sampler do lote; até nove quads.</remarks>
    public void Draw(NineSlice slice, RectangleF destination, Color color, float borderScale = 1f)
    {
        if (!_begun) throw new InvalidOperationException("Chame Begin() antes de Draw().");
        ArgumentNullException.ThrowIfNull(slice);
        slice.Draw(this, destination, color, borderScale);
    }

    /// <summary>Desenha uma região de um atlas com o pivô da região em <paramref name="position"/>.</summary>
    /// <param name="atlas">Atlas que contém a região.</param>
    /// <param name="region">Nome da região dentro do atlas.</param>
    /// <param name="position">Posição, em coordenadas virtuais.</param>
    /// <param name="color">Cor de tinta (multiplica as cores da imagem).</param>
    /// <param name="scale">Fator de escala (1 = tamanho original).</param>
    /// <param name="rotation">Rotação, em radianos.</param>
    public void Draw(TextureAtlas atlas, string region, Vector2 position, Color color, float scale = 1f, float rotation = 0f)
    {
        ArgumentNullException.ThrowIfNull(atlas);
        var r = atlas[region];
        var origin = new Vector2(r.Bounds.Width * r.PivotX, r.Bounds.Height * r.PivotY);
        var dest = new RectangleF(position.X - origin.X * scale, position.Y - origin.Y * scale, r.Bounds.Width * scale, r.Bounds.Height * scale);
        Draw(atlas.Texture, dest, r.Bounds, color, rotation, origin);
    }

    /// <summary>Desenha o sprite com a origem em <paramref name="position"/>, usando a cor, escala e rotação do próprio sprite.</summary>
    /// <param name="sprite">Sprite a desenhar.</param>
    /// <param name="position">Posição, em coordenadas virtuais.</param>
    public void Draw(Sprite sprite, Vector2 position)
    {
        ArgumentNullException.ThrowIfNull(sprite);
        var size = sprite.Source.Size * sprite.Scale;
        var origin = sprite.Origin * sprite.Scale;
        var dest = new RectangleF(position.X - origin.X, position.Y - origin.Y, size.X, size.Y);
        Draw(sprite.Texture, dest, sprite.Source, sprite.Color, sprite.Rotation, sprite.Origin);
    }

    /// <summary>Escreve texto. Uma quebra de linha (<c>\n</c>) desce uma linha.</summary>
    /// <param name="font">Fonte do texto.</param>
    /// <param name="text">Texto a desenhar.</param>
    /// <param name="position">Posição, em coordenadas virtuais.</param>
    /// <param name="color">Cor de tinta (multiplica as cores da imagem).</param>
    /// <param name="scale">Fator de escala (1 = tamanho original).</param>
    public void DrawString(SpriteFont font, string text, Vector2 position, Color color, float scale = 1f)
    {
        ArgumentNullException.ThrowIfNull(font);
        font.Draw(this, text, position, color, scale);
    }

    /// <summary>Envia o que falta ao dispositivo e encerra o lote.</summary>
    public void End()
    {
        if (!_begun) throw new InvalidOperationException("End() sem Begin().");
        Flush();
        _begun = false;
    }

    private void Flush()
    {
        if (_quads == 0 || _texture is null) return;
        var projection = _device.Projection;
        // Os uniforms do shader são lidos no momento do desenho (o jogo pode mudá-los entre lotes).
        _device.Backend.SetDrawState(_shader is null ? _state : _state with { Uniforms = _shader.Values });
        _device.Backend.DrawQuads(_texture.Handle, _vertices.AsSpan(0, _quads * 4), _quads, in projection);
        _quads = 0;
        _texture = null;
    }
}
