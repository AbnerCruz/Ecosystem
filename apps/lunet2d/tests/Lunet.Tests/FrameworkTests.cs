using System.Numerics;
using Lunet.Graphics;
using Lunet.Input;

namespace Lunet.Tests;

public class FrameworkTests
{
    [Fact]
    public void FixedLoop_RunsStepsForElapsedTimeAndKeepsRemainder()
    {
        var loop = new FixedTimestepLoop(60, 0.25);
        Assert.Equal(0, loop.Advance(0.005));
        Assert.Equal(1, loop.Advance(0.0125));
        Assert.Equal(3, loop.Advance(0.05));
    }

    [Fact]
    public void FixedLoop_ClampsHugeFramesToAvoidSpiralOfDeath()
    {
        var loop = new FixedTimestepLoop(60, 0.25);
        Assert.Equal(15, loop.Advance(30));
    }

    [Fact]
    public void FixedLoop_ReportsInterpolationWithinStep()
    {
        var loop = new FixedTimestepLoop(10, 0.25);
        loop.Advance(0.15);
        Assert.InRange(loop.Interpolation, 0.49f, 0.51f);
    }

    [Fact]
    public void GraphicsDevice_LetterboxesAndMapsSurfaceToVirtual()
    {
        var device = new GraphicsDevice(new RecordingBackend(), 360, 640);
        device.Resize(1080, 1920);
        Assert.Equal(new Vector2(180, 320), device.SurfaceToVirtual(new Vector2(540, 960)));

        device.Resize(1920, 1080); // paisagem: barras laterais
        var mapped = device.SurfaceToVirtual(new Vector2(960, 540));
        Assert.Equal(180, mapped.X, 0.01);
        Assert.Equal(320, mapped.Y, 0.01);
    }

    [Fact]
    public void ViewportScaling_FitHasBars_FillCoversWithoutDistortionAndMapsTouches()
    {
        var device = new GraphicsDevice(new RecordingBackend(), 360, 640);
        device.Resize(1920, 1080);
        Assert.Equal(ViewportScalingMode.Fit, device.ViewportScaling);
        var fitted = device.Viewport;
        Assert.True(fitted.Width <= 1920);
        Assert.True(fitted.Height <= 1080);

        device.ViewportScaling = ViewportScalingMode.Fill;
        var filled = device.Viewport;
        Assert.True(filled.X <= 0 && filled.Y <= 0);
        Assert.True(filled.X + filled.Width >= 1920);
        Assert.True(filled.Y + filled.Height >= 1080);
        Assert.Equal(new Vector2(180, 320), device.SurfaceToVirtual(new Vector2(960, 540)));
        var point = new Vector2(24, 556);
        var actual = device.SurfaceToVirtual(device.VirtualToSurface(point));
        Assert.InRange(actual.X, point.X - .001f, point.X + .001f);
        Assert.InRange(actual.Y, point.Y - .001f, point.Y + .001f);
        Assert.True(device.SafeArea.Y > 0);
        Assert.True(device.SafeArea.Height < 640);
        Assert.True(device.SafeArea.Width <= 360);
        device.ViewportScaling = ViewportScalingMode.Fit;
        Assert.Equal(fitted, device.Viewport);
    }

    [Fact]
    public void ViewportScaling_FillHandlesPortraitAndPixelPerfect()
    {
        var device = new GraphicsDevice(new RecordingBackend(), 360, 640);
        device.Resize(412, 915);
        device.ViewportScaling = ViewportScalingMode.Fill;
        Assert.True(device.Viewport.Width >= 412);
        Assert.True(device.Viewport.Height >= 915);
        device.PixelPerfect = true;
        Assert.True(device.Viewport.Width >= 412);
        Assert.True(device.Viewport.Height >= 915);
        Assert.Equal(MathF.Ceiling(MathF.Max(412f / 360, 915f / 640)), device.Scale);
        device.Resize(1920, 1080);
        Assert.True(device.Viewport.Width >= 1920);
        Assert.True(device.Viewport.Height >= 1080);
        device.ViewportScaling = ViewportScalingMode.Fit;
        Assert.True(device.Viewport.Width <= 1920);
        Assert.True(device.Viewport.Height <= 1080);
        Assert.Throws<ArgumentOutOfRangeException>(() => device.ViewportScaling = (ViewportScalingMode)999);
        Assert.Equal(ViewportScalingMode.Fit, device.ViewportScaling);
    }

