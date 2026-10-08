using System.Numerics;
using Lunet.Graphics;

namespace Lunet.Tests;

[Collection("Frame allocation")]
public class BitmapFontTests
{
    private static Dictionary<int, BitmapGlyph> Glyphs() => new()
    {
        ['A'] = new(new(2, 3, 4, 6), 7, new(-1, 2)),
        ['i'] = new(new(8, 3, 2, 6), 3),
        ['?'] = new(new(12, 3, 4, 6), 5),
        [' '] = new(default, 2),
        [0x1F600] = new(new(18, 3, 6, 6), 8),
        [0x301] = new(new(26, 3, 2, 2), 0, new(-3, -1))
    };

    private static void Near(Vector2 expected, Vector2 actual)
    {
        Assert.InRange(actual.X, expected.X - 0.001f, expected.X + 0.001f);
        Assert.InRange(actual.Y, expected.Y - 0.001f, expected.Y + 0.001f);
    }

    [Fact]
    public void ProportionalGlyphs_UseOffsetsUvsAdvancesAndTint()
    {
        var backend = new RecordingBackend(); var device = new GraphicsDevice(backend, 360, 640);
        using var texture = Texture2D.CreateSolid(device, 32, 16, Color.White);
        var font = SpriteFont.FromBitmap(texture, Glyphs(), 10); var batch = new SpriteBatch(device);
        Assert.Equal(new Vector2(24, 40), font.Measure("Ai \n?", 2));
        batch.Begin(); batch.DrawString(font, "Ai \n?", new(20, 30), Color.Yellow, 2); batch.End();
        var drawn = Assert.Single(backend.Batches); Assert.Equal(3, drawn.QuadCount);
        Near(new(18, 34), drawn.Vertices[0].Position); Near(new(26, 46), drawn.Vertices[2].Position);
        Near(new(34, 30), drawn.Vertices[4].Position); Near(new(38, 42), drawn.Vertices[6].Position);
        Near(new(20, 50), drawn.Vertices[8].Position);
        Near(new(2f / 32, 3f / 16), drawn.Vertices[0].TexCoord);
        Near(new(6f / 32, 9f / 16), drawn.Vertices[2].TexCoord);
        Assert.All(drawn.Vertices, v => Assert.Equal(Color.Yellow.PackedRgba, v.Color));
    }

    [Theory]
    [InlineData("A\ni", 7, 20)]
    [InlineData("A\ri", 7, 20)]
    [InlineData("A\r\ni", 7, 20)]
    [InlineData("\r\n\n", 0, 30)]
    [InlineData("", 0, 10)]
    public void NewlinesAndEmptyText_HaveConsistentLayoutAndDrawing(string text, int width, int height)
    {
        var backend = new RecordingBackend(); var device = new GraphicsDevice(backend, 360, 640);
        using var texture = Texture2D.CreateSolid(device, 32, 16, Color.White);
        var font = SpriteFont.FromBitmap(texture, Glyphs(), 10); var batch = new SpriteBatch(device);
        Assert.Equal(new Vector2(width, height), font.Measure(text));
        batch.Begin(); batch.DrawString(font, text, Vector2.Zero, Color.White); batch.End();
        if (width == 0) Assert.Empty(backend.Batches);
        else { Assert.Equal(2, Assert.Single(backend.Batches).QuadCount); Near(new(0, 10), backend.Batches[0].Vertices[4].Position); }
    }

    [Fact]
    public void UnicodeFallbackAndCombiningMarks_UseOneQuadPerScalar()
    {
        var backend = new RecordingBackend(); var device = new GraphicsDevice(backend, 360, 640);
        using var texture = Texture2D.CreateSolid(device, 32, 16, Color.White);
        var font = SpriteFont.FromBitmap(texture, Glyphs(), 10); var batch = new SpriteBatch(device);
        string text = "😀Z\uD800A\u0301";
        Assert.Equal(new Vector2(25, 10), font.Measure(text));
        batch.Begin(); batch.DrawString(font, text, Vector2.Zero, Color.White); batch.End();
        var v = Assert.Single(backend.Batches).Vertices; Assert.Equal(20, v.Length);
        Near(new(18f / 32, 3f / 16), v[0].TexCoord);
        Near(v[4].TexCoord, v[8].TexCoord); // ausente e UTF-16 inválido usam o mesmo fallback
        Near(new(22, -1), v[16].Position); // marca depois de A, avanço zero
    }

