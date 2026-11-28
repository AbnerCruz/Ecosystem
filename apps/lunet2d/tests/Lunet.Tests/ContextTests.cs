using Lunet.Graphics;

namespace Lunet.Tests;

public sealed class ContextTests
{
    private sealed class Probe : Game
    {
        public Action? DuringUpdate;
        public int Calls;
        private void Verify(bool graphics)
        {
            Assert.Same(Input, Context.Input);
            Assert.Same(Rng, Context.Rng);
            Assert.Same(VisualRng, Context.VisualRng);
            Assert.Same(Rng, Services.Get<RandomSource>());
            Assert.Same(Save, Context.Save);
            Assert.Same(Audio, Context.Audio);
            Assert.Same(Services, Context.Services);
            Assert.Same(Timers, Context.Timers);
            Assert.Same(Log, Context.Log);
            Assert.Same(Dispatcher, Context.Dispatcher);
            if (graphics)
            {
                Assert.Same(Content, Context.Content);
                Assert.Same(GraphicsDevice, Context.Graphics);
            }
            else
            {
                Assert.Throws<InvalidOperationException>(() => _ = Context.Graphics);
                Assert.Throws<InvalidOperationException>(() => _ = Context.Content);
            }
            Calls++;
        }
        protected override void Initialize() => Verify(true);
        protected override void LoadContent() => Verify(true);
        protected override void Update(GameTime time) { Verify(true); DuringUpdate?.Invoke(); }
        protected override void Draw(GameTime time) => Verify(true);
        protected override void OnPause() => Verify(false);
        protected override void OnResume() => Verify(false);
        protected override void UnloadContent() => Verify(false);
    }

    [Fact]
    public void ContextIsCallbackScopedAndRestoresNestedHost()
    {
        var first = new Probe();
        var second = new Probe();
        Assert.NotSame(first.Rng, second.Rng);
        var a = new GameHost(first, new HeadlessGraphicsBackend());
        var b = new GameHost(second, new HeadlessGraphicsBackend());
        Assert.Throws<InvalidOperationException>(() => _ = Context.Input);
        Assert.True(a.Start(100, 100), a.Fault?.ToString());
        Assert.True(b.Start(100, 100), b.Fault?.ToString());
        first.DuringUpdate = () =>
        {
            Assert.Same(first.Input, Context.Input);
            Assert.Same(first.Rng, Context.Rng);
            b.Step();
            Assert.False(b.IsFaulted, b.Fault?.ToString());
            Assert.Same(first.Input, Context.Input);
            Assert.Same(first.Rng, Context.Rng);
        };
        bool dispatcherRan = false;
        first.Dispatcher.Post(() => { Assert.Same(first.Dispatcher, Context.Dispatcher); dispatcherRan = true; });
        a.Tick(1.0 / 30);
        Assert.True(dispatcherRan);
        Assert.True(first.Calls > 0);
        Assert.False(a.IsFaulted, a.Fault?.ToString());
        a.Pause(); a.Step(); a.Resume();
        Assert.False(a.IsFaulted, a.Fault?.ToString());
        a.Stop(); a.Stop(); b.Stop();
        Assert.False(a.IsFaulted, a.Fault?.ToString());
        Assert.Throws<InvalidOperationException>(() => _ = Context.Services);
    }

    [Fact]
    public void ContextDoesNotFlowToWorkerAndIsClearedOnFault()
    {
        var game = new Probe();
        var host = new GameHost(game, new HeadlessGraphicsBackend());
        Assert.True(host.Start(100, 100), host.Fault?.ToString());
        game.DuringUpdate = () =>
        {
            Exception? observed = null;
            var worker = new Thread(() =>
            {
                try { _ = Context.Rng; }
                catch (Exception ex) { observed = ex; }
            });
            worker.Start(); worker.Join();
            Assert.IsType<InvalidOperationException>(observed);
            throw new InvalidOperationException("test fault");
        };
        host.Step();
        Assert.Equal("test fault", host.Fault?.Message);
        Assert.Throws<InvalidOperationException>(() => _ = Context.Input);
        host.Stop();
        Assert.Throws<InvalidOperationException>(() => _ = Context.Log);
    }

