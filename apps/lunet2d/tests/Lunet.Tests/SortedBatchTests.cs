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

/// <summary>E1.6 gaps found in the 26/11 audit: ±0 ties, −∞, A/B/A texture switches with equal keys, and the new rectangle shortcut in a sorted pass.</summary>
public sealed class SortedBatchEdgeTests
{
    private static float[] Xs(RecordingBackend backend) =>
        backend.Batches.SelectMany(x => x.Vertices.Where((_, i) => i % 4 == 0)).Select(v => v.Position.X).ToArray();

    [Fact]
    public void PlusAndMinusZeroAreTheSameKey_SubmissionOrderBreaksTheTie()
    {
        var backend = new RecordingBackend(); var device = new GraphicsDevice(backend, 100, 100);
        using var a = Texture2D.CreateSolid(device, 1, 1, Color.White);
        var batch = new SpriteBatch(device);
        batch.BeginSorted();
        batch.Draw(a, new Vector2(1, 0), Color.White, 0f);
        batch.Draw(a, new Vector2(2, 0), Color.White, -0f);
        batch.Draw(a, new Vector2(3, 0), Color.White, 0f);
        batch.End();
        Assert.Equal(new float[] { 1, 2, 3 }, Xs(backend));
    }

    [Fact]
    public void NegativeInfinityIsRejected_AndThePassStaysUsable()
    {
        var device = new GraphicsDevice(new RecordingBackend(), 100, 100);
        using var a = Texture2D.CreateSolid(device, 1, 1, Color.White);
        var batch = new SpriteBatch(device);
        batch.BeginSorted();
        Assert.Throws<ArgumentOutOfRangeException>(() => batch.Draw(a, Vector2.Zero, Color.White, float.NegativeInfinity));
        Assert.Throws<ArgumentOutOfRangeException>(() => batch.SetOrder(float.NegativeInfinity));
        batch.Draw(a, Vector2.Zero, Color.White, -1e30f);
        batch.End();
    }

    [Fact]
    public void ABA_WithEqualKeys_KeepsSubmissionOrderAcrossTextureSwitches()
    {
        var backend = new RecordingBackend(); var device = new GraphicsDevice(backend, 100, 100);
        using var a = Texture2D.CreateSolid(device, 1, 1, Color.White);
        using var b = Texture2D.CreateSolid(device, 1, 1, Color.White);
        var batch = new SpriteBatch(device);
        batch.BeginSorted();
        batch.Draw(a, new Vector2(1, 0), Color.White, 1);
        batch.Draw(b, new Vector2(2, 0), Color.White, 1);
        batch.Draw(a, new Vector2(3, 0), Color.White, 1);
        batch.End();
        Assert.Equal(new float[] { 1, 2, 3 }, Xs(backend));                       // never regrouped by texture
        Assert.Equal(new[] { a.Handle, b.Handle, a.Handle }, backend.Batches.Select(x => x.Texture));
    }

    [Fact]
    public void TheRectangleShortcut_UsesTheCurrentOrderInASortedPass()
    {
        var backend = new RecordingBackend(); var device = new GraphicsDevice(backend, 100, 100);
        using var a = Texture2D.CreateSolid(device, 1, 1, Color.White);
        var batch = new SpriteBatch(device);
        batch.BeginSorted();
        batch.SetOrder(10);
        batch.Draw(new RectangleF(new Vector2(1, 0), new Vector2(1, 1)), Color.Red); // drawn later (key 10)
        batch.SetOrder(0);
        batch.Draw(a, new Vector2(2, 0), Color.White);                               // drawn first (key 0)
        batch.End();
        Assert.Equal(new float[] { 2, 1 }, Xs(backend));
    }

    [Fact]
    public void ConsecutiveSameTexture_IsOneBatch_InSortedMode()
    {
        var backend = new RecordingBackend(); var device = new GraphicsDevice(backend, 100, 100);
        using var a = Texture2D.CreateSolid(device, 1, 1, Color.White);
        using var b = Texture2D.CreateSolid(device, 1, 1, Color.White);
        var batch = new SpriteBatch(device);
        batch.BeginSorted();
        batch.Draw(a, new Vector2(1, 0), Color.White, 1);
        batch.Draw(a, new Vector2(2, 0), Color.White, 2);
        batch.Draw(b, new Vector2(3, 0), Color.White, 3);
        batch.End();
        Assert.Equal([2, 1], backend.Batches.Select(x => x.QuadCount));
    }
}

/// <summary>E1.7: every SpriteBatch consumer draws the same vertices in a sorted pass with one key as in the legacy pass.</summary>
public sealed class SortedBatchConsumerTests
{
    private static List<SpriteVertex[]> Draw(bool sorted)
    {
        var backend = new RecordingBackend(); var device = new GraphicsDevice(backend, 200, 200);
        var tex = Texture2D.CreateSolid(device, 16, 16, Color.White);
        var font = SpriteFont.CreateDefault(device);
        var atlas = new TextureAtlas(tex, [new AtlasRegion("a", new RectangleF(0, 0, 8, 8))]);
        var slice = new NineSlice(tex, new RectangleF(0, 0, 16, 16), 4, 4, 4, 4);
        var sprite = new Sprite(tex, new RectangleF(4, 4, 8, 8));
        var particles = new ParticleEmitter(tex, new ParticleSettings(2, 50, 50), capacity: 16, seed: 7);
        particles.Burst(5, new Vector2(100, 100));
        particles.Update(0.1);
        var map = TileMap.Create("Textures/t.png", 3, 2, 8, 8).WithTile(0, new Lunet.Pathfinding.GridPoint(1, 1), 1).WithTile(0, new Lunet.Pathfinding.GridPoint(2, 0), 2);
        var batch = new SpriteBatch(device);
        if (sorted) batch.BeginSorted(); else batch.Begin();
        batch.Draw(tex, new Vector2(1, 2), new RectangleF(0, 0, 4, 4), Color.White); // cropped texture
        batch.Draw(atlas, "a", new Vector2(20, 20), Color.White);
        batch.Draw(sprite, new Vector2(40, 40));
        batch.DrawString(font, "Ola", new Vector2(60, 60), Color.White);
        batch.Draw(slice, new RectangleF(80, 80, 40, 30), Color.White);
        batch.FillRect(new RectangleF(5, 150, 30, 10), Color.Red);
        batch.Rect(new RectangleF(50, 150, 30, 10), Color.Green, 2);
        particles.Draw(batch);
        map.Draw(batch, tex, new RectangleF(0, 0, 200, 200), new Vector2(120, 150), Color.White);
        var scene = new Lunet.Scenes.Scene2D();
        var entity = new Lunet.Scenes.Entity2D();
        entity.Add(new Box(tex));
        scene.Add(entity);
        scene.Draw(batch, new GameTime(0, 1 / 60f, 1));
        batch.End();
        return backend.Batches.Select(b => b.Vertices).ToList();
    }

    private sealed class Box(Texture2D texture) : Lunet.Scenes.Component2D
    {
        public override void Draw(SpriteBatch batch, GameTime time) => batch.Draw(texture, new RectangleF(170, 10, 12, 12), Color.Blue);
    }

    [Fact]
    public void ConsumersDrawTheSameVertices_SortedWithOneKeyAsLegacy()
    {
        var legacy = Draw(sorted: false).SelectMany(v => v).ToArray();
        var sorted = Draw(sorted: true).SelectMany(v => v).ToArray();
        Assert.NotEmpty(legacy);
        Assert.Equal(legacy, sorted);
    }
}
