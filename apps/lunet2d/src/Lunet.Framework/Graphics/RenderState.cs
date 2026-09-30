namespace Lunet.Graphics;

/// <summary>Modos de mistura suportados por <see cref="BlendState"/>.</summary>
/// <summary>Para alfa pré-multiplicado.</summary>
/// <summary>Multiplica as cores (sombras).</summary>
/// <summary>Sem mistura.</summary>
/// <summary>Soma as cores (brilho, fogo).</summary>
/// <summary>Transparência comum.</summary>
public enum BlendMode
{
    /// <summary>Transparência comum.</summary>
    Alpha,
    /// <summary>Soma as cores (brilho, fogo).</summary>
    Additive,
    /// <summary>Sem mistura.</summary>
    Opaque,
    /// <summary>Multiplica as cores (sombras).</summary>
    Multiply,
    /// <summary>Para alfa pré-multiplicado.</summary>
    Premultiplied,
}

/// <summary>Como o desenho se mistura com o que já está na tela.</summary>
/// <example>
/// <code>
/// batch.Begin(BlendState.Additive);
/// batch.Draw(texture, position, Color.White);
/// batch.End();
/// </code>
/// </example>
public sealed class BlendState
{
    private BlendState(BlendMode mode, string name)
    {
        Mode = mode;
        Name = name;
    }

    /// <summary>Modo de mistura.</summary>
    public BlendMode Mode { get; }
    /// <summary>Nome do estado.</summary>
    public string Name { get; }

    /// <summary>Transparência comum (padrão).</summary>
    public static readonly BlendState Alpha = new(BlendMode.Alpha, nameof(Alpha));

    /// <summary>Soma as cores: brilho, fogo, luz.</summary>
    public static readonly BlendState Additive = new(BlendMode.Additive, nameof(Additive));

    /// <summary>Sem mistura: substitui o fundo.</summary>
    public static readonly BlendState Opaque = new(BlendMode.Opaque, nameof(Opaque));

    /// <summary>Multiplica as cores: sombras e tintas.</summary>
    public static readonly BlendState Multiply = new(BlendMode.Multiply, nameof(Multiply));

    /// <summary>Para texturas com alfa pré-multiplicado.</summary>
    public static readonly BlendState Premultiplied = new(BlendMode.Premultiplied, nameof(Premultiplied));
}

/// <summary>O que acontece com coordenadas de textura fora de 0–1.</summary>
/// <summary>Repete a textura.</summary>
/// <summary>Repete o pixel da borda.</summary>
public enum TextureWrap
{
    /// <summary>Repete o pixel da borda.</summary>
    Clamp,
    /// <summary>Repete a textura.</summary>
    Repeat,
}

/// <summary>Como a textura é amostrada: filtro e comportamento fora de [0,1].</summary>
/// <example>
/// <code>
/// batch.Begin(sampler: SamplerState.PointClamp);
/// batch.End();
/// </code>
/// </example>
public sealed class SamplerState
{
    /// <summary>Cria um estado de amostragem.</summary>
    /// <param name="filter">Ponto ou linear.</param>
    /// <param name="wrap">Comportamento fora de 0 a 1.</param>
    public SamplerState(TextureFilter filter, TextureWrap wrap)
    {
        Filter = filter;
        Wrap = wrap;
    }

    /// <summary>Filtro de ampliação e redução.</summary>
    public TextureFilter Filter { get; }
    /// <summary>Comportamento das coordenadas fora de 0 a 1.</summary>
    public TextureWrap Wrap { get; }

    /// <summary>Pixels nítidos, sem repetir.</summary>
    public static readonly SamplerState PointClamp = new(TextureFilter.Point, TextureWrap.Clamp);
    /// <summary>Suavizado, sem repetir.</summary>
    public static readonly SamplerState LinearClamp = new(TextureFilter.Linear, TextureWrap.Clamp);
    /// <summary>Pixels nítidos, repetindo a textura.</summary>
    public static readonly SamplerState PointWrap = new(TextureFilter.Point, TextureWrap.Repeat);
    /// <summary>Suavizado, repetindo a textura.</summary>
    public static readonly SamplerState LinearWrap = new(TextureFilter.Linear, TextureWrap.Repeat);
}

/// <summary>Retângulo em pixels inteiros.</summary>
/// <param name="X">Coordenada X do canto de origem, em pixels.</param>
/// <param name="Y">Coordenada Y do canto de origem, em pixels.</param>
/// <param name="Width">Largura, em pixels.</param>
/// <param name="Height">Altura, em pixels.</param>
/// <example>
/// <code>
/// var clip = new RectI(0, 0, 200, 100);
/// int area = clip.Width * clip.Height;
/// </code>
/// </example>
public readonly record struct RectI(int X, int Y, int Width, int Height)
{
    /// <summary>Coordenada X logo após o lado direito (X + Width).</summary>
    public int Right => X + Width;
    /// <summary>Coordenada Y logo após o lado inferior (Y + Height).</summary>
    public int Bottom => Y + Height;
}

/// <summary>Área da superfície onde o jogo desenha (a resolução virtual com barras), em pixels da superfície.</summary>
/// <param name="X">Distância da borda esquerda da superfície até a área do jogo, em pixels.</param>
/// <param name="Y">Distância da borda superior da superfície até a área do jogo, em pixels.</param>
/// <param name="Width">Largura da área do jogo, em pixels.</param>
/// <param name="Height">Altura da área do jogo, em pixels.</param>
/// <example>
/// <code>
/// Viewport view = device.Viewport;
/// float aspect = view.AspectRatio;
/// </code>
/// </example>
public readonly record struct Viewport(int X, int Y, int Width, int Height)
{
    /// <summary>Proporção largura por altura da área do jogo.</summary>
    public float AspectRatio => Height == 0 ? 0f : (float)Width / Height;
}

/// <summary>Estado aplicado pelo <see cref="SpriteBatch"/> antes de cada chamada de desenho (struct: sem alocação por Begin).</summary>
/// <param name="Scissor">Recorte em pixels do alvo com origem no canto inferior esquerdo (convenção do GL); nulo = sem recorte extra.</param>
/// <param name="Blend">Modo de mistura das cores.</param>
/// <param name="Sampler">Filtro e repetição de textura; nulo usa os da própria textura.</param>
/// <param name="Shader">Handle do shader do backend; 0 usa o shader padrão.</param>
/// <param name="Uniforms">Valores dos uniforms do shader, por nome; nulo se não houver.</param>
/// <example>
/// <code>
/// var additive = DrawState.Default with { Blend = BlendMode.Additive };
/// </code>
/// </example>
public readonly record struct DrawState(
    BlendMode Blend,
    SamplerState? Sampler,
    RectI? Scissor,
    int Shader,
    IReadOnlyDictionary<string, float[]>? Uniforms)
{
    /// <summary>Estado padrão: mistura alfa, sem recorte e sem shader personalizado.</summary>
    public static readonly DrawState Default = new(BlendMode.Alpha, null, null, 0, null);
}
