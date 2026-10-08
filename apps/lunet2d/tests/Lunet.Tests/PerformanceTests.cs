using System.Numerics;
using Lunet.Graphics;
using Lunet.Input;

namespace Lunet.Tests;

/// <summary>Backend que não faz nada nem aloca: isola o custo do próprio framework.</summary>
internal sealed class NoOpBackend : IGraphicsBackend
{
    private int _next = 1;
    public int CreateTexture(int width, int height, ReadOnlySpan<byte> rgba, TextureFilter filter) => _next++;
    public void DeleteTexture(int handle) { }
    public void SetViewport(int x, int y, int width, int height) { }
    public void Clear(Color color) { }
    public void SetDrawState(DrawState state) { }
    public int CreateRenderTarget(int width, int height, TextureFilter filter, out int textureHandle) { textureHandle = _next++; return _next++; }
    public void DeleteRenderTarget(int handle) { }
    public void SetRenderTarget(int handle, int width, int height) { }
    public int CreateShader(string fragmentSource) => _next++;
    public void DeleteShader(int handle) { }
    public void DrawQuads(int textureHandle, ReadOnlySpan<SpriteVertex> vertices, int quadCount, in Matrix4x4 projection) { }
}

// Medições de alocação não podem concorrer com compilação, reflection e outros
// workloads da suíte. O limite de zero bytes continua obrigatório.
[CollectionDefinition("Frame allocation", DisableParallelization = true)]
public sealed class FrameAllocationCollection;

[Collection("Frame allocation")]
public class PerformanceTests
{
    private sealed class HotPathGame : Game
    {
        SpriteBatch _batch = null!;
        SpriteFont _font = null!;
        Texture2D _texture = null!;
        Vector2 _position;
        readonly VirtualStick _stick = new(new Vector2(60, 500), 40);
        int _timerHits;

        protected override void LoadContent()
        {
            _batch = new SpriteBatch(GraphicsDevice);
            _font = SpriteFont.CreateDefault(GraphicsDevice);
            _texture = Texture2D.CreateSolid(GraphicsDevice, 8, 8, Color.White);
            Timers.Every(0.5f, () => _timerHits++);
        }

        protected override void Update(GameTime time)
        {
            _stick.Update(Input);
            if (Input.TryGetPointer(out var p)) _position = Vector2.Lerp(_position, p, 0.2f);
            foreach (var g in Input.Gestures) _position += g.Delta;
            _position += _stick.Direction;
        }

        protected override void Draw(GameTime time)
        {
            GraphicsDevice.Clear(Color.Black);
            _batch.Begin(BlendState.Alpha, SamplerState.PointClamp);
            for (var i = 0; i < 50; i++) _batch.Draw(_texture, _position + new Vector2(i * 3, i), Color.White);
            _batch.DrawString(_font, "Pontos: 100", new Vector2(8, 8), Color.White, 2);
            _batch.Line(Vector2.Zero, _position, Color.Red, 2);
            _batch.End();
        }
    }

    private static long AllocatedPerFrame(GameHost host, Action<int> perFrame, int frames = 300)
    {
        for (var i = 0; i < 100; i++) { perFrame(i); host.Tick(1.0 / 60); } // aquecimento (JIT, caches)
        GC.Collect();
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < frames; i++) { perFrame(i); host.Tick(1.0 / 60); }
        return (GC.GetAllocatedBytesForCurrentThread() - before) / frames;
    }

    [Fact]
    public void FrameworkHotPath_DoesNotAllocatePerFrame_WithTouchAndGestures()
    {
        var host = new GameHost(new HotPathGame(), new NoOpBackend());
        Assert.True(host.Start(360, 640), host.Fault?.ToString());
        var touches = new TouchPoint[1];
        var allocated = AllocatedPerFrame(host, frame =>
        {
            // dedo arrastando em círculos; solta e toca de novo de tempos em tempos
            var angle = frame * 0.1f;
            var phase = frame % 120 < 100 ? TouchPhase.Moved : TouchPhase.Released;
            touches[0] = new TouchPoint(1, phase, new Vector2(180 + MathF.Cos(angle) * 60, 320 + MathF.Sin(angle) * 60));
            host.SetSurfaceTouches(touches);
        });
        Assert.True(allocated <= 0, $"{allocated} bytes alocados por quadro");
    }

    [Fact]
    public void FrameworkHotPath_DoesNotAllocateWithoutInput()
    {
        var host = new GameHost(new HotPathGame(), new NoOpBackend());
        host.Start(360, 640);
        Assert.True(AllocatedPerFrame(host, _ => { }) <= 0);
    }

    [Theory]
    [InlineData(30)]
    [InlineData(60)]
    [InlineData(90)]
    [InlineData(120)]
    [InlineData(144)]
    public void UpdateRateIsIndependentOfRefreshRate(int refreshHz)
    {
        var game = new CountGame();
        var host = new GameHost(game, new NoOpBackend());
        host.Start(100, 100);
        for (var i = 0; i < refreshHz * 5; i++) host.Tick(1.0 / refreshHz); // 5 s simulados
        Assert.InRange(game.Updates, 5 * 60 - 1, 5 * 60 + 1);
        Assert.Equal(refreshHz * 5, game.Draws);
    }

    private sealed class CountGame : Game
    {
        public int Updates, Draws;
        protected override void Update(GameTime time) => Updates++;
        protected override void Draw(GameTime time) => Draws++;
    }

    [Fact]
    public void DrawInterpolationStaysBetweenZeroAndOneAtHighRefreshRates()
    {
        var game = new InterpolationGame();
        var host = new GameHost(game, new NoOpBackend());
        host.Start(100, 100);
        for (var i = 0; i < 500; i++) host.Tick(1.0 / 144);
        Assert.All(game.Values, v => Assert.InRange(v, 0f, 1f));
        Assert.True(game.Values.Distinct().Count() > 3); // varia entre quadros: dá para interpolar o desenho
    }

    private sealed class InterpolationGame : Game
    {
        public List<float> Values { get; } = [];
        protected override void Draw(GameTime time) => Values.Add(time.Interpolation);
    }
}
