using System.Numerics;
using Lunet.Input;

namespace Lunet.Tests;

public class VirtualControlPrecisionTests
{
    private static TouchPoint Finger(int id, TouchPhase phase, float x, float y) =>
        new(id, phase, new Vector2(x, y));

    private static InputState State(params TouchPoint[] touches)
    {
        var state = new InputState();
        state.SetTouches(touches);
        return state;
    }

    [Fact]
    public void Stick_DefaultSettingsPreserveLegacyOutputAndCapture()
    {
        var stick = new VirtualStick(new Vector2(100, 100), 100);
        Assert.Equal(0.15f, stick.DeadZone);
        Assert.Equal(1f, stick.ResponseExponent);
        Assert.Equal(1f, stick.Sensitivity);
        Assert.False(stick.RescaleDeadZone);
        Assert.False(stick.RequireFreshPress);

        // Moved que começou fora da área ainda é capturado no modo legado.
        stick.Update(State(Finger(2, TouchPhase.Moved, 150, 100)));
        Assert.Equal(2, stick.TouchId);
        Assert.Equal(0.5f, stick.Direction.X, 0.001f);
        // A borda exata da DeadZone mantém o comportamento da implementação anterior.
        stick.Update(State(Finger(2, TouchPhase.Moved, 115, 100)));
        Assert.Equal(0.15f, stick.Direction.X, 0.001f);
        stick.Update(State(Finger(2, TouchPhase.Moved, 400, 100)));
        Assert.Equal(1f, stick.Direction.X, 0.001f);
        stick.Update(State(Finger(2, TouchPhase.Released, 400, 100)));
        Assert.False(stick.IsActive);
        Assert.Equal(-1, stick.TouchId);
        Assert.Equal(Vector2.Zero, stick.Direction);
    }

    [Fact]
    public void RescaledDeadZoneIsContinuousAndNeverExceedsOne()
    {
        var stick = new VirtualStick(new Vector2(100, 100), 100)
        {
            DeadZone = 0.2f,
            RescaleDeadZone = true
        };
        stick.Update(State(Finger(3, TouchPhase.Pressed, 120, 100)));
        Assert.Equal(Vector2.Zero, stick.Direction);
        stick.Update(State(Finger(3, TouchPhase.Moved, 130, 100)));
        Assert.Equal(0.125f, stick.Direction.X, 0.001f);
        stick.Update(State(Finger(3, TouchPhase.Moved, 200, 100)));
        Assert.Equal(1f, stick.Direction.X, 0.001f);
        stick.Update(State(Finger(3, TouchPhase.Moved, 800, 100)));
        Assert.Equal(1f, stick.Direction.Length(), 0.001f);
        stick.DeadZone = 1f;
        stick.Update(State(Finger(3, TouchPhase.Moved, 800, 100)));
        Assert.Equal(Vector2.Zero, stick.Direction);
    }

    [Fact]
    public void ResponseCurveAndSensitivityAdjustAimWithoutChangingDirection()
    {
        var stick = new VirtualStick(new Vector2(100, 100), 100)
        {
            DeadZone = 0f,
            ResponseExponent = 2f,
            Sensitivity = 1.5f
        };
        stick.Update(State(Finger(1, TouchPhase.Pressed, 100, 100)));
        Assert.Equal(Vector2.Zero, stick.Direction); // zona morta zero não gera NaN
        stick.Update(State(Finger(1, TouchPhase.Moved, 150, 100)));
        Assert.Equal(0.375f, stick.Direction.X, 0.001f);
        Assert.Equal(0f, stick.Direction.Y);
        stick.Update(State(Finger(1, TouchPhase.Moved, 200, 100)));
        Assert.Equal(1f, stick.Direction.X, 0.001f);
        stick.Sensitivity = 0f;
        stick.Update(State(Finger(1, TouchPhase.Moved, 200, 100)));
        Assert.Equal(Vector2.Zero, stick.Direction);
    }

