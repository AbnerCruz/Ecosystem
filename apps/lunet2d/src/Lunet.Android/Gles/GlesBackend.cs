using System.Numerics;
using System.Runtime.InteropServices;
using Android.Opengl;
using Java.Nio;
using Lunet.Graphics;

namespace Lunet.Android.Gles;

/// <summary>Backend gráfico OpenGL ES 3.0. Deve ser usado somente na thread do GLSurfaceView.</summary>
internal sealed class GlesBackend : IGraphicsBackend, IDisposable
{
    private const string VertexShader = """
        #version 300 es
        layout(location = 0) in vec2 aPos;
        layout(location = 1) in vec2 aUv;
        layout(location = 2) in vec4 aColor;
        uniform mat4 uProj;
        out vec2 vUv;
        out vec4 vColor;
        void main() {
            vUv = aUv;
            vColor = aColor;
            gl_Position = uProj * vec4(aPos, 0.0, 1.0);
        }
        """;

    private const string FragmentShader = """
        #version 300 es
        precision mediump float;
        uniform sampler2D uTex;
        in vec2 vUv;
        in vec4 vColor;
        out vec4 outColor;
        void main() {
            outColor = texture(uTex, vUv) * vColor;
        }
        """;

    private sealed class ShaderProgram(int id, int projectionLocation)
    {
        public int Id { get; } = id;
        public int ProjectionLocation { get; } = projectionLocation;
        public Dictionary<string, int> Locations { get; } = new(StringComparer.Ordinal);
    }

    private readonly ShaderProgram _defaultProgram;
    private readonly Dictionary<int, ShaderProgram> _shaders = new();
    private readonly Dictionary<int, (int Fbo, int Texture)> _targets = new();
    private readonly Dictionary<int, TextureFilter> _textureFilters = new();
    private readonly Dictionary<int, (TextureFilter Filter, TextureWrap Wrap)> _appliedSampler = new();
    private ShaderProgram _current;
    private SamplerState? _sampler;
    private (int X, int Y, int Width, int Height) _viewport;
    private int _nextShader = 1;
    private int _nextTarget = 1;
    private readonly int _vao;
    private readonly int _vbo;
    private readonly int _ibo;
    private readonly ByteBuffer _vertexStaging;
    private readonly byte[] _vertexBytes = new byte[SpriteBatch.MaxQuads * 4 * SpriteVertex.SizeInBytes];
    private readonly float[] _matrix = new float[16];

    // Contadores GL reais, reiniciados pela thread de desenho apenas quando o Profiler está ligado.
    internal int FrameDrawCalls { get; private set; }
    internal int FrameTriangles { get; private set; }

    internal void BeginProfileFrame()
    {
        FrameDrawCalls = 0;
        FrameTriangles = 0;
    }

    public GlesBackend()
    {
        _defaultProgram = LinkProgram(FragmentShader);
        _current = _defaultProgram;

        var ids = new int[1];
        GLES30.GlGenVertexArrays(1, ids, 0);
        _vao = ids[0];
        GLES30.GlBindVertexArray(_vao);

        GLES30.GlGenBuffers(1, ids, 0);
        _vbo = ids[0];
        GLES30.GlBindBuffer(GLES30.GlArrayBuffer, _vbo);
        GLES30.GlBufferData(GLES30.GlArrayBuffer, _vertexBytes.Length, null, GLES30.GlDynamicDraw);

        const int stride = SpriteVertex.SizeInBytes;
        GLES30.GlEnableVertexAttribArray(0);
        GLES30.GlVertexAttribPointer(0, 2, GLES30.GlFloat, false, stride, 0);
        GLES30.GlEnableVertexAttribArray(1);
        GLES30.GlVertexAttribPointer(1, 2, GLES30.GlFloat, false, stride, 8);
        GLES30.GlEnableVertexAttribArray(2);
        GLES30.GlVertexAttribPointer(2, 4, GLES30.GlUnsignedByte, true, stride, 16);

        var indices = new short[SpriteBatch.MaxQuads * 6];
        for (var q = 0; q < SpriteBatch.MaxQuads; q++)
        {
            var v = (short)(q * 4);
            var i = q * 6;
            indices[i] = v; indices[i + 1] = (short)(v + 1); indices[i + 2] = (short)(v + 2);
            indices[i + 3] = v; indices[i + 4] = (short)(v + 2); indices[i + 5] = (short)(v + 3);
        }
        var indexBytes = new byte[indices.Length * 2];
        System.Buffer.BlockCopy(indices, 0, indexBytes, 0, indexBytes.Length);
        using var indexBuffer = ToDirect(indexBytes, indexBytes.Length);
        GLES30.GlGenBuffers(1, ids, 0);
        _ibo = ids[0];
        GLES30.GlBindBuffer(GLES30.GlElementArrayBuffer, _ibo);
        GLES30.GlBufferData(GLES30.GlElementArrayBuffer, indexBytes.Length, indexBuffer, GLES30.GlStaticDraw);

        _vertexStaging = ByteBuffer.AllocateDirect(_vertexBytes.Length)!.Order(ByteOrder.NativeOrder())!;

        GLES30.GlEnable(GLES30.GlBlend);
        GLES30.GlBlendFunc(GLES30.GlSrcAlpha, GLES30.GlOneMinusSrcAlpha);
        GLES30.GlDisable(GLES30.GlDepthTest);
        GLES30.GlEnable(GLES30.GlScissorTest);
    }

