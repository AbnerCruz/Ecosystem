using System.Numerics;
using System.Reflection;
using Lunet;
using Lunet.Audio;
using Lunet.Compiler;
using Lunet.Content;
using Lunet.Core;
using Lunet.Graphics;
using Lunet.Input;
using Lunet.Physics;
using Lunet.Runtime;

namespace Lunet.Tests;

[Collection("Frame allocation")]
public sealed class FrameworkAdvancedGateTests
{
    private sealed class AudioRecorder : IAudioBackend
    {
        public int Plays, LiveSounds;
        public int LoadSound(byte[] data, string name) { Assert.True(data.Length > 44); LiveSounds++; return 1; }
        public void UnloadSound(int soundId) => LiveSounds--;
        public int Play(int soundId, float volume, float pan, float pitch, bool loop) { Plays++; return 1; }
        public void SetStream(int streamId, float volume, float pan, float pitch) { }
        public void Stop(int streamId) { }
        public void PauseSounds() { }
        public void ResumeSounds() { }
        public int LoadMusic(byte[] data, string name) => throw new NotSupportedException();
        public void UnloadMusic(int musicId) { }
        public void PlayMusic(int musicId, float volume, bool loop) { }
        public void SetMusicVolume(float volume) { }
        public void PauseMusic() { }
        public void ResumeMusic() { }
        public void StopMusic() { }
        public void Dispose() { }
    }

    [Fact]
    public void PublishedGameRunsVictoryDefeatRestartAndReleasesResourcesThroughPublicApis()
    {
        string temp = Path.Combine(Path.GetTempPath(), "lunet-f4-gate-" + Guid.NewGuid().ToString("N"));
        try
        {
            var project = new ProjectStore(temp).Create("GateF4", ProjectTemplate.CoinCatcher);
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "docs", "guides"))) dir = dir.Parent;
            string guide = File.ReadAllText(Path.Combine(dir!.FullName, "docs", "guides", "gate-framework-advanced.md"));
            string code = guide.ReplaceLineEndings("\n").Split("```csharp\n")[1].Split("```")[0];
            var compiled = new GameCompiler(new LoadedAssembliesReferenceProvider(typeof(Game).Assembly))
                .Compile("GateF4", [new SourceFile("Game.cs", code)]);
            Assert.True(compiled.Success, string.Join("\n", compiled.Diagnostics));
            Assert.DoesNotContain(compiled.Diagnostics, x => x.Severity == DiagnosticSeverity.Warning);
            using var loaded = GameLoader.Load(compiled.Assembly!, compiled.Symbols);
            var backend = new RecordingBackend();
            var audio = new AudioRecorder();
            var host = new GameHost(loaded.Game, backend,
                new DirectoryContentSource(Path.Combine(project.Directory, "Content")), audio);
            Assert.True(host.Start(720, 1280), host.Fault?.ToString());
            object Field(string name) => loaded.Game.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(loaded.Game)!;
            void Tick(int frames = 1)
            {
                for (int i = 0; i < frames; i++)
                {
                    host.Tick(1.0 / 60);
                    Assert.False(host.IsFaulted, host.Fault?.ToString());
                    backend.Batches.Clear();
                }
            }
            void Touch(TouchPhase phase, float x, float y)
            {
                host.SetSurfaceTouches([new TouchPoint(1, phase, loaded.Game.GraphicsDevice.VirtualToSurface(new Vector2(x, y)))]);
                Tick();
            }
            void Reset()
            {
                Touch(TouchPhase.Pressed, 260, 582);
                Touch(TouchPhase.Released, 260, 582);
                host.SetSurfaceTouches([]); Tick();
                Assert.Equal(0, Field("status")); Assert.Equal(0, Field("score"));
            }
            Tick();
            var player = (RigidBody2D)Field("player");
            Vector2 start = player.Position;
            Touch(TouchPhase.Pressed, 205, 505); Tick(20);
            Assert.True(player.Position.X > start.X);
            Touch(TouchPhase.Released, 205, 505); host.SetSurfaceTouches([]); Tick();
            Assert.Equal(Vector2.Zero, player.LinearVelocity);
            var map = (TileMap)Field("map");
            Assert.True(map.IsBlocked(new(0, 0)));
            Assert.False(map.IsBlocked(new(1, 1)));
            // Exercise the solver against a real wall instead of a hand-coded collision substitute.
            player.SetTransform(new(1.5f, 1.5f), 0);
            Touch(TouchPhase.Pressed, 40, 505); Tick(90);
            Assert.InRange(player.Position.X, 1.25f, 1.35f);
            Touch(TouchPhase.Released, 40, 505); host.SetSurfaceTouches([]); Tick();
            foreach (var target in (Vector2[])Field("targets"))
            {
                player.SetTransform(target / 32, 0); Tick(3);
            }
            Assert.Equal(3, Field("score")); Assert.Equal(1, Field("status"));
            Assert.Equal(3, audio.Plays);
            Assert.True(((ParticleEmitter)Field("particles")).Count > 0);
            Reset();
            ((RigidBody2D)Field("player")).SetTransform(((RigidBody2D)Field("hazard")).Position, 0);
            Tick(3); Assert.Equal(2, Field("status"));
            Reset(); Tick(2702); Assert.Equal(2, Field("status"));
            Reset();
            float elapsed = (float)Field("elapsed");
            host.Pause(); Tick(30); Assert.Equal(elapsed, Field("elapsed"));
            host.Resume(); Tick(); Assert.True((float)Field("elapsed") > elapsed);
            host.Stop();
            Assert.Empty(backend.LiveTextures); Assert.Empty(backend.LiveTargets); Assert.Empty(backend.LiveShaders);
            Assert.Equal(0, audio.LiveSounds);
        }
        finally { if (Directory.Exists(temp)) Directory.Delete(temp, true); }
    }
}
