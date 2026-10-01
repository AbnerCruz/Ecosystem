using System.IO.Compression;
using Lunet.Compiler;
using Lunet.Content;
using Lunet.Graphics;
using Lunet.Runtime;

namespace Lunet.Tests;

public class ContentTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "lunet-content-" + Guid.NewGuid().ToString("N"));

    public ContentTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    /// <summary>Codifica um PNG (CRC zerado: o decodificador não o verifica).</summary>
    internal static byte[] EncodePng(int width, int height, int colorType, byte[] scanlines, byte[]? palette = null, byte[]? trns = null)
    {
        using var output = new MemoryStream();
        output.Write([137, 80, 78, 71, 13, 10, 26, 10]);
        void Chunk(string type, byte[] body)
        {
            var len = new byte[4];
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(len, (uint)body.Length);
            output.Write(len);
            output.Write(System.Text.Encoding.ASCII.GetBytes(type));
            output.Write(body);
            output.Write(new byte[4]);
        }
        var ihdr = new byte[13];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(ihdr, (uint)width);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(ihdr.AsSpan(4), (uint)height);
        ihdr[8] = 8; ihdr[9] = (byte)colorType;
        Chunk("IHDR", ihdr);
        if (palette is not null) Chunk("PLTE", palette);
        if (trns is not null) Chunk("tRNS", trns);
        using var compressed = new MemoryStream();
        using (var z = new ZLibStream(compressed, CompressionLevel.Fastest, leaveOpen: true)) z.Write(scanlines);
        Chunk("IDAT", compressed.ToArray());
        Chunk("IEND", []);
        return output.ToArray();
    }

    [Fact]
    public void Png_DecodesRgbaWithNoFilter()
    {
        // 2x1: vermelho opaco, azul meio transparente
        var png = EncodePng(2, 1, 6, [0, 255, 0, 0, 255, 0, 0, 255, 128]);
        var image = PngDecoder.Decode(png);
        Assert.Equal((2, 1), (image.Width, image.Height));
        Assert.Equal(new byte[] { 255, 0, 0, 255, 0, 0, 255, 128 }, image.Rgba);
    }

    [Fact]
    public void Png_AppliesSubUpAndPaethFilters()
    {
        // 3x2 em cinza. Linha 0 com Sub: valores 10, +5, +5 => 10,15,20. Linha 1 com Up: +1,+1,+1 => 11,16,21.
        var png = EncodePng(3, 2, 0, [1, 10, 5, 5, 2, 1, 1, 1]);
        var image = PngDecoder.Decode(png);
        Assert.Equal(new byte[] { 10, 15, 20 }, new[] { image.Rgba[0], image.Rgba[4], image.Rgba[8] });
        Assert.Equal(new byte[] { 11, 16, 21 }, new[] { image.Rgba[12], image.Rgba[16], image.Rgba[20] });

        // Paeth com linha anterior: (a=0,b=10,c=0) => predição b=10; 5+10=15
        var paeth = PngDecoder.Decode(EncodePng(1, 2, 0, [0, 10, 4, 5]));
        Assert.Equal(15, paeth.Rgba[4]);
    }

    [Fact]
    public void Png_DecodesPaletteWithTransparency()
    {
        var png = EncodePng(2, 1, 3, [0, 0, 1], palette: [10, 20, 30, 40, 50, 60], trns: [0]);
        var image = PngDecoder.Decode(png);
        Assert.Equal(new byte[] { 10, 20, 30, 0, 40, 50, 60, 255 }, image.Rgba);
    }

    [Fact]
    public void Png_RejectsGarbageTruncatedAndInterlaced()
    {
        Assert.Throws<InvalidDataException>(() => PngDecoder.Decode([1, 2, 3]));
        var png = EncodePng(2, 2, 6, new byte[2 * (1 + 8)]);
        Assert.Throws<InvalidDataException>(() => PngDecoder.Decode(png.AsSpan(0, png.Length - 30)));
        png[8 + 8 + 12] = 1; // flag de entrelaçamento no IHDR
        Assert.Throws<NotSupportedException>(() => PngDecoder.Decode(png));
    }

    [Fact]
    public void DirectorySource_BlocksTraversal()
    {
        File.WriteAllText(Path.Combine(_root, "a.txt"), "x");
        var source = new DirectoryContentSource(Path.Combine(_root));
        Assert.True(source.Exists("a.txt"));
        Assert.False(source.Exists("../a.txt"));
        Assert.False(source.Exists("/etc/passwd"));
        Assert.Throws<FileNotFoundException>(() => source.Open("../x"));
    }

    [Fact]
    public void ContentManager_CachesTexturesAndDisposesThem()
    {
        File.WriteAllBytes(Path.Combine(_root, "t.png"), EncodePng(1, 1, 6, [0, 1, 2, 3, 4]));
        var backend = new RecordingBackend();
        var device = new GraphicsDevice(backend, 10, 10);
        var content = new ContentManager(new DirectoryContentSource(_root), device);
        var a = content.LoadTexture("t.png");
        Assert.Same(a, content.LoadTexture("t.png"));
        Assert.Single(backend.LiveTextures);
        content.Dispose();
        Assert.Empty(backend.LiveTextures);
        Assert.Throws<FileNotFoundException>(() => new ContentManager(new DirectoryContentSource(_root), device).LoadTexture("nope.png"));
    }

    [Fact]
    public void Game_LoadsTextureFromProjectContentThroughHost()
    {
        Directory.CreateDirectory(Path.Combine(_root, "Textures"));
        File.WriteAllBytes(Path.Combine(_root, "Textures", "hero.png"), EncodePng(4, 4, 6, Enumerable.Range(0, 4 * 17).Select(i => (byte)(i % 17 == 0 ? 0 : 200)).ToArray()));
        var result = new GameCompiler(new LoadedAssembliesReferenceProvider(typeof(Game).Assembly)).Compile("content_game", [new SourceFile("G.cs", """
            using System.Numerics;
            using Lunet; using Lunet.Graphics;
            public sealed class G : Game
            {
                SpriteBatch batch = null!; Texture2D hero = null!;
                protected override void LoadContent() { batch = new SpriteBatch(GraphicsDevice); hero = Content.LoadTexture("Textures/hero.png"); }
                protected override void Draw(GameTime t) { batch.Begin(); batch.Draw(hero, Vector2.Zero, Color.White); batch.End(); }
            }
            """)]);
        Assert.True(result.Success, string.Join("\n", result.Diagnostics));
        using var loaded = GameLoader.Load(result.Assembly!);
        var backend = new RecordingBackend();
        var host = new GameHost(loaded.Game, backend, new DirectoryContentSource(_root));
        Assert.True(host.Start(100, 100), host.Fault?.ToString());
        host.Tick(0.016);
        Assert.Single(backend.Batches);
        host.Stop();
        Assert.Empty(backend.LiveTextures);
    }

    [Fact]
    public void MissingContentFile_FaultsWithReadableMessage()
    {
        var result = new GameCompiler(new LoadedAssembliesReferenceProvider(typeof(Game).Assembly)).Compile("content_missing", [new SourceFile("G.cs",
            "using Lunet; public sealed class G : Game { protected override void LoadContent() { Content.LoadTexture(\"x.png\"); } }")]);
        using var loaded = GameLoader.Load(result.Assembly!);
        var host = new GameHost(loaded.Game, new RecordingBackend());
        Assert.False(host.Start(10, 10));
        Assert.Contains("x.png", host.Fault!.Message);
    }
}

