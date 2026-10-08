using System.Numerics;
using Lunet.Graphics;

namespace Lunet.Tests;

[Collection("Frame allocation")]
public class CameraTests
{
    private static void Close(Vector2 expected, Vector2 actual, float tolerance = 0.001f)
    {
        Assert.InRange(actual.X, expected.X - tolerance, expected.X + tolerance);
        Assert.InRange(actual.Y, expected.Y - tolerance, expected.Y + tolerance);
    }

    [Fact]
    public void Camera_CentersPosition_AndZoomsAroundTheCenter()
    {
        var camera = new Camera2D { Position = new(500, 400), Zoom = 2 };
        var size = new Vector2(360, 640);
        Close(new(180, 320), camera.WorldToScreen(camera.Position, size));
        Close(new(200, 320), camera.WorldToScreen(new(510, 400), size));
        Close(new(510, 400), camera.ScreenToWorld(new(200, 320), size));
        var centered = new Camera2D { Position = size / 2 };
        Assert.Equal(Matrix3x2.Identity, centered.GetViewMatrix(size));
    }

    [Theory]
    [InlineData(0.25f, 0f)]
    [InlineData(1f, 1.5707963f)]
    [InlineData(2f, -0.7f)]
    [InlineData(100f, 3.1415926f)]
    public void CoordinateConversions_RoundTripWithZoomAndRotation(float zoom, float rotation)
    {
        var camera = new Camera2D { Position = new(35, -27), Zoom = zoom, Rotation = rotation };
        var size = new Vector2(720, 1280);
        foreach (var point in new[] { Vector2.Zero, camera.Position, new Vector2(-50, 70), new Vector2(120, -80) })
            Close(point, camera.ScreenToWorld(camera.WorldToScreen(point, size), size), 0.005f);
    }

    [Fact]
    public void PositiveCameraRotation_RotatesTheWorldInTheOppositeDirection()
    {
        var camera = new Camera2D { Rotation = MathF.PI / 2 };
        Close(new(100, 90), camera.WorldToScreen(new(10, 0), new(200, 200)));
        Close(new(10, 0), camera.ScreenToWorld(new(100, 90), new(200, 200)));
    }

