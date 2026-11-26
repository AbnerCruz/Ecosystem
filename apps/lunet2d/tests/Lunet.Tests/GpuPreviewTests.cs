using Avalonia.Controls;
using Avalonia.VisualTree;
using Lunet.Client;
using Lunet.Client.Shell;
using Lunet.Input;

namespace Lunet.Tests;

/// <summary>
/// M5: the shell drives a head's GPU preview (Android: GLES) from the Jogo tab — compile, show the surface, pause,
/// step, restart, keys, stop, faults (a late fault of a stopped run is ignored). A fake preview stands in for GLES:
/// the GL surface itself only runs on Android and is not tested here.
/// </summary>
public sealed class GpuPreviewTests
{
    private sealed class FakeGpu : IGpuGamePreview
    {
        public Control View { get; } = new Border { Name = "GpuSurface" };
        public string Backend => "GPU · teste";
        public long FramesRendered { get; set; }
        public List<string> Calls { get; } = [];
        public void SetPaused(bool paused) => Calls.Add("pause:" + paused);
        public void Step() => Calls.Add("step");
        public void Restart() => Calls.Add("restart");
        public void ToggleInspector() => Calls.Add("inspector");
        public void ToggleProfiler() => Calls.Add("profiler");
        public void Key(Keys key, bool down) => Calls.Add($"key:{key}:{down}");
        public void SetAppVisible(bool visible) => Calls.Add("visible:" + visible);
        public bool Disposed;
        public void Dispose() => Disposed = true;
    }

    [Fact]
    public void TheJogoTab_HostsTheGpuSurface_AndTheBarDrivesIt()
    {
        var root = Path.Combine(Path.GetTempPath(), "lunet-gpu-" + Guid.NewGuid().ToString("N"));
        try
        {
            UiSession.Run(() =>
            {
                var shell = new LunetShell(root);
                FakeGpu? gpu = null; Action<string>? fault = null;
                shell.Game.GpuFactory = (compiled, project, f) => { Assert.NotNull(compiled.Assembly); fault = f; return gpu = new FakeGpu(); };
                var window = new Window { Width = 412, Height = 915, Content = shell };
                window.Show();
                shell.CreateProject();
                shell.RunGame();
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Assert.Equal(GameState.Running, shell.Game.State);
                Assert.Same(gpu, shell.Game.Gpu);
                Assert.Contains(window.GetVisualDescendants().OfType<Border>(), b => b.Name == "GpuSurface"); // on the stage
                Assert.DoesNotContain(window.GetVisualDescendants().OfType<Button>(), b => b.Name == "SpeedChip"); // no fake speed
                gpu!.FramesRendered = 42;
                Assert.Equal(42, shell.Game.FramesRendered);                 // the GPU's real count (the CI journey reads it)

                shell.Game.TogglePause(); Assert.Contains("pause:True", gpu!.Calls);
                shell.Game.Step(); Assert.Contains("step", gpu.Calls);
                shell.Game.TogglePause(); Assert.Contains("pause:False", gpu.Calls);
                shell.Game.Key(Keys.Space, true); Assert.Contains("key:Space:True", gpu.Calls);
                shell.Game.Restart(); Assert.Contains("restart", gpu.Calls);

                shell.StopGame();
                Assert.True(gpu.Disposed);
                Assert.Null(shell.Game.Gpu);
                fault!("tarde demais");                                   // a stopped run's late fault…
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Assert.Equal(GameState.Stopped, shell.Game.State);        // …changes nothing

                shell.RunGame();
                var second = gpu;
                fault!("OpenGL ES 3.0 indisponível");
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Assert.Equal(GameState.Faulted, shell.Game.State);
                Assert.True(second.Disposed);
                Assert.Contains("OpenGL ES 3.0 indisponível", shell.Game.Problems[0]);
                window.Close();
            });
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void ACompileError_NeverOpensTheGpuSurface()
    {
        var root = Path.Combine(Path.GetTempPath(), "lunet-gpu-err-" + Guid.NewGuid().ToString("N"));
        try
        {
            UiSession.Run(() =>
            {
                var shell = new LunetShell(root);
                var opened = 0;
                shell.Game.GpuFactory = (_, _, _) => { opened++; return new FakeGpu(); };
                var window = new Window { Width = 412, Height = 915, Content = shell };
                window.Show();
                shell.CreateProject();
                shell.WorkspaceView.Code.Surface.Text = "class Quebrado {";
                shell.RunGame();
                Assert.Equal(GameState.CompileErrors, shell.Game.State);
                Assert.Equal(0, opened);
                window.Close();
            });
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void Ajustes_OfferTheGpuOptions_OnlyWhereThereIsAGpuPreview()
    {
        var root = Path.Combine(Path.GetTempPath(), "lunet-gpu-settings-" + Guid.NewGuid().ToString("N"));
        try
        {
            UiSession.Run(() =>
            {
                var shell = new LunetShell(root);
                var window = new Window { Width = 412, Height = 915, Content = shell };
                window.Show();
                shell.Home.ShowSettings();
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Assert.DoesNotContain(window.GetVisualDescendants().OfType<Control>(), c => c.Name == "IsolatedPreview");
                shell.Home.CloseSheet();
                shell.Game.GpuFactory = (_, _, _) => new FakeGpu();
                shell.Home.ShowSettings();
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Assert.Contains(window.GetVisualDescendants().OfType<Control>(), c => c.Name == "IsolatedPreview");
                window.Close();
            });
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
