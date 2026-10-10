using System.Numerics;
using Lunet.Graphics;
using Lunet.Input;
using Lunet.UI;

namespace Lunet.Tests;

[Collection("Frame allocation")]
public class TouchButtonTests
{
    private static readonly RectangleF Area = new(20, 100, 160, 48);
    private static TouchPoint T(int id, TouchPhase phase, float x = 50, float y = 120) => new(id, phase, new(x, y));
    private static void Step(TouchButton b, InputState input, params TouchPoint[] touches) { input.SetTouches(touches); b.Update(input); }

    [Fact]
    public void ClickIsConfirmedOnlyOnReleaseAndIsOneUpdatePulse()
    {
        var b = new TouchButton(Area); var input = new InputState();
        Step(b, input, T(3, TouchPhase.Pressed)); Assert.True(b.IsCaptured); Assert.True(b.IsPressed); Assert.False(b.WasClicked);
        b.Update(input); Assert.True(b.IsPressed); Assert.False(b.WasClicked);
        Step(b, input, T(3, TouchPhase.Released)); Assert.True(b.WasClicked); Assert.False(b.IsCaptured); Assert.False(b.IsPressed);
        b.Update(input); Assert.False(b.WasClicked);
        Step(b, input, T(3, TouchPhase.Pressed)); Step(b, input, T(3, TouchPhase.Released)); Assert.True(b.WasClicked);
    }

    [Fact]
    public void DragOutCancelsVisualPressAndRelease_WhileReturnInsideAllowsClick()
    {
        var b = new TouchButton(Area); var input = new InputState();
        Step(b, input, T(1, TouchPhase.Pressed)); Step(b, input, T(1, TouchPhase.Moved, 250));
        Assert.True(b.IsCaptured); Assert.False(b.IsPressed);
        Step(b, input, T(1, TouchPhase.Released, 250)); Assert.False(b.WasClicked);
        Step(b, input, T(1, TouchPhase.Pressed)); Step(b, input, T(1, TouchPhase.Moved, 250));
        Step(b, input, T(1, TouchPhase.Moved)); Assert.True(b.IsPressed);
        Step(b, input, T(1, TouchPhase.Released)); Assert.True(b.WasClicked);
    }

    [Fact]
    public void StartingOutsideOrAlreadyMovedDoesNotCapture()
    {
        var b = new TouchButton(Area); var input = new InputState();
        Step(b, input, T(1, TouchPhase.Pressed, 250)); Step(b, input, T(1, TouchPhase.Moved));
        Assert.False(b.IsCaptured); Step(b, input, T(1, TouchPhase.Released)); Assert.False(b.WasClicked);
        Step(b, input, T(2, TouchPhase.Moved)); Assert.False(b.IsCaptured);
        // Pressed is a retained snapshot, not a new down edge for the same ID.
        Step(b, input, T(2, TouchPhase.Pressed)); Assert.False(b.IsCaptured);
    }

    [Fact]
    public void CaptureFollowsItsIdDespiteOrderOtherFingersAndNegativeId()
    {
        var b = new TouchButton(Area); var input = new InputState();
        Step(b, input, T(-5, TouchPhase.Pressed));
        Step(b, input, T(2, TouchPhase.Released), T(-5, TouchPhase.Moved)); Assert.True(b.IsPressed); Assert.False(b.WasClicked);
        Step(b, input, T(2, TouchPhase.Pressed), T(-5, TouchPhase.Released)); Assert.True(b.WasClicked); Assert.False(b.IsCaptured);
        b.Update(input); Assert.False(b.IsCaptured); Assert.False(b.WasClicked);
        Step(b, input, T(2, TouchPhase.Released)); Assert.False(b.WasClicked);
    }

    [Theory]
    [InlineData(TouchPhase.Cancelled)]
    [InlineData((TouchPhase)99)]
    public void CancellationOrUnknownPhaseCannotClick(TouchPhase phase)
    {
        var b = new TouchButton(Area); var input = new InputState();
        Step(b, input, T(1, TouchPhase.Pressed)); Step(b, input, T(1, phase));
        Assert.False(b.IsCaptured); Assert.False(b.WasClicked);
    }

    [Fact]
    public void LostTouchOrInvalidPositionCancelsWithoutTransferring()
    {
        var b = new TouchButton(Area); var input = new InputState();
        Step(b, input, T(1, TouchPhase.Pressed)); Step(b, input, T(2, TouchPhase.Pressed)); Assert.False(b.IsCaptured);
        Step(b, input, T(2, TouchPhase.Released)); Assert.False(b.WasClicked);
        Step(b, input, T(1, TouchPhase.Pressed)); Step(b, input, T(1, TouchPhase.Moved, float.NaN)); Assert.False(b.IsCaptured);
        Step(b, input, T(1, TouchPhase.Released)); Assert.False(b.WasClicked);
        Step(b, input, T(1, TouchPhase.Pressed)); Step(b, input); Assert.False(b.IsCaptured); Assert.False(b.WasClicked);
    }