    public int CreateTexture(int width, int height, ReadOnlySpan<byte> rgba, TextureFilter filter)
    {
        var ids = new int[1];
        GLES30.GlGenTextures(1, ids, 0);
        GLES30.GlBindTexture(GLES30.GlTexture2d, ids[0]);
        GLES30.GlPixelStorei(GLES30.GlUnpackAlignment, 1);
        using var pixels = ToDirect(rgba.ToArray(), rgba.Length);
        GLES30.GlTexImage2D(GLES30.GlTexture2d, 0, GLES30.GlRgba, width, height, 0, GLES30.GlRgba, GLES30.GlUnsignedByte, pixels);
        _textureFilters[ids[0]] = filter;
        _appliedSampler[ids[0]] = (filter, TextureWrap.Clamp);
        var glFilter = filter == TextureFilter.Point ? GLES30.GlNearest : GLES30.GlLinear;
        GLES30.GlTexParameteri(GLES30.GlTexture2d, GLES30.GlTextureMinFilter, glFilter);
        GLES30.GlTexParameteri(GLES30.GlTexture2d, GLES30.GlTextureMagFilter, glFilter);
        GLES30.GlTexParameteri(GLES30.GlTexture2d, GLES30.GlTextureWrapS, GLES30.GlClampToEdge);
        GLES30.GlTexParameteri(GLES30.GlTexture2d, GLES30.GlTextureWrapT, GLES30.GlClampToEdge);
        return ids[0];
    }

    public void DeleteTexture(int handle)
    {
        _textureFilters.Remove(handle);
        _appliedSampler.Remove(handle);
        GLES30.GlDeleteTextures(1, [handle], 0);
    }

    public void SetViewport(int x, int y, int width, int height)
    {
        _viewport = (x, y, width, height);
        GLES30.GlViewport(x, y, width, height);
        GLES30.GlScissor(x, y, width, height);
    }

    public void Clear(Color color)
    {
        GLES30.GlClearColor(color.R / 255f, color.G / 255f, color.B / 255f, color.A / 255f);
        GLES30.GlClear(GLES30.GlColorBufferBit);
    }

    public void DrawQuads(int textureHandle, ReadOnlySpan<SpriteVertex> vertices, int quadCount, in Matrix4x4 projection)
    {
        if (quadCount == 0) return;
        var bytes = MemoryMarshal.AsBytes(vertices);
        bytes.CopyTo(_vertexBytes);
        _vertexStaging.Clear();
        _vertexStaging.Put(_vertexBytes, 0, bytes.Length);
        _vertexStaging.Position(0);

        WriteMatrix(in projection);
        GLES30.GlUniformMatrix4fv(_current.ProjectionLocation, 1, false, _matrix, 0);
        GLES30.GlActiveTexture(GLES30.GlTexture0);
        GLES30.GlBindTexture(GLES30.GlTexture2d, textureHandle);
        ApplySampler(textureHandle);
        GLES30.GlBindVertexArray(_vao);
        GLES30.GlBindBuffer(GLES30.GlArrayBuffer, _vbo);
        GLES30.GlBufferSubData(GLES30.GlArrayBuffer, 0, bytes.Length, _vertexStaging);
        GLES30.GlDrawElements(GLES30.GlTriangles, quadCount * 6, GLES30.GlUnsignedShort, 0);
        FrameDrawCalls++;
        FrameTriangles += quadCount * 2;
    }

