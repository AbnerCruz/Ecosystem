using System.Numerics;
using Lunet.Input;

namespace Lunet.Tests;

public class UtilityTests
{
    [Fact]
    public void GameServices_AddGetRemove()
    {
        var services = new GameServices();
        services.Add("texto");
        Assert.Equal("texto", services.Get<string>());
        Assert.True(services.TryGet<string>(out _));
        Assert.False(services.TryGet<Uri>(out _));
        Assert.Throws<InvalidOperationException>(() => services.Get<Uri>());
        Assert.True(services.Remove<string>());
        Assert.False(services.TryGet<string>(out _));
    }

    [Fact]
    public void Dispatcher_RunsPostedActionsFromOtherThreadsOnRunPending()
    {
        var game = new DispatchGame();
        var host = new GameHost(game, new RecordingBackend());
        host.Start(10, 10);
        var thread = new Thread(() => game.Dispatcher.Post(() => game.Ran.Add(Environment.CurrentManagedThreadId)));
        thread.Start();
        thread.Join();
        Assert.Empty(game.Ran);
        host.Tick(0.016);
        Assert.Equal([Environment.CurrentManagedThreadId], game.Ran);
    }

    private sealed class DispatchGame : Game
    {
        public List<int> Ran { get; } = [];
    }

    [Fact]
    public void Dispatcher_ActionsPostedWhileRunningWaitForNextFrame()
    {
        var game = new DispatchGame();
        var host = new GameHost(game, new RecordingBackend());
        host.Start(10, 10);
        game.Dispatcher.Post(() =>
        {
            game.Ran.Add(1);
            game.Dispatcher.Post(() => game.Ran.Add(2));
        });
        host.Tick(0.016);
        Assert.Equal([1], game.Ran);
        host.Tick(0.016);
        Assert.Equal([1, 2], game.Ran);
    }

    [Fact]
    public void Timers_FireOnceRepeatAndCancel_AndStopWhenGamePaused()
    {
        var game = new DispatchGame();
        var host = new GameHost(game, new RecordingBackend());
        host.Start(10, 10);
        int once = 0, every = 0;
        game.Timers.After(0.1f, () => once++);
        var repeating = game.Timers.Every(0.05f, () => every++);
        for (var i = 0; i < 12; i++) host.Tick(1.0 / 60); // 0,2 s
        Assert.Equal(1, once);
        Assert.InRange(every, 3, 4);

        repeating.Cancel();
        var before = every;
        for (var i = 0; i < 12; i++) host.Tick(1.0 / 60);
        Assert.Equal(before, every);

        int paused = 0;
        game.Timers.After(0.05f, () => paused++);
        host.Pause();
        for (var i = 0; i < 12; i++) host.Tick(1.0 / 60);
        Assert.Equal(0, paused);
        host.Resume();
        for (var i = 0; i < 12; i++) host.Tick(1.0 / 60);
        Assert.Equal(1, paused);
        Assert.Equal(0, game.Timers.ActiveCount);
    }

    [Fact]
    public void Timers_CreatedInsideCallback_StartNextStep()
    {
        var timers = new Timers();
        var log = new List<string>();
        timers.After(0.1f, () =>
        {
            log.Add("a");
            timers.After(0.1f, () => log.Add("b"));
        });
        timers.Update(0.11);
        Assert.Equal(["a"], log);
        timers.Update(0.11);
        Assert.Equal(["a", "b"], log);
        Assert.Throws<ArgumentOutOfRangeException>(() => timers.After(0, () => { }));
    }

    [Fact]
    public void ObjectPool_ReusesAndResets()
    {
        var created = 0;
        var pool = new ObjectPool<List<int>>(() => { created++; return []; }, list => list.Clear(), maxRetained: 2);
        var a = pool.Get();
        a.Add(1);
        pool.Return(a);
        var b = pool.Get();
        Assert.Same(a, b);
        Assert.Empty(b);
        Assert.Equal(1, created);
        Assert.Equal(1, pool.InUse);

        var items = Enumerable.Range(0, 5).Select(_ => pool.Get()).ToList();
        foreach (var item in items) pool.Return(item);
        Assert.Equal(2, pool.Available);
    }

