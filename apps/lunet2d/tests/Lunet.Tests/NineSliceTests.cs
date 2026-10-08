using System.Numerics;
using Lunet.Graphics;

namespace Lunet.Tests;

[Collection("Frame allocation")]
public class NineSliceTests
{
    private static void Near(Vector2 expected, Vector2 actual)
    {
        Assert.InRange(actual.X, expected.X - 0.001f, expected.X + 0.001f);
        Assert.InRange(actual.Y, expected.Y - 0.001f, expected.Y + 0.001f);
    }

    [Fact]
    public void AtlasRegion_PreservesAsymmetricCornersUvsAndTint()
    {
        var backend = new RecordingBackend(); var device = new GraphicsDevice(backend, 360, 640);
        var batch = new SpriteBatch(device);
        using var texture = Texture2D.CreateSolid(device, 64, 64, Color.White);
        var slice = new NineSlice(texture, new(8, 12, 20, 24), 2, 3, 4, 5);
        batch.Begin(); batch.Draw(slice, new(10, 20, 100, 80), Color.Yellow); batch.End();
        var drawn = Assert.Single(backend.Batches); Assert.Equal(9, drawn.QuadCount);
        float[] x = [10, 12, 106, 110], y = [20, 23, 95, 100];
        float[] u = [8, 10, 24, 28], v = [12, 15, 31, 36];
        for (int row = 0; row < 3; row++)
        for (int col = 0; col < 3; col++)
        {
            int i = (row * 3 + col) * 4;
            Near(new(x[col], y[row]), drawn.Vertices[i].Position);
            Near(new(x[col + 1], y[row + 1]), drawn.Vertices[i + 2].Position);
            Near(new(u[col] / 64, v[row] / 64), drawn.Vertices[i].TexCoord);
            Near(new(u[col + 1] / 64, v[row + 1] / 64), drawn.Vertices[i + 2].TexCoord);
        }
        Assert.All(drawn.Vertices, vertex => Assert.Equal(Color.Yellow.PackedRgba, vertex.Color));
    }

    [Theory]
    [InlineData(40, 50, 2, 9, 4, 6)]
    [InlineData(3, 4, 1, 4, 1, 1.5f)]
    [InlineData(3, 50, 2, 6, 1, 6)]
    [InlineData(40, 4, 2, 6, 4, 1.5f)]
    public void ScaleAndSmallDestinations_PreserveCoverageWithoutOverlap(float width, float height, float scale, int count, float left, float top)
    {
        var backend = new RecordingBackend(); var device = new GraphicsDevice(backend, 360, 640);
        using var texture = Texture2D.CreateSolid(device, 20, 24, Color.White);
        var slice = new NineSlice(texture, new(0, 0, 20, 24), 2, 3, 4, 5);
        var batch = new SpriteBatch(device); batch.Begin(); batch.Draw(slice, new(0, 0, width, height), Color.White, scale); batch.End();
        var drawn = Assert.Single(backend.Batches); Assert.Equal(count, drawn.QuadCount);
        Near(new(left, top), drawn.Vertices[2].Position);
        double area = 0;
        for (int i = 0; i < drawn.Vertices.Length; i += 4)
        {
            var a = drawn.Vertices[i].Position; var b = drawn.Vertices[i + 2].Position;
            Assert.True(b.X > a.X && b.Y > a.Y);
            Assert.InRange(a.X, 0, width); Assert.InRange(b.X, 0, width);
            Assert.InRange(a.Y, 0, height); Assert.InRange(b.Y, 0, height);
            area += (b.X - a.X) * (b.Y - a.Y);
            for (int j = 0; j < i; j += 4)
            {
                var c = drawn.Vertices[j].Position; var d = drawn.Vertices[j + 2].Position;
                Assert.False(a.X < d.X && b.X > c.X && a.Y < d.Y && b.Y > c.Y);
            }
        }
        Assert.InRange(area, width * height - 0.01, width * height + 0.01);
    }

