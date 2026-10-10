using System.Numerics;
using Lunet.Graphics;

namespace Lunet.Tests;

[Collection("Frame allocation")]
public sealed class TrueTypeFontTests
{
    private static byte[]? ReadSystemFont()
    {
        string[] candidates =
        [
            "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
            "/usr/share/fonts/truetype/liberation2/LiberationSans-Regular.ttf",
            "/usr/share/fonts/truetype/freefont/FreeSans.ttf",
            "C:/Windows/Fonts/arial.ttf",
            "/System/Library/Fonts/Supplemental/Arial.ttf"
        ];
        foreach (string path in candidates)
            if (File.Exists(path)) return File.ReadAllBytes(path);
        return null;
    }

    [Fact]
    public void InvalidOptionsAreRejectedBeforeRastersOrTextures()
    {
        var device = new GraphicsDevice(new NoOpBackend(), 360, 640);
        byte[] invalid = [1, 2, 3];
        Assert.Throws<ArgumentNullException>(() => TrueTypeFont.Bake(null!, invalid, 22));
        Assert.Throws<ArgumentNullException>(() => TrueTypeFont.Bake(device, null!, 22));
        Assert.Throws<ArgumentOutOfRangeException>(() => TrueTypeFont.Bake(device, invalid, 22));
        var bytes = new byte[12];
        foreach (float size in new[] { -1, 0, 3, 193, float.NaN, float.PositiveInfinity })
            Assert.Throws<ArgumentOutOfRangeException>(() => TrueTypeFont.Bake(device, bytes, size));
        foreach (int side in new[] { 1, 127, 2049, 99999 })
            Assert.Throws<ArgumentOutOfRangeException>(() => TrueTypeFont.Bake(device, bytes, 22, atlasSize: side));
        Assert.Throws<ArgumentOutOfRangeException>(() => TrueTypeFont.Bake(device, new byte[8_388_609], 22));
        var tooMany = new string(Enumerable.Range(0, 1100).Select(i => (char)(0x100 + i)).ToArray());
        Assert.Throws<ArgumentOutOfRangeException>(() => TrueTypeFont.Bake(device, bytes, 24, tooMany));
    }

    [Fact]
    public void StandardTtfProducesAntialiasedAtlasAccentsFallbackAndMetrics()
    {
        var bytes = ReadSystemFont();
        if (bytes is null) return; // Fontes do SO não são requisito para compilação/Android.
        var backend = new RecordingBackend();
        var device = new GraphicsDevice(backend, 360, 640);
        using var baked = TrueTypeFont.Bake(device, bytes, 26, "Olá!çÁ? Mwi €", 512);
        Assert.Equal(512, baked.Atlas.Width);
        Assert.Equal(512, baked.Atlas.Height);
        Assert.False(baked.IsDisposed);
        Assert.InRange(baked.Font.LineHeight, 18, 50);
        Assert.True(baked.Font.Measure("WW").X > baked.Font.Measure("ii").X);
        Assert.True(baked.Font.Measure("Olá").X > 0);
        Assert.Equal(baked.Font.Measure("☃"), baked.Font.Measure("?")); // ausente -> fallback único
        Assert.Equal(baked.Font.Measure("a\n?").Y, baked.Font.LineHeight * 2);
        var batch = new SpriteBatch(device);
        batch.Begin();
        batch.DrawString(baked.Font, "Olá! ç", new Vector2(20, 30), Color.White);
        batch.End();
        Assert.True(backend.Batches.Sum(x => x.QuadCount) >= 4);
        Assert.True(backend.Batches.Sum(x => x.QuadCount) <= 6); // espaços invisíveis
        baked.Dispose();
        Assert.True(baked.IsDisposed);
        Assert.Throws<ObjectDisposedException>(() =>
        {
            batch.Begin();
            try { batch.DrawString(baked.Font, "Olá", Vector2.Zero, Color.White); }
            finally { batch.End(); }
        });
    }

