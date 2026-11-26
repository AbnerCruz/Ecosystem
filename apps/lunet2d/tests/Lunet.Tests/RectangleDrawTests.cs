using System.Numerics;
using Lunet.Graphics;

namespace Lunet.Tests;

/// <summary>
/// Anexo B (tipos e sobrecargas), partes B e C: <c>RectangleF(Vector2, Vector2)</c>, <c>Draw(RectangleF, Color)</c> e
/// <c>Draw(Texture2D, RectangleF, Color)</c> são atalhos do caminho canônico — mesmos vértices, UV, cor, textura e
/// número de lotes que <c>FillRect</c>/a sobrecarga completa, sem textura nova e sem alocação no caminho aquecido.
/// </summary>
[Collection("Frame allocation")]
public sealed class RectangleDrawTests
{
    private static (SpriteBatch Batch, RecordingBackend Backend, GraphicsDevice Device) New()
    {
        var backend = new RecordingBackend(); var device = new GraphicsDevice(backend, 360, 640); device.Resize(720, 1280);
        return (new SpriteBatch(device), backend, device);
    }

    [Fact]
    public void RectangleFromPositionAndSize_EqualsTheFourFloatConstructor()
    {
        var pos = new Vector2(100.5f, -20f); var size = new Vector2(32, 64);
        Assert.Equal(new RectangleF(100.5f, -20f, 32, 64), new RectangleF(pos, size));
        Assert.Equal(pos, new RectangleF(pos, size).Position);
        Assert.Equal(size, new RectangleF(pos, size).Size);
        Assert.Equal(new RectangleF(0, 0, 0, 0), new RectangleF(Vector2.Zero, Vector2.Zero));
        Assert.Equal(-5f, new RectangleF(Vector2.Zero, new Vector2(-5, 3)).Width); // no hidden clamping or rounding
        RectangleF targetTyped = new(pos, size);                                    // target-typed new resolves
        Assert.Equal(new RectangleF(pos, size), targetTyped);
    }

    [Fact]
    public void DrawRectangleColor_IsExactlyFillRect_AndReusesTheWhitePixel()
    {
        var rect = new RectangleF(new Vector2(10, 20), new Vector2(30, 40));
        var (a, recordA, _) = New(); a.Begin(); a.FillRect(rect, Color.Red); a.End();
        var (b, recordB, device) = New(); b.Begin(); b.Draw(rect, Color.Red); b.End();
        var fill = Assert.Single(recordA.Batches); var draw = Assert.Single(recordB.Batches);
        Assert.Equal(fill.QuadCount, draw.QuadCount);
        Assert.Equal(fill.Vertices, draw.Vertices);                      // positions, UV and color identical
        Assert.Equal(device.WhiteTexture.Handle, draw.Texture);
        var textures = recordB.LiveTextures.Count;
        b.Begin(); for (var i = 0; i < 50; i++) b.Draw(rect, Color.Blue); b.End();
        Assert.Equal(textures, recordB.LiveTextures.Count);              // no texture per shape or per frame
    }

    [Fact]
    public void DrawTextureRectangleColor_EqualsTheFullOverload_AndKeepsOrderAndBatches()
    {
        var (a, recordA, deviceA) = New(); var texA = Texture2D.CreateSolid(deviceA, 4, 4, Color.White);
        var (b, recordB, deviceB) = New(); var texB = Texture2D.CreateSolid(deviceB, 4, 4, Color.White);
        var ground = new RectangleF(0, 300, 360, 40); var player = new RectangleF(new Vector2(100, 100), new Vector2(32, 64));
        a.Begin(); a.Draw(texA, ground, null, Color.Green, 0f, Vector2.Zero); a.FillRect(player, Color.Red); a.Draw(texA, player, null, Color.White, 0f, Vector2.Zero); a.End();
        b.Begin(); b.Draw(texB, ground, Color.Green); b.Draw(player, Color.Red); b.Draw(texB, player, Color.White); b.End();
        Assert.Equal(recordA.Batches.Count, recordB.Batches.Count);
        for (var i = 0; i < recordA.Batches.Count; i++) Assert.Equal(recordA.Batches[i].Vertices, recordB.Batches[i].Vertices);
        // The older overloads still resolve as before (Vector2 position, named arguments).
        b.Begin(); b.Draw(texB, new Vector2(1, 2), Color.White); b.Draw(texture: texB, position: new(3, 4), color: Color.White); b.End();
        // Target-typed new(x, y) written before the RectangleF overload still means a position (no ambiguity).
        b.Begin(); b.Draw(texB, new(5, 6), Color.White); b.End();
        Assert.Equal(new Vector2(5, 6), recordB.Batches[^1].Vertices[0].Position); // top-left at the position (virtual units)
    }

