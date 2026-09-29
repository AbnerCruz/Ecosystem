using System.Numerics;
using Lunet.Graphics;

namespace Lunet.Tests;

public class GraphicsStateTests
{
    private static (GraphicsDevice Device, RecordingBackend Backend) New(int w = 360, int h = 640)
    {
        var backend = new RecordingBackend();
        return (new GraphicsDevice(backend, w, h), backend);
    }

    private static void DrawOne(SpriteBatch batch, Texture2D texture, Action begin)
    {
        begin();
        batch.Draw(texture, Vector2.Zero, Color.White);
        batch.End();
    }

    [Fact]
    public void Begin_SetsBlendSamplerAndDefaultsBeforeEachDraw()
    {
        var (device, backend) = New();
        using var texture = Texture2D.CreateSolid(device, 4, 4, Color.White);
        var batch = new SpriteBatch(device);

        DrawOne(batch, texture, () => batch.Begin());
        DrawOne(batch, texture, () => batch.Begin(BlendState.Additive, SamplerState.PointWrap));

        Assert.Equal(BlendMode.Alpha, backend.States[0].Blend);
        Assert.Null(backend.States[0].Sampler);
        Assert.Null(backend.States[0].Scissor);
        Assert.Equal(BlendMode.Additive, backend.States[1].Blend);
        Assert.Same(SamplerState.PointWrap, backend.States[1].Sampler);
    }

    [Fact]
    public void Clip_ConvertsVirtualAreaToBottomLeftScissorInSurfacePixels()
    {
        var (device, backend) = New(360, 640);
        device.Resize(720, 1280); // escala 2
        using var texture = Texture2D.CreateSolid(device, 4, 4, Color.White);
        var batch = new SpriteBatch(device);
        DrawOne(batch, texture, () => batch.Begin(clip: new RectangleF(10, 20, 100, 50)));
        // x: 20..220; y do topo 40..140 → base-esquerda: 1280-140 = 1140, altura 100
        Assert.Equal(new RectI(20, 1140, 200, 100), backend.States[0].Scissor);
    }

    [Fact]
    public void Clip_AccountsForLetterboxAndClampsToSurface()
    {
        var (device, backend) = New(100, 100);
        device.Resize(300, 200); // escala 2, barras laterais de 50
        using var texture = Texture2D.CreateSolid(device, 4, 4, Color.White);
        var batch = new SpriteBatch(device);
        DrawOne(batch, texture, () => batch.Begin(clip: new RectangleF(-50, 0, 400, 10)));
        var scissor = backend.States[0].Scissor!.Value;
        Assert.Equal(0, scissor.X);
        Assert.Equal(300, scissor.Width);
        Assert.Equal(200 - 20, scissor.Y);
    }

    [Fact]
    public void RenderTarget_SwitchesTargetFlipsProjectionAndRestoresScreen()
    {
        var (device, backend) = New();
        device.Resize(720, 1280);
        var screenProjection = device.Projection;
        using var target = new RenderTarget2D(device, 128, 64);
        Assert.Equal(128, target.Texture.Width);

        device.SetRenderTarget(target);
        Assert.Same(target, device.RenderTarget);
        Assert.Equal(new Vector2(128, 64), device.ViewSize);
        Assert.NotEqual(screenProjection, device.Projection);
        Assert.Equal(new Vector2(-1, -1), Vector2.Transform(new Vector2(0, 0), device.Projection.ToMatrix3x2()));
        Assert.Equal(new Vector2(1, 1), Vector2.Transform(new Vector2(128, 64), device.Projection.ToMatrix3x2()));
        Assert.Equal((backend.RenderTargets[0].Handle, 128, 64), backend.TargetSwitches[^1]);

        device.Clear(Color.Red);
        Assert.Equal(Color.Red, backend.Clears[^1]);
        Assert.Equal((0, 0, 128, 64), backend.Viewports[^1]);

        device.SetRenderTarget(null);
        Assert.Equal(screenProjection, device.Projection);
        Assert.Equal((0, 720, 1280), (backend.TargetSwitches[^1].Handle, backend.TargetSwitches[^1].Width, backend.TargetSwitches[^1].Height));
    }