    [Fact]
    public void AtlasOverflowIsReportedAndUnicodeSetIsExplicit()
    {
        var bytes = ReadSystemFont();
        if (bytes is null) return;
        var device = new GraphicsDevice(new NoOpBackend(), 360, 640);
        Assert.Throws<InvalidOperationException>(() =>
            TrueTypeFont.Bake(device, bytes, 100,
                TrueTypeFont.DefaultCharacters + "あいうえお漢字", atlasSize: 128));
        using var baked = TrueTypeFont.Bake(device, bytes, 22, "A?á", atlasSize: 256);
        Assert.Equal(baked.Font.Measure("ü"), baked.Font.Measure("?"));
        Assert.Equal(baked.Font.Measure("😀"), baked.Font.Measure("?"));
        Assert.True(baked.Font.Measure("Á\r\nA").Y > baked.Font.Measure("A").Y);
    }

    private sealed class FontSource(byte[] data) : Lunet.Content.IContentSource
    {
        public int OpenCount { get; private set; }
        public bool Exists(string path) => path == "Fonts/ui.ttf";
        public Stream Open(string path)
        {
            if (!Exists(path)) throw new FileNotFoundException(path);
            OpenCount++;
            return new MemoryStream(data, writable: false);
        }
    }

    [Fact]
    public void ContentManagerCachesTtfByOptionsAndDisposesAllAtlases()
    {
        var bytes = ReadSystemFont();
        if (bytes is null) return;
        var source = new FontSource(bytes);
        var device = new GraphicsDevice(new NoOpBackend(), 360, 640);
        var content = new Lunet.Content.ContentManager(source, device);
        var first = content.LoadTrueTypeFont("Fonts/ui.ttf", 22, "Olá ?");
        var same = content.LoadTrueTypeFont("Fonts/ui.ttf", 22, "Olá ?");
        Assert.Same(first, same);
        Assert.Equal(1, source.OpenCount);
        var second = content.LoadTrueTypeFont("Fonts/ui.ttf", 32, "Olá ?");
        Assert.NotSame(first, second);
        Assert.Equal(2, source.OpenCount);
        content.Dispose();
        Assert.True(first.IsDisposed);
        Assert.True(second.IsDisposed);
    }

    [Fact]
    public void OfflineGameGuideCompilesAgainstActualFramework()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "docs", "guides", "fontes-truetype.md")))
            root = root.Parent;
        Assert.NotNull(root);
        var guide = File.ReadAllText(Path.Combine(root!.FullName, "docs", "guides", "fontes-truetype.md"));
        var code = guide.Split("```csharp\n")[1].Split("```")[0];
        var compiler = new Lunet.Compiler.GameCompiler(
            new Lunet.Compiler.LoadedAssembliesReferenceProvider(typeof(Game).Assembly));
        var result = compiler.Compile("TrueTypeGuide", [new Lunet.Compiler.SourceFile("Game.cs", code)]);
        Assert.True(result.Success, string.Join("\n", result.Diagnostics));
        Assert.DoesNotContain(result.Diagnostics,
            d => d.Severity == Lunet.Compiler.DiagnosticSeverity.Warning);
    }

    [Fact]
    public void FontMeasureAndDrawAreAllocationFreeAfterPrebaking()
    {
        var bytes = ReadSystemFont();
        if (bytes is null) return;
        var device = new GraphicsDevice(new NoOpBackend(), 360, 640);
        using var baked = TrueTypeFont.Bake(device, bytes, 24, "Olá AV?ç", 256);
        var batch = new SpriteBatch(device);
        void Frame()
        {
            _ = baked.Font.Measure("Olá AV?ç", 1);
            batch.Begin();
            batch.DrawString(baked.Font, "Olá AV?ç", Vector2.Zero, Color.White);
            batch.End();
        }
        for (int i = 0; i < 100; i++) Frame();
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 100; i++) Frame();
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }
}