    public void SetDrawState(DrawState state)
    {
        switch (state.Blend)
        {
            case BlendMode.Opaque:
                GLES30.GlDisable(GLES30.GlBlend);
                break;
            case BlendMode.Additive:
                GLES30.GlEnable(GLES30.GlBlend);
                GLES30.GlBlendFunc(GLES30.GlSrcAlpha, GLES30.GlOne);
                break;
            case BlendMode.Multiply:
                GLES30.GlEnable(GLES30.GlBlend);
                GLES30.GlBlendFunc(GLES30.GlDstColor, GLES30.GlOneMinusSrcAlpha);
                break;
            case BlendMode.Premultiplied:
                GLES30.GlEnable(GLES30.GlBlend);
                GLES30.GlBlendFunc(GLES30.GlOne, GLES30.GlOneMinusSrcAlpha);
                break;
            default:
                GLES30.GlEnable(GLES30.GlBlend);
                GLES30.GlBlendFunc(GLES30.GlSrcAlpha, GLES30.GlOneMinusSrcAlpha);
                break;
        }

        if (state.Scissor is { } rect) GLES30.GlScissor(rect.X, rect.Y, rect.Width, rect.Height);
        else GLES30.GlScissor(_viewport.X, _viewport.Y, _viewport.Width, _viewport.Height);

        _sampler = state.Sampler;
        _current = state.Shader != 0 && _shaders.TryGetValue(state.Shader, out var program) ? program : _defaultProgram;
        GLES30.GlUseProgram(_current.Id);
        if (state.Uniforms is { Count: > 0 } uniforms)
        {
            foreach (var (name, value) in uniforms)
            {
                if (!_current.Locations.TryGetValue(name, out var location))
                    _current.Locations[name] = location = GLES30.GlGetUniformLocation(_current.Id, name);
                if (location < 0) continue; // o shader não usa esse uniform
                switch (value.Length)
                {
                    case 1: GLES30.GlUniform1f(location, value[0]); break;
                    case 2: GLES30.GlUniform2f(location, value[0], value[1]); break;
                    case 3: GLES30.GlUniform3f(location, value[0], value[1], value[2]); break;
                    case 4: GLES30.GlUniform4f(location, value[0], value[1], value[2], value[3]); break;
                }
            }
        }
    }

    private void ApplySampler(int textureHandle)
    {
        var baseFilter = _textureFilters.GetValueOrDefault(textureHandle, TextureFilter.Linear);
        var desired = _sampler is null ? (baseFilter, TextureWrap.Clamp) : (_sampler.Filter, _sampler.Wrap);
        if (_appliedSampler.TryGetValue(textureHandle, out var applied) && applied == desired) return;
        var glFilter = desired.Item1 == TextureFilter.Point ? GLES30.GlNearest : GLES30.GlLinear;
        var glWrap = desired.Item2 == TextureWrap.Repeat ? GLES30.GlRepeat : GLES30.GlClampToEdge;
        GLES30.GlTexParameteri(GLES30.GlTexture2d, GLES30.GlTextureMinFilter, glFilter);
        GLES30.GlTexParameteri(GLES30.GlTexture2d, GLES30.GlTextureMagFilter, glFilter);
        GLES30.GlTexParameteri(GLES30.GlTexture2d, GLES30.GlTextureWrapS, glWrap);
        GLES30.GlTexParameteri(GLES30.GlTexture2d, GLES30.GlTextureWrapT, glWrap);
        _appliedSampler[textureHandle] = desired;
    }

    public int CreateRenderTarget(int width, int height, TextureFilter filter, out int textureHandle)
    {
        var ids = new int[1];
        GLES30.GlGenTextures(1, ids, 0);
        var texture = ids[0];
        GLES30.GlBindTexture(GLES30.GlTexture2d, texture);
        GLES30.GlTexImage2D(GLES30.GlTexture2d, 0, GLES30.GlRgba, width, height, 0, GLES30.GlRgba, GLES30.GlUnsignedByte, null);
        var glFilter = filter == TextureFilter.Point ? GLES30.GlNearest : GLES30.GlLinear;
        GLES30.GlTexParameteri(GLES30.GlTexture2d, GLES30.GlTextureMinFilter, glFilter);
        GLES30.GlTexParameteri(GLES30.GlTexture2d, GLES30.GlTextureMagFilter, glFilter);
        GLES30.GlTexParameteri(GLES30.GlTexture2d, GLES30.GlTextureWrapS, GLES30.GlClampToEdge);
        GLES30.GlTexParameteri(GLES30.GlTexture2d, GLES30.GlTextureWrapT, GLES30.GlClampToEdge);
        _textureFilters[texture] = filter;
        _appliedSampler[texture] = (filter, TextureWrap.Clamp);

        GLES30.GlGenFramebuffers(1, ids, 0);
        var fbo = ids[0];
        var previous = new int[1];
        GLES30.GlGetIntegerv(GLES30.GlFramebufferBinding, previous, 0);
        GLES30.GlBindFramebuffer(GLES30.GlFramebuffer, fbo);
        GLES30.GlFramebufferTexture2D(GLES30.GlFramebuffer, GLES30.GlColorAttachment0, GLES30.GlTexture2d, texture, 0);
        var status = GLES30.GlCheckFramebufferStatus(GLES30.GlFramebuffer);
        GLES30.GlBindFramebuffer(GLES30.GlFramebuffer, previous[0]);
        if (status != GLES30.GlFramebufferComplete)
        {
            GLES30.GlDeleteFramebuffers(1, [fbo], 0);
            GLES30.GlDeleteTextures(1, [texture], 0);
            throw new InvalidOperationException($"Não foi possível criar o alvo de desenho {width}×{height} (status GL 0x{status:X}).");
        }
        var handle = _nextTarget++;
        _targets[handle] = (fbo, texture);
        textureHandle = texture;
        return handle;
    }

