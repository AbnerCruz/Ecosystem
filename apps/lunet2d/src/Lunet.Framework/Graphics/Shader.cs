using System.Numerics;
using System.Text.RegularExpressions;

namespace Lunet.Graphics;

/// <summary>O shader não compilou; a mensagem traz o log do driver.</summary>
public sealed class ShaderCompileException(string message) : Exception(message);

/// <summary>
/// Shader de fragmento personalizado. Você escreve só o corpo; o framework acrescenta a declaração de
/// <c>vUv</c>, <c>vColor</c>, <c>uTex</c> e <c>outColor</c> (veja <see cref="Prelude"/>). Exemplo:
/// <code>uniform float uAmount; void main() { vec4 c = texture(uTex, vUv) * vColor; float g = dot(c.rgb, vec3(0.3, 0.59, 0.11)); outColor = vec4(mix(c.rgb, vec3(g), uAmount), c.a); }</code>
/// </summary>
public sealed partial class Shader : IDisposable
{
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
    public bool IsDisposed { get; private set; }

    /// <exception cref="ShaderCompileException">O shader não compila.</exception>
    public static Shader FromFragmentSource(GraphicsDevice device, string source)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        return new Shader(device, device.Backend.CreateShader(Prelude + source));
    }

    public void SetFloat(string name, float value) => Set(name, [value]);
    public void SetVector2(string name, Vector2 value) => Set(name, [value.X, value.Y]);
    public void SetVector3(string name, Vector3 value) => Set(name, [value.X, value.Y, value.Z]);
    public void SetVector4(string name, Vector4 value) => Set(name, [value.X, value.Y, value.Z, value.W]);
    public void SetColor(string name, Color color) => Set(name, [color.R / 255f, color.G / 255f, color.B / 255f, color.A / 255f]);

    private void Set(string name, float[] value)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        if (!UniformName().IsMatch(name)) throw new ArgumentException("Nome de uniform inválido.", nameof(name));
        _values[name] = value; // substitui o array: o SpriteBatch guarda uma cópia do dicionário a cada Begin
    }

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
public sealed class Material
{
    public Shader? Shader { get; set; }
    public BlendState Blend { get; set; } = BlendState.Alpha;
    public SamplerState? Sampler { get; set; }
}
