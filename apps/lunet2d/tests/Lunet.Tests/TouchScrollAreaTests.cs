using System.Numerics;
using Lunet.Input;
using Lunet.UI;

namespace Lunet.Tests;

[Collection("Frame allocation")]
public sealed class TouchScrollAreaTests
{
    private static TouchPoint T(int id, TouchPhase phase, float x = 80, float y = 180)
        => new(id, phase, new Vector2(x, y));
    private static void Frame(TouchScrollArea area, InputState input, float dt = 1f / 60, params TouchPoint[] points)
    {
        input.SetTouches(points);
        area.Update(input, dt);
    }

    [Fact]
    public void PressAndTapDoNotScrollAndDraggingOnlyStartsAfterThreshold()
    {
        var area = new TouchScrollArea(new(20, 100, 200, 240), 800);
        var input = new InputState();
        Frame(area, input, points: [T(7, TouchPhase.Pressed)]);
        Assert.True(area.IsCaptured);
        Assert.False(area.IsDragging);
        Frame(area, input, points: [T(7, TouchPhase.Moved, y: 175)]);
        Assert.Equal(0, area.OffsetY);
        Frame(area, input, points: [T(7, TouchPhase.Released, y: 175)]);
        Assert.False(area.WasDragged);
        Assert.Equal(0, area.OffsetY);
        Frame(area, input, points: [T(7, TouchPhase.Pressed)]);
        Frame(area, input, points: [T(7, TouchPhase.Moved, y: 160)]);
        Assert.True(area.IsDragging);
        Assert.Equal(20, area.OffsetY);
        Frame(area, input, points: [T(7, TouchPhase.Released, y: 150)]);
        Assert.True(area.WasDragged);
        Assert.Equal(30, area.OffsetY);
        Frame(area, input);
        Assert.False(area.WasDragged);
        Assert.True(area.OffsetY > 30); // inércia depois do Released.
    }

    [Fact]
    public void MultitouchKeepsOriginalFingerAndCancelDoesNotTransfer()
    {
        var area = new TouchScrollArea(new(0, 0, 200, 180), 900);
        var input = new InputState();
        Frame(area, input, points: [T(1, TouchPhase.Pressed, 50, 80), T(2, TouchPhase.Pressed, 100, 100)]);
        Assert.True(area.IsCaptured);
        Frame(area, input, points: [T(2, TouchPhase.Moved, 100, 25), T(1, TouchPhase.Moved, 50, 60)]);
        Assert.Equal(20, area.OffsetY);
        Frame(area, input, points: [T(2, TouchPhase.Moved, 100, 10), T(1, TouchPhase.Cancelled, 50, 60)]);
        Assert.False(area.IsCaptured);
        Assert.Equal(20, area.OffsetY);
        Frame(area, input, points: [T(2, TouchPhase.Moved, 100, 1)]);
        Assert.False(area.IsCaptured);
    }

    [Fact]
    public void ResizeAndContentChangesClampAndProgrammaticScrollIsFinite()
    {
        var area = new TouchScrollArea(new(10, 10, 120, 150), 1000);
        Assert.Equal(850, area.MaximumOffsetY);
        area.ScrollTo(9000);
        Assert.Equal(850, area.OffsetY);
        area.ContentHeight = 250;
        Assert.Equal(100, area.OffsetY);
        area.Bounds = new(10, 10, 120, 300);
        Assert.Equal(0, area.OffsetY);
        Assert.Equal(0, area.MaximumOffsetY);
        Assert.Throws<ArgumentOutOfRangeException>(() => area.ContentHeight = float.NaN);
        Assert.Throws<ArgumentOutOfRangeException>(() => area.ScrollTo(float.PositiveInfinity));
        Assert.Throws<ArgumentOutOfRangeException>(() => area.Bounds = new(float.NaN, 0, 10, 20));
        Assert.Equal(0, area.OffsetY);
    }

    [Fact]
    public void BoundaryOverscrollStopsMomentumWithoutOvershooting()
    {
        var area = new TouchScrollArea(new(20, 100, 200, 200), 500);
        var input = new InputState();
        Frame(area, input, points: [T(1, TouchPhase.Pressed, y: 180)]);
        Frame(area, input, points: [T(1, TouchPhase.Moved, y: -1000)]);
        Assert.Equal(300, area.OffsetY);
        Frame(area, input, points: [T(1, TouchPhase.Released, y: -1000)]);
        for (int i = 0; i < 60; i++) Frame(area, input);
        Assert.Equal(300, area.OffsetY);
        area.ScrollTo(0);
        Assert.Equal(0, area.OffsetY);
    }