    [Fact]
    public void Host_RegistersDefaultServices()
    {
        var game = new DispatchGame();
        new GameHost(game, new RecordingBackend()).Start(10, 10);
        Assert.NotNull(game.Services.Get<Lunet.Graphics.GraphicsDevice>());
        Assert.NotNull(game.Services.Get<InputState>());
        Assert.Same(game.Timers, game.Services.Get<Timers>());
        Assert.Same(game.Log, game.Services.Get<GameLog>());
    }
}

public class GeometryTests
{
    [Fact]
    public void Transform2D_MatrixAndInverse()
    {
        var t = new Transform2D(new Vector2(10, 20), MathF.PI / 2, new Vector2(2, 2), new Vector2(1, 0));
        var world = t.TransformPoint(new Vector2(1, 0)); // a própria origem vai para a posição
        Assert.Equal(10, world.X, 0.001);
        Assert.Equal(20, world.Y, 0.001);
        var p = t.TransformPoint(new Vector2(2, 0)); // 1 unidade à frente da origem, escala 2, girado 90°
        Assert.Equal(10, p.X, 0.001);
        Assert.Equal(22, p.Y, 0.001);
        Assert.True(t.TryInverseTransformPoint(p, out var back));
        Assert.Equal(2, back.X, 0.001);
        Assert.Equal(0, back.Y, 0.001);
        Assert.False(new Transform2D(Vector2.Zero, 0, Vector2.Zero).TryInverseTransformPoint(Vector2.One, out _));
        Assert.Equal(0, new Transform2D(Vector2.Zero, MathF.PI / 2).Forward.X, 0.001);
    }

    [Fact]
    public void Ray_HitsCirclesAndRectangles()
    {
        var ray = new Ray2D(new Vector2(0, 0), new Vector2(3, 0));
        Assert.Equal(new Vector2(1, 0), ray.Direction);
        Assert.True(ray.Intersects(new Circle(new Vector2(10, 0), 2), out var d));
        Assert.Equal(8, d, 0.001);
        Assert.False(ray.Intersects(new Circle(new Vector2(10, 5), 2), out _));
        Assert.False(ray.Intersects(new Circle(new Vector2(-10, 0), 2), out _));
        Assert.True(ray.Intersects(new Circle(Vector2.Zero, 3), out var inside));
        Assert.Equal(0, inside);

        Assert.True(ray.Intersects(new RectangleF(5, -1, 2, 2), out var r));
        Assert.Equal(5, r, 0.001);
        Assert.False(ray.Intersects(new RectangleF(5, 1, 2, 2), out _));
        Assert.False(ray.Intersects(new RectangleF(-9, -1, 2, 2), out _));
        Assert.Throws<ArgumentException>(() => new Ray2D(Vector2.Zero, Vector2.Zero));
    }

    [Fact]
    public void Segments_DistanceAndIntersection()
    {
        Assert.Equal(3, Geometry.DistanceToSegment(new Vector2(5, 3), new Vector2(0, 0), new Vector2(10, 0)), 0.001);
        Assert.Equal(5, Geometry.DistanceToSegment(new Vector2(-3, 4), new Vector2(0, 0), new Vector2(10, 0)), 0.001);
        Assert.True(Geometry.SegmentsIntersect(new Vector2(0, 0), new Vector2(10, 10), new Vector2(0, 10), new Vector2(10, 0), out var p));
        Assert.Equal(new Vector2(5, 5), p);
        Assert.False(Geometry.SegmentsIntersect(new Vector2(0, 0), new Vector2(1, 1), new Vector2(5, 0), new Vector2(6, 1), out _));
        Assert.False(Geometry.SegmentsIntersect(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1), out _)); // paralelos
    }

    private static Vector2[] Square(float x, float y, float size) =>
        [new(x, y), new(x + size, y), new(x + size, y + size), new(x, y + size)];

    [Fact]
    public void Polygon_ContainsAndArea()
    {
        var sq = Square(0, 0, 10);
        Assert.True(Geometry.PolygonContains(sq, new Vector2(5, 5)));
        Assert.False(Geometry.PolygonContains(sq, new Vector2(11, 5)));
        Assert.Equal(100, MathF.Abs(Geometry.SignedArea(sq)), 0.001);
        Vector2[] concave = [new(0, 0), new(10, 0), new(10, 10), new(5, 4), new(0, 10)];
        Assert.False(Geometry.PolygonContains(concave, new Vector2(5, 8)));
        Assert.True(Geometry.PolygonContains(concave, new Vector2(5, 2)));
    }

    [Fact]
    public void Sat_DetectsOverlapAndGivesSeparatingPush()
    {
        var a = Square(0, 0, 10);
        var b = Square(8, 2, 10);
        Assert.True(Geometry.SatOverlap(a, b, out var push));
        Assert.Equal(-2, push.X, 0.001); // a empurrado 2 unidades para a esquerda
        Assert.Equal(0, push.Y, 0.001);

        var moved = a.Select(v => v + push).ToArray();
        Assert.False(Geometry.SatOverlap(moved.Select(v => v + new Vector2(-0.01f, 0)).ToArray(), b, out _));
        Assert.False(Geometry.SatOverlap(a, Square(20, 0, 5), out _));

        Vector2[] triangle = [new(0, 0), new(10, 0), new(0, 10)];
        Assert.True(Geometry.SatOverlap(triangle, Square(1, 1, 2), out _));
        Assert.False(Geometry.SatOverlap(triangle, Square(8, 8, 2), out _)); // fora da hipotenusa
    }
}

