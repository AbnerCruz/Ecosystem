using System.Numerics;

namespace Lunet.Graphics;

/// <summary>Desenho de depuração: linhas, contornos e preenchimentos com a textura branca do dispositivo.</summary>
public static class DebugDraw
{
    /// <summary>Desenha uma linha.</summary>
    /// <param name="batch">Lote em uso (entre Begin e End).</param>
    /// <param name="from">Início.</param>
    /// <param name="to">Fim.</param>
    /// <param name="color">Cor.</param>
    /// <param name="thickness">Espessura.</param>
    public static void Line(this SpriteBatch batch, Vector2 from, Vector2 to, Color color, float thickness = 1f)
    {
        ArgumentNullException.ThrowIfNull(batch);
        var delta = to - from;
        var length = delta.Length();
        if (length <= 0) return;
        var angle = MathF.Atan2(delta.Y, delta.X);
        // Textura 1×1: destino = comprimento × espessura, centrado em "from"; gira em torno de "from" (origem no meio da espessura).
        batch.Draw(batch.GraphicsDevice.WhiteTexture, new RectangleF(from.X, from.Y - thickness * 0.5f, length, thickness), null, color, angle, new Vector2(0f, 0.5f));
    }

    /// <summary>Desenha um retângulo preenchido.</summary>
    /// <param name="batch">Lote em uso.</param>
    /// <param name="rect">Retângulo.</param>
    /// <param name="color">Cor.</param>
    public static void FillRect(this SpriteBatch batch, RectangleF rect, Color color) =>
        batch.Draw(batch.GraphicsDevice.WhiteTexture, rect, null, color, 0f, Vector2.Zero);

    /// <summary>Desenha o contorno de um retângulo.</summary>
    /// <param name="batch">Lote em uso.</param>
    /// <param name="rect">Retângulo.</param>
    /// <param name="color">Cor.</param>
    /// <param name="thickness">Espessura.</param>
    public static void Rect(this SpriteBatch batch, RectangleF rect, Color color, float thickness = 1f)
    {
        var t = thickness;
        batch.FillRect(new RectangleF(rect.X, rect.Y, rect.Width, t), color);
        batch.FillRect(new RectangleF(rect.X, rect.Bottom - t, rect.Width, t), color);
        batch.FillRect(new RectangleF(rect.X, rect.Y + t, t, rect.Height - 2 * t), color);
        batch.FillRect(new RectangleF(rect.Right - t, rect.Y + t, t, rect.Height - 2 * t), color);
    }

    /// <summary>Desenha o contorno de um círculo com segmentos de reta.</summary>
    /// <param name="batch">Lote em uso.</param>
    /// <param name="center">Centro.</param>
    /// <param name="radius">Raio.</param>
    /// <param name="color">Cor.</param>
    /// <param name="thickness">Espessura.</param>
    /// <param name="segments">Quantidade de lados.</param>
    public static void Circle(this SpriteBatch batch, Vector2 center, float radius, Color color, float thickness = 1f, int segments = 24)
    {
        segments = Math.Max(3, segments);
        var previous = center + new Vector2(radius, 0);
        for (var i = 1; i <= segments; i++)
        {
            var angle = MathF.Tau * i / segments;
            var next = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
            batch.Line(previous, next, color, thickness);
            previous = next;
        }
    }

    /// <summary>Desenha uma cruz.</summary>
    /// <param name="batch">Lote em uso.</param>
    /// <param name="center">Centro.</param>
    /// <param name="size">Meia largura.</param>
    /// <param name="color">Cor.</param>
    /// <param name="thickness">Espessura.</param>
    public static void Cross(this SpriteBatch batch, Vector2 center, float size, Color color, float thickness = 1f)
    {
        batch.Line(center - new Vector2(size, 0), center + new Vector2(size, 0), color, thickness);
        batch.Line(center - new Vector2(0, size), center + new Vector2(0, size), color, thickness);
    }
}
