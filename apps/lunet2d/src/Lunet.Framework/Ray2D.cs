using System.Numerics;

namespace Lunet;

/// <summary>Raio 2D: origem e direção (normalizada na criação).</summary>
public readonly struct Ray2D
{
    public Ray2D(Vector2 origin, Vector2 direction)
    {
        if (direction == Vector2.Zero) throw new ArgumentException("A direção não pode ser zero.", nameof(direction));
        Origin = origin;
        Direction = Vector2.Normalize(direction);
    }

    public Vector2 Origin { get; }
    public Vector2 Direction { get; }

    public Vector2 PointAt(float distance) => Origin + Direction * distance;

    /// <summary>Distância até a primeira interseção com o círculo, ou falso.</summary>
    public bool Intersects(Circle circle, out float distance)
    {
        var toCenter = circle.Center - Origin;
        var projection = Vector2.Dot(toCenter, Direction);
        var closestSquared = toCenter.LengthSquared() - projection * projection;
        var radiusSquared = circle.Radius * circle.Radius;
        distance = 0;
        if (closestSquared > radiusSquared) return false;
        var half = MathF.Sqrt(radiusSquared - closestSquared);
        var near = projection - half;
        var far = projection + half;
        if (far < 0) return false;
        distance = near >= 0 ? near : 0; // origem dentro do círculo: acerto imediato
        return true;
    }

    /// <summary>Distância até a primeira interseção com o retângulo (método das lâminas), ou falso.</summary>
    public bool Intersects(RectangleF rect, out float distance)
    {
        var tMin = 0f;
        var tMax = float.PositiveInfinity;
        distance = 0;
        if (!Slab(Origin.X, Direction.X, rect.X, rect.Right, ref tMin, ref tMax)) return false;
        if (!Slab(Origin.Y, Direction.Y, rect.Y, rect.Bottom, ref tMin, ref tMax)) return false;
        distance = tMin;
        return true;
    }

    private static bool Slab(float origin, float direction, float min, float max, ref float tMin, ref float tMax)
    {
        if (MathF.Abs(direction) < 1e-8f) return origin >= min && origin <= max;
        var t1 = (min - origin) / direction;
        var t2 = (max - origin) / direction;
        if (t1 > t2) (t1, t2) = (t2, t1);
        tMin = MathF.Max(tMin, t1);
        tMax = MathF.Min(tMax, t2);
        return tMin <= tMax;
    }
}
