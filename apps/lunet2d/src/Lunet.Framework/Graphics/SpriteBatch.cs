using System.Numerics;

namespace Lunet.Graphics;

/// <summary>Agrupa quads com a mesma textura e os envia ao backend. Sem alocações por quadro.</summary>
public sealed class SpriteBatch
{
    /// <summary>Máximo de quads por chamada de desenho ao backend.</summary>
    public const int MaxQuads = 2048;
    private readonly GraphicsDevice _device;
    private readonly SpriteVertex[] _vertices = new SpriteVertex[MaxQuads * 4];
    private int _quads;
    private Texture2D? _texture;
    private bool _begun;

    public SpriteBatch(GraphicsDevice device) => _device = device ?? throw new ArgumentNullException(nameof(device));

    public void Begin()
    {
        if (_begun) throw new InvalidOperationException("End() deve ser chamado antes de outro Begin().");
        _begun = true;
    }

    public void Draw(Texture2D texture, Vector2 position, Color color) =>
        Draw(texture, new RectangleF(position.X, position.Y, texture.Width, texture.Height), null, color, 0f, Vector2.Zero);

    /// <summary>Desenha <paramref name="source"/> (em pixels; nulo = textura inteira) em <paramref name="destination"/>.</summary>
    /// <param name="rotation">Radianos, em torno de <paramref name="origin"/> (em pixels do retângulo de origem).</param>
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
            vertex.TexCoord = new Vector2(u, v);
            vertex.Color = packed;
        }
    }

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
        _device.Backend.DrawQuads(_texture.Handle, _vertices.AsSpan(0, _quads * 4), _quads, in projection);
        _quads = 0;
        _texture = null;
    }
}
