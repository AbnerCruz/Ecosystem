using System.Numerics;
using Lunet.Graphics;
using Lunet.Input;

namespace Lunet.Tests;

[Collection("Frame allocation")]
public class DebugShapeTests
{
    private static (SpriteBatch Batch, RecordingBackend Backend) New()
    {
        var backend = new RecordingBackend(); var device = new GraphicsDevice(backend, 360, 640); device.Resize(720, 1280);
        return (new SpriteBatch(device), backend);
    }
    private static void Near(Vector2 expected, Vector2 actual) => Assert.True(Vector2.Distance(expected, actual) < 0.01f, $"{expected} != {actual}");
    private static Vector2 Start(RecordingBackend.Batch batch, int i) => (batch.Vertices[i * 4].Position + batch.Vertices[i * 4 + 3].Position) / 2;
    private static Vector2 End(RecordingBackend.Batch batch, int i) => (batch.Vertices[i * 4 + 1].Position + batch.Vertices[i * 4 + 2].Position) / 2;

    [Fact]
    public void PolygonPreservesInputAndConnectsTransformedVerticesInOrder()
    {
        Vector2[] vertices = { new(0, 0), new(10, 0), new(10, 20) }; var copy = vertices.ToArray();
        var matrix = Matrix3x2.CreateScale(-2, 3) * Matrix3x2.CreateRotation(0.5f) * Matrix3x2.CreateTranslation(90, 120);
        var (batch, backend) = New(); batch.Begin(); batch.Polygon(vertices, Color.Yellow, thickness: 3, transform: matrix); batch.End();
        var draw = Assert.Single(backend.Batches); Assert.Equal(3, draw.QuadCount);
        for (int i = 0; i < 3; i++) { Near(Vector2.Transform(vertices[i], matrix), Start(draw, i)); Near(Vector2.Transform(vertices[(i + 1) % 3], matrix), End(draw, i)); }
        Assert.Equal(copy, vertices); Assert.All(draw.Vertices, v => Assert.Equal(Color.Yellow.PackedRgba, v.Color));
    }

    [Fact]
    public void OpenPolylineAndRepeatedVerticesSkipOnlyZeroLengthSegments()
    {
        var (batch, backend) = New(); batch.Begin(); batch.Polygon([new(0, 0), new(0, 0), new(10, 0), new(10, 20)], Color.White, false); batch.End();
        var draw = Assert.Single(backend.Batches); Assert.Equal(2, draw.QuadCount); Near(new(0, 0), Start(draw, 0)); Near(new(10, 20), End(draw, 1));
    }

    [Fact]
    public void RayUsesWorldDistanceAndZeroLengthDoesNotDraw()
    {
        var (batch, backend) = New(); var ray = new Ray2D(new(20, 40), new(3, 4)); batch.Begin(); batch.Ray(ray, 0, Color.White); batch.Ray(ray, 50, Color.Green, 2); batch.End();
        var draw = Assert.Single(backend.Batches); Assert.Equal(1, draw.QuadCount); Near(new(20, 40), Start(draw, 0)); Near(new(50, 80), End(draw, 0));
    }

    [Fact]
    public void AxesUsePivotRotationAndSignedScaleWithSeparateColors()
    {
        var (batch, backend) = New(); var transform = new Transform2D(new(100, 200), MathF.PI / 2, new(-2, 3), new(500, -900));
        batch.Begin(); batch.Axes(transform, 10, Color.Red, Color.Green, 2); batch.End(); var draw = Assert.Single(backend.Batches);
        Assert.Equal(2, draw.QuadCount); Near(new(100, 200), Start(draw, 0)); Near(new(100, 180), End(draw, 0));
        Near(new(100, 200), Start(draw, 1)); Near(new(70, 200), End(draw, 1));
        Assert.Equal(Color.Red.PackedRgba, draw.Vertices[0].Color); Assert.Equal(Color.Green.PackedRgba, draw.Vertices[4].Color);
    }

    [Fact]
    public void GridIncludesEachBorderOnceWithAsymmetricSpacing()
    {
        var (batch, backend) = New(); batch.Begin(); batch.Grid(new(10, 20, 25, 20), new(10, 10), Color.White); batch.End();
        var draw = Assert.Single(backend.Batches); Assert.Equal(7, draw.QuadCount);
        for (int i = 0; i < 4; i++) { Near(new(i == 3 ? 35 : 10 + i * 10, 20), Start(draw, i)); Near(new(i == 3 ? 35 : 10 + i * 10, 40), End(draw, i)); }
        for (int i = 0; i < 3; i++) { Near(new(10, 20 + i * 10), Start(draw, i + 4)); Near(new(35, 20 + i * 10), End(draw, i + 4)); }
    }