    private sealed class FillConfigGame : Game
    {
        protected override void Initialize() => Configuration.ViewportScaling = ViewportScalingMode.Fill;
    }

    [Fact]
    public void GameHost_RespectsInitialFillConfigAndClipsScissor()
    {
        var backend = new RecordingBackend();
        var host = new GameHost(new FillConfigGame(), backend);
        Assert.True(host.Start(1920, 1080));
        var device = host.GraphicsDevice!;
        Assert.Equal(ViewportScalingMode.Fill, device.ViewportScaling);
        var scissor = device.ToScissor(new RectangleF(0, 0, 360, 640));
        Assert.Equal(0, scissor.X);
        Assert.Equal(0, scissor.Y);
        Assert.Equal(1920, scissor.Width);
        Assert.Equal(1080, scissor.Height);
        using var target = new RenderTarget2D(device, 128, 64);
        device.SetRenderTarget(target);
        Assert.Equal(new Vector2(128, 64), device.ViewSize);
        device.SetRenderTarget(null);
        Assert.Equal(new Vector2(360, 640), device.ViewSize);
        host.Stop();
    }

    [Fact]
    public void ViewportScaling_NoPerFrameAllocationAfterWarmup()
    {
        var device = new GraphicsDevice(new NoOpBackend(), 360, 640);
        device.Resize(412, 915);
        for (int i = 0; i < 40; i++) { device.ViewportScaling = ViewportScalingMode.Fill; device.ViewportScaling = ViewportScalingMode.Fit; }
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++)
        {
            device.ViewportScaling = ViewportScalingMode.Fill;
            _ = device.SurfaceToVirtual(new Vector2(170, 360));
            _ = device.Viewport;
            device.ViewportScaling = ViewportScalingMode.Fit;
        }
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [Fact]
    public void SpriteBatch_GroupsSameTextureAndSplitsOnTextureChange()
    {
        var backend = new RecordingBackend();
        var device = new GraphicsDevice(backend, 100, 100);
        using var a = Texture2D.CreateSolid(device, 4, 4, Color.White);
        using var b = Texture2D.CreateSolid(device, 4, 4, Color.Red);
        var batch = new SpriteBatch(device);

        batch.Begin();
        batch.Draw(a, new Vector2(0, 0), Color.White);
        batch.Draw(a, new Vector2(10, 0), Color.White);
        batch.Draw(b, new Vector2(20, 0), Color.White);
        batch.End();

        Assert.Equal(2, backend.Batches.Count);
        Assert.Equal(2, backend.Batches[0].QuadCount);
        Assert.Equal(1, backend.Batches[1].QuadCount);
    }

    [Fact]
    public void SpriteBatch_ProducesExpectedCornersAndRotation()
    {
        var backend = new RecordingBackend();
        var device = new GraphicsDevice(backend, 100, 100);
        using var t = Texture2D.CreateSolid(device, 10, 10, Color.White);
        var batch = new SpriteBatch(device);

        batch.Begin();
        batch.Draw(t, new Vector2(5, 7), Color.White);
        batch.End();
        var v = backend.Batches[0].Vertices;
        Assert.Equal(new Vector2(5, 7), v[0].Position);
        Assert.Equal(new Vector2(15, 17), v[2].Position);
        Assert.Equal(new Vector2(1, 1), v[2].TexCoord);

        batch.Begin();
        batch.Draw(t, new RectangleF(50, 50, 10, 10), null, Color.White, MathF.PI / 2, new Vector2(5, 5));
        batch.End();
        var c = backend.LastQuadCenter();
        Assert.Equal(55, c.X, 0.001);
        Assert.Equal(55, c.Y, 0.001);
    }

    [Fact]
    public void SpriteBatch_RequiresBeginAndRejectsDisposedTexture()
    {
        var device = new GraphicsDevice(new RecordingBackend(), 10, 10);
        var t = Texture2D.CreateSolid(device, 2, 2, Color.White);
        var batch = new SpriteBatch(device);
        Assert.Throws<InvalidOperationException>(() => batch.Draw(t, Vector2.Zero, Color.White));
        t.Dispose();
        batch.Begin();
        Assert.Throws<ObjectDisposedException>(() => batch.Draw(t, Vector2.Zero, Color.White));
    }