    [Fact]
    public void RenderTarget_ClipUsesTargetSpaceAndDisposeReleasesEverything()
    {
        var (device, backend) = New();
        var target = new RenderTarget2D(device, 100, 100);
        var batch = new SpriteBatch(device);
        using var texture = Texture2D.CreateSolid(device, 2, 2, Color.White);
        device.SetRenderTarget(target);
        DrawOne(batch, texture, () => batch.Begin(clip: new RectangleF(10, 10, 20, 20)));
        Assert.Equal(new RectI(10, 10, 20, 20), backend.States[0].Scissor);

        target.Dispose();
        Assert.Null(device.RenderTarget); // voltou à tela
        Assert.Empty(backend.LiveTargets);
        Assert.True(target.Texture.IsDisposed);
        Assert.Throws<ObjectDisposedException>(() => device.SetRenderTarget(target));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RenderTarget2D(device, 0, 10));
    }

    [Fact]
    public void RenderTargetTexture_CanBeDrawnLikeAnyTexture()
    {
        var (device, backend) = New();
        using var target = new RenderTarget2D(device, 32, 32);
        var batch = new SpriteBatch(device);
        batch.Begin();
        batch.Draw(target.Texture, new Vector2(5, 5), Color.White);
        batch.End();
        Assert.Equal(target.Texture.Handle, backend.Batches[^1].Texture);
    }

    [Fact]
    public void Shader_CompilesWithPreludeAndCarriesUniformsToTheDrawState()
    {
        var (device, backend) = New();
        var shader = Shader.FromFragmentSource(device, "uniform float uAmount; void main() { outColor = texture(uTex, vUv) * uAmount; }");
        Assert.StartsWith("#version 300 es", backend.Shaders[0]);
        Assert.Contains("uniform sampler2D uTex;", backend.Shaders[0]);

        shader.SetFloat("uAmount", 0.5f);
        shader.SetVector2("uCenter", new Vector2(1, 2));
        shader.SetColor("uTint", Color.Red);
        using var texture = Texture2D.CreateSolid(device, 2, 2, Color.White);
        var batch = new SpriteBatch(device);
        DrawOne(batch, texture, () => batch.Begin(shader: shader));

        var state = backend.States[^1];
        Assert.Equal(200, state.Shader);
        Assert.Equal([0.5f], state.Uniforms!["uAmount"]);
        Assert.Equal([1f, 2f], state.Uniforms["uCenter"]);
        Assert.Equal(4, state.Uniforms["uTint"].Length);

        shader.Dispose();
        Assert.Empty(backend.LiveShaders);
        Assert.Throws<ObjectDisposedException>(() => batch.Begin(shader: shader));
    }

    [Fact]
    public void Shader_RejectsBadUniformNamesAndSurfacesCompileErrors()
    {
        var (device, _) = New();
        var shader = Shader.FromFragmentSource(device, "void main() { outColor = vec4(1.0); }");
        Assert.Throws<ArgumentException>(() => shader.SetFloat("bad name;", 1));
        Assert.Throws<ArgumentException>(() => shader.SetFloat("1abc", 1));
        var error = Assert.Throws<ShaderCompileException>(() => Shader.FromFragmentSource(device, "SYNTAX_ERROR"));
        Assert.Contains("syntax error", error.Message);
    }

    [Fact]
    public void Material_BundlesShaderBlendAndSampler()
    {
        var (device, backend) = New();
        var shader = Shader.FromFragmentSource(device, "void main() { outColor = vec4(1.0); }");
        var material = new Material { Shader = shader, Blend = BlendState.Multiply, Sampler = SamplerState.LinearWrap };
        using var texture = Texture2D.CreateSolid(device, 2, 2, Color.White);
        var batch = new SpriteBatch(device);
        DrawOne(batch, texture, () => batch.Begin(material));
        Assert.Equal(BlendMode.Multiply, backend.States[^1].Blend);
        Assert.Equal(shader.Handle, backend.States[^1].Shader);
        Assert.Same(SamplerState.LinearWrap, backend.States[^1].Sampler);
    }

    [Fact]
    public void PixelPerfect_UsesIntegerScaleWhenItFits()
    {
        var (device, _) = New(320, 180);
        device.Resize(1000, 700); // ajuste exato = 3,125
        Assert.Equal(3.125f, device.Scale, 0.001);
        device.PixelPerfect = true;
        Assert.Equal(3f, device.Scale);
        Assert.Equal(new Viewport(20, 80, 960, 540), device.Viewport);

        device.Resize(200, 100); // menor que 1×: mantém o ajuste fracionário
        Assert.True(device.Scale < 1f);
    }

    [Fact]
    public void SafeArea_ShrinksByInsetsAndIgnoresLetterboxBars()
    {
        var (device, _) = New(100, 200);
        device.Resize(100, 200);
        device.SetDisplay(2.5f, 0, 24, 0, 16);
        Assert.Equal(2.5f, device.Density);
        Assert.Equal(new RectangleF(0, 24, 100, 160), device.SafeArea);

        device.Resize(300, 200); // barras laterais de 100 px: o recuo horizontal já está "fora" da área virtual
        device.SetDisplay(2.5f, 40, 0, 40, 0);
        Assert.Equal(new RectangleF(0, 0, 100, 200), device.SafeArea);
    }

    [Fact]
    public void DebugDraw_ProducesLinesRectanglesAndCircles()
    {
        var (device, backend) = New();
        var batch = new SpriteBatch(device);
        batch.Begin();
        batch.Line(new Vector2(0, 0), new Vector2(10, 0), Color.Red, 2);
        batch.Rect(new RectangleF(0, 0, 20, 20), Color.Green);
        batch.Circle(new Vector2(50, 50), 10, Color.Blue, 1, segments: 8);
        batch.Line(Vector2.Zero, Vector2.Zero, Color.Red); // comprimento zero: ignorado
        batch.End();
        Assert.Equal(1 + 4 + 8, backend.Batches.Sum(b => b.QuadCount));
        var line = backend.Batches[0].Vertices;
        Assert.Equal(new Vector2(0, -1), line[0].Position); // meia espessura acima da origem
        Assert.Equal(new Vector2(10, 1), line[2].Position);
    }

    [Fact]
    public void Host_AppliesDisplayInfoAndReleasesWhiteTexture()
    {
        var backend = new RecordingBackend();
        var game = new WhiteGame();
        var host = new GameHost(game, backend);
        host.SetDisplay(3f, 0, 90, 0, 0);
        host.Start(360, 700);
        Assert.Equal(3f, host.GraphicsDevice!.Density);
        Assert.True(host.GraphicsDevice.SafeArea.Y > 0);
        host.Tick(0.016);
        Assert.NotEmpty(backend.LiveTextures);
        host.Stop();
        Assert.Empty(backend.LiveTextures);
    }

    private sealed class WhiteGame : Game
    {
        protected override void Draw(GameTime time) => _ = GraphicsDevice.WhiteTexture;
    }
}

