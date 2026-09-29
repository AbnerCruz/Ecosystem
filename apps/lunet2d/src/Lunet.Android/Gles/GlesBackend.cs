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

    private readonly int _program;
    private readonly int _projectionLocation;
    private readonly int _vao;
    private readonly int _vbo;
    private readonly int _ibo;
    private readonly ByteBuffer _vertexStaging;
    private readonly byte[] _vertexBytes = new byte[SpriteBatch.MaxQuads * 4 * SpriteVertex.SizeInBytes];
    private readonly float[] _matrix = new float[16];

    public GlesBackend()
    {
        _program = LinkProgram(VertexShader, FragmentShader);
        _projectionLocation = GLES30.GlGetUniformLocation(_program, "uProj");

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

    public int CreateTexture(int width, int height, ReadOnlySpan<byte> rgba)
    {
        var ids = new int[1];
        GLES30.GlGenTextures(1, ids, 0);
        GLES30.GlBindTexture(GLES30.GlTexture2d, ids[0]);
        GLES30.GlPixelStorei(GLES30.GlUnpackAlignment, 1);
        using var pixels = ToDirect(rgba.ToArray(), rgba.Length);
        GLES30.GlTexImage2D(GLES30.GlTexture2d, 0, GLES30.GlRgba, width, height, 0, GLES30.GlRgba, GLES30.GlUnsignedByte, pixels);
        GLES30.GlTexParameteri(GLES30.GlTexture2d, GLES30.GlTextureMinFilter, GLES30.GlLinear);
        GLES30.GlTexParameteri(GLES30.GlTexture2d, GLES30.GlTextureMagFilter, GLES30.GlLinear);
        GLES30.GlTexParameteri(GLES30.GlTexture2d, GLES30.GlTextureWrapS, GLES30.GlClampToEdge);
        GLES30.GlTexParameteri(GLES30.GlTexture2d, GLES30.GlTextureWrapT, GLES30.GlClampToEdge);
        return ids[0];
    }

    public void DeleteTexture(int handle) => GLES30.GlDeleteTextures(1, [handle], 0);

    public void SetViewport(int x, int y, int width, int height)
    {
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

        GLES30.GlUseProgram(_program);
        WriteMatrix(in projection);
        GLES30.GlUniformMatrix4fv(_projectionLocation, 1, false, _matrix, 0);
        GLES30.GlActiveTexture(GLES30.GlTexture0);
        GLES30.GlBindTexture(GLES30.GlTexture2d, textureHandle);
        GLES30.GlBindVertexArray(_vao);
        GLES30.GlBindBuffer(GLES30.GlArrayBuffer, _vbo);
        GLES30.GlBufferSubData(GLES30.GlArrayBuffer, 0, bytes.Length, _vertexStaging);
        GLES30.GlDrawElements(GLES30.GlTriangles, quadCount * 6, GLES30.GlUnsignedShort, 0);
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
        GLES30.GlDeleteProgram(_program);
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

    private static int LinkProgram(string vertexSource, string fragmentSource)
    {
        var vertex = Compile(GLES30.GlVertexShader, vertexSource);
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
        return program;
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
