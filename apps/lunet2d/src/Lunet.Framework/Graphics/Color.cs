namespace Lunet.Graphics;

/// <summary>Cor RGBA de 8 bits por canal, não pré-multiplicada.</summary>
/// <example>
/// <code>
/// var orange = Color.FromHex(0xFF8800);
/// var faded = orange.WithAlpha(128);
/// device.Clear(Color.CornflowerBlue);
/// </code>
/// </example>
public readonly struct Color : IEquatable<Color>
{
    /// <summary>Cria uma cor a partir dos canais de 0 a 255.</summary>
    /// <param name="r">Vermelho.</param>
    /// <param name="g">Verde.</param>
    /// <param name="b">Azul.</param>
    /// <param name="a">Opacidade (255 = opaco).</param>
    public Color(byte r, byte g, byte b, byte a = 255)
    {
        R = r; G = g; B = b; A = a;
    }

    /// <summary>Canal vermelho, de 0 a 255.</summary>
    public byte R { get; }
    /// <summary>Canal verde, de 0 a 255.</summary>
    public byte G { get; }
    /// <summary>Canal azul, de 0 a 255.</summary>
    public byte B { get; }
    /// <summary>Opacidade, de 0 (transparente) a 255 (opaco).</summary>
    public byte A { get; }

    /// <summary>Valor empacotado como 0xAABBGGRR (ordem de bytes R,G,B,A na memória little-endian).</summary>
    public uint PackedRgba => (uint)(R | G << 8 | B << 16 | A << 24);

    /// <summary>Devolve a mesma cor com outra opacidade.</summary>
    /// <param name="alpha">Nova opacidade, de 0 a 255.</param>
    /// <returns>Cópia com a opacidade trocada.</returns>
    public Color WithAlpha(byte alpha) => new(R, G, B, alpha);

    /// <summary>Cria uma cor opaca a partir de um valor 0xRRGGBB.</summary>
    /// <param name="rgb">Valor hexadecimal, por exemplo 0xFF8800.</param>
    /// <returns>A cor correspondente.</returns>
    public static Color FromHex(uint rgb) => new((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);

    /// <summary>Totalmente transparente.</summary>
    public static readonly Color Transparent = new(0, 0, 0, 0);
    /// <summary>Preto.</summary>
    public static readonly Color Black = new(0, 0, 0);
    /// <summary>Branco.</summary>
    public static readonly Color White = new(255, 255, 255);
    /// <summary>Vermelho.</summary>
    public static readonly Color Red = new(230, 57, 70);
    /// <summary>Verde.</summary>
    public static readonly Color Green = new(76, 175, 80);
    /// <summary>Azul.</summary>
    public static readonly Color Blue = new(66, 133, 244);
    /// <summary>Amarelo.</summary>
    public static readonly Color Yellow = new(255, 214, 10);
    /// <summary>Azul centáurea, o azul clássico de fundo.</summary>
    public static readonly Color CornflowerBlue = new(100, 149, 237);

    public bool Equals(Color other) => PackedRgba == other.PackedRgba;
    public override bool Equals(object? obj) => obj is Color c && Equals(c);
    public override int GetHashCode() => (int)PackedRgba;
    public static bool operator ==(Color a, Color b) => a.Equals(b);
    public static bool operator !=(Color a, Color b) => !a.Equals(b);
}
