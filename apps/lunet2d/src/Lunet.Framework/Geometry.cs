using System.Numerics;

namespace Lunet;

/// <summary>Funções geométricas 2D: distâncias, interseções, polígonos e SAT.</summary>
public static class Geometry
{
    /// <summary>Distância do ponto ao segmento de reta.</summary>
    /// <param name="point">Ponto.</param>
    /// <param name="a">Início do segmento.</param>
    /// <param name="b">Fim do segmento.</param>
    /// <returns>Menor distância entre o ponto e o segmento.</returns>
    public static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b) => Vector2.Distance(point, ClosestPointOnSegment(point, a, b));

    /// <summary>Ponto do segmento mais próximo do ponto dado.</summary>
    /// <param name="point">Ponto.</param>
    /// <param name="a">Início do segmento.</param>
    /// <param name="b">Fim do segmento.</param>
    /// <returns>O ponto do segmento mais próximo.</returns>
    public static Vector2 ClosestPointOnSegment(Vector2 point, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        var lengthSquared = ab.LengthSquared();
        if (lengthSquared == 0) return a;
        var t = Math.Clamp(Vector2.Dot(point - a, ab) / lengthSquared, 0f, 1f);
        return a + ab * t;
    }

    /// <summary>Interseção de dois segmentos; falso se forem paralelos ou não se cruzarem.</summary>
    public static bool SegmentsIntersect(Vector2 a1, Vector2 a2, Vector2 b1, Vector2 b2, out Vector2 point)
    {
        var r = a2 - a1;
        var s = b2 - b1;
        var denominator = Cross(r, s);
        point = default;
        if (MathF.Abs(denominator) < 1e-8f) return false;
        var t = Cross(b1 - a1, s) / denominator;
        var u = Cross(b1 - a1, r) / denominator;
        if (t < 0 || t > 1 || u < 0 || u > 1) return false;
        point = a1 + r * t;
        return true;
    }

    /// <summary>Produto vetorial 2D (componente Z): positivo se b está à esquerda de a.</summary>
    /// <param name="a">Primeiro vetor.</param>
    /// <param name="b">Segundo vetor.</param>
    /// <returns>a.X * b.Y − a.Y * b.X.</returns>
    public static float Cross(Vector2 a, Vector2 b) => a.X * b.Y - a.Y * b.X;

    /// <summary>Ponto dentro de um polígono qualquer (regra par-ímpar).</summary>
    public static bool PolygonContains(ReadOnlySpan<Vector2> polygon, Vector2 point)
    {
        var inside = false;
        for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
        {
            var a = polygon[i];
            var b = polygon[j];
            if ((a.Y > point.Y) != (b.Y > point.Y) && point.X < (b.X - a.X) * (point.Y - a.Y) / (b.Y - a.Y) + a.X)
                inside = !inside;
        }
        return inside;
    }

    /// <summary>Área com sinal (positiva = anti-horário no plano matemático).</summary>
    public static float SignedArea(ReadOnlySpan<Vector2> polygon)
    {
        var sum = 0f;
        for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++) sum += Cross(polygon[j], polygon[i]);
        return sum * 0.5f;
    }

    /// <summary>
    /// Teste SAT entre dois polígonos convexos. Se houver sobreposição, <paramref name="push"/> é o menor vetor que,
    /// somado à posição de <paramref name="a"/>, o separa de <paramref name="b"/>.
    /// </summary>
    public static bool SatOverlap(ReadOnlySpan<Vector2> a, ReadOnlySpan<Vector2> b, out Vector2 push)
    {
        push = default;
        if (a.Length < 3 || b.Length < 3) return false;
        var smallest = float.MaxValue;
        var best = Vector2.Zero;
        if (!TestAxes(a, a, b, ref smallest, ref best) || !TestAxes(b, a, b, ref smallest, ref best)) return false;

        // Orienta o vetor de empurrão para afastar A de B.
        if (Vector2.Dot(best, Centroid(a) - Centroid(b)) < 0) best = -best;
        push = best * smallest;
        return true;
    }

    private static bool TestAxes(ReadOnlySpan<Vector2> edges, ReadOnlySpan<Vector2> a, ReadOnlySpan<Vector2> b, ref float smallest, ref Vector2 best)
    {
        for (int i = 0, j = edges.Length - 1; i < edges.Length; j = i++)
        {
            var edge = edges[i] - edges[j];
            if (edge == Vector2.Zero) continue;
            var axis = Vector2.Normalize(new Vector2(-edge.Y, edge.X));
            Project(a, axis, out var minA, out var maxA);
            Project(b, axis, out var minB, out var maxB);
            var overlap = MathF.Min(maxA, maxB) - MathF.Max(minA, minB);
            if (overlap <= 0) return false;
            if (overlap < smallest) { smallest = overlap; best = axis; }
        }
        return true;
    }

    private static void Project(ReadOnlySpan<Vector2> polygon, Vector2 axis, out float min, out float max)
    {
        min = max = Vector2.Dot(polygon[0], axis);
        for (var i = 1; i < polygon.Length; i++)
        {
            var d = Vector2.Dot(polygon[i], axis);
            if (d < min) min = d;
            if (d > max) max = d;
        }
    }

    private static Vector2 Centroid(ReadOnlySpan<Vector2> polygon)
    {
        var sum = Vector2.Zero;
        foreach (var p in polygon) sum += p;
        return sum / polygon.Length;
    }
}
