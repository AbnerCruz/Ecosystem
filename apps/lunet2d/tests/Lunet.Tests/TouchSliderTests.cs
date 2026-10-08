using System.Numerics;
using Lunet.Graphics;
using Lunet.Input;
using Lunet.UI;

namespace Lunet.Tests;

[Collection("Frame allocation")]
public class TouchSliderTests
{
    private static readonly RectangleF Area = new(20, 100, 200, 40);
    private static TouchPoint T(int id, TouchPhase phase, float x = 120, float y = 120) => new(id, phase, new(x, y));
    private static void Frame(TouchSlider s, InputState input, params TouchPoint[] touches) { input.SetTouches(touches); s.Update(input); }

    [Fact]
    public void ContinuousValuesFollowTravelAndReleaseAppliesFinalPosition()
    {
        var s = new TouchSlider(Area, 0, 100, 25); var input = new InputState();
        Frame(s, input, T(1, TouchPhase.Pressed)); Assert.Equal(50, s.Value); Assert.True(s.IsCaptured); Assert.True(s.WasChanged);
        s.Update(input); Assert.False(s.WasChanged);
        Frame(s, input, T(1, TouchPhase.Moved, -500, 500)); Assert.Equal(0, s.Value); Assert.True(s.IsCaptured);
        Frame(s, input, T(1, TouchPhase.Moved, 500)); Assert.Equal(100, s.Value);
        Frame(s, input, T(1, TouchPhase.Released, 75)); Assert.Equal(25, s.Value); Assert.False(s.IsCaptured); Assert.True(s.WasChanged);
        s.Update(input); Assert.False(s.WasChanged); Assert.Equal(25, s.Value);
    }

    [Theory]
    [InlineData(-5, -5)]
    [InlineData(-3, -1)]
    [InlineData(1, 3)]
    [InlineData(6, 7)]
    public void StepsAreRelativeToMinimumTiesRoundUpAndMaximumRemainsReachable(float value, float expected)
    {
        var s = new TouchSlider(Area, -5, 7, value, 4); Assert.Equal(expected, s.Value);
        s.Value = -100; Assert.Equal(-5, s.Value); s.Value = 100; Assert.Equal(7, s.Value);
        Assert.False(s.WasChanged);
    }

    [Fact]
    public void NonDividingAndOversizedStepsStillReachBothEndpoints()
    {
        var input = new InputState();
        foreach (var (maximum, step) in new[] { (95f, 10f), (25f, 100f) })
        {
            var s = new TouchSlider(Area, 0, maximum, step: step);
            Frame(s, input, T(1, TouchPhase.Pressed, 210)); Assert.Equal(maximum, s.Value);
            Frame(s, input, T(1, TouchPhase.Released, 30)); Assert.Equal(0, s.Value);
            s.Value = maximum; Assert.Equal(maximum, s.Value);
        }
    }

    [Fact]
    public void QuantizedDragSignalsOnlyEffectiveChanges()
    {
        var s = new TouchSlider(Area, 0, 100, 50, 10); var input = new InputState();
        Frame(s, input, T(1, TouchPhase.Pressed, 121)); Assert.Equal(50, s.Value); Assert.False(s.WasChanged);
        Frame(s, input, T(1, TouchPhase.Moved, 122)); Assert.False(s.WasChanged);
        Frame(s, input, T(1, TouchPhase.Moved, 138)); Assert.Equal(60, s.Value); Assert.True(s.WasChanged);
        Frame(s, input, T(1, TouchPhase.Released, 138)); Assert.False(s.WasChanged);
    }

    [Fact]
    public void CaptureFollowsIdAndCannotStartByEnteringOrRetainedPressed()
    {
        var s = new TouchSlider(Area, 0, 100); var input = new InputState();
        Frame(s, input, T(1, TouchPhase.Pressed, 400)); Frame(s, input, T(1, TouchPhase.Moved)); Assert.False(s.IsCaptured);
        Frame(s, input, T(1, TouchPhase.Pressed)); Assert.False(s.IsCaptured);
        Frame(s, input); Frame(s, input, T(-8, TouchPhase.Pressed));
        Frame(s, input, T(2, TouchPhase.Released, 30), T(-8, TouchPhase.Moved, 165)); Assert.Equal(75, s.Value); Assert.True(s.IsCaptured);
        Frame(s, input, T(2, TouchPhase.Pressed, 30), T(-8, TouchPhase.Released, 210)); Assert.Equal(100, s.Value); Assert.False(s.IsCaptured);
        s.Update(input); Assert.False(s.IsCaptured);
    }