    [Fact]
    public void InvalidControlSettingsAreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new VirtualStick(Vector2.Zero, float.NaN));
        var stick = new VirtualStick(Vector2.Zero, 40);
        Assert.Throws<ArgumentOutOfRangeException>(() => stick.DeadZone = -0.1f);
        Assert.Throws<ArgumentOutOfRangeException>(() => stick.DeadZone = 1.1f);
        Assert.Throws<ArgumentOutOfRangeException>(() => stick.ResponseExponent = 0f);
        Assert.Throws<ArgumentOutOfRangeException>(() => stick.ResponseExponent = float.PositiveInfinity);
        Assert.Throws<ArgumentOutOfRangeException>(() => stick.Sensitivity = -1f);
        Assert.Throws<ArgumentOutOfRangeException>(() => stick.Sensitivity = float.NaN);
    }

    [Fact]
    public void TwoSticksCanReserveCapturedFingerAndKeepOtherFingerActive()
    {
        var area = new RectangleF(0, 0, 240, 240);
        var first = new VirtualStick(new Vector2(100, 100), 80, area);
        var second = new VirtualStick(new Vector2(140, 100), 80, area);
        var input = State(Finger(10, TouchPhase.Pressed, 100, 100));
        first.Update(input);
        second.Update(input, first.TouchId);
        Assert.True(first.IsActive);
        Assert.False(second.IsActive);

        input = State(Finger(10, TouchPhase.Moved, 100, 100),
                      Finger(11, TouchPhase.Pressed, 170, 100));
        first.Update(input);
        second.Update(input, first.TouchId);
        Assert.Equal(10, first.TouchId);
        Assert.Equal(11, second.TouchId);
        Assert.True(second.IsActive);

        input = State(Finger(10, TouchPhase.Cancelled, 100, 100),
                      Finger(11, TouchPhase.Moved, 180, 100));
        first.Update(input);
        second.Update(input, first.TouchId);
        Assert.False(first.IsActive);
        Assert.Equal(11, second.TouchId);
        Assert.True(second.IsActive);
        second.Cancel();
        Assert.Equal(-1, second.TouchId);
        Assert.Equal(Vector2.Zero, second.Direction);
    }

    [Fact]
    public void RequireFreshPressRejectsSlideInButRetainsCapturedDrag()
    {
        var stick = new VirtualStick(new Vector2(100, 100), 50)
        {
            RequireFreshPress = true
        };
        stick.Update(State(Finger(1, TouchPhase.Moved, 100, 100)));
        Assert.False(stick.IsActive);
        stick.Update(State(Finger(2, TouchPhase.Pressed, 100, 100)));
        Assert.Equal(2, stick.TouchId);
        stick.Update(State(Finger(2, TouchPhase.Moved, 400, 100)));
        Assert.Equal(1f, stick.Direction.X, 0.001f);
        stick.Cancel();
        Assert.Equal(new Vector2(100, 100), stick.Center);
        stick.Update(State(Finger(2, TouchPhase.Moved, 100, 100)));
        Assert.False(stick.IsActive);
    }

    [Fact]
    public void ButtonRespectsReservedTouchAndFreshPressAndCancellation()
    {
        var button = new VirtualButton(new Circle(new Vector2(100, 100), 50))
        {
            RequireFreshPress = true
        };
        button.Update(State(Finger(1, TouchPhase.Moved, 100, 100)));
        Assert.False(button.IsDown);
        button.Update(State(Finger(1, TouchPhase.Pressed, 100, 100)), 1);
        Assert.False(button.IsDown);
        button.Update(State(Finger(2, TouchPhase.Pressed, 100, 100)), 1);
        Assert.Equal(2, button.TouchId);
        Assert.True(button.WasPressed);
        button.Update(State(Finger(2, TouchPhase.Moved, 999, 999)), 1);
        Assert.True(button.IsDown);
        Assert.False(button.WasPressed);
        button.Update(State(Finger(2, TouchPhase.Cancelled, 999, 999)), 1);
        Assert.False(button.IsDown);
        Assert.Equal(-1, button.TouchId);
        button.Update(State(Finger(3, TouchPhase.Pressed, 100, 100)));
        button.Cancel();
        Assert.False(button.IsDown);
        Assert.False(button.WasPressed);
        Assert.Equal(-1, button.TouchId);
    }

    [Fact]
    public void StickAndButtonHotPathDoNotAllocateAfterWarmup()
    {
        var input = new InputState();
        var stick = new VirtualStick(new Vector2(100, 100), 80)
        {
            RescaleDeadZone = true, ResponseExponent = 1.6f, Sensitivity = 1.2f
        };
        var button = new VirtualButton(new Circle(new Vector2(240, 100), 30));
        var frame = new TouchPoint[2];
        frame[0] = Finger(1, TouchPhase.Pressed, 130, 100);
        frame[1] = Finger(2, TouchPhase.Pressed, 240, 100);
        for (var i = 0; i < 128; i++)
        {
            input.SetTouches(frame);
            stick.Update(input);
            button.Update(input, stick.TouchId);
            frame[0] = Finger(1, TouchPhase.Moved, 130, 100);
            frame[1] = Finger(2, TouchPhase.Moved, 240, 100);
        }
        GC.Collect();
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1024; i++)
        {
            input.SetTouches(frame);
            stick.Update(input);
            button.Update(input, stick.TouchId);
        }
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }
}