    [Fact]
    public void DrawRectangleColor_DoesNotAllocateAfterWarmup()
    {
        var batch = new SpriteBatch(new GraphicsDevice(new NoOpBackend(), 360, 640)); var rect = new RectangleF(new Vector2(1, 2), new Vector2(3, 4));
        void Frame() { batch.Begin(); for (var i = 0; i < 200; i++) batch.Draw(rect, Color.Red); batch.End(); }
        Assert.Equal(0, Allocation.Steady(() => { for (var i = 0; i < 5; i++) Frame(); }, Frame));
    }
}

/// <summary>Anexo B, parte A: System.Drawing.Size/Point na API, provados com o compilador do jogo (o mesmo do Preview e do APK).</summary>
public sealed class SizeOverloadTests
{
    [Fact]
    public void TheGameCompilerSeesSizeAndPoint_WithAliases_AndTheGroupedOverloadsResolve()
    {
        var compiler = new Lunet.Compiler.GameCompiler(new Lunet.Compiler.LoadedAssembliesReferenceProvider(typeof(Lunet.Game).Assembly));
        var source = """
            using System.Numerics;
            using Lunet;
            using Lunet.Graphics;
            using Size = System.Drawing.Size;
            using Point = System.Drawing.Point;
            class Probe
            {
                static Texture2D Make(GraphicsDevice d) => Texture2D.CreateSolid(d, new Size(32, 64), Color.Red);
                static void Resolution(GraphicsDevice d) => d.SetVirtualResolution(new Size(320, 180));
                static RenderTarget2D Target(GraphicsDevice d) => new(d, new Size(64, 64));
                static Point Cell => new(3, 4);
                static RectangleF Box(Vector2 p, Vector2 s) => new(p, s);
                static void Paint(SpriteBatch b, Texture2D t) { b.Draw(Box(Vector2.Zero, Vector2.One), Color.Red); b.Draw(t, new(1, 2), Color.White); }
            }
            """;
        var result = compiler.Compile("SizeProbe", [new Lunet.Compiler.SourceFile("Probe.cs", source)]);
        Assert.True(result.Success, string.Join("\n", result.Diagnostics));
        // Without the alias, Color and RectangleF stay Lunet's (System.Drawing is not imported globally).
        var plain = compiler.Compile("SizeProbe2", [new Lunet.Compiler.SourceFile("P.cs", "using Lunet; using Lunet.Graphics; class P { Color c = Color.Red; RectangleF r = new(0,0,1,1); }")]);
        Assert.True(plain.Success, string.Join("\n", plain.Diagnostics));
    }

    [Fact]
    public void SizeOverloads_EqualTheScalarOnes_IncludingValidation()
    {
        var backend = new RecordingBackend(); var device = new GraphicsDevice(backend, 360, 640);
        var a = Texture2D.CreateSolid(device, 3, 2, Color.Red); var b = Texture2D.CreateSolid(device, new System.Drawing.Size(3, 2), Color.Red);
        Assert.Equal((a.Width, a.Height), (b.Width, b.Height));
        Assert.Equal(backend.Pixels[a.Handle].Rgba, backend.Pixels[b.Handle].Rgba);
        var rgba = new byte[2 * 2 * 4];
        Assert.Equal(TextureFilter.Point, Texture2D.FromPixels(device, new System.Drawing.Size(2, 2), rgba, TextureFilter.Point).Filter);
        Assert.Throws<ArgumentOutOfRangeException>(() => Texture2D.FromPixels(device, new System.Drawing.Size(0, 2), rgba));
        device.SetVirtualResolution(new System.Drawing.Size(320, 180));
        Assert.Equal((320, 180), (device.VirtualWidth, device.VirtualHeight));
        Assert.Throws<ArgumentOutOfRangeException>(() => device.SetVirtualResolution(new System.Drawing.Size(-1, 180)));
        using var target = new RenderTarget2D(device, new System.Drawing.Size(64, 32));
        Assert.Equal((64, 32), (target.Width, target.Height));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RenderTarget2D(device, new System.Drawing.Size(5000, 1)));
    }
}
