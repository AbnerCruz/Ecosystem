using System.Numerics;
using Lunet.Graphics;
using Lunet.Input;
using Lunet.UI;

namespace Lunet.Tests;

[Collection("Frame allocation")]
public sealed class TouchRadialMenuTests
{
    private static TouchPoint T(int id, TouchPhase phase, float x, float y) => new(id, phase, new(x, y));
    private static void Step(TouchRadialMenu menu, InputState input, params TouchPoint[] touches)
    {
        input.SetTouches(touches);
        menu.Update(input);
    }

    [Fact]
    public void ClockwiseSectorsAndDeadZoneHavePredictableHitTesting()
    {
        var menu = new TouchRadialMenu(new(200, 200), 30, 100, 4);
        Assert.Equal(0, menu.HitTest(new(200, 130)));
        Assert.Equal(1, menu.HitTest(new(270, 200)));
        Assert.Equal(2, menu.HitTest(new(200, 270)));
        Assert.Equal(3, menu.HitTest(new(130, 200)));
        Assert.Equal(-1, menu.HitTest(new(200, 200)));
        Assert.Equal(-1, menu.HitTest(new(200, 180)));
        Assert.Equal(-1, menu.HitTest(new(400, 200)));
        Assert.Equal(-1, menu.HitTest(new(float.NaN, 200)));
        Assert.InRange(Vector2.Distance(menu.GetItemPosition(0), new Vector2(200, 135)), 0, 0.001f);
        Assert.Throws<ArgumentOutOfRangeException>(() => menu.GetItemPosition(4));
    }

    [Fact]
    public void DragFromCenterActivatesOnlyUponReleaseInRing()
    {
        var menu = new TouchRadialMenu(new(200, 200), 30, 100, 4);
        var input = new InputState();
        Step(menu, input, T(6, TouchPhase.Pressed, 200, 200));
        Assert.True(menu.IsCaptured);
        Assert.Equal(-1, menu.SelectedIndex);
        Step(menu, input, T(6, TouchPhase.Moved, 200, 130));
        Assert.Equal(0, menu.SelectedIndex);
        Assert.Equal(-1, menu.ActivatedIndex);
        Step(menu, input, T(6, TouchPhase.Released, 200, 130));
        Assert.Equal(0, menu.ActivatedIndex);
        Assert.False(menu.IsCaptured);
        menu.Update(input);
        Assert.Equal(-1, menu.ActivatedIndex);
        Step(menu, input, T(6, TouchPhase.Pressed, 200, 200));
        Step(menu, input, T(6, TouchPhase.Released, 200, 200));
        Assert.Equal(-1, menu.ActivatedIndex);
        Step(menu, input, T(6, TouchPhase.Pressed, 200, 200));
        Step(menu, input, T(6, TouchPhase.Released, 200, 20));
        Assert.Equal(-1, menu.ActivatedIndex);
    }

    [Fact]
    public void MultitouchCannotStealSelectionAndCancellationDoesNotClick()
    {
        var menu = new TouchRadialMenu(new(200, 200), 30, 100, 4);
        var input = new InputState();
        Step(menu, input, T(-5, TouchPhase.Pressed, 200, 200), T(9, TouchPhase.Pressed, 270, 200));
        Step(menu, input, T(9, TouchPhase.Released, 270, 200), T(-5, TouchPhase.Moved, 200, 130));
        Assert.Equal(0, menu.SelectedIndex);
        Step(menu, input, T(-5, TouchPhase.Released, 200, 130), T(9, TouchPhase.Pressed, 270, 200));
        Assert.Equal(0, menu.ActivatedIndex);
        menu.Update(input);
        Assert.False(menu.IsCaptured);
        Step(menu, input, T(9, TouchPhase.Moved, 270, 200));
        Assert.False(menu.IsCaptured);
        Step(menu, input);
        Step(menu, input, T(9, TouchPhase.Pressed, 270, 200));
        Assert.True(menu.IsCaptured);
        Step(menu, input, T(9, TouchPhase.Cancelled, 270, 200));
        Assert.False(menu.IsCaptured);
        Assert.Equal(-1, menu.ActivatedIndex);
    }