    [Fact]
    public void DisableAndRetainedPressedDoNotRecapture()
    {
        var area = new TouchScrollArea(new(0, 0, 200, 150), 800);
        var input = new InputState();
        Frame(area, input, points: [T(1, TouchPhase.Pressed, y: 90)]);
        area.IsEnabled = false;
        Assert.False(area.IsCaptured);
        area.IsEnabled = true;
        area.Update(input, 1f / 60);
        Assert.False(area.IsCaptured);
        Frame(area, input, points: [T(1, TouchPhase.Moved, y: 90)]);
        Assert.False(area.IsCaptured);
        Frame(area, input);
        Frame(area, input, points: [T(2, TouchPhase.Pressed, y: 90)]);
        Assert.True(area.IsCaptured);
        area.Cancel();
        area.Update(input, 1f / 60);
        Assert.False(area.IsCaptured);
        Assert.Throws<ArgumentOutOfRangeException>(() => area.Update(input, float.NaN));
        Assert.Throws<ArgumentNullException>(() => area.Update(null!, 1f / 60));
    }

    [Fact]
    public void InertiaDependsOnRealDeltaNotFixedSixtyHertz()
    {
        float Final(float dt)
        {
            var area = new TouchScrollArea(new(0, 0, 100, 100), 1000);
            var input = new InputState();
            Frame(area, input, dt, T(1, TouchPhase.Pressed, y: 95));
            float releaseY = 95 - 1200 * dt; // mesma velocidade física nos dois refresh rates.
            Frame(area, input, dt, T(1, TouchPhase.Moved, y: releaseY));
            Frame(area, input, dt, T(1, TouchPhase.Released, y: releaseY));
            for (int i = 0; i < (int)(1f / dt); i++) Frame(area, input, dt);
            return area.OffsetY;
        }
        float a = Final(1f / 60), b = Final(1f / 120);
        Assert.InRange(a, 70, 110);
        Assert.InRange(b, 70, 110);
        Assert.InRange(Math.Abs(a - b), 0, 15); // diferença somente do primeiro passo de arraste.
        Assert.True(float.IsFinite(a) && float.IsFinite(b));
    }

    [Fact]
    public void InertiaSurvivesReleaseAtSamePositionButNotLongStationaryHold()
    {
        var area = new TouchScrollArea(new(0, 0, 200, 120), 1200);
        var input = new InputState();
        Frame(area, input, points: [T(1, TouchPhase.Pressed, y: 110)]);
        Frame(area, input, points: [T(1, TouchPhase.Moved, y: 80)]);
        Frame(area, input, points: [T(1, TouchPhase.Released, y: 80)]);
        Assert.Equal(30, area.OffsetY);
        Frame(area, input);
        Assert.True(area.OffsetY > 30); // evento Up reaproveita a velocidade da última amostra útil.

        area.ScrollTo(0);
        Frame(area, input, points: [T(2, TouchPhase.Pressed, y: 110)]);
        Frame(area, input, points: [T(2, TouchPhase.Moved, y: 80)]);
        for (int i = 0; i < 80; i++)
            Frame(area, input, points: [T(2, TouchPhase.Moved, y: 80)]);
        Frame(area, input, points: [T(2, TouchPhase.Released, y: 80)]);
        float stopped = area.OffsetY;
        Frame(area, input);
        Assert.InRange(area.OffsetY - stopped, 0, 0.01f); // manter dedo parado não cria fling.
    }

    [Fact]
    public void UpdateAndScrollingDoNotAllocateAfterWarmup()
    {
        var area = new TouchScrollArea(new(0, 0, 200, 120), 800);
        var input = new InputState();
        void Run()
        {
            input.SetTouches([T(2, TouchPhase.Pressed, y: 100)]); area.Update(input, 1f / 60);
            input.SetTouches([T(2, TouchPhase.Moved, y: 30)]); area.Update(input, 1f / 60);
            input.SetTouches([T(2, TouchPhase.Released, y: 20)]); area.Update(input, 1f / 60);
            input.SetTouches([]); area.Update(input, 1f / 60);
            area.ScrollTo(0);
        }
        for (int i = 0; i < 80; i++) Run();
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 100; i++) Run();
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }
}
