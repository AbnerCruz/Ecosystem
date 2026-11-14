using System.Numerics;
using Lunet.Graphics;
using Lunet.Runtime;

namespace Lunet.Tests;

/// <summary>LUNET-482: the CPU backend shows what the GL backend would, so desktop and tests can see the game.</summary>
public sealed class SoftwareRendererTests
{
    private sealed class Scene : Game
    {
        public Action<SpriteBatch, GraphicsDevice>? Paint;
        private SpriteBatch _batch = null!;
        protected override void LoadContent() => _batch = new SpriteBatch(GraphicsDevice);
        protected override void Draw(GameTime time) { GraphicsDevice.Clear(Color.Black); Paint?.Invoke(_batch, GraphicsDevice); }
    }

    private static SoftwareGraphicsBackend Render(Action<SpriteBatch, GraphicsDevice> paint, int w = 64, int h = 48)
    {
        var game = new Scene { Paint = paint };
        game.Configuration.VirtualWidth = w; game.Configuration.VirtualHeight = h;
        var backend = new SoftwareGraphicsBackend(w, h);
        var host = new GameHost(game, backend);
        Assert.True(host.Start(w, h), host.Fault?.ToString());
        host.Tick(1 / 30.0);
        Assert.False(host.IsFaulted, host.Fault?.ToString());
        return backend;
    }

    [Fact]
    public void DrawsSpritesWhereTheGameSays_WithTopLeftOrigin()
    {
        var screen = Render((b, d) =>
        {
            var red = Texture2D.CreateSolid(d, 1, 1, Color.Red);
            b.Begin();
            b.Draw(red, new RectangleF(10, 5, 8, 4), null, Color.White, 0, Vector2.Zero);
            b.End();
        });
        Assert.Equal(Color.Red, screen.ScreenPixel(12, 6));
        Assert.Equal(Color.Black, screen.ScreenPixel(12, 20)); // below the rectangle
        Assert.Equal(Color.Black, screen.ScreenPixel(2, 6));
        Assert.Equal(1, screen.QuadsDrawn);
    }

    [Fact]
    public void TintAlphaAndAdditiveBlendMatchGl()
    {
        var screen = Render((b, d) =>
        {
            var white = Texture2D.CreateSolid(d, 1, 1, Color.White);
            b.Begin();
            b.Draw(white, new RectangleF(0, 0, 10, 10), null, new Color(255, 0, 0, 128), 0, Vector2.Zero);
            b.End();
            b.Begin(BlendState.Additive);
            b.Draw(white, new RectangleF(20, 0, 10, 10), null, new Color(0, 0, 255, 255), 0, Vector2.Zero);
            b.Draw(white, new RectangleF(20, 0, 10, 10), null, new Color(0, 255, 0, 255), 0, Vector2.Zero);
            b.End();
        });
        var half = screen.ScreenPixel(5, 5);
        Assert.InRange(half.R, 126, 130);
        Assert.Equal(0, half.G);
        Assert.Equal(new Color(0, 255, 255, 255), screen.ScreenPixel(25, 5));
    }

    [Fact]
    public void RenderTargets_KeepTheImageUpright_AndScissorClips()
    {
        var screen = Render((b, d) =>
        {
            var target = new RenderTarget2D(d, 16, 16);
            var green = Texture2D.CreateSolid(d, 1, 1, Color.Green);
            d.SetRenderTarget(target);
            d.Clear(Color.Transparent);
            b.Begin();
            b.Draw(green, new RectangleF(0, 0, 16, 4), null, Color.White, 0, Vector2.Zero); // top strip of the target
            b.End();
            d.SetRenderTarget(null);
            b.Begin(clip: new RectangleF(0, 0, 40, 48));
            b.Draw(target.Texture, new RectangleF(30, 10, 16, 16), null, Color.White, 0, Vector2.Zero);
            b.End();
        });
        Assert.Equal(Color.Green, screen.ScreenPixel(35, 11));   // top strip stays on top
        Assert.Equal(Color.Black, screen.ScreenPixel(35, 22));
        Assert.Equal(Color.Black, screen.ScreenPixel(42, 11));   // clipped at x = 40
    }

    [Fact]
    public void CopyScreenRgba_IsTopDown()
    {
        var screen = Render((b, d) =>
        {
            var blue = Texture2D.CreateSolid(d, 1, 1, new Color(0, 0, 255));
            b.Begin();
            b.Draw(blue, new RectangleF(0, 0, 64, 1), null, Color.White, 0, Vector2.Zero);
            b.End();
        });
        var rgba = new byte[64 * 48 * 4];
        screen.CopyScreenRgba(rgba);
        Assert.Equal([0, 0, 255, 255], rgba[..4]);
        Assert.Equal([0, 0, 0, 255], rgba[(47 * 64 * 4)..(47 * 64 * 4 + 4)]);
    }
}