    [Fact]
    public void InvalidCameraState_IsRejectedWithoutChangingThePreviousValue()
    {
        var camera = new Camera2D { Position = new(10, 20), Zoom = 2, Rotation = 0.5f };
        foreach (var zoom in new[] { 0, -1, float.NaN, float.PositiveInfinity, float.Epsilon })
            Assert.Throws<ArgumentOutOfRangeException>(() => camera.Zoom = zoom);
        Assert.Equal(2, camera.Zoom);
        Assert.Throws<ArgumentOutOfRangeException>(() => camera.Position = new(float.NaN, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => camera.Position = new(0, float.NegativeInfinity));
        Assert.Equal(new Vector2(10, 20), camera.Position);
        Assert.Throws<ArgumentOutOfRangeException>(() => camera.Rotation = float.PositiveInfinity);
        Assert.Equal(0.5f, camera.Rotation);
        foreach (var size in new[] { Vector2.Zero, new Vector2(-1, 10), new Vector2(10, float.NaN), new Vector2(float.PositiveInfinity, 10) })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => camera.GetViewMatrix(size));
            Assert.Throws<ArgumentOutOfRangeException>(() => camera.ScreenToWorld(Vector2.Zero, size));
        }
    }

    [Fact]
    public void SpriteBatch_SnapshotsCameraAcrossTextureFlushes_AndResetsForHud()
    {
        var backend = new RecordingBackend();
        var device = new GraphicsDevice(backend, 200, 200);
        var batch = new SpriteBatch(device);
        using var a = Texture2D.CreateSolid(device, 2, 2, Color.White);
        using var b = Texture2D.CreateSolid(device, 2, 2, Color.Red);
        var camera = new Camera2D { Position = new(10, 20), Zoom = 2 };
        batch.Begin(camera);
        camera.Position = new(300, 300); // não pode mudar o lote em andamento
        batch.Draw(a, new Vector2(10, 20), Color.White);
        batch.Draw(b, new Vector2(10, 20), Color.White);
        batch.End();
        Assert.Equal(2, backend.Batches.Count);
        Assert.All(backend.Batches, drawn =>
        {
            Close(new(100, 100), drawn.Vertices[0].Position);
            Close(new(104, 104), drawn.Vertices[2].Position);
        });
        batch.Begin();
        batch.Draw(a, new Vector2(10, 20), Color.White);
        batch.End();
        Close(new(10, 20), backend.Batches[^1].Vertices[0].Position);
        batch.Begin(camera);
        batch.End();
        batch.Begin(new Material());
        batch.Draw(a, new Vector2(10, 20), Color.White);
        batch.End();
        Close(new(10, 20), backend.Batches[^1].Vertices[0].Position);
    }

    [Fact]
    public void SpriteBatch_CameraSurvivesCapacityFlushes()
    {
        var backend = new RecordingBackend();
        var device = new GraphicsDevice(backend, 200, 200);
        var batch = new SpriteBatch(device);
        var camera = new Camera2D { Position = new(40, 50) };
        using var texture = Texture2D.CreateSolid(device, 1, 1, Color.White);
        batch.Begin(camera);
        for (var i = 0; i < SpriteBatch.MaxQuads + 1; i++) batch.Draw(texture, new(40, 50), Color.White);
        batch.End();
        Assert.Equal(2, backend.Batches.Count);
        Assert.All(backend.Batches, drawn => Close(new(100, 100), drawn.Vertices[0].Position));
    }

    [Fact]
    public void TextAndDebugDraw_UseTheSameCamera_AndKeepUvsAndColors()
    {
        var backend = new RecordingBackend();
        var device = new GraphicsDevice(backend, 200, 200);
        var batch = new SpriteBatch(device);
        using var texture = Texture2D.CreateSolid(device, 5, 5, Color.White);
        var font = SpriteFont.CreateDefault(device);
        void Draw()
        {
            batch.Draw(texture, new(10, 20), Color.White);
            batch.DrawString(font, "A", new(30, 40), Color.Yellow);
            batch.Line(new(10, 20), new(30, 40), Color.Red, 2);
            batch.End();
        }
        batch.Begin(); Draw();
        var original = backend.Batches.ToArray();
        backend.Batches.Clear();
        var camera = new Camera2D { Position = new(30, 40), Zoom = 1.5f, Rotation = 0.5f };
        batch.Begin(camera); Draw();
        Assert.Equal(original.Length, backend.Batches.Count);
        for (var i = 0; i < original.Length; i++)
        {
            var a = original[i];
            var b = backend.Batches[i];
            Assert.Equal(a.QuadCount, b.QuadCount);
            for (var j = 0; j < a.Vertices.Length; j++)
            {
                Close(camera.WorldToScreen(a.Vertices[j].Position, device.ViewSize), b.Vertices[j].Position);
                Assert.Equal(a.Vertices[j].TexCoord, b.Vertices[j].TexCoord);
                Assert.Equal(a.Vertices[j].Color, b.Vertices[j].Color);
            }
        }
    }

    [Fact]
    public void Camera_UsesCurrentRenderTargetSize_AndClipStaysInViewCoordinates()
    {
        var backend = new RecordingBackend();
        var device = new GraphicsDevice(backend, 200, 200);
        device.Resize(200, 200);
        var batch = new SpriteBatch(device);
        using var texture = Texture2D.CreateSolid(device, 2, 2, Color.White);
        using var target = new RenderTarget2D(device, 128, 64);
        device.SetRenderTarget(target);
        var camera = new Camera2D { Position = new(500, 400), Zoom = 2 };
        var clip = new RectangleF(10, 10, 20, 20);
        batch.Begin(clip: clip);
        batch.Draw(texture, Vector2.Zero, Color.White); batch.End();
        var scissor = backend.States[^1].Scissor;
        batch.Begin(camera, clip: clip);
        batch.Draw(texture, camera.Position, Color.White); batch.End();
        Close(new(64, 32), backend.Batches[^1].Vertices[0].Position);
        Assert.Equal(scissor, backend.States[^1].Scissor);
        device.SetRenderTarget(null);
        batch.Begin(camera);
        batch.Draw(texture, camera.Position, Color.White); batch.End();
        Close(new(100, 100), backend.Batches[^1].Vertices[0].Position);
    }

    [Fact]
    public void CameraDrawingAndConversions_DoNotAllocateInTheFrameLoop()
    {
        var device = new GraphicsDevice(new NoOpBackend(), 200, 200);
        var batch = new SpriteBatch(device);
        using var texture = Texture2D.CreateSolid(device, 2, 2, Color.White);
        var camera = new Camera2D { Position = new(10, 20), Zoom = 2, Rotation = 0.4f };
        void Frame()
        {
            var point = camera.ScreenToWorld(new(100, 100), device.ViewSize);
            camera.WorldToScreen(point, device.ViewSize);
            batch.Begin(camera);
            batch.Draw(texture, point, Color.White);
            batch.End();
        }
        for (var i = 0; i < 1000; i++) Frame();
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; i++) Frame();
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [Fact]
    public void OfflineGuide_CompilesAndConvertsTouchThroughTheCameraInTheRuntime()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "docs", "guides", "camera.md"))) root = root.Parent;
        Assert.NotNull(root);
        var guide = File.ReadAllText(Path.Combine(root!.FullName, "docs", "guides", "camera.md"));
        var source = guide.Split("```csharp\n")[1].Split("```")[0];
        var compiler = new Lunet.Compiler.GameCompiler(new Lunet.Compiler.LoadedAssembliesReferenceProvider(typeof(Game).Assembly));
        var result = compiler.Compile("CameraGuide", [new Lunet.Compiler.SourceFile("Game.cs", source)]);
        Assert.True(result.Success, string.Join("\n", result.Diagnostics));
        Assert.DoesNotContain(result.Diagnostics, d => d.Severity == Lunet.Compiler.DiagnosticSeverity.Warning);
        using var loaded = Lunet.Runtime.GameLoader.Load(result.Assembly!, result.Symbols);
        var backend = new RecordingBackend();
        var host = new GameHost(loaded.Game, backend);
        Assert.True(host.Start(720, 1280)); // escala física 2×; Input converte antes da câmera
        host.Tick(1.0 / 60);
        host.SetSurfaceTouches([new Lunet.Input.TouchPoint(0, Lunet.Input.TouchPhase.Pressed, new(560, 640))]);
        host.Tick(1.0 / 60);
        var type = loaded.Game.GetType();
        var player = type.GetField("player", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        Close(new(600, 400), (Vector2)player.GetValue(loaded.Game)!);
        Assert.NotEmpty(backend.Batches);
        Assert.False(host.IsFaulted);
    }

    [Fact]
    public void NullCamera_DoesNotLeaveBatchOpen()
    {
        var batch = new SpriteBatch(new GraphicsDevice(new NoOpBackend(), 200, 200));
        Assert.Throws<ArgumentNullException>(() => batch.Begin((Camera2D)null!));
        batch.Begin();
        Assert.Throws<InvalidOperationException>(() => batch.Begin(new Camera2D()));
        batch.End();
    }
}