    private sealed class BatchProbe : Game
    {
        public SpriteBatch? DefaultBatch;
        public Action<BatchProbe>? DuringDraw;

        protected override void Draw(GameTime time)
        {
            DefaultBatch ??= Context.Batch;
            Assert.Same(DefaultBatch, Context.Batch);
            DuringDraw?.Invoke(this);
        }

        protected override void OnPause() =>
            Assert.Throws<InvalidOperationException>(() => _ = Context.Batch);
    }

    [Fact]
    public void ContextBatchReusesDefaultAndResolvesExplicitPasses()
    {
        var game = new BatchProbe();
        var host = new GameHost(game, new HeadlessGraphicsBackend());
        Assert.Throws<InvalidOperationException>(() => _ = Context.Batch);
        Assert.True(host.Start(100, 100), host.Fault?.ToString());
        game.DuringDraw = g =>
        {
            var fallback = Context.Batch;
            Assert.Same(g.GraphicsDevice, fallback.GraphicsDevice);
            Context.Batch.Begin();
            Context.Batch.Draw(new RectangleF(0, 0, 20, 20), Color.Red);
            Context.Batch.End();
            Assert.Same(fallback, Context.Batch);

            var world = new SpriteBatch(g.GraphicsDevice);
            var hud = new SpriteBatch(g.GraphicsDevice);
            world.Begin();
            Assert.Same(world, Context.Batch);
            hud.Begin();
            Assert.Throws<InvalidOperationException>(() => _ = Context.Batch);
            world.End();
            Assert.Same(hud, Context.Batch);
            hud.End();
            Assert.Same(fallback, Context.Batch);

            world.BeginSorted();
            Assert.Same(world, Context.Batch);
            Assert.Throws<InvalidOperationException>(() => hud.Begin());
            Assert.Same(world, Context.Batch);
            Context.Batch.CancelSorted();
            Assert.Same(fallback, Context.Batch);
            hud.BeginSorted();
            Context.Batch.Draw(new RectangleF(0, 0, 5, 5), Color.Red);
            Context.Batch.End();
            Assert.Same(fallback, Context.Batch);
        };
        host.Step();
        Assert.False(host.IsFaulted, host.Fault?.ToString());
        host.Step();
        Assert.False(host.IsFaulted, host.Fault?.ToString());
        host.Pause();
        Assert.False(host.IsFaulted, host.Fault?.ToString());
        host.Stop();
        Assert.Throws<InvalidOperationException>(() => _ = Context.Batch);
    }

    [Fact]
    public void ContextBatchIsIsolatedBetweenNestedHostsAndWorkers()
    {
        var first = new BatchProbe();
        var second = new BatchProbe();
        var a = new GameHost(first, new HeadlessGraphicsBackend());
        var b = new GameHost(second, new HeadlessGraphicsBackend());
        Assert.True(a.Start(100, 100), a.Fault?.ToString());
        Assert.True(b.Start(100, 100), b.Fault?.ToString());
        first.DuringDraw = g =>
        {
            var active = new SpriteBatch(g.GraphicsDevice);
            active.Begin();
            b.Step();
            Assert.False(b.IsFaulted, b.Fault?.ToString());
            Assert.NotSame(first.DefaultBatch, second.DefaultBatch);
            Assert.Same(active, Context.Batch);
            Exception? error = null;
            var worker = new Thread(() =>
            {
                try { _ = Context.Batch; }
                catch (Exception ex) { error = ex; }
            });
            worker.Start(); worker.Join();
            Assert.IsType<InvalidOperationException>(error);
            Context.Batch.End();
        };
        a.Step();
        Assert.False(a.IsFaulted, a.Fault?.ToString());
        a.Stop(); b.Stop();
        Assert.Throws<InvalidOperationException>(() => _ = Context.Batch);
    }
}
