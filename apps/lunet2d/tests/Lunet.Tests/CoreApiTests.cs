using System.Numerics;
using Lunet.Graphics;
using Lunet.Input;
using Lunet.Storage;

namespace Lunet.Tests;

public class GraphicsExtrasTests
{
    private static (GraphicsDevice Device, RecordingBackend Backend) NewDevice()
    {
        var backend = new RecordingBackend();
        return (new GraphicsDevice(backend, 200, 200), backend);
    }

    [Fact]
    public void Font_UsesPointFilterAndDrawsOneQuadPerVisibleGlyph()
    {
        var (device, backend) = NewDevice();
        var font = SpriteFont.CreateDefault(device);
        Assert.Equal(TextureFilter.Point, backend.Pixels.Values.Single().Filter);

        var batch = new SpriteBatch(device);
        batch.Begin();
        batch.DrawString(font, "Ola, Ção!\nx", new Vector2(10, 10), Color.White, 2);
        batch.End();
        Assert.Equal(9, backend.Batches.Single().QuadCount); // espaço e \n não desenham
    }

    [Fact]
    public void Font_MeasuresLinesAndScale()
    {
        var (device, _) = NewDevice();
        var font = SpriteFont.CreateDefault(device);
        Assert.Equal(new Vector2(17, font.LineHeight), font.Measure("abc"));
        Assert.Equal(new Vector2(34, font.LineHeight * 2 * 2), font.Measure("abc\nab", 2)); // 2 linhas, escala 2
        Assert.Equal(new Vector2(0, font.LineHeight), font.Measure(""));
    }

    [Fact]
    public void Font_ReplacesUnknownCharactersWithQuestionMark_AndAccentsDifferFromBase()
    {
        var (device, backend) = NewDevice();
        var font = SpriteFont.CreateDefault(device);
        var batch = new SpriteBatch(device);
        batch.Begin();
        batch.DrawString(font, "?☃", Vector2.Zero, Color.White);
        batch.End();
        var v = backend.Batches[0].Vertices;
        Assert.Equal(v[0].TexCoord, v[4].TexCoord); // ☃ desenhado como ?

        batch.Begin();
        batch.DrawString(font, "aá", Vector2.Zero, Color.White);
        batch.End();
        var w = backend.Batches[1].Vertices;
        Assert.NotEqual(w[0].TexCoord, w[4].TexCoord);
    }

    [Fact]
    public void SpriteSheet_SlicesFramesRowMajor()
    {
        var (device, _) = NewDevice();
        using var texture = Texture2D.CreateSolid(device, 64, 32, Color.White);
        var sheet = new SpriteSheet(texture, 16, 16);
        Assert.Equal(8, sheet.FrameCount);
        Assert.Equal(new RectangleF(16, 0, 16, 16), sheet.Frame(1));
        Assert.Equal(new RectangleF(0, 16, 16, 16), sheet.Frame(4));
        Assert.Throws<ArgumentOutOfRangeException>(() => sheet.Frame(8));
        Assert.Throws<ArgumentException>(() => new SpriteSheet(texture, 128, 16));
    }

    [Fact]
    public void SpriteBatch_DrawsSourceRectangleAtNativeSize()
    {
        var (device, backend) = NewDevice();
        using var texture = Texture2D.CreateSolid(device, 64, 32, Color.White);
        var batch = new SpriteBatch(device);
        batch.Begin();
        batch.Draw(texture, new Vector2(5, 5), new RectangleF(16, 0, 16, 16), Color.White);
        batch.End();
        var v = backend.Batches[0].Vertices;
        Assert.Equal(new Vector2(21, 21), v[2].Position);
        Assert.Equal(new Vector2(0.25f, 0), v[0].TexCoord);
        Assert.Equal(new Vector2(0.5f, 0.5f), v[2].TexCoord);
    }
}

public class MathAndRandomTests
{
    [Fact]
    public void MathEx_BasicFunctions()
    {
        Assert.Equal(5f, MathEx.Remap(0.5f, 0, 1, 0, 10));
        Assert.Equal(0.5f, MathEx.SmoothStep(0.5f));
        Assert.Equal(3f, MathEx.MoveToward(0f, 10f, 3f));
        Assert.Equal(10f, MathEx.MoveToward(9f, 10f, 3f));
        Assert.Equal(new Vector2(3, 0), MathEx.MoveToward(Vector2.Zero, new Vector2(10, 0), 3));
        Assert.Equal(-0.2f, MathEx.AngleDifference(0.1f, MathF.Tau - 0.1f), 0.0001f);
        Assert.Equal(180f, MathEx.ToDegrees(MathF.PI), 0.001f);
    }