    [Fact]
    public void CreationCopiesMetrics_AndDoesNotOwnTheTexture()
    {
        var device = new GraphicsDevice(new NoOpBackend(), 360, 640);
        using var texture = Texture2D.CreateSolid(device, 32, 16, Color.White);
        var map = Glyphs(); var font = SpriteFont.FromBitmap(texture, map, 10);
        map['A'] = new(default, 100); map.Clear();
        Assert.Equal(new Vector2(7, 10), font.Measure("A")); Assert.Equal(10, font.LineHeight);
        Assert.False(texture.IsDisposed);
        var customFallback = SpriteFont.FromBitmap(texture, new Dictionary<int, BitmapGlyph> { ['0'] = new(default, 4) }, 12, '0');
        Assert.Equal(new Vector2(4, 12), customFallback.Measure("X"));
    }

    [Fact]
    public void InvalidGlyphsTextureMetricsAndLayoutArguments_AreRejected()
    {
        var device = new GraphicsDevice(new NoOpBackend(), 360, 640);
        using var texture = Texture2D.CreateSolid(device, 32, 16, Color.White);
        foreach (float bad in new[] { -1, float.NaN, float.PositiveInfinity })
            Assert.Throws<ArgumentOutOfRangeException>(() => new BitmapGlyph(default, bad));
        Assert.Throws<ArgumentOutOfRangeException>(() => new BitmapGlyph(default, 1, new(float.NaN, 0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new BitmapGlyph(default, 1, new(0, float.PositiveInfinity)));
        foreach (int code in new[] { -1, 0xD800, 0x110000, (int)'\n', (int)'\r' })
        {
            var map = Glyphs(); map[code] = default;
            Assert.Throws<ArgumentException>(() => SpriteFont.FromBitmap(texture, map, 10));
        }
        foreach (var source in new[] { new RectangleF(-1, 0, 1, 1), new RectangleF(0, 0, 33, 1), new RectangleF(0, 0, 1, 0), new RectangleF(0, 0, -1, -1), new RectangleF(float.NaN, 0, 1, 1), new RectangleF(0, 0, 1, float.PositiveInfinity) })
        {
            var map = Glyphs(); map['A'] = new(source, 1);
            Assert.Throws<ArgumentException>(() => SpriteFont.FromBitmap(texture, map, 10));
        }
        Assert.Throws<ArgumentNullException>(() => SpriteFont.FromBitmap(null!, Glyphs(), 10));
        Assert.Throws<ArgumentNullException>(() => SpriteFont.FromBitmap(texture, null!, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => SpriteFont.FromBitmap(texture, Glyphs(), 0));
        Assert.Throws<ArgumentException>(() => SpriteFont.FromBitmap(texture, new Dictionary<int, BitmapGlyph>(), 10));
        Assert.Throws<ArgumentException>(() => SpriteFont.FromBitmap(texture, Glyphs(), 10, 0xD800));
        var font = SpriteFont.FromBitmap(texture, Glyphs(), 10); var batch = new SpriteBatch(device); batch.Begin();
        Assert.Throws<ArgumentNullException>(() => font.Measure(null!));
        Assert.Throws<ArgumentNullException>(() => batch.DrawString(font, null!, Vector2.Zero, Color.White));
        foreach (float bad in new[] { 0, -1, float.NaN, float.PositiveInfinity })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => font.Measure("A", bad));
            Assert.Throws<ArgumentOutOfRangeException>(() => batch.DrawString(font, "A", Vector2.Zero, Color.White, bad));
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => batch.DrawString(font, "A", new(float.PositiveInfinity, 0), Color.White));
        texture.Dispose();
        Assert.Throws<ObjectDisposedException>(() => SpriteFont.FromBitmap(texture, Glyphs(), 10));
        Assert.Throws<ObjectDisposedException>(() => batch.DrawString(font, "", Vector2.Zero, Color.White)); batch.End();
    }

    [Fact]
    public void ExtremeLayout_ThrowsOnMeasurementAndOmitsUnrepresentableQuads()
    {
        var backend = new RecordingBackend(); var device = new GraphicsDevice(backend, 360, 640);
        using var texture = Texture2D.CreateSolid(device, 32, 16, Color.White);
        var map = Glyphs(); map['A'] = new(new(0, 0, 1, 1), float.MaxValue);
        var font = SpriteFont.FromBitmap(texture, map, 10); var batch = new SpriteBatch(device);
        Assert.Throws<OverflowException>(() => font.Measure("AAA"));
        Assert.Throws<OverflowException>(() => font.Measure("", float.MaxValue));
        batch.Begin(); batch.DrawString(font, "AAA", Vector2.Zero, Color.White, 2); batch.End();
        Assert.Equal(1, Assert.Single(backend.Batches).QuadCount);
        Assert.All(backend.Batches[0].Vertices, v => Assert.True(float.IsFinite(v.Position.X) && float.IsFinite(v.Position.Y)));
    }

    [Fact]
    public void CustomText_UsesCameraClipStateAndCapacityFlushes()
    {
        var backend = new RecordingBackend(); var device = new GraphicsDevice(backend, 360, 640); device.Resize(720, 1280);
        using var texture = Texture2D.CreateSolid(device, 32, 16, Color.White);
        var font = SpriteFont.FromBitmap(texture, Glyphs(), 10); var batch = new SpriteBatch(device);
        var clip = new RectangleF(10, 20, 200, 300);
        batch.Begin(BlendState.Additive, SamplerState.PointClamp, clip: clip);
        batch.DrawString(font, "Ai", new(20, 30), Color.Yellow); batch.End();
        var original = backend.Batches[0].Vertices; var state = backend.States[0]; backend.Batches.Clear(); backend.States.Clear();
        var camera = new Camera2D { Position = new(20, 30), Zoom = 2, Rotation = 0.4f };
        batch.Begin(camera, BlendState.Additive, SamplerState.PointClamp, clip: clip);
        for (int i = 0; i < 1100; i++) batch.DrawString(font, "Ai", new(20, 30), Color.Yellow);
        batch.End(); Assert.Equal(2, backend.Batches.Count); Assert.Equal(2200, backend.Batches.Sum(b => b.QuadCount));
        Assert.All(backend.States, s => Assert.Equal(state, s));
        int index = 0;
        foreach (var drawn in backend.Batches)
        foreach (var vertex in drawn.Vertices)
        {
            var old = original[index++ % original.Length];
            Near(camera.WorldToScreen(old.Position, device.ViewSize), vertex.Position);
            Assert.Equal(old.TexCoord, vertex.TexCoord); Assert.Equal(old.Color, vertex.Color);
        }
    }

    [Fact]
    public void MeasuringAndDrawingRepeatedly_DoNotAllocate()
    {
        var device = new GraphicsDevice(new NoOpBackend(), 360, 640);
        using var texture = Texture2D.CreateSolid(device, 32, 16, Color.White);
        var font = SpriteFont.FromBitmap(texture, Glyphs(), 10); var batch = new SpriteBatch(device);
        void Frame()
        {
            font.Measure("A😀 i\r\n?\u0301", 2);
            batch.Begin();
            for (int j = 0; j < 500; j++) batch.DrawString(font, "A😀 i\r\n?\u0301", new(20, 30), Color.White, 2);
            batch.End();
        }
        for (int i = 0; i < 100; i++) Frame(); long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 100; i++) Frame(); Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [Fact]
    public void OfflineGuide_CompilesAndRespondsToScaledTouchInRealRuntime()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "docs", "guides", "fontes-bitmap.md"))) root = root.Parent;
        Assert.NotNull(root);
        var source = File.ReadAllText(Path.Combine(root!.FullName, "docs", "guides", "fontes-bitmap.md")).Split("```csharp\n")[1].Split("```")[0];
        var compiler = new Lunet.Compiler.GameCompiler(new Lunet.Compiler.LoadedAssembliesReferenceProvider(typeof(Game).Assembly));
        var compiled = compiler.Compile("BitmapFontGuide", [new Lunet.Compiler.SourceFile("Game.cs", source)]);
        Assert.True(compiled.Success, string.Join("\n", compiled.Diagnostics)); Assert.DoesNotContain(compiled.Diagnostics, d => d.Severity == Lunet.Compiler.DiagnosticSeverity.Warning);
        using var loaded = Lunet.Runtime.GameLoader.Load(compiled.Assembly!, compiled.Symbols);
        var backend = new RecordingBackend(); var host = new GameHost(loaded.Game, backend); Assert.True(host.Start(720, 1280)); host.Tick(1.0 / 60);
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        object Field(string name) => loaded.Game.GetType().GetField(name, flags)!.GetValue(loaded.Game)!;
        void Touch(float x, float y)
        {
            host.SetSurfaceTouches([new Lunet.Input.TouchPoint(0, Lunet.Input.TouchPhase.Pressed, new(x * 2, y * 2))]); host.Tick(1.0 / 60);
            host.SetSurfaceTouches([]); host.Tick(1.0 / 60);
        }
        Touch(40, 60); Assert.Equal(1235, Field("score"));
        Touch(200, 60); Assert.Equal(2f, Field("scale"));
        var font = (SpriteFont)Field("digits"); Assert.Equal(new Vector2(34, 16), font.Measure("1235"));
        Assert.False(host.IsFaulted); Assert.NotEmpty(backend.Batches); host.Stop();
        Assert.True(((Texture2D)Field("texture")).IsDisposed);
    }
}
