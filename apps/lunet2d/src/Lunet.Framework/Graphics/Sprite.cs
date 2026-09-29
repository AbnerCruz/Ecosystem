using System.Numerics;

namespace Lunet.Graphics;

/// <summary>Desenhável reutilizável: textura (ou região dela), cor, origem e escala. Facilita desenhar o mesmo sprite muitas vezes.</summary>
public sealed class Sprite
{
    public Sprite(Texture2D texture, RectangleF? source = null, Vector2? origin = null)
    {
        Texture = texture ?? throw new ArgumentNullException(nameof(texture));
        Source = source ?? new RectangleF(0, 0, texture.Width, texture.Height);
        Origin = origin ?? new Vector2(Source.Width * 0.5f, Source.Height * 0.5f);
    }

    public Texture2D Texture { get; }

    /// <summary>Região da textura, em pixels.</summary>
    public RectangleF Source { get; }

    /// <summary>Ponto (em pixels da região) que fica sobre a posição de desenho e em torno do qual gira. Padrão: centro.</summary>
    public Vector2 Origin { get; set; }

    public Color Color { get; set; } = Color.White;

    public Vector2 Scale { get; set; } = Vector2.One;

    public float Rotation { get; set; }

    public static Sprite FromAtlas(TextureAtlas atlas, string region)
    {
        var r = atlas[region];
        return new Sprite(atlas.Texture, r.Bounds, new Vector2(r.Bounds.Width * r.PivotX, r.Bounds.Height * r.PivotY));
    }
}