    [Fact]
    public void ZeroBordersDrawOneQuad_ZeroDestinationDrawsNothing_AndLargeScaleStaysFinite()
    {
        var backend = new RecordingBackend(); var device = new GraphicsDevice(backend, 360, 640);
        using var texture = Texture2D.CreateSolid(device, 24, 24, Color.White);
        var plain = new NineSlice(texture, new(0, 0, 24, 24), 0, 0, 0, 0);
        var bordered = new NineSlice(texture, new(0, 0, 24, 24), 6, 6, 6, 6);
        var batch = new SpriteBatch(device);
        batch.Begin(); batch.Draw(plain, new(0, 0, 30, 40), Color.White); batch.End();
        Assert.Equal(1, Assert.Single(backend.Batches).QuadCount);
        batch.Begin(); batch.Draw(plain, new(0, 0, 0, 10), Color.White); batch.Draw(bordered, new(0, 0, 10, 0), Color.White); batch.End();
        Assert.Single(backend.Batches);
        backend.Batches.Clear();
        batch.Begin(); batch.Draw(bordered, new(0, 0, 30, 40), Color.White, float.MaxValue); batch.End();
        Assert.Equal(4, Assert.Single(backend.Batches).QuadCount);
        Assert.All(backend.Batches[0].Vertices, v => Assert.True(float.IsFinite(v.Position.X) && float.IsFinite(v.Position.Y)));
    }

