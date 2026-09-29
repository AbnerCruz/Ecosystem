namespace Lunet.Graphics;

/// <summary>Divide uma textura em quadros de tamanho igual, numerados da esquerda para a direita, de cima para baixo.</summary>
public sealed class SpriteSheet
{
    /// <summary>Divide uma textura em quadros de tamanho igual.</summary>
    /// <param name="texture">Textura.</param>
    /// <param name="frameWidth">Largura de um quadro.</param>
    /// <param name="frameHeight">Altura de um quadro.</param>
    public SpriteSheet(Texture2D texture, int frameWidth, int frameHeight)
    {
        ArgumentNullException.ThrowIfNull(texture);
        if (frameWidth < 1 || frameHeight < 1) throw new ArgumentOutOfRangeException(nameof(frameWidth), "O quadro deve ter tamanho positivo.");
        Texture = texture;
        FrameWidth = frameWidth;
        FrameHeight = frameHeight;
        Columns = texture.Width / frameWidth;
        Rows = texture.Height / frameHeight;
        if (Columns == 0 || Rows == 0) throw new ArgumentException("O quadro é maior que a textura.", nameof(frameWidth));
    }

    /// <summary>Textura dividida.</summary>
    public Texture2D Texture { get; }
    /// <summary>Largura de um quadro.</summary>
    public int FrameWidth { get; }
    /// <summary>Altura de um quadro.</summary>
    public int FrameHeight { get; }
    /// <summary>Quadros por linha.</summary>
    public int Columns { get; }
    /// <summary>Quantidade de linhas.</summary>
    public int Rows { get; }
    /// <summary>Total de quadros.</summary>
    public int FrameCount => Columns * Rows;

    /// <summary>Região de um quadro, numerados da esquerda para a direita e de cima para baixo.</summary>
    /// <param name="index">Número do quadro.</param>
    /// <returns>Retângulo do quadro na textura.</returns>
    public RectangleF Frame(int index)
    {
        if ((uint)index >= (uint)FrameCount) throw new ArgumentOutOfRangeException(nameof(index));
        return new RectangleF(index % Columns * FrameWidth, index / Columns * FrameHeight, FrameWidth, FrameHeight);
    }
}
