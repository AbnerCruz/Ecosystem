using System.Numerics;
using Lunet.Graphics;

namespace Lunet.Tests;

/// <summary>Backend em memória que registra o que o jogo desenhou.</summary>
public sealed class RecordingBackend : IGraphicsBackend
{
    public sealed record Batch(int Texture, SpriteVertex[] Vertices, int QuadCount);

    private int _nextTexture = 1;
    public HashSet<int> LiveTextures { get; } = [];
    public Dictionary<int, (int Width, int Height, byte[] Rgba, TextureFilter Filter)> Pixels { get; } = [];
    public List<Batch> Batches { get; } = [];
    public List<Color> Clears { get; } = [];
    public List<(int X, int Y, int W, int H)> Viewports { get; } = [];

    public int CreateTexture(int width, int height, ReadOnlySpan<byte> rgba, TextureFilter filter)
    {
        var handle = _nextTexture++;
        LiveTextures.Add(handle);
        Pixels[handle] = (width, height, rgba.ToArray(), filter);
        return handle;
    }

    public void DeleteTexture(int handle) => LiveTextures.Remove(handle);
    public void SetViewport(int x, int y, int width, int height) => Viewports.Add((x, y, width, height));
    public void Clear(Color color) => Clears.Add(color);

    public void DrawQuads(int textureHandle, ReadOnlySpan<SpriteVertex> vertices, int quadCount, in Matrix4x4 projection) =>
        Batches.Add(new Batch(textureHandle, vertices.ToArray(), quadCount));

    public Vector2 LastQuadCenter()
    {
        var v = Batches[^1].Vertices;
        var i = (Batches[^1].QuadCount - 1) * 4;
        return (v[i].Position + v[i + 2].Position) / 2;
    }
}