    [Fact]
    public void CancelLossInvalidAndDisableKeepLastValueWithoutRearming()
    {
        var s = new TouchSlider(Area, 0, 100); var input = new InputState();
        Frame(s, input, T(1, TouchPhase.Pressed)); Frame(s, input, T(1, TouchPhase.Cancelled, 30)); Assert.Equal(50, s.Value); Assert.False(s.IsCaptured); Assert.False(s.WasChanged);
        Frame(s, input, T(1, TouchPhase.Pressed)); Frame(s, input, T(1, TouchPhase.Moved, float.NaN)); Assert.Equal(50, s.Value); Assert.False(s.IsCaptured);
        Frame(s, input); Frame(s, input, T(1, TouchPhase.Pressed)); s.Cancel(); s.Update(input); Assert.False(s.IsCaptured);
        Frame(s, input); Frame(s, input, T(1, TouchPhase.Pressed)); s.IsEnabled = false; Assert.False(s.IsCaptured);
        s.IsEnabled = true; s.Update(input); Assert.False(s.IsCaptured);
        Frame(s, input, T(1, TouchPhase.Released, 30)); Assert.Equal(50, s.Value); Assert.False(s.WasChanged);
        Frame(s, input, T(1, TouchPhase.Pressed)); Frame(s, input, T(2, TouchPhase.Pressed, 30)); Assert.False(s.IsCaptured); Assert.Equal(50, s.Value);
        Frame(s, input); s.IsEnabled = false; Frame(s, input, T(3, TouchPhase.Pressed)); s.IsEnabled = true; s.Update(input); Assert.False(s.IsCaptured);
    }

    [Fact]
    public void NewBoundsApplyOnNextUpdateAndNoTravelCancels()
    {
        var s = new TouchSlider(Area, 0, 100, 50); var input = new InputState();
        Frame(s, input, T(1, TouchPhase.Pressed)); s.Bounds = new(100, 100, 200, 40);
        Frame(s, input, T(1, TouchPhase.Moved, 120)); Assert.Equal(100f / 18, s.Value, 3);
        s.Bounds = new(100, 100, 20, 40); s.Update(input); Assert.False(s.IsCaptured);
        s.Bounds = default; Frame(s, input, T(4, TouchPhase.Pressed)); Assert.False(s.IsCaptured);
        s.Bounds = new(20, 100, 200, 0); Frame(s, input, T(5, TouchPhase.Pressed)); Assert.False(s.IsCaptured);
    }

    [Fact]
    public void ExtremeRangesSmallStepsAndLargeCoordinatesStayFinite()
    {
        var s = new TouchSlider(Area, -float.MaxValue, float.MaxValue, 0, float.Epsilon); Assert.Equal(0.5f, s.NormalizedValue);
        var input = new InputState(); Frame(s, input, T(1, TouchPhase.Pressed, 210)); Assert.Equal(float.MaxValue, s.Value);
        Frame(s, input, T(1, TouchPhase.Released, 30)); Assert.Equal(-float.MaxValue, s.Value);
        Assert.True(float.IsFinite(s.GetKnobBounds().Right));
        s.Bounds = new(-float.MaxValue, 0, float.MaxValue, 1); s.Value = 0; Assert.True(float.IsFinite(s.GetKnobBounds().X));
    }

