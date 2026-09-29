using System.Numerics;
using System.Runtime.InteropServices;

namespace Lunet.Graphics;

/// <summary>Vértice de sprite: 20 bytes (posição, UV, cor RGBA empacotada).</summary>
[StructLayout(LayoutKind.Sequential)]
public struct SpriteVertex
{
    public Vector2 Position;
    public Vector2 TexCoord;
    public uint Color;

    public const int SizeInBytes = 20;
}