    [Fact]
    public void SpriteBatch_FlushesWhenBufferFills()
    {
        var backend = new RecordingBackend();
        var device = new GraphicsDevice(backend, 100, 100);
        using var t = Texture2D.CreateSolid(device, 2, 2, Color.White);
        var batch = new SpriteBatch(device);
        batch.Begin();
        for (var i = 0; i < 3000; i++) batch.Draw(t, Vector2.Zero, Color.White);
        batch.End();
        Assert.Equal(2, backend.Batches.Count);
        Assert.Equal(3000, backend.Batches.Sum(b => b.QuadCount));
    }

    [Fact]
    public void Texture_RejectsWrongPixelBufferSize()
    {
        var device = new GraphicsDevice(new RecordingBackend(), 10, 10);
        Assert.Throws<ArgumentException>(() => Texture2D.FromPixels(device, 2, 2, new byte[3]));
    }

    [Fact]
    public void InputState_ReturnsFirstActiveTouch()
    {
        var input = new InputState();
        input.SetTouches([
            new TouchPoint(1, TouchPhase.Released, new Vector2(1, 1)),
            new TouchPoint(2, TouchPhase.Moved, new Vector2(3, 4)),
        ]);
        Assert.True(input.TryGetPointer(out var p));
        Assert.Equal(new Vector2(3, 4), p);
        input.SetTouches([]);
        Assert.False(input.TryGetPointer(out _));
    }

    private sealed class CountingGame : Game
    {
        public int Updates, Draws, Loads;
        protected override void LoadContent() => Loads++;
        protected override void Update(GameTime time) => Updates++;
        protected override void Draw(GameTime time) => Draws++;
    }

    [Fact]
    public void GameHost_RunsLifecycleAndFixedUpdates()
    {
        var game = new CountingGame();
        var host = new GameHost(game, new RecordingBackend());
        Assert.True(host.Start(360, 640));
        host.Tick(1.0 / 30);
        Assert.Equal(1, game.Loads);
        Assert.Equal(2, game.Updates);
        Assert.Equal(1, game.Draws);
    }

    [Fact]
    public void GameHost_PauseStopsUpdatesButKeepsDrawing_AndStepAdvancesOne()
    {
        var game = new CountingGame();
        var host = new GameHost(game, new RecordingBackend());
        host.Start(100, 100);
        host.Pause();
        host.Tick(1);
        Assert.Equal(0, game.Updates);
        Assert.Equal(1, game.Draws);
        host.Step();
        Assert.Equal(1, game.Updates);
        host.Resume();
        Assert.False(host.IsPaused);
    }

    private sealed class ThrowingGame : Game
    {
        protected override void Update(GameTime time) => throw new InvalidOperationException("boom");
    }

    [Fact]
    public void GameHost_CapturesGameExceptionsAndStopsTheGame()
    {
        var game = new ThrowingGame();
        var errors = new List<string>();
        game.Log.Written += (level, message) => { if (level == LogLevel.Error) errors.Add(message); };
        var host = new GameHost(game, new RecordingBackend());
        host.Start(100, 100);
        host.Tick(0.1);
        Assert.True(host.IsFaulted);
        Assert.Contains("boom", errors.Single());
        host.Tick(0.1); // não lança
    }

    private sealed class BadConfigGame : Game
    {
        protected override void Initialize() => Configuration.UpdatesPerSecond = 0;
    }

    [Fact]
    public void GameHost_ReportsInvalidConfiguration()
    {
        var host = new GameHost(new BadConfigGame(), new RecordingBackend());
        Assert.False(host.Start(100, 100));
        Assert.Contains("UpdatesPerSecond", host.Fault!.Message);
    }

    [Fact]
    public void GameHost_ConvertsSurfaceTouchesToVirtual()
    {
        var host = new GameHost(new CountingGame(), new RecordingBackend());
        host.Start(720, 1280); // 2x da resolução virtual 360x640
        host.SetSurfaceTouches([new TouchPoint(0, TouchPhase.Pressed, new Vector2(360, 640))]);
        Assert.True(host.Input.TryGetPointer(out var p));
        Assert.Equal(new Vector2(180, 320), p);
    }
}
