using System.Numerics;
using Lunet.Content;
using Lunet.Graphics;
using Lunet.Input;

namespace Lunet.Tests;

public class ContentDataTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "lunet-data-" + Guid.NewGuid().ToString("N"));

    public ContentDataTests()
    {
        Directory.CreateDirectory(Path.Combine(_root, "Data"));
        Directory.CreateDirectory(Path.Combine(_root, "Textures"));
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private (ContentManager Content, RecordingBackend Backend) New()
    {
        var backend = new RecordingBackend();
        return (new ContentManager(new DirectoryContentSource(_root), new GraphicsDevice(backend, 100, 100)), backend);
    }

    private void Write(string path, string text) => File.WriteAllText(Path.Combine(_root, path), text);

    private void WriteAtlasFiles()
    {
        File.WriteAllBytes(Path.Combine(_root, "Textures", "atlas.png"), ContentTests.EncodePng(4, 4, 6, Enumerable.Range(0, 4 * 17).Select(i => (byte)(i % 17 == 0 ? 0 : 255)).ToArray()));
        Write("Data/atlas.json", """
            { "texture": "Textures/atlas.png",
              "regions": {
                "hero":  { "x": 0, "y": 0, "width": 2, "height": 4, "pivotY": 1 },
                "coin":  { "x": 2, "y": 0, "width": 2, "height": 2 }
              } }
            """);
    }

    public sealed class Level
    {
        public string Name = "";
        public int Coins { get; set; }
        public List<Vector2Data> Spawns { get; set; } = [];
    }

    public sealed class Vector2Data
    {
        public float X { get; set; }
        public float Y { get; set; }
    }

    [Fact]
    public void LoadJson_ReadsFieldsPropertiesCommentsAndTrailingCommas()
    {
        Write("Data/level1.json", """
            {
              // fase 1
              "name": "Campo", "coins": 12,
              "spawns": [ { "x": 1, "y": 2 }, { "x": 3, "y": 4 }, ],
            }
            """);
        var (content, _) = New();
        var level = content.LoadJson<Level>("Data/level1.json");
        Assert.Equal(("Campo", 12), (level.Name, level.Coins));
        Assert.Equal([1f, 3f], level.Spawns.Select(s => s.X));
    }

    [Fact]
    public void LoadJson_ReportsFileNameOnBadContent()
    {
        Write("Data/bad.json", "{ nope");
        Write("Data/null.json", "null");
        var (content, _) = New();
        Assert.Contains("bad.json", Assert.Throws<InvalidDataException>(() => content.LoadJson<Level>("Data/bad.json")).Message);
        Assert.Contains("null.json", Assert.Throws<InvalidDataException>(() => content.LoadJson<Level>("Data/null.json")).Message);
        Assert.Throws<FileNotFoundException>(() => content.LoadJson<Level>("Data/none.json"));
    }

    [Fact]
    public void Atlas_LoadsRegionsWithPivotAndCaches()
    {
        WriteAtlasFiles();
        var (content, backend) = New();
        var atlas = content.LoadAtlas("Data/atlas.json");
        Assert.Same(atlas, content.LoadAtlas("Data/atlas.json"));
        Assert.Equal(2, atlas.Count);
        Assert.Equal(new RectangleF(0, 0, 2, 4), atlas["hero"].Bounds);
        Assert.Equal(1f, atlas["hero"].PivotY);
        Assert.Equal(0.5f, atlas["coin"].PivotX);
        Assert.Equal(TextureFilter.Point, backend.Pixels[atlas.Texture.Handle].Filter);
        Assert.Throws<KeyNotFoundException>(() => atlas["nada"]);
        Assert.False(atlas.TryGetRegion("nada", out _));
    }

    [Fact]
    public void Atlas_DrawsRegionAroundItsPivotWithScale()
    {
        WriteAtlasFiles();
        var (content, backend) = New();
        var atlas = content.LoadAtlas("Data/atlas.json");
        var batch = new SpriteBatch(new GraphicsDevice(backend, 100, 100));
        batch.Begin();
        batch.Draw(atlas, "hero", new Vector2(50, 60), Color.White, scale: 2f); // pivô embaixo-centro
        batch.End();
        var v = backend.Batches[^1].Vertices;
        Assert.Equal(new Vector2(48, 52), v[0].Position); // x: 50 - 1*2 ; y: 60 - 4*2
        Assert.Equal(new Vector2(52, 60), v[2].Position);
        Assert.Equal(new Vector2(0, 0), v[0].TexCoord);
        Assert.Equal(new Vector2(0.5f, 1f), v[2].TexCoord);
    }

    [Fact]
    public void Atlas_RejectsBadFiles()
    {
        Assert.Throws<InvalidDataException>(() => TextureAtlas.Parse("{ }"));
        Assert.Throws<InvalidDataException>(() => TextureAtlas.Parse("nope"));
        Assert.Throws<InvalidDataException>(() => TextureAtlas.Parse("""{ "texture": "a.png", "regions": { "x": { "width": 0, "height": 1 } } }"""));
    }

    [Fact]
    public void UnloadTexture_LetsTheNextLoadReadTheFileAgain()
    {
        WriteAtlasFiles();
        var (content, backend) = New();
        var first = content.LoadTexture("Textures/atlas.png");
        content.UnloadTexture("Textures/atlas.png");
        Assert.True(first.IsDisposed);
        var second = content.LoadTexture("Textures/atlas.png");
        Assert.NotSame(first, second);
        Assert.Single(backend.LiveTextures);
        content.UnloadTexture("Textures/nunca-carregada.png"); // não lança
    }

    [Fact]
    public void Localization_UsesLanguageThenFallbackThenKey_AndFormats()
    {
        Write("Data/strings.en.json", """{ "play": "Play", "score": "Score: {0}", "only_en": "Only English" }""");
        Write("Data/strings.pt.json", """{ "play": "Jogar", "score": "Pontos: {0}" }""");
        var (content, _) = New();
        var l = content.Localization;

        Assert.True(l.SetLanguage("pt"));
        Assert.Equal("Jogar", l.Get("play"));
        Assert.Equal("Pontos: 7", l.Get("score", 7));
        Assert.Equal("Only English", l.Get("only_en")); // reserva
        Assert.Equal("missing_key", l.Get("missing_key"));
        Assert.True(l.Contains("only_en"));
        Assert.False(l.Contains("missing_key"));

        Assert.False(l.SetLanguage("es")); // sem arquivo: usa a reserva
        Assert.Equal("Play", l.Get("play"));
        Assert.Equal("Score: {0}", l.Get("score"));
    }

    [Fact]
    public void Localization_BadFormatReturnsRawTextAndBrokenFileNamesTheFile()
    {
        Write("Data/strings.en.json", """{ "bad": "Valor {1}" }""");
        Write("Data/strings.xx.json", "{ quebrado");
        var (content, _) = New();
        content.Localization.SetLanguage("en");
        Assert.Equal("Valor {1}", content.Localization.Get("bad", "só um argumento"));
        Assert.Contains("strings.xx.json", Assert.Throws<InvalidDataException>(() => content.Localization.SetLanguage("xx")).Message);
    }

    [Fact]
    public void Localization_UseDeviceLanguageFallsBackWhenUnavailable()
    {
        Write("Data/strings.en.json", """{ "hi": "Hello" }""");
        var (content, _) = New();
        var old = System.Globalization.CultureInfo.CurrentUICulture;
        try
        {
            System.Globalization.CultureInfo.CurrentUICulture = new System.Globalization.CultureInfo("ja-JP");
            Assert.Equal("en", content.Localization.UseDeviceLanguage("en"));
            Assert.Equal("Hello", content.Localization.Get("hi"));
        }
        finally { System.Globalization.CultureInfo.CurrentUICulture = old; }
    }
}