    [Fact]
    public void InvalidArgumentsFailAndDoNotCorruptCurrentBoundsOrValue()
    {
        foreach (float bad in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new TouchSlider(Area, minimum: bad));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TouchSlider(Area, maximum: bad));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TouchSlider(Area, value: bad));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TouchSlider(Area, step: bad));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TouchSlider(Area, knobWidth: bad));
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => new TouchSlider(Area, 1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TouchSlider(Area, step: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TouchSlider(Area, knobWidth: 0));
        var s = new TouchSlider(Area, value: 0.5f);
        foreach (var bad in new[] { new RectangleF(0, 0, -1, 1), new RectangleF(float.NaN, 0, 1, 1), new RectangleF(float.MaxValue, 0, float.MaxValue, 1) })
        { Assert.Throws<ArgumentOutOfRangeException>(() => s.Bounds = bad); Assert.Equal(Area, s.Bounds); }
        Assert.Throws<ArgumentOutOfRangeException>(() => s.Value = float.NaN); Assert.Equal(0.5f, s.Value);
        Assert.Throws<ArgumentNullException>(() => s.Update(null!)); Assert.Throws<ArgumentNullException>(() => s.Draw(null!, TouchSliderStyle.Default));
    }

    [Fact]
    public void DrawingUsesValueAndCaptureDisabledColorsAndSkipsZeroArea()
    {
        var backend = new RecordingBackend(); var device = new GraphicsDevice(backend, 360, 640); var batch = new SpriteBatch(device);
        var s = new TouchSlider(Area, 0, 100, 50); var input = new InputState(); var style = TouchSliderStyle.Default;
        void Draw(uint knobColor)
        {
            backend.Batches.Clear(); batch.Begin(); s.Draw(batch, style); batch.End();
            var vertices = backend.Batches.SelectMany(b => b.Vertices).ToArray();
            Assert.Contains(vertices, v => v.Position == new Vector2(110, 100) && v.Color == knobColor);
            Assert.Contains(vertices, v => v.Position == new Vector2(120, 116));
        }
        Assert.Equal(new RectangleF(110, 100, 20, 40), s.GetKnobBounds()); Draw(style.Knob.PackedRgba);
        Frame(s, input, T(1, TouchPhase.Pressed)); Draw(style.PressedKnob.PackedRgba);
        s.IsEnabled = false; Draw(style.DisabledKnob.PackedRgba);
        s.Bounds = default; backend.Batches.Clear(); batch.Begin(); s.Draw(batch, style); batch.End(); Assert.Empty(backend.Batches);
    }

    [Fact]
    public void UpdateAndDrawDoNotAllocateAfterWarmup()
    {
        var device = new GraphicsDevice(new NoOpBackend(), 360, 640); var batch = new SpriteBatch(device);
        var s = new TouchSlider(Area, 0, 100); var input = new InputState();
        void Frame()
        {
            input.SetTouches([T(1, TouchPhase.Pressed)]); s.Update(input); batch.Begin(); s.Draw(batch, TouchSliderStyle.Default); batch.End();
            input.SetTouches([T(1, TouchPhase.Released, 210)]); s.Update(input); batch.Begin(); s.Draw(batch, TouchSliderStyle.Default); batch.End();
            input.SetTouches([]); s.Update(input);
        }
        for (int i = 0; i < 100; i++) Frame(); long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 100; i++) Frame(); Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [Fact]
    public void LaboratoryRunsSliderPageAtPhysical2xAndPreservesExistingProject()
    {
        var root = Path.Combine(Path.GetTempPath(), "lunet-slider-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new Lunet.Core.ProjectStore(root); var old = store.Create("Existing"); old.WriteText("Game.cs", "// preserved project\n");
            var oldCode = File.ReadAllBytes(Path.Combine(old.Directory, "Game.cs")); var oldManifest = File.ReadAllBytes(Path.Combine(old.Directory, "lunet.json"));
            var project = store.Create("LabSlider", Lunet.Core.ProjectTemplate.Lab);
            var compiler = new Lunet.Compiler.GameCompiler(new Lunet.Compiler.LoadedAssembliesReferenceProvider(typeof(Game).Assembly));
            var result = compiler.Compile("SliderLab", project.LoadSources().Select(s => new Lunet.Compiler.SourceFile(s.Path, s.Text)).ToList());
            Assert.True(result.Success, string.Join("\n", result.Diagnostics)); Assert.DoesNotContain(result.Diagnostics, d => d.Severity == Lunet.Compiler.DiagnosticSeverity.Warning);
            using var loaded = Lunet.Runtime.GameLoader.Load(result.Assembly!, result.Symbols);
            var backend = new RecordingBackend(); var host = new GameHost(loaded.Game, backend, new Lunet.Content.DirectoryContentSource(Path.Combine(project.Directory, "Content")));
            Assert.True(host.Start(720, 1280), host.Fault?.ToString()); host.Tick(1.0 / 60);
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            object Field(string name) => loaded.Game.GetType().GetField(name, flags)!.GetValue(loaded.Game)!;
            void Touch(TouchPhase phase, float x, float y) { host.SetSurfaceTouches([new TouchPoint(1, phase, new(x * 2, y * 2))]); host.Tick(1.0 / 60); }
            void Tap(float x, float y) { Touch(TouchPhase.Pressed, x, y); Touch(TouchPhase.Released, x, y); host.SetSurfaceTouches([]); host.Tick(1.0 / 60); host.Tick(1.0 / 60); }
            Tap(100, 23); Tap(100, 23); Tap(100, 23); Assert.Equal(3, Field("page"));
            var radius = (TouchSlider)Field("radius"); var level = (TouchSlider)Field("level");
            Touch(TouchPhase.Pressed, 324, 190); Assert.Equal(72, radius.Value); Assert.True(radius.IsCaptured);
            Touch(TouchPhase.Moved, 36, 190); Assert.Equal(16, radius.Value);
            host.Pause(); Assert.False(radius.IsCaptured); host.Resume(); Touch(TouchPhase.Released, 324, 190); Assert.Equal(16, radius.Value);
            Tap(180, 290); Assert.Equal(50, level.Value); Tap(180, 372); Assert.False(level.IsEnabled);
            Tap(324, 290); Assert.Equal(50, level.Value); Tap(180, 372); Assert.True(level.IsEnabled);
            Tap(324, 290); Assert.Equal(100, level.Value);
            host.SetSurfaceTouches([T(1, TouchPhase.Pressed, 120 * 2, 190 * 2), T(2, TouchPhase.Pressed, 36 * 2, 290 * 2)]); host.Tick(1.0 / 60);
            Assert.True(radius.IsCaptured); Assert.True(level.IsCaptured); Assert.Equal(0, level.Value);
            host.SetSurfaceTouches([T(1, TouchPhase.Released, 120 * 2, 190 * 2), T(2, TouchPhase.Released, 36 * 2, 290 * 2)]); host.Tick(1.0 / 60);
            Tap(100, 23); Assert.Equal(4, Field("page")); Assert.False(radius.IsCaptured);
            Tap(100, 23); Assert.Equal(0, Field("page"));
            Assert.False(host.IsFaulted, host.Fault?.ToString()); host.Stop(); Assert.Empty(backend.LiveTargets); Assert.Empty(backend.LiveShaders);
            Assert.Equal(oldCode, File.ReadAllBytes(Path.Combine(old.Directory, "Game.cs"))); Assert.Equal(oldManifest, File.ReadAllBytes(Path.Combine(old.Directory, "lunet.json")));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
