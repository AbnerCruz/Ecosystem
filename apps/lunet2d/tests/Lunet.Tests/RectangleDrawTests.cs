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
