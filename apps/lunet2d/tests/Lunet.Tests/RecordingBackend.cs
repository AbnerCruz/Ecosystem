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
    public List<DrawState> States { get; } = [];
    public List<(int Handle, int Width, int Height, int Texture)> RenderTargets { get; } = [];
    public List<(int Handle, int Width, int Height)> TargetSwitches { get; } = [];
    public List<string> Shaders { get; } = [];
    public HashSet<int> LiveShaders { get; } = [];
    public HashSet<int> LiveTargets { get; } = [];
    private int _nextTarget = 100;
    private int _nextShader = 200;
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

    public void SetDrawState(DrawState state) => States.Add(state);

    public int CreateRenderTarget(int width, int height, TextureFilter filter, out int textureHandle)
    {
        textureHandle = CreateTexture(width, height, new byte[width * height * 4], filter);
        var handle = _nextTarget++;
        RenderTargets.Add((handle, width, height, textureHandle));
        LiveTargets.Add(handle);
        return handle;
    }

    public void DeleteRenderTarget(int handle) => LiveTargets.Remove(handle);
    public void SetRenderTarget(int handle, int width, int height) => TargetSwitches.Add((handle, width, height));

    public int CreateShader(string fragmentSource)
    {
        if (fragmentSource.Contains("SYNTAX_ERROR")) throw new ShaderCompileException("0:5: syntax error");
        Shaders.Add(fragmentSource);
        var handle = _nextShader++;
        LiveShaders.Add(handle);
        return handle;
    }

    public void DeleteShader(int handle) => LiveShaders.Remove(handle);

    public void DrawQuads(int textureHandle, ReadOnlySpan<SpriteVertex> vertices, int quadCount, in Matrix4x4 projection) =>
        Batches.Add(new Batch(textureHandle, vertices.ToArray(), quadCount));

    public Vector2 LastQuadCenter()
    {
        var v = Batches[^1].Vertices;
        var i = (Batches[^1].QuadCount - 1) * 4;
        return (v[i].Position + v[i + 2].Position) / 2;
    }
}
