using Lunet.Graphics;
using static Lunet.Context;

namespace Lunet.Tests;

public sealed class ContextTimeTests
{
    private sealed class Player
    {
        public GameTime UpdateTime;
        public GameTime DrawTime;
        public void Update() => UpdateTime = Time;
        public void Draw() => DrawTime = Time;
    }

    private sealed class Probe : Game
    {
        private readonly Player _player = new();
        public readonly List<GameTime> Updates = new();
        public readonly List<GameTime> Draws = new();
        public Action? DuringUpdate;
        protected override void Initialize() => Assert.Throws<InvalidOperationException>(() => _ = Context.Time);
        protected override void LoadContent() => Assert.Throws<InvalidOperationException>(() => _ = Context.Time);
        protected override void Update(GameTime time)
        {
            _player.Update();
            Assert.Equal(time, _player.UpdateTime);
            Updates.Add(time);
            DuringUpdate?.Invoke();
            Assert.Equal(time, Context.Time);
        }
        protected override void Draw(GameTime time)
        {
            _player.Draw();
            Assert.Equal(time, _player.DrawTime);
            Draws.Add(time);
        }
        protected override void OnPause() => Assert.Throws<InvalidOperationException>(() => _ = Context.Time);
        protected override void OnResume() => Assert.Throws<InvalidOperationException>(() => _ = Context.Time);
        protected override void UnloadContent() => Assert.Throws<InvalidOperationException>(() => _ = Context.Time);
    }

    [Fact]
    public void ContextTimeMatchesExactCallbackIncludingFixedStepsAndPausedDraw()
    {
        var game = new Probe();
        var host = new GameHost(game, new HeadlessGraphicsBackend());
        Assert.Throws<InvalidOperationException>(() => _ = Context.Time);
        Assert.True(host.Start(100, 100), host.Fault?.ToString());
        game.Dispatcher.Post(() => Assert.Throws<InvalidOperationException>(() => _ = Context.Time));
        host.Tick(0.04);
        Assert.False(host.IsFaulted, host.Fault?.ToString());
        Assert.Equal(2, game.Updates.Count);
        Assert.All(game.Updates, time => Assert.Equal(1f / 60f, time.DeltaSeconds));
        Assert.True(game.Updates[1].TotalSeconds > game.Updates[0].TotalSeconds);
        Assert.Equal(0.04f, Assert.Single(game.Draws).DeltaSeconds);
        Assert.Equal(game.Updates[1].TotalSeconds, game.Draws[0].TotalSeconds);
        Assert.Throws<InvalidOperationException>(() => _ = Context.Time);
        host.Pause();
        host.Tick(0.02);
        Assert.Equal(2, game.Updates.Count);
        Assert.Equal(0.02f, game.Draws[1].DeltaSeconds);
        host.Step();
        Assert.Equal(3, game.Updates.Count);
        Assert.Equal(1f / 60f, game.Draws[2].DeltaSeconds);
        host.Resume();
        host.Stop();
        Assert.False(host.IsFaulted, host.Fault?.ToString());
        Assert.Throws<InvalidOperationException>(() => _ = Context.Time);
    }

    [Fact]
    public void ContextTimeRestoresNestedHostAndDoesNotLeakOnWorkerOrFault()
    {
        var first = new Probe();
        var second = new Probe();
        var a = new GameHost(first, new HeadlessGraphicsBackend());
        var b = new GameHost(second, new HeadlessGraphicsBackend());
        Assert.True(a.Start(100, 100), a.Fault?.ToString());
        Assert.True(b.Start(100, 100), b.Fault?.ToString());
        b.Step();
        first.DuringUpdate = () =>
        {
            var outer = Context.Time;
            b.Step();
            Assert.False(b.IsFaulted, b.Fault?.ToString());
            Assert.NotEqual(outer.TotalSeconds, second.Updates[^1].TotalSeconds);
            Assert.Equal(outer, Context.Time);
            Exception? error = null;
            var worker = new Thread(() =>
            {
                try { _ = Context.Time; }
                catch (Exception ex) { error = ex; }
            });
            worker.Start(); worker.Join();
            Assert.IsType<InvalidOperationException>(error);
            throw new InvalidOperationException("time fault");
        };
        a.Step();
        Assert.Equal("time fault", a.Fault?.Message);
        Assert.Throws<InvalidOperationException>(() => _ = Context.Time);
        a.Stop(); b.Stop();
        Assert.Throws<InvalidOperationException>(() => _ = Context.Time);
    }
}
