using System.Collections;
using System.Numerics;
using Lunet.Compiler;
using Lunet.Content;
using Lunet.Core;
using Lunet.Graphics;
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
        public void PauseSounds() { }
        public void ResumeSounds() { }
        public int LoadMusic(byte[] data, string name) => 1;
        public void UnloadMusic(int musicId) { }
        public void PlayMusic(int musicId, float volume, bool loop) { }
        public void SetMusicVolume(float volume) { }
        public void PauseMusic() { }
        public void ResumeMusic() { }
        public void StopMusic() { }
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

public class LabTemplateTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "lunet-lab-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private sealed class Audio : Lunet.Audio.IAudioBackend
    {
        public List<string> Calls { get; } = [];
        public int LoadSound(byte[] data, string name) => 1;
        public void UnloadSound(int soundId) { }
        public int Play(int soundId, float volume, float pan, float pitch, bool loop) { Calls.Add("play"); return 1; }
        public void SetStream(int streamId, float volume, float pan, float pitch) { }
        public void Stop(int streamId) { }
        public void PauseSounds() { }
        public void ResumeSounds() { }
        public int LoadMusic(byte[] data, string name) { Calls.Add("loadMusic:" + data.Length); return 1; }
        public void UnloadMusic(int musicId) { }
        public void PlayMusic(int musicId, float volume, bool loop) => Calls.Add("playMusic");
        public void SetMusicVolume(float volume) => Calls.Add($"musicVolume:{volume:0.0}");
        public void PauseMusic() { }
        public void ResumeMusic() { }
        public void StopMusic() => Calls.Add("stopMusic");
        public void Dispose() { }
    }

    private sealed class Haptics : Lunet.Input.IHaptics
    {
        public List<int> Calls { get; } = [];
        public void Vibrate(int milliseconds, float intensity = 1f) => Calls.Add(milliseconds);
        public void Cancel() { }
    }

    [Fact]
    public void Lab_CompilesWithoutWarningsAndExercisesAudioSensorsGamepadAndControls()
    {
        var project = new ProjectStore(_root).Create("Lab", ProjectTemplate.Lab);
        Assert.True(File.Exists(Path.Combine(_root, "Lab", "Content", "Audio", "loop.wav")));
        Assert.True(new FileInfo(Path.Combine(_root, "Lab", "Content", "Audio", "loop.wav")).Length > 100_000);

        var result = new GameCompiler(new LoadedAssembliesReferenceProvider(typeof(Game).Assembly))
            .Compile("lab_game", project.LoadSources().Select(s => new SourceFile(s.Path, s.Text)).ToList());
        Assert.True(result.Success, string.Join("\n", result.Diagnostics));
        Assert.DoesNotContain(result.Diagnostics, d => d.Severity == DiagnosticSeverity.Warning);

        using var loaded = GameLoader.Load(result.Assembly!, result.Symbols);
        var audio = new Audio();
        var haptics = new Haptics();
        var backend = new RecordingBackend();
        var host = new GameHost(loaded.Game, backend, new DirectoryContentSource(Path.Combine(project.Directory, "Content")), audio, null, haptics);
        Assert.True(host.Start(360, 640), host.Fault?.ToString());

        void Frame(int n = 1) { for (var i = 0; i < n; i++) host.Tick(1.0 / 60); }
        void Tap(float x, float y)
        {
            host.SetSurfaceTouches([new TouchPoint(7, TouchPhase.Pressed, new Vector2(x, y))]);
            Frame();
            host.SetSurfaceTouches([new TouchPoint(7, TouchPhase.Released, new Vector2(x, y))]);
            Frame(2);
            host.SetSurfaceTouches([]);
            Frame(2);
        }

        Tap(100, 59); // música liga
        Assert.Contains("playMusic", audio.Calls);
        Tap(100, 95); // beep + vibrar
        Assert.Contains(60, haptics.Calls);
        Tap(100, 131); // volume música +
        Tap(300, 131); // volume música -
        Tap(100, 59); // música desliga com fade
        Frame(120);
        Assert.Contains("stopMusic", audio.Calls);

        // Acelerômetro e controle
        host.Input.SetAccelerometer(new Vector3(-2, 0, 9.8f));
        host.Input.SetGamepad(new GamepadState(true, GamepadButtons.A, new Vector2(1, 0), Vector2.Zero, 0, 0));
        Frame(3);
        Assert.Contains(30, haptics.Calls);

        // Pinça com dois dedos
        host.SetSurfaceTouches([new TouchPoint(1, TouchPhase.Moved, new Vector2(100, 400)), new TouchPoint(2, TouchPhase.Moved, new Vector2(160, 400))]);
        Frame();
        host.SetSurfaceTouches([new TouchPoint(1, TouchPhase.Moved, new Vector2(80, 400)), new TouchPoint(2, TouchPhase.Moved, new Vector2(180, 400))]);
        Frame();
        host.SetSurfaceTouches([]);
        Frame(3);

        // Joystick e botão de tela
        host.SetSurfaceTouches([new TouchPoint(3, TouchPhase.Moved, new Vector2(110, 560))]);
        Frame(5);
        host.SetSurfaceTouches([new TouchPoint(4, TouchPhase.Moved, new Vector2(290, 560))]);
        Frame(2);
        host.SetSurfaceTouches([]);
        Frame(3);
        Assert.Contains(15, haptics.Calls);

        Assert.False(host.IsFaulted, host.Fault?.ToString());
        Assert.NotEmpty(backend.Batches);

        // Página 2: gráficos (mistura, shader, amostragem, recorte, alvo de desenho, pixel perfect).
        var statesBefore = backend.States.Count;
        Tap(100, 23);
        Frame(3);
        var page2 = backend.States.Skip(statesBefore).ToList();
        Assert.Contains(page2, s => s.Blend == BlendMode.Additive);
        Assert.Contains(page2, s => s.Shader != 0 && s.Uniforms!.ContainsKey("uAmount"));
        Assert.Contains(page2, s => s.Scissor is not null);
        Assert.Contains(page2, s => s.Sampler == SamplerState.PointClamp);
        Assert.Contains(page2, s => s.Sampler == SamplerState.LinearClamp);
        Assert.Single(backend.RenderTargets);
        Assert.Contains(backend.TargetSwitches, t => t.Handle == backend.RenderTargets[0].Handle);
        Assert.False(host.GraphicsDevice!.PixelPerfect);
        Tap(100, 455); // botão "Pixel perfect"
        Assert.True(host.GraphicsDevice.PixelPerfect);
        Tap(100, 455);
        Assert.False(host.GraphicsDevice.PixelPerfect);

        // Resolução virtual alternativa: força escala fracionada; com pixel perfect a escala vira inteira.
        Tap(100, 489);
        Assert.Equal((350, 620), (host.GraphicsDevice.VirtualWidth, host.GraphicsDevice.VirtualHeight));
        Tap(100, 489);
        Assert.Equal((360, 640), (host.GraphicsDevice.VirtualWidth, host.GraphicsDevice.VirtualHeight));

        // Página 3: câmera testável sem copiar código. Preserva as páginas 1/2.
        Tap(100, 23);
        var type = loaded.Game.GetType();
        const System.Reflection.BindingFlags fields = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var camera = (Camera2D)type.GetField("camera", fields)!.GetValue(loaded.Game)!;
        var marker = type.GetField("cameraMarker", fields)!;
        Assert.Equal(1, camera.Zoom);
        Tap(40, 65);
        Assert.Equal(2, camera.Zoom);
        Tap(130, 65);
        Assert.Equal(1, camera.Zoom);
        Tap(260, 65);
        Assert.Equal(MathF.PI / 4, camera.Rotation, 5);
        var expected = camera.ScreenToWorld(new Vector2(260, 320), host.GraphicsDevice.ViewSize);
        host.SetSurfaceTouches([new TouchPoint(9, TouchPhase.Moved, new Vector2(260, 320))]);
        Frame();
        var actual = (Vector2)marker.GetValue(loaded.Game)!;
        Assert.Equal(expected.X, actual.X, 3);
        Assert.Equal(expected.Y, actual.Y, 3);
        host.SetSurfaceTouches([]);
        Frame();
        Tap(100, 23);
        Assert.Equal(3, type.GetField("page", fields)!.GetValue(loaded.Game));
        Tap(100, 23);
        Assert.Equal(4, type.GetField("page", fields)!.GetValue(loaded.Game));
        Tap(100, 23);
        Assert.Equal(5, type.GetField("page", fields)!.GetValue(loaded.Game));
        Tap(100, 23);
        Assert.Equal(6, type.GetField("page", fields)!.GetValue(loaded.Game));
        // As sete áreas antigas continuam nas mesmas posições; as cinco novas vêm depois.
        for (int next = 7; next < 12; next++)
        {
            Tap(100, 23);
            Assert.Equal(next, type.GetField("page", fields)!.GetValue(loaded.Game));
        }
        Tap(100, 23);
        Assert.Equal(0, type.GetField("page", fields)!.GetValue(loaded.Game));
        Assert.False(host.IsFaulted, host.Fault?.ToString());

        host.Stop();
        Assert.Empty(backend.LiveTargets);
        Assert.Empty(backend.LiveShaders);
        Assert.False(host.IsFaulted, host.Fault?.ToString());
    }
}
