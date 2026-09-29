using System.Numerics;

namespace Lunet;

/// <summary>Círculo definido por centro e raio, com testes de contenção e interseção.</summary>
public readonly struct Circle : IEquatable<Circle>
{
    /// <summary>Cria um círculo.</summary>
    /// <param name="center">Centro do círculo.</param>
    /// <param name="radius">Raio, em unidades de jogo.</param>
    public Circle(Vector2 center, float radius)
    {
        Center = center;
        Radius = radius;
    }

    /// <summary>Centro do círculo.</summary>
    public Vector2 Center { get; }
    /// <summary>Raio do círculo.</summary>
    public float Radius { get; }

    /// <summary>Diz se o ponto está dentro ou sobre o círculo.</summary>
    /// <param name="point">Ponto a testar.</param>
    /// <returns>Verdadeiro se o ponto está dentro.</returns>
    public bool Contains(Vector2 point) => Vector2.DistanceSquared(Center, point) <= Radius * Radius;

    /// <summary>Diz se este círculo toca ou sobrepõe a outra forma.</summary>
    /// <param name="other">Outro círculo.</param>
    /// <returns>Verdadeiro se há interseção.</returns>
    public bool Intersects(Circle other)
    {
        var r = Radius + other.Radius;
        return Vector2.DistanceSquared(Center, other.Center) <= r * r;
    }

    /// <summary>Diz se este círculo toca ou sobrepõe a outra forma.</summary>
    /// <param name="rect">Retângulo a testar.</param>
    /// <returns>Verdadeiro se há interseção.</returns>
    public bool Intersects(RectangleF rect)
    {
        var closest = new Vector2(Math.Clamp(Center.X, rect.X, rect.Right), Math.Clamp(Center.Y, rect.Y, rect.Bottom));
        return Contains(closest);
    }

    public bool Equals(Circle other) => Center == other.Center && Radius == other.Radius;
    public override bool Equals(object? obj) => obj is Circle c && Equals(c);
    public override int GetHashCode() => HashCode.Combine(Center, Radius);
    public static bool operator ==(Circle a, Circle b) => a.Equals(b);
    public static bool operator !=(Circle a, Circle b) => !a.Equals(b);
}