    [Fact]
    public void RandomSource_IsDeterministicAndInRange()
    {
        var a = new RandomSource(42);
        var b = new RandomSource(42);
        for (var i = 0; i < 100; i++) Assert.Equal(a.NextUInt64(), b.NextUInt64());
        Assert.NotEqual(new RandomSource(1).NextUInt64(), new RandomSource(2).NextUInt64());

        var r = new RandomSource(7);
        for (var i = 0; i < 1000; i++)
        {
            Assert.InRange(r.NextFloat(), 0f, 0.99999994f);
            Assert.InRange(r.NextInt(3, 8), 3, 7);
            Assert.Equal(1f, r.NextDirection().Length(), 0.001f);
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => r.NextInt(0));
    }

    [Fact]
    public void RandomSource_IsRoughlyUniform()
    {
        var r = new RandomSource(123);
        var buckets = new int[4];
        for (var i = 0; i < 4000; i++) buckets[r.NextInt(4)]++;
        Assert.All(buckets, count => Assert.InRange(count, 850, 1150));
    }

    [Fact]
    public void Circle_Intersections()
    {
        var c = new Circle(new Vector2(0, 0), 5);
        Assert.True(c.Contains(new Vector2(3, 4)));
        Assert.False(c.Contains(new Vector2(4, 4)));
        Assert.True(c.Intersects(new Circle(new Vector2(9, 0), 4)));
        Assert.False(c.Intersects(new Circle(new Vector2(10, 0), 4)));
        Assert.True(c.Intersects(new RectangleF(4, -1, 10, 2)));
        Assert.False(c.Intersects(new RectangleF(6, 6, 2, 2)));
    }
}

public class StorageAndKeyboardTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "lunet-save-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    public sealed class Progress
    {
        public int Score;
        public string Name = "";
    }

    [Fact]
    public void SaveData_RoundTripsJsonAndFallsBackWhenMissingOrCorrupt()
    {
        var store = new DirectorySaveStore(_root);
        var save = new SaveData(store);
        Assert.Equal(7, save.Load("best", 7));

        save.Save("progress", new Progress { Score = 120, Name = "Ana" });
        var loaded = save.Load("progress", new Progress());
        Assert.Equal((120, "Ana"), (loaded.Score, loaded.Name));

        store.WriteText("broken", "{ not json");
        Assert.Equal(-1, save.Load("broken", -1));
        Assert.Empty(Directory.GetFiles(_root, "*.tmp"));

        save.Delete("progress");
        Assert.False(save.Exists("progress"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("../x")]
    [InlineData("a/b")]
    [InlineData(".hidden")]
    public void DirectorySaveStore_RejectsUnsafeKeys(string key) =>
        Assert.Throws<ArgumentException>(() => new DirectorySaveStore(_root).WriteText(key, "x"));

    private sealed class KeyGame : Game
    {
        public int Presses, Downs;
        protected override void Update(GameTime time)
        {
            if (Input.IsKeyPressed(Keys.Space)) Presses++;
            if (Input.IsKeyDown(Keys.Space)) Downs++;
        }
    }

    [Fact]
    public void Keyboard_PressedFiresOnceWhileDownPersists()
    {
        var game = new KeyGame();
        var host = new GameHost(game, new RecordingBackend());
        host.Start(100, 100);
        host.Input.SetKey(Keys.Space, true);
        host.Tick(0.05); // 3 passos
        host.Tick(0.05);
        Assert.Equal(1, game.Presses);
        Assert.Equal(6, game.Downs);
        host.Input.SetKey(Keys.Space, false);
        host.Input.SetKey(Keys.Space, true);
        host.Tick(0.02);
        Assert.Equal(2, game.Presses);
    }

    [Fact]
    public void Accelerometer_IsExposedToTheGame()
    {
        var input = new InputState();
        input.SetAccelerometer(new Vector3(0, 0, 9.8f));
        Assert.Equal(9.8f, input.Accelerometer.Z);
    }

    private sealed class SavingGame : Game
    {
        protected override void LoadContent() => Save.Save("hi", 99);
    }

    [Fact]
    public void Host_ProvidesSaveStoreToGame()
    {
        var store = new MemorySaveStore();
        new GameHost(new SavingGame(), new RecordingBackend(), null, null, store).Start(10, 10);
        Assert.Equal("99", store.ReadText("hi"));
    }
}
