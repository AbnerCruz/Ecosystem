using System.Numerics;
using System.Runtime.InteropServices;

namespace Lunet.Graphics;

/// <summary>Vértice de sprite: 20 bytes (posição, UV, cor RGBA empacotada).</summary>
[StructLayout(LayoutKind.Sequential)]
public struct SpriteVertex
{
    /// <summary>Posição em coordenadas de desenho.</summary>
    public Vector2 Position;
    /// <summary>Coordenada de textura de 0 a 1.</summary>
    public Vector2 TexCoord;
    /// <summary>Cor RGBA empacotada.</summary>
    public uint Color;

    /// <summary>Tamanho de um vértice, em bytes.</summary>
    public const int SizeInBytes = 20;
}