    [Fact]
    public void RelayoutAndDisableCancelWithoutRecapturingHeldFinger()
    {
        var menu = new TouchRadialMenu(new(200, 200), 30, 100, 4);
        var input = new InputState();
        Step(menu, input, T(1, TouchPhase.Pressed, 200, 200));
        menu.SetGeometry(new(200, 200), 30, 100);
        Assert.True(menu.IsCaptured);
        menu.SetGeometry(new(240, 200), 30, 100);
        Assert.False(menu.IsCaptured);
        Step(menu, input, T(1, TouchPhase.Released, 240, 130));
        Assert.Equal(-1, menu.ActivatedIndex);
        Step(menu, input, T(2, TouchPhase.Pressed, 240, 200));
        menu.IsEnabled = false;
        menu.IsEnabled = true;
        menu.Update(input);
        Assert.False(menu.IsCaptured);
        Step(menu, input, T(2, TouchPhase.Released, 240, 130));
        Assert.Equal(-1, menu.ActivatedIndex);
        Step(menu, input, T(3, TouchPhase.Pressed, 240, 200));
        menu.ItemCount = 8;
        Assert.False(menu.IsCaptured);
        Assert.Throws<ArgumentOutOfRangeException>(() => menu.ItemCount = 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => menu.ItemCount = 17);
        Assert.Throws<ArgumentOutOfRangeException>(() => menu.SetGeometry(new(float.NaN, 0), 0, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => menu.SetGeometry(new(0, 0), 30, 30));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TouchRadialMenu(new(0, 0), 0, -1, 4));
        Assert.Equal(new Vector2(240, 200), menu.Center);
        Assert.Throws<ArgumentNullException>(() => menu.Update(null!));
    }

    [Fact]
    public void DrawShowsHighlightedSectorAndRejectsInvalidLabels()
    {
        var backend = new RecordingBackend();
        var device = new GraphicsDevice(backend, 360, 640);
        device.Resize(360, 640);
        var menu = new TouchRadialMenu(new(160, 240), 30, 100, 4);
        var batch = new SpriteBatch(device);
        var font = SpriteFont.CreateDefault(device);
        string[] labels = ["A", "B", "C", "D"];
        batch.Begin(); menu.Draw(batch, font, labels, Color.White, Color.Yellow, Color.White); batch.End();
        int idleQuads = backend.Batches.Sum(b => b.QuadCount);
        Assert.True(idleQuads > 20);
        var input = new InputState();
        Step(menu, input, T(1, TouchPhase.Pressed, 160, 240));
        Step(menu, input, T(1, TouchPhase.Moved, 160, 170));
        backend.Batches.Clear();
        batch.Begin(); menu.Draw(batch, font, labels, Color.White, Color.Yellow, Color.White); batch.End();
        Assert.True(backend.Batches.Sum(b => b.QuadCount) > idleQuads);
        Assert.Throws<ArgumentException>(() => menu.Draw(batch, font, ["A"], Color.White, Color.Yellow, Color.White));
        Assert.Throws<ArgumentOutOfRangeException>(() => menu.Draw(batch, font, labels, Color.White, Color.Yellow, Color.White, 0));
    }

    [Fact]
    public void HotPathAvoidsManagedAllocationsAfterWarmup()
    {
        var device = new GraphicsDevice(new NoOpBackend(), 360, 640);
        var menu = new TouchRadialMenu(new(160, 240), 30, 100, 4);
        var input = new InputState();
        var font = SpriteFont.CreateDefault(device);
        var batch = new SpriteBatch(device);
        string[] labels = ["A", "B", "C", "D"];
        void Frame()
        {
            input.SetTouches([T(1, TouchPhase.Pressed, 160, 240)]); menu.Update(input);
            input.SetTouches([T(1, TouchPhase.Moved, 160, 170)]); menu.Update(input);
            batch.Begin(); menu.Draw(batch, font, labels, Color.White, Color.Yellow, Color.White); batch.End();
            input.SetTouches([T(1, TouchPhase.Released, 160, 170)]); menu.Update(input);
            input.SetTouches([]); menu.Update(input);
            menu.HitTest(new(160, 170));
            menu.GetItemPosition(0);
        }
        for (int i = 0; i < 50; i++) Frame();
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 80; i++) Frame();
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }
}
