using Lunet.Graphics;
using static Lunet.Context;

namespace Lunet.Tests;

public sealed class ParameterlessLifecycleTests
{
    private class SimpleGame : Game
    {
        public readonly List<GameTime> Updates = new();
        public readonly List<GameTime> Draws = new();

        protected override void Update() => Updates.Add(Time);
        protected override void Draw()
        {
            Draws.Add(Time);
            Batch.Begin();
            Batch.Draw(new RectangleF(0, 0, 10, 10), Color.Red);
            Batch.End();
        }
    }

    private sealed class ExplicitGame : SimpleGame
    {
        public int ExplicitUpdates;
        public int ExplicitDraws;
        protected override void Update(GameTime time)
        {
            Assert.Equal(time, Time);
            ExplicitUpdates++;
        }
        protected override void Draw(GameTime time)
        {
            Assert.Equal(time, Time);
            ExplicitDraws++;
        }
    }

    private sealed class ChainedGame : SimpleGame
    {
        public int ExplicitUpdates;
        public int ExplicitDraws;
        protected override void Update(GameTime time)
        {
            ExplicitUpdates++;
            base.Update(time);
        }
        protected override void Draw(GameTime time)
        {
            ExplicitDraws++;
            base.Draw(time);
        }
    }

    private sealed class MixedGame : Game
    {
        public int Updates;
        public int Draws;
        protected override void Update() { Assert.True(Time.DeltaSeconds > 0); Updates++; }
        protected override void Draw(GameTime time) { Assert.Equal(time, Time); Draws++; }
    }

    [Fact]
    public void ParameterlessOverridesReceiveContextAndRunOncePerCallback()
    {
        var game = new SimpleGame();
        var host = new GameHost(game, new HeadlessGraphicsBackend());
        Assert.True(host.Start(100, 100), host.Fault?.ToString());
        host.Tick(0.04);
        Assert.False(host.IsFaulted, host.Fault?.ToString());
        Assert.Equal(2, game.Updates.Count);
        Assert.Equal(0.04f, Assert.Single(game.Draws).DeltaSeconds);
        host.Pause();
        host.Tick(0.02);
        Assert.Equal(2, game.Updates.Count);
        Assert.Equal(2, game.Draws.Count);
        host.Step();
        Assert.Equal(3, game.Updates.Count);
        Assert.Equal(3, game.Draws.Count);
        host.Stop();
        Assert.False(host.IsFaulted, host.Fault?.ToString());
    }

    [Fact]
    public void ExplicitOverrideTakesPrecedenceWithoutDoubleDispatch()
    {
        var game = new ExplicitGame();
        var host = new GameHost(game, new HeadlessGraphicsBackend());
        Assert.True(host.Start(100, 100), host.Fault?.ToString());
        host.Step();
        Assert.False(host.IsFaulted, host.Fault?.ToString());
        Assert.Equal(1, game.ExplicitUpdates);
        Assert.Equal(1, game.ExplicitDraws);
        Assert.Empty(game.Updates);
        Assert.Empty(game.Draws);
        host.Stop();
    }

    [Fact]
    public void ExplicitBaseCallCanOptIntoParameterlessOverride()
    {
        var game = new ChainedGame();
        var host = new GameHost(game, new HeadlessGraphicsBackend());
        Assert.True(host.Start(100, 100), host.Fault?.ToString());
        host.Step();
        Assert.False(host.IsFaulted, host.Fault?.ToString());
        Assert.Equal(1, game.ExplicitUpdates);
        Assert.Equal(1, game.ExplicitDraws);
        Assert.Single(game.Updates);
        Assert.Single(game.Draws);
        host.Stop();
    }

    [Fact]
    public void BothStylesCanBeMixedIndependently()
    {
        var game = new MixedGame();
        var host = new GameHost(game, new HeadlessGraphicsBackend());
        Assert.True(host.Start(100, 100), host.Fault?.ToString());
        host.Step();
        Assert.False(host.IsFaulted, host.Fault?.ToString());
        Assert.Equal(1, game.Updates);
        Assert.Equal(1, game.Draws);
        host.Stop();
    }
}