public class MultiTouchGestureTests
{
    private static TouchPoint Down(int id, float x, float y) => new(id, TouchPhase.Moved, new Vector2(x, y));
    private static TouchPoint Up(int id, float x, float y) => new(id, TouchPhase.Released, new Vector2(x, y));

    [Fact]
    public void TwoFingersMovingApart_EmitPinchWithScaleAboveOne()
    {
        var r = new GestureRecognizer();
        var g = new List<Gesture>();
        r.Update([Down(1, 100, 100), Down(2, 200, 100)], 0.0, g);
        r.Update([Down(1, 90, 100), Down(2, 210, 100)], 0.05, g);
        r.Update([Down(1, 80, 100), Down(2, 220, 100)], 0.10, g);
        var pinches = g.Where(x => x.Type == GestureType.Pinch).ToList();
        Assert.Equal(2, pinches.Count);
        Assert.All(pinches, p => Assert.True(p.Scale > 1f));
        Assert.Equal(new Vector2(150, 100), pinches[0].Position);
        Assert.DoesNotContain(g, x => x.Type is GestureType.Drag or GestureType.Tap);
    }

    [Fact]
    public void TwoFingersGettingCloser_EmitPinchBelowOne_AndNoTapOnRelease()
    {
        var r = new GestureRecognizer();
        var g = new List<Gesture>();
        r.Update([Down(1, 100, 100), Down(2, 200, 100)], 0.0, g);
        r.Update([Down(1, 120, 100), Down(2, 180, 100)], 0.05, g);
        r.Update([Up(1, 120, 100), Up(2, 180, 100)], 0.10, g);
        Assert.Contains(g, x => x.Type == GestureType.Pinch && x.Scale < 1f);
        Assert.DoesNotContain(g, x => x.Type == GestureType.Tap);
    }

    [Fact]
    public void TwoFingersTwisting_EmitRotate()
    {
        var r = new GestureRecognizer();
        var g = new List<Gesture>();
        r.Update([Down(1, 100, 100), Down(2, 200, 100)], 0.0, g);
        // gira o segundo dedo 90° em torno do primeiro, sem mudar a distância
        r.Update([Down(1, 100, 100), Down(2, 100, 200)], 0.05, g);
        var rotate = Assert.Single(g, x => x.Type == GestureType.Rotate);
        Assert.Equal(MathF.PI / 2, rotate.Rotation, 0.01);
        Assert.DoesNotContain(g, x => x.Type == GestureType.Pinch);
    }

