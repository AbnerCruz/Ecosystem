using System.Numerics;

namespace Lunet.Graphics;

/// <summary>Região e métricas de uma letra, símbolo ou espaço em uma fonte bitmap.</summary>
/// <example><code>var glyph = new BitmapGlyph(new RectangleF(0, 0, 6, 10), 8, new Vector2(1, 2));</code></example>
/// <remarks>Source é validado contra a textura em SpriteFont.FromBitmap. default representa um glifo invisível sem avanço.</remarks>
public readonly struct BitmapGlyph
{
    /// <summary>Define a imagem e o avanço horizontal do glifo.</summary>
    /// <param name="source">Região em pixels; tamanho 0×0 representa espaço sem desenho.</param>
    /// <param name="advance">Deslocamento horizontal finito e não negativo até o próximo glifo.</param>
    /// <param name="offset">Deslocamento finito da imagem em relação ao cursor; pode ser negativo.</param>
    public BitmapGlyph(RectangleF source, float advance, Vector2 offset = default)
    {
        if (!float.IsFinite(advance) || advance < 0) throw new ArgumentOutOfRangeException(nameof(advance));
        if (!float.IsFinite(offset.X) || !float.IsFinite(offset.Y)) throw new ArgumentOutOfRangeException(nameof(offset));
        Source = source; Advance = advance; Offset = offset;
    }
    /// <summary>Recorte em pixels da textura.</summary>
    public RectangleF Source { get; }
    /// <summary>Avanço horizontal em unidades da fonte; zero permite marcas sobrepostas.</summary>
    public float Advance { get; }
    /// <summary>Deslocamento da imagem a partir do cursor da linha.</summary>
    public Vector2 Offset { get; }
}
