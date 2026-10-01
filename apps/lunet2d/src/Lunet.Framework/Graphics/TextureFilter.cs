namespace Lunet.Graphics;

/// <summary>Filtro usado ao ampliar ou reduzir uma textura.</summary>
public enum TextureFilter
{
    /// <summary>Suavizado; bom para arte em alta resolução.</summary>
    Linear,

    /// <summary>Pixels nítidos; use para pixel art e fontes bitmap.</summary>
    Point,
}