internal static class MatrixExtensions
{
    /// <summary>Aplica só a parte 2D (X,Y) da projeção, para testar o mapeamento para o espaço do dispositivo.</summary>
    public static Matrix3x2 ToMatrix3x2(this Matrix4x4 m) => new(m.M11, m.M12, m.M21, m.M22, m.M41, m.M42);
}

public class SpriteTests
{
    [Fact]
    public void Sprite_DrawsAroundItsOriginWithScaleAndKeepsRegion()
    {
        var backend = new RecordingBackend();
        var device = new GraphicsDevice(backend, 100, 100);
        using var texture = Texture2D.CreateSolid(device, 16, 16, Color.White);
        var sprite = new Sprite(texture, new RectangleF(8, 0, 8, 8)) { Scale = new Vector2(2, 2), Color = Color.Red };
        Assert.Equal(new Vector2(4, 4), sprite.Origin); // centro da região

        var batch = new SpriteBatch(device);
        batch.Begin();
        batch.Draw(sprite, new Vector2(50, 50));
        batch.End();
        var v = backend.Batches[^1].Vertices;
        Assert.Equal(new Vector2(42, 42), v[0].Position); // 50 - 4*2
        Assert.Equal(new Vector2(58, 58), v[2].Position);
        Assert.Equal(new Vector2(0.5f, 0), v[0].TexCoord);
        Assert.Equal(Color.Red.PackedRgba, v[0].Color);
    }

    [Fact]
    public void Sprite_FromAtlasUsesRegionPivot()
    {
        var backend = new RecordingBackend();
        var device = new GraphicsDevice(backend, 100, 100);
        using var texture = Texture2D.CreateSolid(device, 16, 16, Color.White);
        var atlas = new TextureAtlas(texture, [new AtlasRegion("pé", new RectangleF(0, 0, 8, 16), 0.5f, 1f)]);
        var sprite = Sprite.FromAtlas(atlas, "pé");
        Assert.Equal(new Vector2(4, 16), sprite.Origin);
        Assert.Throws<KeyNotFoundException>(() => Sprite.FromAtlas(atlas, "nada"));
    }
}
