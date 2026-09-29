using System.Numerics;

namespace Lunet.Graphics;

/// <summary>
/// Contrato entre o framework e a API gráfica (OpenGL ES no Android, um coletor em memória nos testes).
/// Cada quad ocupa 4 vértices consecutivos (topo-esq, topo-dir, base-dir, base-esq).
/// </summary>
public interface IGraphicsBackend
{
    int CreateTexture(int width, int height, ReadOnlySpan<byte> rgba, TextureFilter filter);
    void DeleteTexture(int handle);
    void SetViewport(int x, int y, int width, int height);
    void Clear(Color color);
    void DrawQuads(int textureHandle, ReadOnlySpan<SpriteVertex> vertices, int quadCount, in Matrix4x4 projection);
}