public class AudioTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "lunet-audio-" + Guid.NewGuid().ToString("N"));

    public AudioTests()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllBytes(Path.Combine(_root, "jump.wav"), [1, 2, 3]);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private sealed class FakeAudio : Lunet.Audio.IAudioBackend
    {
        public List<string> Calls { get; } = [];
        public int LoadSound(byte[] data, string name) { Calls.Add($"load:{name}:{data.Length}"); return 7; }
        public void UnloadSound(int soundId) => Calls.Add($"unload:{soundId}");
        public int Play(int soundId, float volume, float pan, float pitch, bool loop) { Calls.Add($"play:{soundId}:{volume}:{pan}:{pitch}:{loop}"); return 42; }
        public void SetStream(int streamId, float volume, float pan, float pitch) => Calls.Add($"set:{streamId}:{volume}:{pan}:{pitch}");
        public void Stop(int streamId) => Calls.Add($"stop:{streamId}");
        public void PauseSounds() => Calls.Add("pauseSounds");
        public void ResumeSounds() => Calls.Add("resumeSounds");
        public int LoadMusic(byte[] data, string name) { Calls.Add($"loadMusic:{name}"); return 9; }
        public void UnloadMusic(int musicId) => Calls.Add($"unloadMusic:{musicId}");
        public void PlayMusic(int musicId, float volume, bool loop) => Calls.Add($"playMusic:{musicId}:{volume}:{loop}");
        public void SetMusicVolume(float volume) => Calls.Add($"musicVolume:{volume:0.###}");
        public void PauseMusic() => Calls.Add("pauseMusic");
        public void ResumeMusic() => Calls.Add("resumeMusic");
        public void StopMusic() => Calls.Add("stopMusic");
        public void Dispose() { }
    }

    [Fact]
    public void Sound_ClampsParametersAndControlsInstance()
    {
        var audio = new FakeAudio();
        var content = new Lunet.Content.ContentManager(new Lunet.Content.DirectoryContentSource(_root),
            new Lunet.Graphics.GraphicsDevice(new RecordingBackend(), 10, 10), audio);
        var sound = content.LoadSound("jump.wav");
        Assert.Same(sound, content.LoadSound("jump.wav"));
        var instance = sound.Play(5f, -3f, 9f, true);
        instance.Volume = 0.5f;
        content.Audio.Update(0.016f); // o volume é aplicado pelo mixer a cada passo
        instance.Stop();
        Assert.Equal(["load:jump.wav:3", "play:7:1:-1:2:True", "set:42:0.5:-1:2", "stop:42"], audio.Calls);
        content.Dispose();
        Assert.Equal("unload:7", audio.Calls[^1]);
        Assert.Throws<ObjectDisposedException>(() => sound.Play());
    }

    [Fact]
    public void NullAudio_DoesNotBreakGames()
    {
        var content = new Lunet.Content.ContentManager(new Lunet.Content.DirectoryContentSource(_root),
            new Lunet.Graphics.GraphicsDevice(new RecordingBackend(), 10, 10));
        var instance = content.LoadSound("jump.wav").Play();
        Assert.False(instance.Started);
        instance.Volume = 0.2f;
        instance.Stop();
    }
}
