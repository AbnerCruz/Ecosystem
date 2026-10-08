using System.Numerics;
using Lunet;
using Lunet.Core;
using Lunet.Graphics;
using Lunet.Input;
using Lunet.Pathfinding;

namespace Lunet.Tests;

[Collection("Frame allocation")]
public sealed class LaboratoryV2Tests
{
    [Fact]
    public void NewLaboratoryRunsAllModulesViaIndexAndKeepsExistingProjectUntouched()
    {
        string root = Path.Combine(Path.GetTempPath(), "lunet-lab2-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new ProjectStore(root);
            var old = store.Create("Existing", ProjectTemplate.Blank);
            string oldSource = File.ReadAllText(Path.Combine(old.Directory, old.Manifest.EntryPoint));
            var project = store.Create("LabV2", ProjectTemplate.Lab);
            string source = File.ReadAllText(Path.Combine(project.Directory, project.Manifest.EntryPoint));
            Assert.Contains("PageCount = 12", source);
            Assert.Contains("TileMap.Parse", source);
            Assert.Contains("ParticleEmitter", source);
            Assert.Equal(oldSource, File.ReadAllText(Path.Combine(old.Directory, old.Manifest.EntryPoint)));

            var compiler = new Lunet.Compiler.GameCompiler(
                new Lunet.Compiler.LoadedAssembliesReferenceProvider(typeof(Game).Assembly));
            var compiled = compiler.Compile("LabV2", project.LoadSources()
                .Select(x => new Lunet.Compiler.SourceFile(x.Path, x.Text)).ToList());
            Assert.True(compiled.Success, string.Join("\n", compiled.Diagnostics));
            Assert.DoesNotContain(compiled.Diagnostics, x => x.Severity == Lunet.Compiler.DiagnosticSeverity.Warning);
            using var loaded = Lunet.Runtime.GameLoader.Load(compiled.Assembly!, compiled.Symbols);
            var backend = new RecordingBackend();
            var host = new GameHost(loaded.Game, backend,
                new Lunet.Content.DirectoryContentSource(Path.Combine(project.Directory, "Content")));
            Assert.True(host.Start(720, 1280), host.Fault?.ToString());
            host.Tick(1.0 / 60);
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            object Read(string name) => loaded.Game.GetType().GetField(name, flags)!.GetValue(loaded.Game)!;
            void Touch(TouchPhase phase, float x, float y)
            {
                host.SetSurfaceTouches([new TouchPoint(1, phase,
                    loaded.Game.GraphicsDevice.VirtualToSurface(new Vector2(x, y)))]);
                host.Tick(1.0 / 60);
            }
            void Tap(float x, float y)
            {
                Touch(TouchPhase.Pressed, x, y);
                Touch(TouchPhase.Released, x, y);
                host.SetSurfaceTouches([]);
                host.Tick(1.0 / 60);
            }
            void Go(int index)
            {
                Tap(315, 23);
                Assert.True((bool)Read("menuOpen"));
                Tap((index % 2 == 0 ? 8 : 184) + 84, 92 + index / 2 * 77 + 32);
                Assert.False((bool)Read("menuOpen"));
                Assert.Equal(index, Read("page"));
                Assert.False(host.IsFaulted, host.Fault?.ToString());
            }
            for (int index = 0; index < 12; index++) Go(index);
            Go(7);
            Tap(240, 94); // burst de partículas
            Assert.True(((ParticleEmitter)Read("fxParticles")).Count > 0);
            Tap(100, 23); Assert.Equal(8, Read("page"));
            Assert.True(((TileMap)Read("tileMap")).IsBlocked(new GridPoint(3, 1)));
            Tap(240, 90); Assert.True((bool)Read("tileCameraRotated"));
            Go(9);
            Touch(TouchPhase.Moved, 150, 260);
            Assert.NotEqual(Read("collisionDesired"), Read("collisionResolved"));
            host.SetSurfaceTouches([]); host.Tick(1.0 / 60); // encerra o arraste antes do próximo toque
            Go(10);
            Tap(40, 104); Assert.Equal(1235, Read("score"));
            Go(11);
            Tap(178, 118); Assert.Equal(1, Read("paletteIndex"));
            Tap(160, 280); Assert.Equal(1, Read("themeClicks"));
            Tap(100, 23); Assert.Equal(0, Read("page"));
            host.Pause(); host.Resume(); host.Tick(1.0 / 60);
            Assert.False(host.IsFaulted, host.Fault?.ToString());
            host.Stop();
            Assert.Empty(backend.LiveTargets);
            Assert.Empty(backend.LiveShaders);
            Assert.Equal(source, File.ReadAllText(Path.Combine(project.Directory, project.Manifest.EntryPoint)));
            Assert.Equal(oldSource, File.ReadAllText(Path.Combine(old.Directory, old.Manifest.EntryPoint)));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
