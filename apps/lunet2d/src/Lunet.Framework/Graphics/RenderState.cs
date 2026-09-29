namespace Lunet.Graphics;

public enum BlendMode { Alpha, Additive, Opaque, Multiply, Premultiplied }

/// <summary>Como o desenho se mistura com o que já está na tela.</summary>
public sealed class BlendState
{
    private BlendState(BlendMode mode, string name)
    {
        Mode = mode;
        Name = name;
    }

    public BlendMode Mode { get; }
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

public enum TextureWrap { Clamp, Repeat }

/// <summary>Como a textura é amostrada: filtro e comportamento fora de [0,1].</summary>
public sealed class SamplerState
{
    public SamplerState(TextureFilter filter, TextureWrap wrap)
    {
        Filter = filter;
        Wrap = wrap;
    }

    public TextureFilter Filter { get; }
    public TextureWrap Wrap { get; }

    public static readonly SamplerState PointClamp = new(TextureFilter.Point, TextureWrap.Clamp);
    public static readonly SamplerState LinearClamp = new(TextureFilter.Linear, TextureWrap.Clamp);
    public static readonly SamplerState PointWrap = new(TextureFilter.Point, TextureWrap.Repeat);
    public static readonly SamplerState LinearWrap = new(TextureFilter.Linear, TextureWrap.Repeat);
}

/// <summary>Retângulo em pixels inteiros.</summary>
public readonly record struct RectI(int X, int Y, int Width, int Height)
{
    public int Right => X + Width;
    public int Bottom => Y + Height;
}

/// <summary>Área da superfície onde o jogo desenha (a resolução virtual com barras), em pixels da superfície.</summary>
public readonly record struct Viewport(int X, int Y, int Width, int Height)
{
    public float AspectRatio => Height == 0 ? 0f : (float)Width / Height;
}

/// <summary>Estado aplicado pelo <see cref="SpriteBatch"/> antes de cada chamada de desenho.</summary>
/// <param name="Scissor">Recorte em pixels do alvo com origem no canto inferior esquerdo (convenção do GL); nulo = sem recorte extra.</param>
public sealed record DrawState(
    BlendMode Blend,
    SamplerState? Sampler,
    RectI? Scissor,
    int Shader,
    IReadOnlyDictionary<string, float[]>? Uniforms)
{
    public static readonly DrawState Default = new(BlendMode.Alpha, null, null, 0, null);
}
