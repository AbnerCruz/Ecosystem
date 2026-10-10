using System.Numerics;
using Lunet.Input;
using Lunet.UI;

namespace Lunet.Tests;

[Collection("Frame allocation")]
public sealed class UiRelayoutGestureTests
{
    private static TouchPoint T(int id, TouchPhase phase, float x, float y) =>
        new(id, phase, new Vector2(x, y));
    private static void Frame(InputState input, params TouchPoint[] touches) => input.SetTouches(touches);

    [Fact]
    public void ButtonDoesNotClickFromGestureStartedInPreviousLayout()
    {
        var b = new TouchButton(new(0, 0, 100, 48));
        var input = new InputState();
        Frame(input, T(1, TouchPhase.Pressed, 20, 20)); b.Update(input);
        b.Bounds = new(0, 0, 100, 48);
        Assert.True(b.IsCaptured);
        b.Bounds = new(10, 10, 100, 48);
        Assert.False(b.IsCaptured);
        Frame(input, T(1, TouchPhase.Released, 30, 30)); b.Update(input);
        Assert.False(b.WasClicked);
        Frame(input, T(2, TouchPhase.Pressed, 30, 30)); b.Update(input);
        Frame(input, T(2, TouchPhase.Released, 30, 30)); b.Update(input);
        Assert.True(b.WasClicked);
    }

    [Fact]
    public void SliderKeepsItsDocumentedCaptureAcrossRelayout()
    {
        var slider = new TouchSlider(new(0, 0, 200, 48), 0, 100, 25);
        var input = new InputState();
        Frame(input, T(1, TouchPhase.Pressed, 45, 20)); slider.Update(input);
        slider.Bounds = new(0, 0, 200, 48);
        Assert.True(slider.IsCaptured);
        slider.Bounds = new(100, 0, 200, 48);
        Assert.True(slider.IsCaptured);
        Frame(input, T(1, TouchPhase.Moved, 270, 20)); slider.Update(input);
        Assert.InRange(slider.Value, 80, 100);
        Frame(input, T(1, TouchPhase.Released, 270, 20)); slider.Update(input);
        Assert.False(slider.IsCaptured);
    }

    [Fact]
    public void ScrollBoundsChangeStopsMomentumAndPreservesClampedOffset()
    {
        var scroll = new TouchScrollArea(new(0, 0, 200, 100), 1000);
        var input = new InputState();
        Frame(input, T(1, TouchPhase.Pressed, 30, 80)); scroll.Update(input, 1f / 60);
        Frame(input, T(1, TouchPhase.Moved, 30, 30)); scroll.Update(input, 1f / 60);
        Assert.True(scroll.IsDragging);
        float old = scroll.OffsetY;
        Assert.True(old > 0);
        scroll.Bounds = new(0, 0, 200, 100);
        Assert.True(scroll.IsDragging);
        scroll.Bounds = new(0, 0, 200, 130);
        Assert.False(scroll.IsCaptured);
        Assert.False(scroll.IsDragging);
        Assert.Equal(old, scroll.OffsetY);
        Frame(input, T(1, TouchPhase.Released, 30, 10)); scroll.Update(input, 1f / 60);
        Frame(input); scroll.Update(input, 1f / 60);
        Assert.Equal(old, scroll.OffsetY);
    }

    [Fact]
    public void ListDoesNotLoseTapWhenSameBoundsAssignedEveryFrame()
    {
        var list = new TouchListView(new(0, 0, 200, 130), 10, 40);
        var input = new InputState();
        Frame(input, T(4, TouchPhase.Pressed, 40, 30)); list.Update(input, 1f / 60);
        list.Bounds = new(0, 0, 200, 130);
        Assert.True(list.IsCaptured);
        Frame(input, T(4, TouchPhase.Released, 40, 30)); list.Update(input, 1f / 60);
        Assert.Equal(0, list.ActivatedIndex);
        Frame(input, T(5, TouchPhase.Pressed, 40, 72)); list.Update(input, 1f / 60);
        list.Bounds = new(10, 10, 200, 130);
        Assert.False(list.IsCaptured);
        Frame(input, T(5, TouchPhase.Released, 40, 72)); list.Update(input, 1f / 60);
        Assert.Equal(-1, list.ActivatedIndex);
        Assert.Equal(0, list.SelectedIndex);
    }

    [Fact]
    public void InvalidBoundsCannotMutateControls()
    {
        var original = new RectangleF(0, 0, 100, 40);
        var b = new TouchButton(original); var slider = new TouchSlider(original);
        var scroll = new TouchScrollArea(original, 300); var list = new TouchListView(original, 10, 40);
        var bad = new RectangleF(float.NaN, 0, 100, 40);
        Assert.Throws<ArgumentOutOfRangeException>(() => b.Bounds = bad);
        Assert.Throws<ArgumentOutOfRangeException>(() => slider.Bounds = bad);
        Assert.Throws<ArgumentOutOfRangeException>(() => scroll.Bounds = bad);
        Assert.Throws<ArgumentOutOfRangeException>(() => list.Bounds = bad);
        Assert.Equal(original, b.Bounds); Assert.Equal(original, slider.Bounds);
        Assert.Equal(original, scroll.Bounds); Assert.Equal(original, list.Bounds);
    }

    [Fact]
    public void StableRelayoutHasNoAllocationsOrLostCaptures()
    {
        var r = new RectangleF(0, 0, 200, 100);
        var b = new TouchButton(r); var slider = new TouchSlider(r);
        var scroll = new TouchScrollArea(r, 1000); var list = new TouchListView(r, 8, 40);
        var input = new InputState();
        Frame(input, T(1, TouchPhase.Pressed, 50, 50));
        b.Update(input); slider.Update(input); scroll.Update(input, 1f / 60); list.Update(input, 1f / 60);
        void Refresh() { b.Bounds = r; slider.Bounds = r; scroll.Bounds = r; list.Bounds = r; }
        for (int i = 0; i < 100; i++) Refresh();
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) Refresh();
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.True(b.IsCaptured); Assert.True(slider.IsCaptured);
        Assert.True(scroll.IsCaptured); Assert.True(list.IsCaptured);
    }
}