    [Fact]
    public void InvalidInputAndDisposedTexture_AreRejectedBeforeDrawingAnyParts()
    {
        var backend = new RecordingBackend(); var device = new GraphicsDevice(backend, 360, 640);
        using var texture = Texture2D.CreateSolid(device, 24, 24, Color.White);
        Assert.Throws<ArgumentNullException>(() => new NineSlice(null!, new(0, 0, 24, 24), 1, 1, 1, 1));
        foreach (var source in new[] { new RectangleF(-1, 0, 10, 10), new RectangleF(0, 0, 25, 24), new RectangleF(0, 0, 0, 10), new RectangleF(float.NaN, 0, 10, 10) })
            Assert.Throws<ArgumentOutOfRangeException>(() => new NineSlice(texture, source, 0, 0, 0, 0));
        foreach (var bad in new[] { -1, float.NaN, float.PositiveInfinity })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new NineSlice(texture, new(0, 0, 24, 24), bad, 1, 1, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new NineSlice(texture, new(0, 0, 24, 24), 1, bad, 1, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new NineSlice(texture, new(0, 0, 24, 24), 1, 1, bad, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new NineSlice(texture, new(0, 0, 24, 24), 1, 1, 1, bad));
        }
        Assert.Throws<ArgumentException>(() => new NineSlice(texture, new(0, 0, 24, 24), 12, 1, 12, 1));
        Assert.Throws<ArgumentException>(() => new NineSlice(texture, new(0, 0, 24, 24), 1, 12, 1, 12));
        var slice = new NineSlice(texture, new(0, 0, 24, 24), 6, 6, 6, 6);
        var batch = new SpriteBatch(device);
        Assert.Throws<InvalidOperationException>(() => batch.Draw(slice, new(0, 0, 0, 0), Color.White));
        batch.Begin();
        Assert.Throws<ArgumentNullException>(() => batch.Draw((NineSlice)null!, new(0, 0, 10, 10), Color.White));
        foreach (var rect in new[] { new RectangleF(0, 0, -1, 1), new RectangleF(0, 0, 1, -1), new RectangleF(float.NaN, 0, 1, 1), new RectangleF(0, 0, float.PositiveInfinity, 1), new RectangleF(float.MaxValue, 0, float.MaxValue, 1) })
            Assert.Throws<ArgumentOutOfRangeException>(() => batch.Draw(slice, rect, Color.White));
        foreach (var bad in new[] { 0, -1, float.NaN, float.PositiveInfinity })
            Assert.Throws<ArgumentOutOfRangeException>(() => batch.Draw(slice, new(0, 0, 10, 10), Color.White, bad));
        texture.Dispose();
        Assert.Throws<ObjectDisposedException>(() => batch.Draw(slice, new(0, 0, 10, 10), Color.White));
        Assert.Throws<ObjectDisposedException>(() => new NineSlice(texture, new(0, 0, 24, 24), 1, 1, 1, 1));
        batch.End(); Assert.Empty(backend.Batches);
    }

    [Fact]
    public void CameraStateAndCapacityFlushes_AreSharedWithNormalSprites()
    {
        var backend = new RecordingBackend(); var device = new GraphicsDevice(backend, 360, 640);
        device.Resize(720, 1280);
        using var texture = Texture2D.CreateSolid(device, 24, 24, Color.White);
        var slice = new NineSlice(texture, new(0, 0, 24, 24), 6, 6, 6, 6);
        var batch = new SpriteBatch(device); var clip = new RectangleF(10, 20, 100, 200);
        batch.Begin(BlendState.Additive, SamplerState.PointClamp, clip: clip);
        batch.Draw(slice, new(20, 30, 100, 120), Color.Red); batch.End();
        var reference = backend.Batches[0]; var state = backend.States[0]; backend.Batches.Clear(); backend.States.Clear();
        var camera = new Camera2D { Position = new(10, 20), Zoom = 2, Rotation = 0.3f };
        batch.Begin(camera, BlendState.Additive, SamplerState.PointClamp, clip: clip);
        for (int i = 0; i < 300; i++) batch.Draw(slice, new(20, 30, 100, 120), Color.Red);
        batch.End();
        Assert.Equal(2700, backend.Batches.Sum(b => b.QuadCount)); Assert.Equal(2, backend.Batches.Count);
        Assert.All(backend.States, s => Assert.Equal(state, s));
        int index = 0;
        foreach (var drawn in backend.Batches)
        foreach (var vertex in drawn.Vertices)
        {
            var original = reference.Vertices[index++ % reference.Vertices.Length];
            Near(camera.WorldToScreen(original.Position, device.ViewSize), vertex.Position);
            Assert.Equal(original.TexCoord, vertex.TexCoord); Assert.Equal(original.Color, vertex.Color);
        }
    }

    [Fact]
    public void DrawingRepeatedly_DoesNotAllocate()
    {
        var device = new GraphicsDevice(new NoOpBackend(), 360, 640); var batch = new SpriteBatch(device);
        using var texture = Texture2D.CreateSolid(device, 24, 24, Color.White);
        var slice = new NineSlice(texture, new(0, 0, 24, 24), 6, 6, 6, 6);
        void Frame()
        {
            batch.Begin();
            for (int i = 0; i < 300; i++) batch.Draw(slice, new(10, 20, i + 1, i + 1), Color.White, 2);
            batch.End();
        }
        for (int i = 0; i < 100; i++) Frame();
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 100; i++) Frame();
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [Fact]
    public void OfflineGuide_CompilesAndResizesWithScaledTouchInRealRuntime()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "docs", "guides", "nine-slice.md"))) root = root.Parent;
        Assert.NotNull(root);
        var source = File.ReadAllText(Path.Combine(root!.FullName, "docs", "guides", "nine-slice.md")).Split("```csharp\n")[1].Split("```")[0];
        var compiler = new Lunet.Compiler.GameCompiler(new Lunet.Compiler.LoadedAssembliesReferenceProvider(typeof(Game).Assembly));
        var compiled = compiler.Compile("PanelGuide", [new Lunet.Compiler.SourceFile("Game.cs", source)]);
        Assert.True(compiled.Success, string.Join("\n", compiled.Diagnostics));
        Assert.DoesNotContain(compiled.Diagnostics, d => d.Severity == Lunet.Compiler.DiagnosticSeverity.Warning);
        using var loaded = Lunet.Runtime.GameLoader.Load(compiled.Assembly!, compiled.Symbols);
        var backend = new RecordingBackend(); var host = new GameHost(loaded.Game, backend);
        Assert.True(host.Start(720, 1280)); host.Tick(1.0 / 60);
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        object Field(string name) => loaded.Game.GetType().GetField(name, flags)!.GetValue(loaded.Game)!;
        void Touch(float x, float y)
        {
            host.SetSurfaceTouches([new Lunet.Input.TouchPoint(0, Lunet.Input.TouchPhase.Pressed, new(x * 2, y * 2))]); host.Tick(1.0 / 60);
            host.SetSurfaceTouches([]); host.Tick(1.0 / 60);
        }
        Touch(50, 60); Assert.Equal(2f, Field("borderScale"));
        Touch(28, 144); Assert.Equal(new RectangleF(24, 140, 4, 4), Field("bounds"));
        Touch(300, 500); Assert.Equal(new RectangleF(24, 140, 276, 360), Field("bounds"));
        Assert.False(host.IsFaulted); host.Stop();
        Assert.True(((Texture2D)Field("texture")).IsDisposed);
    }
}
