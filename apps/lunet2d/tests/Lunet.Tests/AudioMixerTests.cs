using System.Numerics;
using Lunet.Audio;
using Lunet.Content;
using Lunet.Graphics;
using Lunet.Input;

namespace Lunet.Tests;

public class AudioMixerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "lunet-mixer-" + Guid.NewGuid().ToString("N"));

    public AudioMixerTests()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllBytes(Path.Combine(_root, "hit.wav"), [1, 2, 3]);
        File.WriteAllBytes(Path.Combine(_root, "theme.ogg"), [4, 5, 6]);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private sealed class Recorder : IAudioBackend
    {
        public List<string> Calls { get; } = [];
        public float LastStreamVolume, LastMusicVolume;
        public int LoadSound(byte[] data, string name) => 1;
        public void UnloadSound(int soundId) => Calls.Add("unloadSound");
        public int Play(int soundId, float volume, float pan, float pitch, bool loop) { LastStreamVolume = volume; Calls.Add($"play:{volume:0.###}"); return 5; }
        public void SetStream(int streamId, float volume, float pan, float pitch) { LastStreamVolume = volume; Calls.Add($"set:{volume:0.###}"); }
        public void Stop(int streamId) => Calls.Add("stop");
        public void PauseSounds() => Calls.Add("pauseSounds");
        public void ResumeSounds() => Calls.Add("resumeSounds");
        public int LoadMusic(byte[] data, string name) => 2;
        public void UnloadMusic(int musicId) => Calls.Add("unloadMusic");
        public void PlayMusic(int musicId, float volume, bool loop) { LastMusicVolume = volume; Calls.Add($"playMusic:{volume:0.###}:{loop}"); }
        public void SetMusicVolume(float volume) { LastMusicVolume = volume; Calls.Add($"musicVolume:{volume:0.###}"); }
        public void PauseMusic() => Calls.Add("pauseMusic");
        public void ResumeMusic() => Calls.Add("resumeMusic");
        public void StopMusic() => Calls.Add("stopMusic");
        public void Dispose() { }
    }

    private (ContentManager Content, Recorder Backend) New()
    {
        var backend = new Recorder();
        var content = new ContentManager(new DirectoryContentSource(_root), new GraphicsDevice(new RecordingBackend(), 10, 10), backend);
        return (content, backend);
    }

    [Fact]
    public void Volume_IsInstanceTimesBusTimesMaster_AndBusChangesApplyToPlayingSounds()
    {
        var (content, backend) = New();
        var mixer = content.Audio;
        var instance = content.LoadSound("hit.wav").Play(0.8f, 0, 1, false);
        Assert.Equal(0.8f, backend.LastStreamVolume, 0.001);

        mixer.Sfx.Volume = 0.5f;
        mixer.Master.Volume = 0.5f;
        mixer.Update(0.016f);
        Assert.Equal(0.2f, backend.LastStreamVolume, 0.001); // 0,8 × 0,5 × 0,5

        mixer.Sfx.Muted = true;
        mixer.Update(0.016f);
        Assert.Equal(0f, backend.LastStreamVolume);
        Assert.False(instance.IsStopped);
    }

    [Fact]
    public void CustomBus_AndUnchangedVolumeDoesNotSpamBackend()
    {
        var (content, backend) = New();
        var voice = content.Audio.GetBus("Voice");
        Assert.Same(voice, content.Audio.GetBus("voice"));
        content.LoadSound("hit.wav").Play(1, 0, 1, false, voice);
        var calls = backend.Calls.Count;
        for (var i = 0; i < 10; i++) content.Audio.Update(0.016f);
        Assert.Equal(calls, backend.Calls.Count);
    }

    [Fact]
    public void SoundFadeOut_StopsWhenDoneAndIsForgotten()
    {
        var (content, backend) = New();
        var instance = content.LoadSound("hit.wav").Play();
        instance.FadeTo(0f, 1f, stopWhenDone: true);
        content.Audio.Update(0.5f);
        Assert.Equal(0.5f, backend.LastStreamVolume, 0.01);
        Assert.False(instance.IsStopped);
        content.Audio.Update(0.6f);
        Assert.True(instance.IsStopped);
        Assert.Contains("stop", backend.Calls);
        content.Audio.Update(0.1f); // já removida: nenhuma chamada extra
        Assert.Single(backend.Calls, c => c == "stop");
    }

    [Fact]
    public void Music_PlaysWithFadeInAndBusVolume()
    {
        var (content, backend) = New();
        var music = content.LoadMusic("theme.ogg");
        Assert.Same(music, content.LoadMusic("theme.ogg"));
        content.Audio.MusicBus.Volume = 0.5f;
        content.Audio.PlayMusic(music, loop: true, fadeInSeconds: 2f);
        Assert.Equal("playMusic:0:True", backend.Calls.Last(c => c.StartsWith("playMusic")));
        content.Audio.Update(1f);
        Assert.Equal(0.25f, backend.LastMusicVolume, 0.001);
        content.Audio.Update(1f);
        Assert.Equal(0.5f, backend.LastMusicVolume, 0.001);
        Assert.Same(music, content.Audio.CurrentMusic);
    }

    [Fact]
    public void Music_FadeOutStopsAtEnd_AndImmediateStopWorks()
    {
        var (content, backend) = New();
        var music = content.LoadMusic("theme.ogg");
        content.Audio.PlayMusic(music);
        content.Audio.StopMusic(fadeOutSeconds: 1f);
        content.Audio.Update(0.5f);
        Assert.Equal(0.5f, backend.LastMusicVolume, 0.01);
        Assert.DoesNotContain("stopMusic", backend.Calls);
        content.Audio.Update(0.6f);
        Assert.Contains("stopMusic", backend.Calls);
        Assert.Null(content.Audio.CurrentMusic);

        content.Audio.PlayMusic(music);
        content.Audio.StopMusic();
        Assert.Equal(2, backend.Calls.Count(c => c == "stopMusic"));
    }

    [Fact]
    public void PauseAndResume_AffectMusicAndSounds_AndNewMusicWhilePausedStaysPaused()
    {
        var (content, backend) = New();
        var music = content.LoadMusic("theme.ogg");
        content.Audio.PlayMusic(music);
        content.Audio.PauseAll();
        content.Audio.PauseAll(); // idempotente
        Assert.Equal(1, backend.Calls.Count(c => c == "pauseMusic"));
        Assert.Contains("pauseSounds", backend.Calls);
        content.Audio.ResumeAll();
        Assert.Contains("resumeMusic", backend.Calls);

        content.Audio.PauseAll();
        content.Audio.PlayMusic(music);
        Assert.Equal("pauseMusic", backend.Calls[^1]);
    }

    private sealed class MusicGame : Game
    {
        protected override void LoadContent() => Audio.PlayMusic(Content.LoadMusic("theme.ogg"));
    }

    [Fact]
    public void Host_PausesAudioWhenGamePausesAndReleasesMusicOnStop()
    {
        var backend = new Recorder();
        var host = new GameHost(new MusicGame(), new RecordingBackend(), new DirectoryContentSource(_root), backend);
        host.Start(10, 10);
        host.Pause();
        Assert.Contains("pauseMusic", backend.Calls);
        host.Resume();
        Assert.Contains("resumeMusic", backend.Calls);
        host.Stop();
        Assert.Contains("stopMusic", backend.Calls);
        Assert.Contains("unloadMusic", backend.Calls);
    }
}

