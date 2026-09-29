namespace Lunet.Graphics;

/// <summary>Cor RGBA de 8 bits por canal, não pré-multiplicada.</summary>
public readonly struct Color : IEquatable<Color>
{
    public Color(byte r, byte g, byte b, byte a = 255)
    {
        R = r; G = g; B = b; A = a;
    }

    public byte R { get; }
    public byte G { get; }
    public byte B { get; }
    public byte A { get; }

    /// <summary>Valor empacotado como 0xAABBGGRR (ordem de bytes R,G,B,A na memória little-endian).</summary>
    public uint PackedRgba => (uint)(R | G << 8 | B << 16 | A << 24);

    public Color WithAlpha(byte alpha) => new(R, G, B, alpha);

    public static Color FromHex(uint rgb) => new((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);

    public static readonly Color Transparent = new(0, 0, 0, 0);
    public static readonly Color Black = new(0, 0, 0);
    public static readonly Color White = new(255, 255, 255);
    public static readonly Color Red = new(230, 57, 70);
    public static readonly Color Green = new(76, 175, 80);
    public static readonly Color Blue = new(66, 133, 244);
    public static readonly Color Yellow = new(255, 214, 10);
    public static readonly Color CornflowerBlue = new(100, 149, 237);

    public bool Equals(Color other) => PackedRgba == other.PackedRgba;
    public override bool Equals(object? obj) => obj is Color c && Equals(c);
    public override int GetHashCode() => (int)PackedRgba;
    public static bool operator ==(Color a, Color b) => a.Equals(b);
    public static bool operator !=(Color a, Color b) => !a.Equals(b);
}
