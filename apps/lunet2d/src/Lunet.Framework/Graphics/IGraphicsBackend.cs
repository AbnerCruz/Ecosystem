using System.Numerics;

namespace Lunet.Graphics;

/// <summary>
/// Contrato entre o framework e a API gráfica (OpenGL ES no Android, um coletor em memória nos testes).
/// Cada quad ocupa 4 vértices consecutivos (topo-esq, topo-dir, base-dir, base-esq).
/// </summary>
/// <example>
/// <code>
/// int handle = backend.CreateTexture(1, 1, new byte[] { 255, 255, 255, 255 }, TextureFilter.Point);
/// backend.DeleteTexture(handle);
/// </code>
/// </example>
public interface IGraphicsBackend
{
    /// <summary>Cria uma textura na GPU.</summary>
    /// <param name="width">Largura.</param>
    /// <param name="height">Altura.</param>
    /// <param name="rgba">Pixels RGBA.</param>
    /// <param name="filter">Filtro.</param>
    /// <returns>Handle da textura.</returns>
    int CreateTexture(int width, int height, ReadOnlySpan<byte> rgba, TextureFilter filter);
    /// <summary>Libera uma textura.</summary>
    /// <param name="handle">Handle da textura.</param>
    void DeleteTexture(int handle);
    /// <summary>Define a área da tela onde se desenha.</summary>
    /// <param name="x">Esquerda.</param>
    /// <param name="y">Base.</param>
    /// <param name="width">Largura.</param>
    /// <param name="height">Altura.</param>
    void SetViewport(int x, int y, int width, int height);
    /// <summary>Limpa a área de desenho.</summary>
    /// <param name="color">Cor de fundo.</param>
    void Clear(Color color);
    /// <summary>Estado usado pelas próximas chamadas de <see cref="DrawQuads"/> (mistura, amostragem, recorte, shader).</summary>
    /// <param name="state">Estado de mistura, amostragem, recorte e shader para os próximos desenhos.</param>
    void SetDrawState(DrawState state);

    /// <summary>Cria um alvo de desenho. Devolve o handle do alvo e, em <paramref name="textureHandle"/>, o da textura que recebe o desenho.</summary>
    /// <param name="width">Largura, em pixels.</param>
    /// <param name="height">Altura, em pixels.</param>
    /// <param name="filter">Filtro de amostragem: `Point` (pixels nítidos) ou `Linear` (suave).</param>
    /// <param name="textureHandle">Recebe o identificador da textura do alvo.</param>
    /// <returns>Identificador do alvo de desenho.</returns>
    int CreateRenderTarget(int width, int height, TextureFilter filter, out int textureHandle);

    /// <summary>Libera um alvo de desenho.</summary>
    /// <param name="handle">Handle do alvo.</param>
    void DeleteRenderTarget(int handle);

    /// <summary>Passa a desenhar no alvo (0 = tela) com o tamanho dado. O viewport é definido em seguida por <see cref="SetViewport"/>.</summary>
    /// <param name="handle">Identificador do alvo (0 é a tela).</param>
    /// <param name="width">Largura, em pixels.</param>
    /// <param name="height">Altura, em pixels.</param>
    void SetRenderTarget(int handle, int width, int height);

    /// <summary>Compila um shader de fragmento completo (com prelúdio). Lança <see cref="ShaderCompileException"/> se falhar.</summary>
    /// <param name="fragmentSource">Código GLSL do fragment shader.</param>
    /// <returns>Identificador do shader.</returns>
    int CreateShader(string fragmentSource);

    /// <summary>Libera um shader.</summary>
    /// <param name="handle">Handle do shader.</param>
    void DeleteShader(int handle);

    /// <summary>Desenha quads com a textura.</summary>
    /// <param name="textureHandle">Textura.</param>
    /// <param name="vertices">Quatro vértices por quad.</param>
    /// <param name="quadCount">Quantidade de quads.</param>
    /// <param name="projection">Matriz de projeção.</param>
    void DrawQuads(int textureHandle, ReadOnlySpan<SpriteVertex> vertices, int quadCount, in Matrix4x4 projection);
}
