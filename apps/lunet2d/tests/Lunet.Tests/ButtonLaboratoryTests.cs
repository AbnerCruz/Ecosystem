using System.Numerics;
using Lunet.Graphics;
using Lunet.Input;
using Lunet.UI;

namespace Lunet.Tests;

[Collection("Frame allocation")]
public class ButtonLaboratoryTests
{
    [Fact]
    public void NewLaboratoryRunsButtonsAndLayoutWithoutEditingSource()
    {
        var root = Path.Combine(Path.GetTempPath(), "lunet-button-lab-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new Lunet.Core.ProjectStore(root);
            var project = store.Create("ButtonLab", Lunet.Core.ProjectTemplate.Lab);
            var compiler = new Lunet.Compiler.GameCompiler(new Lunet.Compiler.LoadedAssembliesReferenceProvider(typeof(Game).Assembly));
            var result = compiler.Compile("ButtonLab", project.LoadSources().Select(s => new Lunet.Compiler.SourceFile(s.Path, s.Text)).ToList());
            Assert.True(result.Success, string.Join("\n", result.Diagnostics));
            Assert.DoesNotContain(result.Diagnostics, d => d.Severity == Lunet.Compiler.DiagnosticSeverity.Warning);
            using var loaded = Lunet.Runtime.GameLoader.Load(result.Assembly!, result.Symbols);
            var backend = new RecordingBackend();
            var host = new GameHost(loaded.Game, backend, new Lunet.Content.DirectoryContentSource(Path.Combine(project.Directory, "Content")));
            Assert.True(host.Start(720, 1280), host.Fault?.ToString()); host.Tick(1.0 / 60);
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            object Field(string name) => loaded.Game.GetType().GetField(name, flags)!.GetValue(loaded.Game)!;
            void Touch(TouchPhase phase, float x, float y)
            {
                host.SetSurfaceTouches([new TouchPoint(1, phase, loaded.Game.GraphicsDevice.VirtualToSurface(new Vector2(x, y)))]);
                host.Tick(1.0 / 60);
            }
            void Tap(float x, float y)
            {
                Touch(TouchPhase.Pressed, x, y); Touch(TouchPhase.Released, x, y);
                host.SetSurfaceTouches([]); host.Tick(1.0 / 60); host.Tick(1.0 / 60);
            }
            for (int i = 0; i < 4; i++) Tap(100, 23);
            Assert.Equal(4, Field("page"));
            var button = (TouchButton)Field("clickButton");
            Touch(TouchPhase.Pressed, 180, 200); Assert.True(button.IsPressed); Assert.Equal(0, Field("buttonClicks"));
            Touch(TouchPhase.Released, 180, 200); Assert.Equal(1, Field("buttonClicks"));
            Touch(TouchPhase.Pressed, 180, 200); Touch(TouchPhase.Moved, 180, 235); Touch(TouchPhase.Released, 180, 235);
            Assert.Equal(1, Field("buttonClicks"));
            Touch(TouchPhase.Pressed, 180, 200); Touch(TouchPhase.Moved, 180, 235); Touch(TouchPhase.Moved, 180, 200);
            Assert.True(button.IsPressed); Touch(TouchPhase.Released, 180, 200); Assert.Equal(2, Field("buttonClicks"));
            Tap(180, 284); Assert.False(button.IsEnabled); Tap(180, 200); Assert.Equal(2, Field("buttonClicks"));
            Tap(180, 284); Assert.True(button.IsEnabled);
            Touch(TouchPhase.Pressed, 180, 200); host.Pause(); Assert.False(button.IsCaptured);
            host.Resume(); Touch(TouchPhase.Released, 180, 200); Assert.Equal(2, Field("buttonClicks"));
            Tap(180, 368); Assert.Equal(438, button.Bounds.Y);
            Tap(180, 200); Assert.Equal(2, Field("buttonClicks")); Tap(180, 464); Assert.Equal(3, Field("buttonClicks"));
            loaded.Game.GraphicsDevice.SetVirtualResolution(400, 640); host.Tick(1.0 / 60);
            Assert.Equal(44, button.Bounds.X); Tap(30, 464); Assert.Equal(3, Field("buttonClicks"));
            Tap(200, 464); Assert.Equal(4, Field("buttonClicks"));
            Assert.Contains(backend.Batches, b => b.Vertices.Any(v => v.Position == new Vector2(44, 438)));
            Touch(TouchPhase.Pressed, 200, 464);
            host.SetSurfaceTouches([
                new TouchPoint(1, TouchPhase.Moved, loaded.Game.GraphicsDevice.VirtualToSurface(new Vector2(200, 464))),
                new TouchPoint(2, TouchPhase.Pressed, loaded.Game.GraphicsDevice.VirtualToSurface(new Vector2(100, 23)))
            ]); host.Tick(1.0 / 60);
            host.SetSurfaceTouches([
                new TouchPoint(1, TouchPhase.Moved, loaded.Game.GraphicsDevice.VirtualToSurface(new Vector2(200, 464))),
                new TouchPoint(2, TouchPhase.Released, loaded.Game.GraphicsDevice.VirtualToSurface(new Vector2(100, 23)))
            ]); host.Tick(1.0 / 60);
            Assert.Equal(5, Field("page")); Assert.False(button.IsCaptured); Assert.Equal(4, Field("buttonClicks"));
            host.SetSurfaceTouches([]); host.Tick(1.0 / 60); Tap(100, 23); Assert.Equal(0, Field("page"));
            Assert.False(host.IsFaulted, host.Fault?.ToString()); host.Stop();
            Assert.Empty(backend.LiveTargets); Assert.Empty(backend.LiveShaders);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
