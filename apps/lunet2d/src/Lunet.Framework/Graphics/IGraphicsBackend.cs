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
    /// <summary>Estado usado pelas próximas chamadas de <see cref="DrawQuads"/> (mistura, amostragem, recorte, shader).</summary>
    void SetDrawState(DrawState state);

    /// <summary>Cria um alvo de desenho. Devolve o handle do alvo e, em <paramref name="textureHandle"/>, o da textura que recebe o desenho.</summary>
    int CreateRenderTarget(int width, int height, TextureFilter filter, out int textureHandle);

    void DeleteRenderTarget(int handle);

    /// <summary>Passa a desenhar no alvo (0 = tela) com o tamanho dado. O viewport é definido em seguida por <see cref="SetViewport"/>.</summary>
    void SetRenderTarget(int handle, int width, int height);

    /// <summary>Compila um shader de fragmento completo (com prelúdio). Lança <see cref="ShaderCompileException"/> se falhar.</summary>
    int CreateShader(string fragmentSource);

    void DeleteShader(int handle);

    void DrawQuads(int textureHandle, ReadOnlySpan<SpriteVertex> vertices, int quadCount, in Matrix4x4 projection);
}
