using System.Numerics;
using Lunet.Graphics;

namespace Lunet.Tests;

public sealed class SortedBatchTests
{
    [Theory]
    [InlineData(SpriteSortDirection.Ascending)]
    [InlineData(SpriteSortDirection.Descending)]
    public void SortsEntirePassAndPreservesEqualKeys(SpriteSortDirection direction)
    {
        var backend = new RecordingBackend();
        var device = new GraphicsDevice(backend, 100, 100);
        using var a = Texture2D.CreateSolid(device, 1, 1, Color.White);
        using var b = Texture2D.CreateSolid(device, 1, 1, Color.White);
        var batch = new SpriteBatch(device);
        batch.BeginSorted(direction: direction);
        batch.SetOrder(5);
        batch.Draw(a, new Vector2(1, 0), Color.White);
        batch.Draw(b, new Vector2(2, 0), Color.White, -2);
        batch.Draw(b, new Vector2(3, 0), Color.White);
        batch.Draw(a, new Vector2(4, 0), Color.White);
        Assert.Empty(backend.Batches);
        batch.End();
        var positions = backend.Batches.SelectMany(x => x.Vertices.Where((_, i) => i % 4 == 0)).Select(v => v.Position.X).ToArray();
        Assert.Equal(direction == SpriteSortDirection.Ascending ? new float[] { 2, 1, 3, 4 } : new float[] { 1, 3, 4, 2 }, positions);
    }

    [Theory]
    [InlineData(2047)]
    [InlineData(2048)]
    [InlineData(2049)]
    [InlineData(4097)]
    public void SortsAcrossPhysicalCapacity(int count)
    {
        var backend = new RecordingBackend();
        var device = new GraphicsDevice(backend, 100, 100);
        using var a = Texture2D.CreateSolid(device, 1, 1, Color.White);
        var batch = new SpriteBatch(device);
        batch.BeginSorted();
        for (int i = 0; i < count; i++) batch.Draw(a, new Vector2(i, 0), Color.White, -i);
        Assert.Empty(backend.Batches);
        batch.End();
        Assert.Equal(count, backend.Batches.Sum(x => x.QuadCount));
        Assert.Equal((float)(count - 1), backend.Batches[0].Vertices[0].Position.X);
        Assert.Equal(0f, backend.Batches[^1].Vertices[^4].Position.X);
        Assert.Equal((count + SpriteBatch.MaxQuads - 1) / SpriteBatch.MaxQuads, backend.Batches.Count);
    }

    [Fact]
    public void GuardsStateAndRejectsInvalidKeysAndForeignTextures()
    {
        var backend = new RecordingBackend();
        var device = new GraphicsDevice(backend, 100, 100);
        var other = new GraphicsDevice(new RecordingBackend(), 100, 100);
        using var a = Texture2D.CreateSolid(device, 1, 1, Color.White);
        using var foreign = Texture2D.CreateSolid(other, 1, 1, Color.White);
        using var shader = Shader.FromFragmentSource(device, "void main() { outColor = vec4(1.0); }");
        var batch = new SpriteBatch(device);
        var second = new SpriteBatch(device);
        second.Begin();
        Assert.Throws<InvalidOperationException>(() => batch.BeginSorted());
        second.End();
        batch.BeginSorted(shader: shader);
        Assert.Throws<ArgumentOutOfRangeException>(() => batch.SetOrder(float.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => batch.Draw(a, Vector2.Zero, Color.White, float.PositiveInfinity));
        Assert.Throws<ArgumentException>(() => batch.Draw(foreign, Vector2.Zero, Color.White));
        Assert.Throws<InvalidOperationException>(() => second.Begin());
        Assert.Throws<InvalidOperationException>(() => device.Clear(Color.Black));
        Assert.Throws<InvalidOperationException>(() => device.Resize(200, 200));
        Assert.Throws<InvalidOperationException>(() => shader.SetFloat("amount", 2));
        batch.Draw(a, Vector2.Zero, Color.White);
        batch.CancelSorted();
        Assert.Empty(backend.Batches);
        device.Clear(Color.Black);
        second.Begin();
        second.End();
    }

    [Fact]
    public void DisposedQueuedTextureClosesWithoutSending()
    {
        var backend = new RecordingBackend();
        var device = new GraphicsDevice(backend, 100, 100);
        var a = Texture2D.CreateSolid(device, 1, 1, Color.White);
        var batch = new SpriteBatch(device);
        batch.BeginSorted();
        batch.Draw(a, Vector2.Zero, Color.White);
        a.Dispose();
        Assert.Throws<ObjectDisposedException>(batch.End);
        Assert.Empty(backend.Batches);
        batch.Begin();
        batch.End();
    }

    [Fact]
    public void SoftwarePixelsKeepInterleavedAlphaOrder()
    {
        var backend = new Lunet.Runtime.SoftwareGraphicsBackend(16, 16);
        var device = new GraphicsDevice(backend, 16, 16);
        device.Resize(16, 16);
        device.Clear(Color.Black);
        using var red = Texture2D.CreateSolid(device, 16, 16, new Color(255, 0, 0, 128));
        using var blue = Texture2D.CreateSolid(device, 16, 16, new Color(0, 0, 255, 128));
        var batch = new SpriteBatch(device);
        batch.BeginSorted();
        batch.Draw(red, Vector2.Zero, Color.White, 2);
        batch.Draw(red, Vector2.Zero, Color.White, 0);
        batch.Draw(blue, Vector2.Zero, Color.White, 1);
        batch.End();
        var sorted = backend.ScreenPixel(8, 8);
        device.Clear(Color.Black);
        batch.Begin();
        batch.Draw(red, Vector2.Zero, Color.White);
        batch.Draw(blue, Vector2.Zero, Color.White);
        batch.Draw(red, Vector2.Zero, Color.White);
        batch.End();
        Assert.Equal(backend.ScreenPixel(8, 8), sorted);
        Assert.InRange(sorted.R, 158, 162);
        Assert.InRange(sorted.B, 62, 66);
    }
}
