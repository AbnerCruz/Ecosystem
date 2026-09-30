using System.Numerics;

namespace Lunet;

/// <summary>Retângulo com coordenadas de ponto flutuante: posição do canto superior esquerdo e tamanho.</summary>
/// <example>
/// <code>
/// var button = new RectangleF(20, 500, 320, 80);
/// bool pressed = input.TryGetPointer(out var touch) &amp;&amp; button.Contains(touch);
/// </code>
/// </example>
public readonly struct RectangleF : IEquatable<RectangleF>
{
    /// <summary>Cria um retângulo.</summary>
    /// <param name="x">Posição X do canto superior esquerdo.</param>
    /// <param name="y">Posição Y do canto superior esquerdo.</param>
    /// <param name="width">Largura.</param>
    /// <param name="height">Altura.</param>
    public RectangleF(float x, float y, float width, float height)
    {
        X = x; Y = y; Width = width; Height = height;
    }

    /// <summary>Posição X do canto superior esquerdo.</summary>
    public float X { get; }
    /// <summary>Posição Y do canto superior esquerdo.</summary>
    public float Y { get; }
    /// <summary>Largura.</summary>
    public float Width { get; }
    /// <summary>Altura.</summary>
    public float Height { get; }
    /// <summary>Coordenada X do lado direito (X + Width).</summary>
    public float Right => X + Width;
    /// <summary>Coordenada Y do lado inferior (Y + Height).</summary>
    public float Bottom => Y + Height;
    /// <summary>Canto superior esquerdo.</summary>
    public Vector2 Position => new(X, Y);
    /// <summary>Largura e altura como vetor.</summary>
    public Vector2 Size => new(Width, Height);
    /// <summary>Ponto central do retângulo.</summary>
    public Vector2 Center => new(X + Width * 0.5f, Y + Height * 0.5f);

    /// <summary>Diz se o ponto está dentro do retângulo (o lado direito e o inferior não contam).</summary>
    /// <param name="point">Ponto a testar.</param>
    /// <returns>Verdadeiro se o ponto está dentro.</returns>
    public bool Contains(Vector2 point) => point.X >= X && point.X < Right && point.Y >= Y && point.Y < Bottom;

    /// <summary>Diz se este retângulo sobrepõe o outro.</summary>
    /// <param name="other">Outro retângulo.</param>
    /// <returns>Verdadeiro se há sobreposição.</returns>
    public bool Intersects(RectangleF other) =>
        X < other.Right && Right > other.X && Y < other.Bottom && Bottom > other.Y;

    public bool Equals(RectangleF other) => X == other.X && Y == other.Y && Width == other.Width && Height == other.Height;
    public override bool Equals(object? obj) => obj is RectangleF r && Equals(r);
    public override int GetHashCode() => HashCode.Combine(X, Y, Width, Height);
    public static bool operator ==(RectangleF a, RectangleF b) => a.Equals(b);
    public static bool operator !=(RectangleF a, RectangleF b) => !a.Equals(b);
}
