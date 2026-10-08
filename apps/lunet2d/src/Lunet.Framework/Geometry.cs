using System.Numerics;

namespace Lunet;

/// <summary>Funções geométricas 2D: distâncias, interseções, polígonos e SAT.</summary>
/// <example>
/// <code>
/// var polygon = new[] { new Vector2(0, 0), new Vector2(100, 0), new Vector2(50, 80) };
/// bool inside = Geometry.PolygonContains(polygon, new Vector2(50, 30));
/// float distance = Geometry.DistanceToSegment(position, new Vector2(0, 0), new Vector2(100, 0));
/// </code>
/// </example>
public static class Geometry
{
    /// <summary>Ponto do retângulo fechado mais próximo do ponto dado.</summary>
    /// <param name="point">Ponto finito.</param>
    /// <param name="rect">Retângulo finito com tamanho não negativo.</param>
    /// <returns>Ponto mais próximo, incluindo as bordas direita e inferior.</returns>
    public static Vector2 ClosestPoint(Vector2 point, RectangleF rect) =>
        new(Math.Clamp(point.X, rect.X, rect.Right), Math.Clamp(point.Y, rect.Y, rect.Bottom));

    /// <summary>Distância de um ponto ao retângulo fechado; zero dentro ou na borda.</summary>
    /// <param name="point">Ponto finito.</param>
    /// <param name="rect">Retângulo finito com tamanho não negativo.</param>
    /// <returns>Menor distância não negativa entre o ponto e o retângulo.</returns>
    public static float Distance(Vector2 point, RectangleF rect) => Vector2.Distance(point, ClosestPoint(point, rect));

    /// <summary>Distância de um ponto ao círculo preenchido; zero dentro ou na borda.</summary>
    /// <param name="point">Ponto finito.</param>
    /// <param name="circle">Círculo finito com raio não negativo.</param>
    /// <returns>Menor distância não negativa entre o ponto e o círculo.</returns>
    public static float Distance(Vector2 point, Circle circle) => MathF.Max(0, Vector2.Distance(point, circle.Center) - circle.Radius);

    /// <summary>Distância entre dois círculos preenchidos; zero quando se tocam ou sobrepõem.</summary>
    /// <param name="a">Primeiro círculo finito com raio não negativo.</param>
    /// <param name="b">Segundo círculo finito com raio não negativo.</param>
    /// <returns>Menor distância não negativa entre os círculos.</returns>
    public static float Distance(Circle a, Circle b) => MathF.Max(0, Vector2.Distance(a.Center, b.Center) - a.Radius - b.Radius);

    /// <summary>Distância entre um círculo preenchido e um retângulo fechado.</summary>
    /// <param name="circle">Círculo finito com raio não negativo.</param>
    /// <param name="rect">Retângulo finito com tamanho não negativo.</param>
    /// <returns>Menor distância não negativa; zero no contato ou na sobreposição.</returns>
    public static float Distance(Circle circle, RectangleF rect) => MathF.Max(0, Distance(circle.Center, rect) - circle.Radius);

    /// <summary>Distância entre dois retângulos fechados.</summary>
    /// <param name="a">Primeiro retângulo finito com tamanho não negativo.</param>
    /// <param name="b">Segundo retângulo finito com tamanho não negativo.</param>
    /// <returns>Menor distância não negativa; zero no contato ou na sobreposição.</returns>
    public static float Distance(RectangleF a, RectangleF b)
    {
        var x = MathF.Max(0, MathF.Max(a.X - b.Right, b.X - a.Right));
        var y = MathF.Max(0, MathF.Max(a.Y - b.Bottom, b.Y - a.Bottom));
        return new Vector2(x, y).Length();
    }

    /// <summary>Calcula a região de sobreposição com área positiva entre dois AABBs.</summary>
    /// <param name="a">Primeiro retângulo finito com tamanho não negativo.</param>
    /// <param name="b">Segundo retângulo finito com tamanho não negativo.</param>
    /// <param name="intersection">Região comum; default quando não há área positiva.</param>
    /// <returns>Verdadeiro quando existe região comum com área positiva; contato de borda é falso.</returns>
    public static bool Intersection(RectangleF a, RectangleF b, out RectangleF intersection)
    {
        intersection = default;
        var left = MathF.Max(a.X, b.X);
        var top = MathF.Max(a.Y, b.Y);
        var right = MathF.Min(a.Right, b.Right);
        var bottom = MathF.Min(a.Bottom, b.Bottom);
        if (right <= left || bottom <= top) return false;
        intersection = new RectangleF(left, top, right - left, bottom - top);
        return true;
    }

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
    /// <param name="a1">Início do primeiro segmento.</param>
    /// <param name="a2">Fim do primeiro segmento.</param>
    /// <param name="b1">Início do segundo segmento.</param>
    /// <param name="b2">Fim do segundo segmento.</param>
    /// <param name="point">Ponto a testar.</param>
    /// <returns>Verdadeiro se os segmentos se cruzam.</returns>
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
    /// <param name="polygon">Vértices do polígono, em ordem.</param>
    /// <param name="point">Ponto a testar.</param>
    /// <returns>Verdadeiro se o ponto está dentro do polígono.</returns>
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
    /// <param name="polygon">Vértices do polígono, em ordem.</param>
    /// <returns>Área com sinal: positiva em sentido anti-horário no plano de coordenadas.</returns>
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
    /// <param name="a">Vértices do primeiro polígono (convexo).</param>
    /// <param name="b">Vértices do segundo polígono (convexo).</param>
    /// <param name="push">Vetor mínimo que separa os polígonos quando se sobrepõem.</param>
    /// <returns>Verdadeiro se os polígonos se sobrepõem com área positiva.</returns>
    /// <remarks>Vértices finitos em ordem, polígonos convexos. Contato de borda e polígonos sem área retornam falso.
    /// O empurrão move A até o contato, inclusive quando uma forma contém a outra. Não aloca memória.</remarks>
    public static bool SatOverlap(ReadOnlySpan<Vector2> a, ReadOnlySpan<Vector2> b, out Vector2 push)
    {
        push = default;
        if (a.Length < 3 || b.Length < 3 || SignedArea(a) == 0 || SignedArea(b) == 0) return false;
        var smallest = float.MaxValue;
        var best = Vector2.Zero;
        if (!TestAxes(a, a, b, ref smallest, ref best) || !TestAxes(b, a, b, ref smallest, ref best)) return false;

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
            // A interseção dos intervalos não é a distância de saída quando há contenção.
            // Compare as duas translações que levam A até uma borda de B.
            var negative = maxA - minB;
            var positive = maxB - minA;
            if (negative <= 0 || positive <= 0) return false;
            var overlap = MathF.Min(negative, positive);
            if (overlap < smallest)
            {
                smallest = overlap;
                best = negative <= positive ? -axis : axis;
            }
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

}