    [Fact]
    public void DisableAndExplicitCancelDoNotRearmRetainedPressedSnapshot()
    {
        var b = new TouchButton(Area); var input = new InputState();
        Step(b, input, T(1, TouchPhase.Pressed)); b.IsEnabled = false; Assert.False(b.IsCaptured);
        b.IsEnabled = true; b.Update(input); Assert.False(b.IsCaptured);
        Step(b, input, T(1, TouchPhase.Released)); Assert.False(b.WasClicked);
        b.IsEnabled = false; Step(b, input, T(2, TouchPhase.Pressed)); b.IsEnabled = true; b.Update(input); Assert.False(b.IsCaptured);
        Step(b, input); Step(b, input, T(2, TouchPhase.Pressed)); b.Cancel(); b.Update(input); Assert.False(b.IsCaptured);
        Step(b, input, T(2, TouchPhase.Released)); Assert.False(b.WasClicked);
        Step(b, input, T(3, TouchPhase.Pressed)); Step(b, input, T(3, TouchPhase.Released)); Assert.True(b.WasClicked);
        b.Cancel(); Assert.False(b.WasClicked);
    }

    [Fact]
    public void BoundsChangeUsesCurrentGeometryAndZeroAreaDoesNotCapture()
    {
        var b = new TouchButton(Area); var input = new InputState();
        Step(b, input, T(1, TouchPhase.Pressed)); b.Bounds = new(200, 100, 160, 48);
        Step(b, input, T(1, TouchPhase.Released)); Assert.False(b.WasClicked);
        Step(b, input, T(1, TouchPhase.Pressed, 230)); Step(b, input, T(1, TouchPhase.Released, 230)); Assert.True(b.WasClicked);
        b.Bounds = new(50, 120, 0, 0); Step(b, input, T(1, TouchPhase.Pressed)); Assert.False(b.IsCaptured);
    }

    [Fact]
    public void InvalidBoundsAndDrawArgumentsAreRejectedWithoutChangingBounds()
    {
        var b = new TouchButton(Area);
        foreach (var rect in new[] { new RectangleF(float.NaN, 0, 1, 1), new RectangleF(0, 0, -1, 1), new RectangleF(0, 0, 1, -1), new RectangleF(0, 0, float.PositiveInfinity, 1), new RectangleF(float.MaxValue, 0, float.MaxValue, 1) })
        { Assert.Throws<ArgumentOutOfRangeException>(() => b.Bounds = rect); Assert.Equal(Area, b.Bounds); Assert.Throws<ArgumentOutOfRangeException>(() => new TouchButton(rect)); }
        Assert.Throws<ArgumentNullException>(() => b.Update(null!));
        var device = new GraphicsDevice(new RecordingBackend(), 360, 640); device.Resize(360, 640);
        var batch = new SpriteBatch(device); var font = SpriteFont.CreateDefault(device);
        foreach (float bad in new[] { 0, -1, float.NaN, float.PositiveInfinity })
            Assert.Throws<ArgumentOutOfRangeException>(() => b.Draw(batch, font, "X", TouchButtonStyle.Default, bad));
        Assert.Throws<ArgumentNullException>(() => b.Draw(null!, font, "X", TouchButtonStyle.Default));
        Assert.Throws<ArgumentNullException>(() => b.Draw(batch, null!, "X", TouchButtonStyle.Default));
        Assert.Throws<ArgumentNullException>(() => b.Draw(batch, font, null!, TouchButtonStyle.Default));
        Assert.Throws<OverflowException>(() => b.Draw(batch, font, "XXXXX", TouchButtonStyle.Default, float.MaxValue));
    }

