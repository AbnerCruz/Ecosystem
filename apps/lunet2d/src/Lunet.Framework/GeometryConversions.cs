using System.Numerics;

namespace Lunet;

/// <summary>Conversões explícitas dos tipos geométricos inteiros para o espaço contínuo.</summary>
public static class GeometryConversions
{
    /// <summary>Converte uma posição inteira para um vetor de ponto flutuante.</summary>
    /// <param name="point">Posição inteira.</param>
    /// <returns>Vetor com X e Y; inteiros grandes podem perder precisão.</returns>
    public static Vector2 ToVector2(this System.Drawing.Point point) => new(point.X, point.Y);

    /// <summary>Converte dimensões inteiras para um vetor de ponto flutuante.</summary>
    /// <param name="size">Largura e altura inteiras.</param>
    /// <returns>Vetor com largura e altura; inteiros grandes podem perder precisão.</returns>
    public static Vector2 ToVector2(this System.Drawing.Size size) => new(size.Width, size.Height);
}
