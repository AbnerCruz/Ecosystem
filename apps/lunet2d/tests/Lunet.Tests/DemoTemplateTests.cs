using System.Collections;
using System.Numerics;
using Lunet.Compiler;
using Lunet.Content;
using Lunet.Core;
using Lunet.Input;
using Lunet.Runtime;
using Lunet.Storage;

namespace Lunet.Tests;

public class DemoTemplateTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "lunet-demo-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private sealed class CountingAudio : Lunet.Audio.IAudioBackend
    {
        public int Plays;
        public int LoadSound(byte[] data, string name) => data.Length > 44 ? 1 : 0;
        public void UnloadSound(int soundId) { }
        public int Play(int soundId, float volume, float pan, float pitch, bool loop) { Plays++; return 1; }
        public void SetStream(int streamId, float volume, float pan, float pitch) { }
        public void Stop(int streamId) { }
        public void Dispose() { }
    }

    [Fact]
    public void WavGenerator_ProducesValidPcmHeader()
    {
        var wav = WavGenerator.Beep(440, 0.1);
        Assert.Equal("RIFF", System.Text.Encoding.ASCII.GetString(wav, 0, 4));
        Assert.Equal("WAVE", System.Text.Encoding.ASCII.GetString(wav, 8, 4));
        Assert.Equal(WavGenerator.SampleRate, BitConverter.ToInt32(wav, 24));
        var dataLength = BitConverter.ToInt32(wav, 40);
        Assert.Equal(wav.Length - 44, dataLength);
        Assert.Contains(wav.Skip(44), b => b != 0);
    }

    [Fact]
    public void CoinCatcher_CompilesWithoutWarningsAndPlaysEndToEnd()
    {
        var project = new ProjectStore(_root).Create("Coletor", ProjectTemplate.CoinCatcher);
        Assert.True(File.Exists(Path.Combine(_root, "Coletor", "Content", "Audio", "beep.wav")));

        var sources = project.LoadSources().Select(s => new SourceFile(s.Path, s.Text)).ToList();
        var result = new GameCompiler(new LoadedAssembliesReferenceProvider(typeof(Game).Assembly)).Compile("demo_coins", sources);
        Assert.True(result.Success, string.Join("\n", result.Diagnostics));
        Assert.DoesNotContain(result.Diagnostics, d => d.Severity == DiagnosticSeverity.Warning);

        using var loaded = GameLoader.Load(result.Assembly!, result.Symbols);
        var backend = new RecordingBackend();
        var audio = new CountingAudio();
        var save = new MemorySaveStore();
        var host = new GameHost(loaded.Game, backend, new DirectoryContentSource(Path.Combine(project.Directory, "Content")), audio, save);
        Assert.True(host.Start(360, 640), host.Fault?.ToString());

        var coinsField = loaded.Game.GetType().GetField("coins", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var scoreField = loaded.Game.GetType().GetField("score", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var livesField = loaded.Game.GetType().GetField("lives", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var now = 0.0;
        void Frame() { now += 1.0 / 60; host.Tick(1.0 / 60); }
        Vector2 FirstCoin()
        {
            var coin = ((IEnumerable)coinsField.GetValue(loaded.Game)!).Cast<object>().First();
            return (Vector2)coin.GetType().GetField("Position")!.GetValue(coin)!;
        }

        // Espera surgir uma moeda e toca nela (pressionar e soltar em 3 quadros = Tap).
        for (var i = 0; i < 120 && ((IList)coinsField.GetValue(loaded.Game)!).Count == 0; i++) Frame();
        var target = FirstCoin();
        host.SetSurfaceTouches([new TouchPoint(1, TouchPhase.Moved, target)]);
        Frame();
        host.SetSurfaceTouches([new TouchPoint(1, TouchPhase.Released, target)]);
        Frame(); Frame();
        Assert.Equal(1, scoreField.GetValue(loaded.Game));
        Assert.Equal(1, audio.Plays);

        // Sem tocar, perde as vidas, salva o recorde e mostra a tela de fim.
        for (var i = 0; i < 60 * 40 && (int)livesField.GetValue(loaded.Game)! > 0; i++) Frame();
        Assert.Equal(0, livesField.GetValue(loaded.Game));
        Assert.Contains("\"Best\": 1", save.ReadText("progress"));
        Assert.False(host.IsFaulted);
        Assert.NotEmpty(backend.Batches);
    }

    [Fact]
    public void Blank_StillDefaultTemplate()
    {
        var project = new ProjectStore(_root).Create("Vazio");
        Assert.DoesNotContain("SpriteFont", project.ReadText("Game.cs"));
        Assert.False(Directory.Exists(Path.Combine(_root, "Vazio", "Content", "Audio")));
    }
}
