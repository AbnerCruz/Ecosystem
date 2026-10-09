using System.Numerics;
using Lunet.Graphics;

namespace Lunet.Tests;

[Collection("Frame allocation")]
public sealed class DefaultBitmapFontTests
{
    [Theory]
    [InlineData("A\nB", 5, 22)]
    [InlineData("A\rB", 5, 22)]
    [InlineData("A\r\nB", 5, 22)]
    [InlineData("\r\n\n", 0, 33)]
    [InlineData("A😀\r\nB", 11, 22)]
    [InlineData("A\uD800B", 17, 11)]
    [InlineData("", 0, 11)]
    public void MeasureUsesUnicodeScalarsAndOneLineForCrLf(string text, int width, int height)
    {
        var device = new GraphicsDevice(new NoOpBackend(), 360, 640);
        var font = SpriteFont.CreateDefault(device);
        Assert.Equal(new Vector2(width, height), font.Measure(text));
    }

    [Fact]
    public void DrawCrLfAndSupplementaryCharacterUsesExactlyOneFallbackQuad()
    {
        var backend = new RecordingBackend();
        var device = new GraphicsDevice(backend, 360, 640);
        var font = SpriteFont.CreateDefault(device);
        var batch = new SpriteBatch(device);
        batch.Begin();
        batch.DrawString(font, "A😀\r\nB", Vector2.Zero, Color.White);
        batch.End();

        var drawn = Assert.Single(backend.Batches);
        Assert.Equal(3, drawn.QuadCount); // A, ?, B; CRLF não desenha '?'
        Assert.Equal(new Vector2(0, 0), drawn.Vertices[0].Position);
        Assert.Equal(new Vector2(6, 0), drawn.Vertices[4].Position);
        Assert.Equal(new Vector2(0, 11), drawn.Vertices[8].Position);
    }

    [Fact]
    public void AccentScaleAndSpaceMetricsRemainStable()
    {
        var backend = new RecordingBackend();
        var device = new GraphicsDevice(backend, 360, 640);
        var font = SpriteFont.CreateDefault(device);
        var batch = new SpriteBatch(device);
        Assert.Equal(new Vector2(34, 22), font.Measure("Olá", 2));
        Assert.Equal(new Vector2(10, 22), font.Measure(" ", 2));
        batch.Begin();
        batch.DrawString(font, "Olá", new Vector2(12, 20), Color.Yellow, 2);
        batch.End();
        Assert.Equal(3, Assert.Single(backend.Batches).QuadCount);
    }

    [Fact]
    public void InvalidScaleAndPositionFailBeforeSubmittingGeometry()
    {
        var backend = new RecordingBackend();
        var device = new GraphicsDevice(backend, 360, 640);
        var font = SpriteFont.CreateDefault(device);
        var batch = new SpriteBatch(device);
        batch.Begin();
        Assert.Throws<ArgumentNullException>(() => font.Measure(null!));
        Assert.Throws<ArgumentNullException>(() => batch.DrawString(font, null!, Vector2.Zero, Color.White));
        foreach (float bad in new[] { 0, -1, float.NaN, float.PositiveInfinity })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => font.Measure("A", bad));
            Assert.Throws<ArgumentOutOfRangeException>(() => batch.DrawString(font, "A", Vector2.Zero, Color.White, bad));
        }
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            batch.DrawString(font, "A", new Vector2(float.NaN, 0), Color.White));
        Assert.Throws<OverflowException>(() => font.Measure("AAA", float.MaxValue));
        batch.DrawString(font, "AAA", Vector2.Zero, Color.White, float.MaxValue);
        batch.End();
        Assert.Empty(backend.Batches);
    }

    [Fact]
    public void DefaultFontMeasureAndDrawDoNotAllocateEveryFrame()
    {
        var device = new GraphicsDevice(new NoOpBackend(), 360, 640);
        var font = SpriteFont.CreateDefault(device);
        var batch = new SpriteBatch(device);
        const string Text = "Olá\nA😀\r\nB";
        void Frame()
        {
            _ = font.Measure(Text, 2);
            batch.Begin();
            for (int j = 0; j < 25; j++)
                batch.DrawString(font, Text, new Vector2(12, 20), Color.White, 2);
            batch.End();
        }
        for (int i = 0; i < 40; i++) Frame();
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 80; i++) Frame();
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }
}