public class TouchCollectionAndPointerTests
{
    [Fact]
    public void TouchCollection_ExposesCountIndexAndLookupWithoutAllocatingArrays()
    {
        var input = new InputState();
        input.SetTouches([new TouchPoint(4, TouchPhase.Moved, new Vector2(1, 2)), new TouchPoint(9, TouchPhase.Released, new Vector2(3, 4))]);
        var touches = input.TouchCollection;
        Assert.Equal(2, touches.Count);
        Assert.Equal(1, touches.DownCount);
        Assert.Equal(new Vector2(3, 4), touches[1].Position);
        Assert.True(touches.TryGetById(9, out var released));
        Assert.Equal(TouchPhase.Released, released.Phase);
        Assert.False(touches.TryGetById(1, out _));
        var ids = 0;
        foreach (var t in touches) ids += t.Id;
        Assert.Equal(13, ids);
    }

    [Fact]
    public void Pointer_ReportsPressedAndReleasedEdgesOnce()
    {
        var input = new InputState();
        Assert.False(input.Pointer.IsDown);
        input.SetTouches([new TouchPoint(1, TouchPhase.Pressed, new Vector2(10, 20))]);
        Assert.True(input.Pointer.IsDown);
        Assert.True(input.Pointer.WasPressed);
        Assert.Equal(new Vector2(10, 20), input.Pointer.Position);

        input.ClearGestures();
        input.SetTouches([new TouchPoint(1, TouchPhase.Moved, new Vector2(15, 20))]);
        Assert.False(input.Pointer.WasPressed);
        Assert.Equal(new Vector2(15, 20), input.Pointer.Position);

        input.SetTouches([new TouchPoint(1, TouchPhase.Released, new Vector2(15, 20))]);
        Assert.False(input.Pointer.IsDown);
        Assert.True(input.Pointer.WasReleased);
        Assert.Equal(new Vector2(15, 20), input.Pointer.Position); // guarda a última posição
        input.ClearGestures();
        Assert.False(input.Pointer.WasReleased);
    }
}
