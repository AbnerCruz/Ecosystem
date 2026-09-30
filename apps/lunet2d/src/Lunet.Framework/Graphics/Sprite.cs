using System.Numerics;

namespace Lunet.Graphics;

/// <summary>Desenhável reutilizável: textura (ou região dela), cor, origem e escala. Facilita desenhar o mesmo sprite muitas vezes.</summary>
/// <example>
/// <code>
/// var sprite = new Sprite(texture) { Color = Color.White, Scale = new Vector2(2, 2) };
/// batch.Draw(sprite, position);
/// </code>
/// </example>
public sealed class Sprite
{
    /// <summary>Cria um sprite.</summary>
    /// <param name="texture">Textura.</param>
    /// <param name="source">Região da textura em pixels</param>
    /// <param name="origin">Pivô em pixels da região</param>
    public Sprite(Texture2D texture, RectangleF? source = null, Vector2? origin = null)
    {
        Texture = texture ?? throw new ArgumentNullException(nameof(texture));
        Source = source ?? new RectangleF(0, 0, texture.Width, texture.Height);
        Origin = origin ?? new Vector2(Source.Width * 0.5f, Source.Height * 0.5f);
    }

    /// <summary>Textura do sprite.</summary>
    public Texture2D Texture { get; }

    /// <summary>Região da textura, em pixels.</summary>
    public RectangleF Source { get; }

    /// <summary>Ponto (em pixels da região) que fica sobre a posição de desenho e em torno do qual gira. Padrão: centro.</summary>
    public Vector2 Origin { get; set; }

    /// <summary>Cor de multiplicação (branco = sem alteração).</summary>
    public Color Color { get; set; } = Color.White;

    /// <summary>Escala em cada eixo.</summary>
    public Vector2 Scale { get; set; } = Vector2.One;

    /// <summary>Rotação em radianos em torno da origem.</summary>
    public float Rotation { get; set; }

    /// <summary>Cria um sprite a partir de uma região de atlas, com o pivô da região.</summary>
    /// <param name="atlas">Atlas.</param>
    /// <param name="region">Nome da região.</param>
    /// <returns>O sprite.</returns>
    public static Sprite FromAtlas(TextureAtlas atlas, string region)
    {
        var r = atlas[region];
        return new Sprite(atlas.Texture, r.Bounds, new Vector2(r.Bounds.Width * r.PivotX, r.Bounds.Height * r.PivotY));
    }
}