    [Fact]
    public void InvalidArgumentsFailBeforeAnyPartialGeometryIsWritten()
    {
        var (batch, backend) = New(); batch.Begin(); batch.FillRect(new(0, 0, 1, 1), Color.White);
        Assert.Throws<ArgumentOutOfRangeException>(() => batch.Polygon([Vector2.Zero, Vector2.One], Color.White));
        Assert.Throws<ArgumentOutOfRangeException>(() => batch.Polygon(new Vector2[4097], Color.White));
        Assert.Throws<ArgumentException>(() => batch.Polygon([Vector2.Zero, Vector2.One, new(float.NaN, 0)], Color.White));
        Assert.Throws<ArgumentOutOfRangeException>(() => batch.Polygon([Vector2.Zero, Vector2.One, new(2, 1)], Color.White, transform: new Matrix3x2(float.NaN, 0, 0, 1, 0, 0)));
        Assert.Throws<ArgumentException>(() => batch.Ray(default, 20, Color.White));
        Assert.Throws<ArgumentException>(() => batch.Ray(new Ray2D(Vector2.Zero, new(float.PositiveInfinity, 0)), 20, Color.White));
        Assert.Throws<ArgumentOutOfRangeException>(() => batch.Ray(new Ray2D(Vector2.Zero, Vector2.One), -1, Color.White));
        Assert.Throws<ArgumentException>(() => batch.Axes(new Transform2D(Vector2.Zero, scale: new(float.NaN, 1)), 20, Color.Red, Color.Green));
        Assert.Throws<ArgumentOutOfRangeException>(() => batch.Axes(Transform2D.Identity, 0, Color.Red, Color.Green));
        Assert.Throws<ArgumentOutOfRangeException>(() => batch.Grid(new(0, 0, -1, 20), Vector2.One, Color.White));
        Assert.Throws<ArgumentOutOfRangeException>(() => batch.Grid(new(0, 0, 20, 20), new(0, 1), Color.White));
        foreach (float bad in new[] { 0, -1, float.NaN, float.PositiveInfinity })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => batch.Polygon([Vector2.Zero, Vector2.One, new(2, 1)], Color.White, thickness: bad));
            Assert.Throws<ArgumentOutOfRangeException>(() => batch.Ray(new Ray2D(Vector2.Zero, Vector2.One), 20, Color.White, bad));
            Assert.Throws<ArgumentOutOfRangeException>(() => batch.Axes(Transform2D.Identity, 20, Color.Red, Color.Green, bad));
            Assert.Throws<ArgumentOutOfRangeException>(() => batch.Grid(new(0, 0, 20, 20), Vector2.One, Color.White, bad));
        }
        batch.End(); Assert.Equal(1, Assert.Single(backend.Batches).QuadCount);
        Assert.Throws<ArgumentNullException>(() => DebugDraw.Grid(null!, default, Vector2.One, Color.White));
        Assert.Throws<ArgumentNullException>(() => DebugDraw.Polygon(null!, [Vector2.Zero, Vector2.One], Color.White, false));
    }

    [Fact]
    public void ExtremeGeometryAndGridBudgetAreRejectedBeforeDrawing()
    {
        var (batch, backend) = New(); batch.Begin();
        Assert.Throws<OverflowException>(() => batch.Polygon([new(-float.MaxValue, 0), new(float.MaxValue, 0)], Color.White, false));
        Assert.Throws<ArgumentException>(() => batch.Polygon([new(float.MaxValue, 0), Vector2.Zero, Vector2.One], Color.White, transform: Matrix3x2.CreateScale(2)));
        Assert.Throws<ArgumentException>(() => batch.Axes(new Transform2D(Vector2.Zero, scale: new(float.MaxValue, 1)), 2, Color.Red, Color.Green));
        Assert.Throws<ArgumentOutOfRangeException>(() => batch.Grid(new(0, 0, 100, 100), new(float.Epsilon, 1), Color.White));
        Assert.Throws<ArgumentOutOfRangeException>(() => batch.Grid(new(0, 0, 4095, 1), Vector2.One, Color.White));
        batch.Grid(new(0, 0, 0, 20), Vector2.One, Color.White); batch.Axes(new Transform2D(Vector2.Zero, scale: Vector2.Zero), 20, Color.Red, Color.Green);
        batch.End(); Assert.Empty(backend.Batches);
    }

    [Fact]
    public void CameraClipBlendAndCapacityFlushArePreserved()
    {
        var (batch, backend) = New(); var camera = new Camera2D { Position = new(5, 10), Zoom = 2 };
        batch.Begin(camera, blend: BlendState.Additive, clip: new(0, 0, 100, 100));
        batch.Grid(new(0, 0, 1000, 1000), Vector2.One, Color.White);
        batch.Polygon([new(0, 0), new(10, 0), new(0, 10)], Color.Yellow); batch.Grid(new(0, 0, 50, 50), Vector2.One, Color.White); batch.End();
        Assert.Equal(2107, backend.Batches.Sum(b => b.QuadCount)); Assert.True(backend.Batches.Count >= 2);
        Assert.All(backend.States, s => { Assert.Equal(BlendMode.Additive, s.Blend); Assert.NotNull(s.Scissor); });
        Near(camera.WorldToScreen(Vector2.Zero, new(360, 640)), Start(backend.Batches[0], 0));
    }

    [Fact]
    public void HelpersDoNotAllocateAfterWarmup()
    {
        var device = new GraphicsDevice(new NoOpBackend(), 360, 640); var batch = new SpriteBatch(device);
        Vector2[] vertices = { new(0, 0), new(20, 0), new(10, 20) }; var ray = new Ray2D(Vector2.Zero, Vector2.One); var transform = new Transform2D(new(50, 80), 0.5f);
        void Draw() { batch.Begin(); batch.Polygon(vertices, Color.White, transform: transform.ToMatrix()); batch.Ray(ray, 100, Color.White); batch.Axes(transform, 30, Color.Red, Color.Green); batch.Grid(new(0, 0, 100, 100), new(10, 10), Color.White); batch.End(); }
        for (int i = 0; i < 2000; i++) Draw(); long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 2000; i++) Draw(); Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [Fact]
    public void LaboratoryRunsDebugPageWithTouchCameraAndPauseWithoutEditingCode()
    {
        var root = Path.Combine(Path.GetTempPath(), "lunet-debug-lab-" + Guid.NewGuid().ToString("N"));
        try
        {
            var project = new Lunet.Core.ProjectStore(root).Create("DebugLab", Lunet.Core.ProjectTemplate.Lab);
            var compiler = new Lunet.Compiler.GameCompiler(new Lunet.Compiler.LoadedAssembliesReferenceProvider(typeof(Game).Assembly));
            var result = compiler.Compile("DebugLab", project.LoadSources().Select(s => new Lunet.Compiler.SourceFile(s.Path, s.Text)).ToList());
            Assert.True(result.Success, string.Join("\n", result.Diagnostics)); Assert.DoesNotContain(result.Diagnostics, d => d.Severity == Lunet.Compiler.DiagnosticSeverity.Warning);
            using var loaded = Lunet.Runtime.GameLoader.Load(result.Assembly!, result.Symbols); var backend = new RecordingBackend();
            var host = new GameHost(loaded.Game, backend, new Lunet.Content.DirectoryContentSource(Path.Combine(project.Directory, "Content")));
            Assert.True(host.Start(720, 1280), host.Fault?.ToString()); host.Tick(1.0 / 60);
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            object Field(string name) => loaded.Game.GetType().GetField(name, flags)!.GetValue(loaded.Game)!;
            void Touch(TouchPhase phase, float x, float y) { host.SetSurfaceTouches([new TouchPoint(1, phase, loaded.Game.GraphicsDevice.VirtualToSurface(new(x, y)))]); host.Tick(1.0 / 60); }
            void Tap(float x, float y) { Touch(TouchPhase.Pressed, x, y); Touch(TouchPhase.Released, x, y); host.SetSurfaceTouches([]); host.Tick(1.0 / 60); }
            for (int i = 0; i < 6; i++) Tap(100, 23); Assert.Equal(6, Field("page"));
            Tap(300, 270); Assert.True((bool)Field("debugHit")); Tap(100, 170); Assert.False((bool)Field("debugHit"));
            Tap(90, 494); Assert.Equal(MathF.PI / 8, ((Transform2D)Field("debugTransform")).Rotation, 5);
            Tap(260, 494); Assert.Equal(-1, ((Transform2D)Field("debugTransform")).Scale.X);
            Assert.Contains(backend.Batches, b => b.Vertices.Any(v => v.Color == Color.Yellow.PackedRgba));
            Tap(180, 438); Assert.False((bool)Field("debugShown")); backend.Batches.Clear(); host.Tick(1.0 / 60);
            Assert.DoesNotContain(backend.Batches, b => b.Vertices.Any(v => v.Color == Color.Yellow.PackedRgba));
            Tap(180, 438); Tap(180, 550); Assert.True((bool)Field("debugUseCamera")); Tap(260, 300);
            var camera = (Camera2D)Field("debugCamera"); Near(camera.ScreenToWorld(new(260, 300), new(360, 640)), (Vector2)Field("debugTarget"));
            Touch(TouchPhase.Pressed, 180, 438); host.Pause(); host.Resume(); Touch(TouchPhase.Released, 180, 438); Assert.True((bool)Field("debugShown"));
            for (int next = 7; next < 12; next++) { Tap(100, 23); Assert.Equal(next, Field("page")); } Tap(100, 23); Assert.Equal(0, Field("page")); Assert.False(host.IsFaulted, host.Fault?.ToString()); host.Stop(); Assert.Empty(backend.LiveTargets); Assert.Empty(backend.LiveShaders);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
