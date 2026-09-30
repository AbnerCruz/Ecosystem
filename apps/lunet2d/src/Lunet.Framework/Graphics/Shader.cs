using System.Numerics;
using System.Text.RegularExpressions;

namespace Lunet.Graphics;

/// <summary>O shader não compilou; a mensagem traz o log do driver.</summary>
/// <example>
/// <code>
/// try
/// {
///     Shader.FromFragmentSource(device, "isto não compila");
/// }
/// catch (ShaderCompileException error)
/// {
///     log.Error(error.Message);
/// }
/// </code>
/// </example>
/// <param name="message">Mensagem de erro do compilador de shaders.</param>
public sealed class ShaderCompileException(string message) : Exception(message);

/// <summary>
/// Shader de fragmento personalizado. Você escreve só o corpo; o framework acrescenta a declaração de
/// <c>vUv</c>, <c>vColor</c>, <c>uTex</c> e <c>outColor</c> (veja <see cref="Prelude"/>). Exemplo:
/// <code>uniform float uAmount; void main() { vec4 c = texture(uTex, vUv) * vColor; float g = dot(c.rgb, vec3(0.3, 0.59, 0.11)); outColor = vec4(mix(c.rgb, vec3(g), uAmount), c.a); }</code>
/// </summary>
/// <example>
/// <code>
/// var glow = Shader.FromFragmentSource(device, "uniform float uAmount; void main() { outColor = texture(uTex, vUv) * vColor * uAmount; }");
/// glow.SetFloat("uAmount", 1.5f);
/// batch.Begin(shader: glow);
/// batch.End();
/// </code>
/// </example>
public sealed partial class Shader : IDisposable
{
    /// <summary>Trecho que o framework coloca antes do seu código: versão, vUv, vColor, uTex e outColor.</summary>
    public const string Prelude = "#version 300 es\nprecision mediump float;\nin vec2 vUv;\nin vec4 vColor;\nuniform sampler2D uTex;\nout vec4 outColor;\n";

    private readonly GraphicsDevice _device;
    private readonly Dictionary<string, float[]> _values = new(StringComparer.Ordinal);

    private Shader(GraphicsDevice device, int handle)
    {
        _device = device;
        Handle = handle;
    }

    internal int Handle { get; }
    internal IReadOnlyDictionary<string, float[]> Values => _values;
    /// <summary>Verdadeiro depois de liberado.</summary>
    public bool IsDisposed { get; private set; }

    /// <summary>Compila um fragment shader a partir do código GLSL.</summary>
    /// <exception cref="ShaderCompileException">O shader não compila.</exception>
    /// <param name="device">Dispositivo gráfico do jogo.</param>
    /// <param name="source">Código GLSL do fragment shader (sem o prelúdio).</param>
    /// <returns>O shader compilado.</returns>
    public static Shader FromFragmentSource(GraphicsDevice device, string source)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        return new Shader(device, device.Backend.CreateShader(Prelude + source));
    }

    /// <summary>Define um uniform float.</summary>
    /// <param name="name">Nome do uniform.</param>
    /// <param name="value">Valor.</param>
    public void SetFloat(string name, float value) => Set(name, [value]);
    /// <summary>Define um uniform vec2.</summary>
    /// <param name="name">Nome do uniform.</param>
    /// <param name="value">Valor.</param>
    public void SetVector2(string name, Vector2 value) => Set(name, [value.X, value.Y]);
    /// <summary>Define um uniform vec3.</summary>
    /// <param name="name">Nome do uniform.</param>
    /// <param name="value">Valor.</param>
    public void SetVector3(string name, Vector3 value) => Set(name, [value.X, value.Y, value.Z]);
    /// <summary>Define um uniform vec4.</summary>
    /// <param name="name">Nome do uniform.</param>
    /// <param name="value">Valor.</param>
    public void SetVector4(string name, Vector4 value) => Set(name, [value.X, value.Y, value.Z, value.W]);
    /// <summary>Define um uniform vec4 com a cor (0 a 1 por canal).</summary>
    /// <param name="name">Nome do uniform.</param>
    /// <param name="color">Cor.</param>
    public void SetColor(string name, Color color) => Set(name, [color.R / 255f, color.G / 255f, color.B / 255f, color.A / 255f]);

    private void Set(string name, float[] value)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        if (!UniformName().IsMatch(name)) throw new ArgumentException("Nome de uniform inválido.", nameof(name));
        _values[name] = value; // substitui o array: o SpriteBatch guarda uma cópia do dicionário a cada Begin
    }

    /// <summary>Libera o shader.</summary>
    public void Dispose()
    {
        if (IsDisposed) return;
        IsDisposed = true;
        _device.Backend.DeleteShader(Handle);
    }

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]{0,63}$")]
    private static partial Regex UniformName();
}

/// <summary>Conjunto de estado de desenho reutilizável: shader, mistura e amostragem.</summary>
/// <example>
/// <code>
/// batch.Begin(new Material { Blend = BlendState.Alpha, Sampler = SamplerState.PointClamp });
/// batch.End();
/// </code>
/// </example>
public sealed class Material
{
    /// <summary>Shader de fragmento, ou nulo para o padrão.</summary>
    public Shader? Shader { get; set; }
    /// <summary>Modo de mistura.</summary>
    public BlendState Blend { get; set; } = BlendState.Alpha;
    /// <summary>Amostragem de textura, ou nulo para usar a da textura.</summary>
    public SamplerState? Sampler { get; set; }
}