    [Fact]
    public void DrawUsesStateColorsCentersTextAndSkipsZeroArea()
    {
        var backend = new RecordingBackend(); var device = new GraphicsDevice(backend, 360, 640); device.Resize(360, 640);
        var batch = new SpriteBatch(device); var font = SpriteFont.CreateDefault(device);
        var b = new TouchButton(Area); var input = new InputState(); var style = TouchButtonStyle.Default;
        void Draw(uint background, uint foreground)
        {
            backend.Batches.Clear(); batch.Begin(); b.Draw(batch, font, "X", style, 2); batch.End();
            var bg = backend.Batches[0].Vertices; Assert.Equal(Area.Position, bg[0].Position); Assert.Equal(background, bg[0].Color);
            var glyph = backend.Batches[^1].Vertices; Assert.Equal(Area.Center - font.Measure("X", 2) / 2, glyph[0].Position); Assert.Equal(foreground, glyph[0].Color);
        }
        Draw(style.Background.PackedRgba, style.Foreground.PackedRgba);
        Step(b, input, T(1, TouchPhase.Pressed)); Draw(style.PressedBackground.PackedRgba, style.Foreground.PackedRgba);
        b.IsEnabled = false; Draw(style.DisabledBackground.PackedRgba, style.DisabledForeground.PackedRgba);
        b.Bounds = default; backend.Batches.Clear(); batch.Begin(); b.Draw(batch, font, "X", style); batch.End(); Assert.Empty(backend.Batches);
    }

    [Fact]
    public void UpdateAndDrawingAllocateZeroBytesAfterWarmup()
    {
        var device = new GraphicsDevice(new NoOpBackend(), 360, 640); device.Resize(360, 640);
        var batch = new SpriteBatch(device); var font = SpriteFont.CreateDefault(device);
        var b = new TouchButton(Area); var input = new InputState(); var style = TouchButtonStyle.Default;
        void Frame()
        {
            input.SetTouches([T(1, TouchPhase.Pressed)]); b.Update(input);
            batch.Begin(); b.Draw(batch, font, "Somar", style, 2); batch.End();
            input.SetTouches([T(1, TouchPhase.Released)]); b.Update(input);
            batch.Begin(); b.Draw(batch, font, "Somar", style, 2); batch.End();
            input.SetTouches([]); b.Update(input);
        }
        for (int i = 0; i < 100; i++) Frame(); long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 100; i++) Frame(); Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [Fact]
    public void OfflineGuideCompilesRunsAtPhysical2xAndCancelsOnPause()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "docs", "guides", "botoes-ui.md"))) root = root.Parent;
        Assert.NotNull(root);
        var source = File.ReadAllText(Path.Combine(root!.FullName, "docs", "guides", "botoes-ui.md")).Split("```csharp\n")[1].Split("```")[0];
        var compiler = new Lunet.Compiler.GameCompiler(new Lunet.Compiler.LoadedAssembliesReferenceProvider(typeof(Game).Assembly));
        var compiled = compiler.Compile("ButtonGuide", [new Lunet.Compiler.SourceFile("Game.cs", source)]);
        Assert.True(compiled.Success, string.Join("\n", compiled.Diagnostics)); Assert.DoesNotContain(compiled.Diagnostics, d => d.Severity == Lunet.Compiler.DiagnosticSeverity.Warning);
        using var loaded = Lunet.Runtime.GameLoader.Load(compiled.Assembly!, compiled.Symbols);
        var backend = new RecordingBackend(); var host = new GameHost(loaded.Game, backend); Assert.True(host.Start(720, 1280)); host.Tick(1.0 / 60);
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        object Field(string name) => loaded.Game.GetType().GetField(name, flags)!.GetValue(loaded.Game)!;
        void Touch(TouchPhase phase, float x, float y) { host.SetSurfaceTouches([new TouchPoint(0, phase, new(x * 2, y * 2))]); host.Tick(1.0 / 60); }
        void Click(float y) { Touch(TouchPhase.Pressed, 100, y); Touch(TouchPhase.Released, 100, y); host.SetSurfaceTouches([]); host.Tick(1.0 / 60); }
        Touch(TouchPhase.Pressed, 100, 190); Assert.Equal(0, Field("clicks")); Assert.True(((TouchButton)Field("action")).IsPressed);
        Touch(TouchPhase.Released, 100, 190); Assert.Equal(1, Field("clicks"));
        Touch(TouchPhase.Pressed, 100, 190); Touch(TouchPhase.Moved, 100, 240); Touch(TouchPhase.Released, 100, 240); Assert.Equal(1, Field("clicks"));
        Click(290); Assert.False(((TouchButton)Field("action")).IsEnabled); Click(190); Assert.Equal(1, Field("clicks"));
        Click(290); Click(390); Assert.True((bool)Field("warm")); Click(190); Assert.Equal(2, Field("clicks"));
        Touch(TouchPhase.Pressed, 100, 190); host.Pause(); Assert.False(((TouchButton)Field("action")).IsCaptured);
        host.Resume(); Touch(TouchPhase.Released, 100, 190); Assert.Equal(2, Field("clicks")); Click(190); Assert.Equal(3, Field("clicks"));
        Assert.Contains(backend.Batches, b => b.Vertices.Any(v => v.Position == new Vector2(24, 160))); Assert.False(host.IsFaulted); host.Stop();
    }
}