public class GamepadAndDeviceTests
{
    [Fact]
    public void Gamepad_ButtonPressedFiresOncePerPressAndClearsAfterFirstStep()
    {
        var input = new InputState();
        Assert.False(input.IsButtonDown(GamepadButtons.A));
        input.SetGamepad(new GamepadState(true, GamepadButtons.A | GamepadButtons.DPadLeft, Vector2.Zero, Vector2.Zero, 0, 0));
        Assert.True(input.IsButtonDown(GamepadButtons.A));
        Assert.True(input.IsButtonPressed(GamepadButtons.A));
        Assert.True(input.IsButtonDown(GamepadButtons.A | GamepadButtons.DPadLeft));
        Assert.False(input.IsButtonDown(GamepadButtons.B));

        input.ClearGestures();
        input.SetGamepad(new GamepadState(true, GamepadButtons.A, Vector2.Zero, Vector2.Zero, 0, 0)); // continua segurando
        Assert.True(input.IsButtonDown(GamepadButtons.A));
        Assert.False(input.IsButtonPressed(GamepadButtons.A));

        input.SetGamepad(default);
        Assert.False(input.IsButtonDown(GamepadButtons.A));
        input.SetGamepad(new GamepadState(true, GamepadButtons.A, Vector2.Zero, Vector2.Zero, 0, 0));
        Assert.True(input.IsButtonPressed(GamepadButtons.A));
    }

    [Fact]
    public void Gamepad_DeadZoneZeroesNoiseAndRescalesTheRest()
    {
        var state = new GamepadState(true, GamepadButtons.None, new Vector2(0.1f, 0.05f), new Vector2(1f, 0f), 0, 0).WithDeadZone(0.2f);
        Assert.Equal(Vector2.Zero, state.LeftStick);
        Assert.Equal(1f, state.RightStick.X, 0.001);
        var mid = new GamepadState(true, GamepadButtons.None, new Vector2(0.6f, 0f), Vector2.Zero, 0, 0).WithDeadZone(0.2f);
        Assert.Equal(0.5f, mid.LeftStick.X, 0.001);
    }

    private sealed class DeviceGame : Game
    {
        public float Gyro;
        public bool Vibrated;
        protected override void Update(GameTime time)
        {
            Gyro = Input.Gyroscope.Z;
            if (Input.IsButtonPressed(GamepadButtons.A)) { Haptics.Vibrate(40, 0.5f); Vibrated = true; }
        }
    }

    private sealed class RecordingHaptics : IHaptics
    {
        public List<(int Ms, float Intensity)> Calls { get; } = [];
        public void Vibrate(int milliseconds, float intensity = 1f) => Calls.Add((milliseconds, intensity));
        public void Cancel() { }
    }

    [Fact]
    public void Host_ExposesGyroscopeGamepadAndHapticsToGame()
    {
        var game = new DeviceGame();
        var haptics = new RecordingHaptics();
        var host = new GameHost(game, new RecordingBackend(), null, null, null, haptics);
        host.Start(10, 10);
        host.Input.SetGyroscope(new Vector3(0, 0, 1.5f));
        host.Input.SetGamepad(new GamepadState(true, GamepadButtons.A, Vector2.Zero, Vector2.Zero, 0, 0));
        host.Tick(0.05);
        Assert.Equal(1.5f, game.Gyro);
        Assert.Equal([(40, 0.5f)], haptics.Calls);
        Assert.Same(haptics, game.Services.Get<IHaptics>());
    }

    [Fact]
    public void Haptics_DefaultsToNullObject()
    {
        var game = new DeviceGame();
        new GameHost(game, new RecordingBackend()).Start(10, 10);
        game.Haptics.Vibrate(10); // não lança
        Assert.IsType<NullHaptics>(game.Haptics);
    }
}
