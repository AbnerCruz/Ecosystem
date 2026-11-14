using System.Numerics;

namespace Lunet.Graphics;

/// <summary>
/// Backend gráfico sem tela nem GPU: texturas, alvos e shaders recebem identificadores válidos e o desenho é só contado.
/// Serve ao servidor dedicado (<c>lunet-server</c>) e a testes automáticos; o jogo roda o mesmo código e pode consultar
/// <see cref="Game.IsHeadless"/> para pular o que só importa na tela.
/// </summary>
/// <example>
/// <code>
/// // No jogo: o mesmo código roda no aparelho e no servidor sem tela.
/// if (IsHeadless) Log.Info("rodando como servidor");
/// // Num teste ou ferramenta: new GameHost(jogo, new HeadlessGraphicsBackend()) roda sem GPU.
/// var semTela = new HeadlessGraphicsBackend();
/// Log.Info("quads desenhados: " + semTela.QuadsDrawn);
/// </code>
/// </example>
public sealed class HeadlessGraphicsBackend : IGraphicsBackend
{
    private int _next = 1;

    /// <summary>Quantos quads o jogo pediu para desenhar desde o início (sinal de vida barato nos logs do servidor).</summary>
    public long QuadsDrawn { get; private set; }
    /// <summary>Quantas chamadas de desenho o jogo fez desde o início.</summary>
    public long DrawCalls { get; private set; }
    /// <summary>Texturas criadas e ainda não liberadas.</summary>
    public int LiveTextures { get; private set; }

    /// <summary>Registra uma textura sem enviar pixels a uma GPU.</summary>
    /// <param name="width">Largura.</param>
    /// <param name="height">Altura.</param>
    /// <param name="rgba">Pixels RGBA (ignorados).</param>
    /// <param name="filter">Filtro (ignorado).</param>
    /// <returns>Identificador da textura.</returns>
    public int CreateTexture(int width, int height, ReadOnlySpan<byte> rgba, TextureFilter filter) { LiveTextures++; return _next++; }

    /// <summary>Libera uma textura.</summary>
    /// <param name="handle">Identificador da textura.</param>
    public void DeleteTexture(int handle) => LiveTextures = Math.Max(0, LiveTextures - 1);

    /// <summary>Não faz nada: não há tela.</summary>
    /// <param name="x">Esquerda.</param>
    /// <param name="y">Base.</param>
    /// <param name="width">Largura.</param>
    /// <param name="height">Altura.</param>
    public void SetViewport(int x, int y, int width, int height) { }

    /// <summary>Não faz nada: não há tela.</summary>
    /// <param name="color">Cor de fundo (ignorada).</param>
    public void Clear(Color color) { }

    /// <summary>Não faz nada: não há tela.</summary>
    /// <param name="state">Estado de desenho (ignorado).</param>
    public void SetDrawState(DrawState state) { }

    /// <summary>Registra um alvo de desenho e sua textura.</summary>
    /// <param name="width">Largura, em pixels.</param>
    /// <param name="height">Altura, em pixels.</param>
    /// <param name="filter">Filtro (ignorado).</param>
    /// <param name="textureHandle">Recebe o identificador da textura do alvo.</param>
    /// <returns>Identificador do alvo.</returns>
    public int CreateRenderTarget(int width, int height, TextureFilter filter, out int textureHandle)
    {
        textureHandle = CreateTexture(width, height, [], filter);
        return _next++;
    }

    /// <summary>Libera um alvo de desenho.</summary>
    /// <param name="handle">Identificador do alvo.</param>
    public void DeleteRenderTarget(int handle) { }

    /// <summary>Não faz nada: não há tela.</summary>
    /// <param name="handle">Identificador do alvo (0 é a tela).</param>
    /// <param name="width">Largura, em pixels.</param>
    /// <param name="height">Altura, em pixels.</param>
    public void SetRenderTarget(int handle, int width, int height) { }

    /// <summary>Registra um shader sem compilá-lo (não há GPU para validar o código).</summary>
    /// <param name="fragmentSource">Código GLSL do fragment shader (ignorado).</param>
    /// <returns>Identificador do shader.</returns>
    public int CreateShader(string fragmentSource) => _next++;

    /// <summary>Libera um shader.</summary>
    /// <param name="handle">Identificador do shader.</param>
    public void DeleteShader(int handle) { }

    /// <summary>Conta o desenho sem rasterizar nada.</summary>
    /// <param name="textureHandle">Textura usada.</param>
    /// <param name="vertices">Vértices (ignorados).</param>
    /// <param name="quadCount">Quantos quads.</param>
    /// <param name="projection">Projeção (ignorada).</param>
    public void DrawQuads(int textureHandle, ReadOnlySpan<SpriteVertex> vertices, int quadCount, in Matrix4x4 projection)
    {
        DrawCalls++;
        QuadsDrawn += quadCount;
    }
}
