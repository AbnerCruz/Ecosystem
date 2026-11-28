using System.Numerics;
using Lunet.Graphics;
using Point = System.Drawing.Point;
using Rectangle = System.Drawing.Rectangle;

namespace Lunet;

public sealed partial class RandomSource
{
    /// <summary>Sorteia as coordenadas de um ponto com os mesmos limites em ambos os eixos.</summary>
    /// <param name="min">Limite inferior incluído.</param>
    /// <param name="maxExclusive">Limite superior excluído.</param>
    /// <returns>Ponto sorteado.</returns>
    public Point NextPoint(int min, int maxExclusive) => NextPoint(new Point(min, min), new Point(maxExclusive, maxExclusive));

    /// <summary>Sorteia X e depois Y com limites por eixo.</summary>
    /// <param name="min">Limites inferiores incluídos.</param>
    /// <param name="maxExclusive">Limites superiores excluídos.</param>
    /// <returns>Ponto sorteado.</returns>
    public Point NextPoint(Point min, Point maxExclusive)
    {
        if (maxExclusive.X < min.X || maxExclusive.Y < min.Y) throw new ArgumentOutOfRangeException(nameof(maxExclusive));
        return new(NextInt(min.X, maxExclusive.X), NextInt(min.Y, maxExclusive.Y));
    }

    /// <summary>Sorteia as componentes de um vetor com os mesmos limites em ambos os eixos.</summary>
    /// <param name="min">Limite inferior incluído.</param>
    /// <param name="maxExclusive">Limite superior excluído.</param>
    /// <returns>Vetor sorteado.</returns>
    public Vector2 NextVector2(float min, float maxExclusive) => NextVector2(new Vector2(min), new Vector2(maxExclusive));

    /// <summary>Sorteia X e depois Y com limites por eixo.</summary>
    /// <param name="min">Limites inferiores incluídos.</param>
    /// <param name="maxExclusive">Limites superiores excluídos.</param>
    /// <returns>Vetor sorteado.</returns>
    public Vector2 NextVector2(Vector2 min, Vector2 maxExclusive)
    {
        ValidateRange(min.X, maxExclusive.X);
        ValidateRange(min.Y, maxExclusive.Y);
        return new(NextFloat(min.X, maxExclusive.X), NextFloat(min.Y, maxExclusive.Y));
    }

    /// <summary>Sorteia direção com ângulo uniforme.</summary>
    /// <returns>Vetor de comprimento aproximadamente unitário.</returns>
    public Vector2 NextDirection() => CirclePoint(Vector2.Zero, 1);

    /// <summary>Sorteia um ponto uniforme pela área de um círculo na origem.</summary>
    /// <param name="radius">Raio finito e não negativo.</param>
    /// <returns>Posição sorteada no círculo.</returns>
    public Vector2 InsideCircle(float radius = 1f) => InsideCircle(Vector2.Zero, radius);

    /// <summary>Sorteia um ponto uniforme pela área do círculo.</summary>
    /// <param name="center">Centro finito do círculo.</param>
    /// <param name="radius">Raio finito e não negativo.</param>
    /// <returns>Posição sorteada, ou centro para raio zero.</returns>
    public Vector2 InsideCircle(Vector2 center, float radius)
    {
        ValidateCircle(center, radius);
        return radius == 0 ? center : CirclePoint(center, radius * Math.Sqrt(NextDouble()));
    }

    /// <summary>Sorteia um ponto no contorno do círculo na origem.</summary>
    /// <param name="radius">Raio finito e não negativo.</param>
    /// <returns>Posição no contorno.</returns>
    public Vector2 OnCircle(float radius = 1f) => OnCircle(Vector2.Zero, radius);

    /// <summary>Sorteia um ponto no contorno do círculo.</summary>
    /// <param name="center">Centro finito do círculo.</param>
    /// <param name="radius">Raio finito e não negativo.</param>
    /// <returns>Posição no contorno, ou centro para raio zero.</returns>
    public Vector2 OnCircle(Vector2 center, float radius)
    {
        ValidateCircle(center, radius);
        return radius == 0 ? center : CirclePoint(center, radius);
    }

    private Vector2 CirclePoint(Vector2 center, double radius)
    {
        var (sin, cos) = Math.SinCos(Math.Tau * NextDouble());
        return new((float)(center.X + cos * radius), (float)(center.Y + sin * radius));
    }

    private static void ValidateCircle(Vector2 center, float radius)
    {
        if (!float.IsFinite(center.X) || !float.IsFinite(center.Y)) throw new ArgumentOutOfRangeException(nameof(center));
        if (!float.IsFinite(radius) || radius < 0 || radius > (double)float.MaxValue - Math.Max(Math.Abs((double)center.X), Math.Abs((double)center.Y)))
            throw new ArgumentOutOfRangeException(nameof(radius));
    }

    /// <summary>Sorteia posição inteira desde a origem, com extremidades direita e inferior excluídas.</summary>
    /// <param name="rect">Retângulo de dimensões positivas.</param>
    /// <returns>Posição inteira no retângulo.</returns>
    public Point InsideRectangle(Rectangle rect)
    {
        var right = (long)rect.X + rect.Width;
        var bottom = (long)rect.Y + rect.Height;
        if (rect.Width <= 0 || rect.Height <= 0 || right > (long)int.MaxValue + 1 || bottom > (long)int.MaxValue + 1)
            throw new ArgumentOutOfRangeException(nameof(rect));
        return new((int)NextInt64(rect.X, right), (int)NextInt64(rect.Y, bottom));
    }

    /// <summary>Sorteia posição real desde a origem, com extremidades direita e inferior excluídas.</summary>
    /// <param name="rect">Retângulo de dimensões positivas e extremidades finitas.</param>
    /// <returns>Posição real no retângulo.</returns>
    public Vector2 InsideRectangle(RectangleF rect)
    {
        var right = (float)((double)rect.X + rect.Width);
        var bottom = (float)((double)rect.Y + rect.Height);
        if (!(right > rect.X && bottom > rect.Y) || !float.IsFinite(right) || !float.IsFinite(bottom))
            throw new ArgumentOutOfRangeException(nameof(rect));
        return NextVector2(rect.Position, new Vector2(right, bottom));
    }

    /// <summary>Sorteia uma cor opaca usando os três bytes superiores da palavra.</summary>
    /// <returns>Cor RGB uniforme por canal.</returns>
    public Color NextColor() => NextColor(255);

    /// <summary>Sorteia uma cor usando os três bytes superiores da palavra, na ordem RGB.</summary>
    /// <param name="alpha">Alfa de 0 a 255.</param>
    /// <returns>Cor RGB uniforme por canal com alfa fixo.</returns>
    public Color NextColor(byte alpha)
    {
        var word = NextUInt64();
        return unchecked(new Color((byte)(word >> 56), (byte)(word >> 48), (byte)(word >> 40), alpha));
    }
}
