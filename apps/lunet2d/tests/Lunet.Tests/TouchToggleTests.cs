using System.Numerics;
using Lunet.Graphics;
using Lunet.Input;
using Lunet.UI;
namespace Lunet.Tests;
[Collection("Frame allocation")]
public sealed class TouchToggleTests
{
    static readonly RectangleF Area = new(24, 100, 200, 64);
    static TouchPoint T(int id, TouchPhase phase, float x = 80, float y = 132) => new(id, phase, new Vector2(x, y));
    static void Frame(TouchToggle toggle, InputState input, params TouchPoint[] touches)
    { input.SetTouches(touches); toggle.Update(input); }

    [Fact]
    public void OnlyReleaseInsideTogglesAndChangeIsOnePulse()
    {
        var t = new TouchToggle(Area); var input = new InputState();
        Frame(t, input, T(1, TouchPhase.Pressed)); Assert.True(t.IsCaptured); Assert.False(t.WasChanged);
        t.Update(input); Assert.False(t.Value);
        Frame(t, input, T(1, TouchPhase.Released)); Assert.True(t.Value); Assert.True(t.WasChanged);
        t.Update(input); Assert.True(t.Value); Assert.False(t.WasChanged);
        Frame(t, input, T(1, TouchPhase.Pressed)); Frame(t, input, T(1, TouchPhase.Released));
        Assert.False(t.Value); Assert.True(t.WasChanged);
        t.Value = true; t.Update(input); Assert.True(t.Value); Assert.False(t.WasChanged);
    }

    [Fact]
    public void OutsideCancelMultitouchAndDisableDoNotStealGesture()
    {
        var t = new TouchToggle(Area, true); var input = new InputState();
        Frame(t, input, T(-1, TouchPhase.Pressed), T(3, TouchPhase.Pressed, 340));
        Frame(t, input, T(3, TouchPhase.Released), T(-1, TouchPhase.Moved, 350));
        Assert.True(t.IsCaptured); Assert.False(t.IsPressed);
        Frame(t, input, T(-1, TouchPhase.Released, 350)); Assert.True(t.Value); Assert.False(t.WasChanged);
        Frame(t, input, T(4, TouchPhase.Pressed)); Frame(t, input, T(4, TouchPhase.Cancelled));
        Assert.True(t.Value); Assert.False(t.WasChanged);
        Frame(t, input, T(5, TouchPhase.Pressed)); t.IsEnabled = false;
        t.IsEnabled = true; t.Update(input); Assert.False(t.IsCaptured);
        Frame(t, input, T(5, TouchPhase.Released)); Assert.True(t.Value);
        Frame(t, input, T(6, TouchPhase.Pressed)); t.Cancel(); t.Update(input);
        Assert.False(t.IsCaptured); Assert.False(t.WasChanged);
    }

    [Fact]
    public void DrawUsesOnOffPalettesAndValidatesArguments()
    {
        var backend = new RecordingBackend(); var device = new GraphicsDevice(backend, 360, 640);
        var batch = new SpriteBatch(device); var font = SpriteFont.CreateDefault(device);
        var off = TouchButtonStyle.Default;
        var on = new TouchButtonStyle(new Color(20, 170, 80), new Color(30, 200, 90), Color.Black, Color.White, Color.White);
        var t = new TouchToggle(Area);
        void Draw(uint expected)
        {
            backend.Batches.Clear();
            batch.Begin(); t.Draw(batch, font, "OFF", "ON", off, on, 2); batch.End();
            Assert.Equal(expected, backend.Batches[0].Vertices[0].Color);
        }
        Draw(off.Background.PackedRgba); t.Value = true; Draw(on.Background.PackedRgba);
        t.IsEnabled = false; Draw(on.DisabledBackground.PackedRgba);
        Assert.Throws<ArgumentNullException>(() => t.Draw(batch, font, null!, "ON", off, on));
        Assert.Throws<ArgumentOutOfRangeException>(() => t.Draw(batch, font, "OFF", "ON", off, on, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => t.Bounds = new(float.NaN, 0, 10, 20));
        Assert.Throws<ArgumentNullException>(() => t.Update(null!));
    }

    [Fact]
    public void HotPathUpdateAndDrawDoNotAllocate()
    {
        var device = new GraphicsDevice(new NoOpBackend(), 360, 640);
        var batch = new SpriteBatch(device); var font = SpriteFont.CreateDefault(device);
        var t = new TouchToggle(Area); var input = new InputState();
        void Cycle()
        {
            input.SetTouches([T(1, TouchPhase.Pressed)]); t.Update(input);
            batch.Begin(); t.Draw(batch, font, "OFF", "ON", TouchButtonStyle.Default, TouchButtonStyle.Default); batch.End();
            input.SetTouches([T(1, TouchPhase.Released)]); t.Update(input);
            batch.Begin(); t.Draw(batch, font, "OFF", "ON", TouchButtonStyle.Default, TouchButtonStyle.Default); batch.End();
            input.SetTouches([]); t.Update(input);
        }
        for (int i = 0; i < 80; i++) Cycle();
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 100; i++) Cycle();
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [Fact]
    public void OfflineGuideCompilesAndRunsWithRealGameHost()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "docs", "guides", "toggle-ui.md"))) dir = dir.Parent;
        Assert.NotNull(dir);
        string source = File.ReadAllText(Path.Combine(dir!.FullName, "docs", "guides", "toggle-ui.md"));
        var fence = new string((char)96, 3);
        source = source.Split(fence + "csharp\n")[1].Split(fence)[0];
        var compiler = new Lunet.Compiler.GameCompiler(new Lunet.Compiler.LoadedAssembliesReferenceProvider(typeof(Game).Assembly));
        var result = compiler.Compile("ToggleGuide", [new Lunet.Compiler.SourceFile("Game.cs", source)]);
        Assert.True(result.Success, string.Join("\n", result.Diagnostics));
        Assert.DoesNotContain(result.Diagnostics, d => d.Severity == Lunet.Compiler.DiagnosticSeverity.Warning);
        using var game = Lunet.Runtime.GameLoader.Load(result.Assembly!, result.Symbols);
        var host = new GameHost(game.Game, new RecordingBackend());
        Assert.True(host.Start(360, 640), host.Fault?.ToString());
        host.Tick(1d / 60);
        var field = game.Game.GetType().GetField("musicEnabled",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(field); Assert.False((bool)field!.GetValue(game.Game)!);
        host.SetSurfaceTouches([T(2, TouchPhase.Pressed)]); host.Tick(1d / 60);
        host.SetSurfaceTouches([T(2, TouchPhase.Released)]); host.Tick(1d / 60);
        Assert.True((bool)field.GetValue(game.Game)!);
        host.SetSurfaceTouches([]); host.Tick(1d / 60);
        Assert.False(host.IsFaulted, host.Fault?.ToString()); host.Stop();
    }
}
