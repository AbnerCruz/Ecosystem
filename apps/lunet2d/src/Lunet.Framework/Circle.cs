using System.Numerics;

namespace Lunet;

public readonly struct Circle : IEquatable<Circle>
{
    public Circle(Vector2 center, float radius)
    {
        Center = center;
        Radius = radius;
    }

    public Vector2 Center { get; }
    public float Radius { get; }

    public bool Contains(Vector2 point) => Vector2.DistanceSquared(Center, point) <= Radius * Radius;

    public bool Intersects(Circle other)
    {
        var r = Radius + other.Radius;
        return Vector2.DistanceSquared(Center, other.Center) <= r * r;
    }

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
