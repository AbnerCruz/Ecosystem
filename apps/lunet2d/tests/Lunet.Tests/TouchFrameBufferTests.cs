using System.Numerics;
using Lunet.Input;

namespace Lunet.Tests;

public class TouchFrameBufferTests
{
    private static TouchPoint Finger(int id, TouchPhase phase, float x, float y) =>
        new(id, phase, new Vector2(x, y));

    [Fact]
    public void PressedSurvivesMovementAndUnconsumedRenderFrames()
    {
        var buffer = new TouchFrameBuffer();
        Span<TouchPoint> frame = stackalloc TouchPoint[InputState.MaxTouches];
        buffer.Submit([Finger(4, TouchPhase.Pressed, 100, 100)]);
        buffer.Submit([Finger(4, TouchPhase.Moved, 160, 110)]);
        Assert.Equal(1, buffer.CopyFrame(frame));
        Assert.Equal(TouchPhase.Pressed, frame[0].Phase);
        Assert.Equal(new Vector2(100, 100), frame[0].Position);

        // Dois desenhos de 120 Hz sem Update não devem consumir Pressed.
        Assert.Equal(1, buffer.CopyFrame(frame));
        Assert.Equal(TouchPhase.Pressed, frame[0].Phase);
        buffer.AcknowledgeFrame();
        Assert.Equal(1, buffer.CopyFrame(frame));
        Assert.Equal(TouchPhase.Moved, frame[0].Phase);
        Assert.Equal(new Vector2(160, 110), frame[0].Position);
    }

    [Fact]
    public void QuickTapDeliversPressedThenReleasedAcrossTwoUpdates()
    {
        var buffer = new TouchFrameBuffer();
        Span<TouchPoint> frame = stackalloc TouchPoint[InputState.MaxTouches];
        buffer.Submit([Finger(1, TouchPhase.Pressed, 8, 12)]);
        buffer.Submit([Finger(1, TouchPhase.Released, 9, 14)]);
        Assert.Equal(1, buffer.CopyFrame(frame));
        Assert.Equal(TouchPhase.Pressed, frame[0].Phase);
        buffer.AcknowledgeFrame();
        Assert.Equal(1, buffer.CopyFrame(frame));
        Assert.Equal(TouchPhase.Released, frame[0].Phase);
        Assert.Equal(new Vector2(9, 14), frame[0].Position);
        buffer.AcknowledgeFrame();
        Assert.Equal(0, buffer.CopyFrame(frame));
    }

    [Fact]
    public void MultiTouchKeepsIndependentIdsAndCancellation()
    {
        var buffer = new TouchFrameBuffer();
        Span<TouchPoint> frame = stackalloc TouchPoint[InputState.MaxTouches];
        buffer.Submit([Finger(5, TouchPhase.Pressed, 10, 10)]);
        buffer.CopyFrame(frame);
        buffer.AcknowledgeFrame();
        buffer.Submit([Finger(5, TouchPhase.Moved, 20, 20), Finger(7, TouchPhase.Pressed, 80, 80)]);
        Assert.Equal(2, buffer.CopyFrame(frame));
        Assert.Equal(5, frame[0].Id);
        Assert.Equal(TouchPhase.Moved, frame[0].Phase);
        Assert.Equal(7, frame[1].Id);
        Assert.Equal(TouchPhase.Pressed, frame[1].Phase);
        buffer.AcknowledgeFrame();

        buffer.Submit([Finger(5, TouchPhase.Cancelled, 20, 20), Finger(7, TouchPhase.Moved, 85, 85)]);
        Assert.Equal(2, buffer.CopyFrame(frame));
        Assert.Equal(TouchPhase.Cancelled, frame[0].Phase);
        Assert.Equal(TouchPhase.Moved, frame[1].Phase);
        buffer.AcknowledgeFrame();
        Assert.Equal(1, buffer.CopyFrame(frame));
        Assert.Equal(7, frame[0].Id);
        buffer.Clear();
        Assert.Equal(0, buffer.CopyFrame(frame));
    }

    [Fact]
    public void MissingFingerSynthesizesCancelAndFrameIsRetainedUntilAcknowledged()
    {
        var buffer = new TouchFrameBuffer();
        Span<TouchPoint> frame = stackalloc TouchPoint[InputState.MaxTouches];
        buffer.Submit([Finger(9, TouchPhase.Pressed, 1, 1)]);
        buffer.CopyFrame(frame);
        buffer.AcknowledgeFrame();
        buffer.Submit(ReadOnlySpan<TouchPoint>.Empty);
        Assert.Equal(1, buffer.CopyFrame(frame));
        Assert.Equal(TouchPhase.Cancelled, frame[0].Phase);
        Assert.Equal(1, buffer.CopyFrame(frame));
        buffer.AcknowledgeFrame();
        Assert.Equal(0, buffer.CopyFrame(frame));
    }

    [Fact]
    public void NoManagedAllocationsAfterWarmup()
    {
        var buffer = new TouchFrameBuffer();
        Span<TouchPoint> frame = stackalloc TouchPoint[InputState.MaxTouches];
        Span<TouchPoint> input = stackalloc TouchPoint[1];
        for (var i = 0; i < 128; i++)
        {
            buffer.Clear();
            input[0] = Finger(3, TouchPhase.Pressed, 5, 5);
            buffer.Submit(input);
            buffer.CopyFrame(frame);
            buffer.AcknowledgeFrame();
        }
        GC.Collect();
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1024; i++)
        {
            buffer.Clear();
            input[0] = Finger(3, TouchPhase.Pressed, 5, 5);
            buffer.Submit(input);
            buffer.CopyFrame(frame);
            buffer.AcknowledgeFrame();
            input[0] = Finger(3, TouchPhase.Released, 5, 5);
            buffer.Submit(input);
            buffer.CopyFrame(frame);
            buffer.AcknowledgeFrame();
        }
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }
}