    public void DeleteRenderTarget(int handle)
    {
        if (!_targets.Remove(handle, out var target)) return;
        GLES30.GlDeleteFramebuffers(1, [target.Fbo], 0);
        DeleteTexture(target.Texture);
    }

    public void SetRenderTarget(int handle, int width, int height)
    {
        var fbo = handle != 0 && _targets.TryGetValue(handle, out var target) ? target.Fbo : 0;
        GLES30.GlBindFramebuffer(GLES30.GlFramebuffer, fbo);
    }

    public int CreateShader(string fragmentSource)
    {
        ShaderProgram program;
        try { program = LinkProgram(fragmentSource); }
        catch (InvalidOperationException ex) { throw new ShaderCompileException(ex.Message); }
        var handle = _nextShader++;
        _shaders[handle] = program;
        return handle;
    }

    public void DeleteShader(int handle)
    {
        if (!_shaders.Remove(handle, out var program)) return;
        if (ReferenceEquals(_current, program)) _current = _defaultProgram;
        GLES30.GlDeleteProgram(program.Id);
    }

    private void WriteMatrix(in Matrix4x4 m)
    {
        // System.Numerics usa vetores-linha; o layout em memória lido pelo GL como coluna-major dá a transposta certa.
        _matrix[0] = m.M11; _matrix[1] = m.M12; _matrix[2] = m.M13; _matrix[3] = m.M14;
        _matrix[4] = m.M21; _matrix[5] = m.M22; _matrix[6] = m.M23; _matrix[7] = m.M24;
        _matrix[8] = m.M31; _matrix[9] = m.M32; _matrix[10] = m.M33; _matrix[11] = m.M34;
        _matrix[12] = m.M41; _matrix[13] = m.M42; _matrix[14] = m.M43; _matrix[15] = m.M44;
    }

    public void Dispose()
    {
        foreach (var program in _shaders.Values) GLES30.GlDeleteProgram(program.Id);
        _shaders.Clear();
        foreach (var target in _targets.Values)
        {
            GLES30.GlDeleteFramebuffers(1, [target.Fbo], 0);
            GLES30.GlDeleteTextures(1, [target.Texture], 0);
        }
        _targets.Clear();
        GLES30.GlDeleteProgram(_defaultProgram.Id);
        GLES30.GlDeleteBuffers(2, [_vbo, _ibo], 0);
        GLES30.GlDeleteVertexArrays(1, [_vao], 0);
        _vertexStaging.Dispose();
    }

    private static ByteBuffer ToDirect(byte[] data, int length)
    {
        var buffer = ByteBuffer.AllocateDirect(length)!.Order(ByteOrder.NativeOrder())!;
        buffer.Put(data, 0, length);
        buffer.Position(0);
        return buffer;
    }

    private static ShaderProgram LinkProgram(string fragmentSource)
    {
        var vertex = Compile(GLES30.GlVertexShader, VertexShader);
        var fragment = Compile(GLES30.GlFragmentShader, fragmentSource);
        var program = GLES30.GlCreateProgram();
        GLES30.GlAttachShader(program, vertex);
        GLES30.GlAttachShader(program, fragment);
        GLES30.GlLinkProgram(program);
        var status = new int[1];
        GLES30.GlGetProgramiv(program, GLES30.GlLinkStatus, status, 0);
        if (status[0] == 0)
            throw new InvalidOperationException("Falha ao ligar o programa GL: " + GLES30.GlGetProgramInfoLog(program));
        GLES30.GlDeleteShader(vertex);
        GLES30.GlDeleteShader(fragment);
        return new ShaderProgram(program, GLES30.GlGetUniformLocation(program, "uProj"));
    }

    private static int Compile(int type, string source)
    {
        var shader = GLES30.GlCreateShader(type);
        GLES30.GlShaderSource(shader, source);
        GLES30.GlCompileShader(shader);
        var status = new int[1];
        GLES30.GlGetShaderiv(shader, GLES30.GlCompileStatus, status, 0);
        if (status[0] == 0)
            throw new InvalidOperationException("Falha ao compilar shader GL: " + GLES30.GlGetShaderInfoLog(shader));
        return shader;
    }
}
