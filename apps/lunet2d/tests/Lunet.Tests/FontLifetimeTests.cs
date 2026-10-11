using System.Numerics;
using Lunet.Graphics;

namespace Lunet.Tests;

public sealed class FontLifetimeTests
{
    [Fact]
    public void DefaultFontReleasesItsOwnAtlasIdempotently()
    {
        var backend = new RecordingBackend();
        var device = new GraphicsDevice(backend, 320, 640);
        var font = SpriteFont.CreateDefault(device);
        Assert.Single(backend.LiveTextures);
        Vector2 metrics = font.Measure("Olá");
        font.Dispose(); font.Dispose();
        Assert.Empty(backend.LiveTextures);
        Assert.Equal(metrics, font.Measure("Olá"));
        var batch = new SpriteBatch(device);
        batch.Begin();
        Assert.Throws<ObjectDisposedException>(() => batch.DrawString(font, "Olá", Vector2.Zero, Color.White));
        batch.End();
    }

    [Fact]
    public void BitmapFontDoesNotDisposeTheBorrowedAtlas()
    {
        var backend = new RecordingBackend();
        var device = new GraphicsDevice(backend, 320, 640);
        using var texture = Texture2D.CreateSolid(device, 8, 8, Color.White);
        var glyph = new BitmapGlyph(new(0, 0, 8, 8), 8, Vector2.Zero);
        var font = SpriteFont.FromBitmap(texture, new Dictionary<int, BitmapGlyph> { ['?'] = glyph }, 8);
        font.Dispose(); font.Dispose();
        Assert.False(texture.IsDisposed); Assert.Single(backend.LiveTextures);
        var batch = new SpriteBatch(device);
        batch.Begin(); batch.DrawString(font, "?", Vector2.Zero, Color.White); batch.End();
        Assert.Single(backend.Batches);
    }
}
