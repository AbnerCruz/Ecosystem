using System.Numerics;
using Lunet.Input;

namespace Lunet.Tests;

public class GestureTests
{
    private static TouchPoint Down(Vector2 p, int id = 1) => new(id, TouchPhase.Moved, p);
    private static TouchPoint Up(Vector2 p, int id = 1) => new(id, TouchPhase.Released, p);

    [Fact]
    public void QuickPressAndRelease_IsTap()
    {
        var r = new GestureRecognizer();
        var g = new List<Gesture>();
        r.Update([Down(new(50, 50))], 0.00, g);
        r.Update([Up(new(52, 51))], 0.12, g);
        var tap = Assert.Single(g);
        Assert.Equal(GestureType.Tap, tap.Type);
        Assert.Equal(new Vector2(52, 51), tap.Position);
    }

    [Fact]
    public void HoldStill_FiresLongPressOnceAndNoTapOnRelease()
    {
        var r = new GestureRecognizer();
        var g = new List<Gesture>();
        for (var t = 0.0; t <= 0.8; t += 0.1) r.Update([Down(new(10, 10))], t, g);
        r.Update([Up(new(10, 10))], 0.9, g);
        Assert.Equal([GestureType.LongPress], g.Select(x => x.Type));
    }

    [Fact]
    public void SlowMove_ProducesDragDeltasAndNoSwipe()
    {
        var r = new GestureRecognizer();
        var g = new List<Gesture>();
        r.Update([Down(new(0, 0))], 0, g);
        r.Update([Down(new(20, 0))], 0.4, g);
        r.Update([Down(new(40, 0))], 0.8, g);
        r.Update([Up(new(40, 0))], 1.2, g);
        Assert.All(g, x => Assert.Equal(GestureType.Drag, x.Type));
        Assert.Equal(40, g.Sum(x => x.Delta.X), 0.001);
    }

    [Fact]
    public void FastFlick_EndsWithSwipe()
    {
        var r = new GestureRecognizer();
        var g = new List<Gesture>();
        r.Update([Down(new(0, 100))], 0, g);
        r.Update([Down(new(60, 100))], 0.05, g);
        r.Update([Down(new(140, 100))], 0.1, g);
        r.Update([Up(new(140, 100))], 0.11, g);
        var swipe = g.Last();
        Assert.Equal(GestureType.Swipe, swipe.Type);
        Assert.True(swipe.Velocity.X > 600);
        Assert.Equal(140, swipe.Delta.X, 0.001);
    }

    [Fact]
    public void VanishedFinger_CountsAsRelease()
    {
        var r = new GestureRecognizer();
        var g = new List<Gesture>();
        r.Update([Down(new(5, 5))], 0, g);
        r.Update([], 0.1, g);
        Assert.Equal(GestureType.Tap, Assert.Single(g).Type);
    }

    [Fact]
    public void Host_DeliversEachGestureToExactlyOneUpdateStep()
    {
        var game = new GestureGame();
        var host = new GameHost(game, new RecordingBackend());
        host.Start(100, 100);
        host.SetSurfaceTouches([Down(new(50, 50))]);
        host.Tick(0.02);
        host.SetSurfaceTouches([Up(new(50, 50))]);
        host.Tick(0.05); // ~3 passos no mesmo quadro
        Assert.Equal(1, game.Taps);
    }

    private sealed class GestureGame : Game
    {
        public int Taps;
        protected override void Update(GameTime time)
        {
            foreach (var gesture in Input.Gestures)
                if (gesture.Type == GestureType.Tap) Taps++;
        }
    }
}