    [Fact]
    public void TwoQuickTaps_EmitDoubleTapOnce()
    {
        var r = new GestureRecognizer();
        var g = new List<Gesture>();
        r.Update([Down(1, 50, 50)], 0.00, g);
        r.Update([Up(1, 50, 50)], 0.05, g);
        r.Update([Down(2, 55, 52)], 0.15, g);
        r.Update([Up(2, 55, 52)], 0.20, g);
        Assert.Equal([GestureType.Tap, GestureType.Tap, GestureType.DoubleTap], g.Select(x => x.Type));

        g.Clear();
        r.Update([Down(3, 55, 52)], 0.25, g);
        r.Update([Up(3, 55, 52)], 0.30, g);
        Assert.Equal([GestureType.Tap], g.Select(x => x.Type)); // terceiro toque começa nova contagem
    }

    [Fact]
    public void SlowOrDistantSecondTap_IsNotDoubleTap()
    {
        var r = new GestureRecognizer();
        var g = new List<Gesture>();
        r.Update([Down(1, 50, 50)], 0.0, g);
        r.Update([Up(1, 50, 50)], 0.05, g);
        r.Update([Down(2, 50, 50)], 0.9, g);
        r.Update([Up(2, 50, 50)], 0.95, g);
        r.Update([Down(3, 300, 300)], 1.0, g);
        r.Update([Up(3, 300, 300)], 1.05, g);
        Assert.DoesNotContain(g, x => x.Type == GestureType.DoubleTap);
    }
}

public class VirtualControlsTests
{
    private static InputState Touches(params TouchPoint[] touches)
    {
        var input = new InputState();
        input.SetTouches(touches);
        return input;
    }

    [Fact]
    public void Stick_ReportsClampedDirectionWhileHeld_AndResetsOnRelease()
    {
        var stick = new VirtualStick(new Vector2(60, 500), 40);
        stick.Update(Touches(new TouchPoint(1, TouchPhase.Pressed, new Vector2(60, 500))));
        Assert.True(stick.IsActive);
        Assert.Equal(Vector2.Zero, stick.Direction); // zona morta

        stick.Update(Touches(new TouchPoint(1, TouchPhase.Moved, new Vector2(80, 500))));
        Assert.Equal(0.5f, stick.Direction.X, 0.001);

        stick.Update(Touches(new TouchPoint(1, TouchPhase.Moved, new Vector2(300, 500))));
        Assert.Equal(1f, stick.Direction.Length(), 0.001); // limitado ao raio
        Assert.Equal(new Vector2(100, 500), stick.Knob);

        stick.Update(Touches());
        Assert.False(stick.IsActive);
        Assert.Equal(Vector2.Zero, stick.Direction);
    }

    [Fact]
    public void Stick_IgnoresTouchesThatStartOutsideItsArea()
    {
        var stick = new VirtualStick(new Vector2(60, 500), 40);
        stick.Update(Touches(new TouchPoint(1, TouchPhase.Pressed, new Vector2(300, 100))));
        Assert.False(stick.IsActive);
    }

    [Fact]
    public void FloatingStick_CentersWhereTheFingerLanded()
    {
        var area = new RectangleF(0, 300, 180, 340);
        var stick = new VirtualStick(new Vector2(60, 500), 40, area, floating: true);
        stick.Update(Touches(new TouchPoint(1, TouchPhase.Pressed, new Vector2(150, 400))));
        Assert.Equal(new Vector2(150, 400), stick.Center);
        stick.Update(Touches(new TouchPoint(1, TouchPhase.Moved, new Vector2(150, 360))));
        Assert.Equal(-1f, stick.Direction.Y, 0.001);
        stick.Update(Touches());
        Assert.Equal(new Vector2(60, 500), stick.Center);
    }

    [Fact]
    public void Button_PressedOnceThenHeld()
    {
        var button = new VirtualButton(new Circle(new Vector2(300, 500), 30));
        button.Update(Touches(new TouchPoint(1, TouchPhase.Pressed, new Vector2(305, 505))));
        Assert.True(button.IsDown);
        Assert.True(button.WasPressed);
        button.Update(Touches(new TouchPoint(1, TouchPhase.Moved, new Vector2(400, 505))));
        Assert.True(button.IsDown); // dedo arrastado para fora continua segurando
        Assert.False(button.WasPressed);
        button.Update(Touches());
        Assert.False(button.IsDown);
        button.Update(Touches(new TouchPoint(2, TouchPhase.Pressed, new Vector2(10, 10))));
        Assert.False(button.IsDown);
    }
}
