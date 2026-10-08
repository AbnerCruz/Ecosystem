using System.Numerics;

namespace Lunet.Graphics;

/// <summary>Região de textura dividida em nove partes para redimensionar painéis preservando os cantos.</summary>
/// <example><code>
/// var panel = new NineSlice(texture, new RectangleF(0, 0, 24, 24), 6, 6, 6, 6);
/// batch.Begin();
/// batch.Draw(panel, new RectangleF(20, 40, 200, 80), Color.White);
/// batch.End();
/// </code></example>
/// <remarks>Imutável; não possui nem libera a textura. Centro e bordas esticam, sem repetição.
/// Não calcula layout, padding de conteúdo ou hit testing.</remarks>
public sealed class NineSlice
{
    /// <summary>Define a região e as bordas em pixels da imagem.</summary>
    /// <param name="texture">Textura viva, mantida pelo jogo.</param>
    /// <param name="source">Região positiva e finita dentro da textura, inclusive em atlas.</param>
    /// <param name="left">Largura não negativa da borda esquerda.</param>
    /// <param name="top">Altura não negativa da borda superior.</param>
    /// <param name="right">Largura não negativa da borda direita.</param>
    /// <param name="bottom">Altura não negativa da borda inferior.</param>
    /// <remarks>As somas das bordas devem deixar um centro com largura e altura positivas.</remarks>
    public NineSlice(Texture2D texture, RectangleF source, float left, float top, float right, float bottom)
    {
        ArgumentNullException.ThrowIfNull(texture);
        if (texture.IsDisposed) throw new ObjectDisposedException(nameof(Texture2D));
        if (!float.IsFinite(source.X) || !float.IsFinite(source.Y)
            || !float.IsFinite(source.Width) || !float.IsFinite(source.Height)
            || source.X < 0 || source.Y < 0 || source.Width <= 0 || source.Height <= 0
            || (double)source.X + source.Width > texture.Width || (double)source.Y + source.Height > texture.Height)
            throw new ArgumentOutOfRangeException(nameof(source));
        CheckBorder(left, nameof(left)); CheckBorder(top, nameof(top));
        CheckBorder(right, nameof(right)); CheckBorder(bottom, nameof(bottom));
        if ((double)left + right >= source.Width || (double)top + bottom >= source.Height)
            throw new ArgumentException("As bordas precisam deixar um centro positivo.");
        Texture = texture; Source = source;
        Left = left; Top = top; Right = right; Bottom = bottom;
    }

    /// <summary>Textura compartilhada; o jogo controla sua vida.</summary>
    public Texture2D Texture { get; }
    /// <summary>Região em pixels da textura.</summary>
    public RectangleF Source { get; }
    /// <summary>Borda esquerda em pixels da imagem.</summary>
    public float Left { get; }
    /// <summary>Borda superior em pixels da imagem.</summary>
    public float Top { get; }
    /// <summary>Borda direita em pixels da imagem.</summary>
    public float Right { get; }
    /// <summary>Borda inferior em pixels da imagem.</summary>
    public float Bottom { get; }

    private static void CheckBorder(float value, string name)
    {
        if (!float.IsFinite(value) || value < 0) throw new ArgumentOutOfRangeException(name);
    }

    internal void Draw(SpriteBatch batch, RectangleF destination, Color color, float borderScale)
    {
        if (Texture.IsDisposed) throw new ObjectDisposedException(nameof(Texture2D));
        if (!float.IsFinite(destination.X) || !float.IsFinite(destination.Y)
            || !float.IsFinite(destination.Width) || !float.IsFinite(destination.Height)
            || destination.Width < 0 || destination.Height < 0
            || !float.IsFinite(destination.Right) || !float.IsFinite(destination.Bottom))
            throw new ArgumentOutOfRangeException(nameof(destination));
        if (!float.IsFinite(borderScale) || borderScale <= 0) throw new ArgumentOutOfRangeException(nameof(borderScale));
        if (destination.Width == 0 || destination.Height == 0) return;

        // Double evita overflow nas bordas escaladas; compressão independente em cada eixo.
        double horizontal = Math.Min(borderScale, (double)destination.Width / ((double)Left + Right));
        double vertical = Math.Min(borderScale, (double)destination.Height / ((double)Top + Bottom));
        Span<float> sx = stackalloc float[4] { Source.X, Source.X + Left, Source.Right - Right, Source.Right };
        Span<float> sy = stackalloc float[4] { Source.Y, Source.Y + Top, Source.Bottom - Bottom, Source.Bottom };
        Span<float> dx = stackalloc float[4] { destination.X, (float)(destination.X + Left * horizontal),
            (float)((double)destination.Right - Right * horizontal), destination.Right };
        Span<float> dy = stackalloc float[4] { destination.Y, (float)(destination.Y + Top * vertical),
            (float)((double)destination.Bottom - Bottom * vertical), destination.Bottom };
        // Arredondamento nos limites não pode inverter a faixa central comprimida.
        dx[2] = Math.Max(dx[1], dx[2]); dy[2] = Math.Max(dy[1], dy[2]);
        for (int y = 0; y < 3; y++)
        for (int x = 0; x < 3; x++)
        {
            var src = new RectangleF(sx[x], sy[y], sx[x + 1] - sx[x], sy[y + 1] - sy[y]);
            var dst = new RectangleF(dx[x], dy[y], dx[x + 1] - dx[x], dy[y + 1] - dy[y]);
            if (src.Width <= 0 || src.Height <= 0 || dst.Width <= 0 || dst.Height <= 0) continue;
            batch.Draw(Texture, dst, src, color, 0, Vector2.Zero);
        }
    }
}
