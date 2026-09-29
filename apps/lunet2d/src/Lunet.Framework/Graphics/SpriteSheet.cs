namespace Lunet.Graphics;

/// <summary>Divide uma textura em quadros de tamanho igual, numerados da esquerda para a direita, de cima para baixo.</summary>
public sealed class SpriteSheet
{
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

    public Texture2D Texture { get; }
    public int FrameWidth { get; }
    public int FrameHeight { get; }
    public int Columns { get; }
    public int Rows { get; }
    public int FrameCount => Columns * Rows;

    public RectangleF Frame(int index)
    {
        if ((uint)index >= (uint)FrameCount) throw new ArgumentOutOfRangeException(nameof(index));
        return new RectangleF(index % Columns * FrameWidth, index / Columns * FrameHeight, FrameWidth, FrameHeight);
    }
}
