using Lunet;
using Lunet.Graphics;

namespace Lunet.Tests;

[Collection("Frame allocation")]
public sealed class GameHostProfilingTests
{
    private sealed class CountingGame : Game
    {
        public int Updates;
        public int Draws;
        public bool FailOnUpdate;

        protected override void Update(GameTime time)
        {
            Updates++;
            if (FailOnUpdate) throw new InvalidOperationException("Falha de atualização simulada");
        }

        protected override void Draw(GameTime time)
        {
            Draws++;
            // A carga mínima torna a medição observável mesmo em runners muito rápidos.
            Thread.SpinWait(1000);
        }
    }

    [Fact]
    public void OptInCountsFixedStepsSeparatesCpuAndResetsWhenDisabled()
    {
        var game = new CountingGame();
        var host = new GameHost(game, new RecordingBackend());
        Assert.True(host.Start(360, 640));
        Assert.False(host.ProfileFrameTimings);
        host.Tick(1.0 / 30);
        Assert.Equal(2, game.Updates);
        Assert.Equal(1, game.Draws);
        Assert.Equal(0, host.LastUpdateSteps);
        Assert.Equal(0, host.LastUpdateCpuMilliseconds);
        Assert.Equal(0, host.LastDrawCpuMilliseconds);

        host.ProfileFrameTimings = true;
        host.Tick(1.0 / 30);
        Assert.Equal(4, game.Updates);
        Assert.Equal(2, game.Draws);
        Assert.Equal(2, host.LastUpdateSteps);
        Assert.True(host.LastUpdateCpuMilliseconds > 0);
        Assert.True(host.LastDrawCpuMilliseconds > 0);

        host.ProfileFrameTimings = false;
        Assert.Equal(0, host.LastUpdateSteps);
        Assert.Equal(0, host.LastUpdateCpuMilliseconds);
        Assert.Equal(0, host.LastDrawCpuMilliseconds);
        host.Tick(1.0 / 60);
        Assert.Equal(0, host.LastDrawCpuMilliseconds);
        host.Stop();
        Assert.False(host.IsFaulted);
    }

    [Fact]
    public void PauseHasZeroUpdatesButDrawsAndStepProfilesExactlyOne()
    {
        var game = new CountingGame();
        var host = new GameHost(game, new RecordingBackend()) { ProfileFrameTimings = true };
        Assert.True(host.Start(360, 640));
        host.Tick(1.0 / 60);
        int updates = game.Updates;
        int draws = game.Draws;
        host.Pause();
        host.Tick(1.0 / 60);
        Assert.Equal(updates, game.Updates);
        Assert.Equal(draws + 1, game.Draws);
        Assert.Equal(0, host.LastUpdateSteps);
        Assert.Equal(0, host.LastUpdateCpuMilliseconds);
        Assert.True(host.LastDrawCpuMilliseconds > 0);

        host.Step();
        Assert.Equal(updates + 1, game.Updates);
        Assert.Equal(1, host.LastUpdateSteps);
        Assert.True(host.LastUpdateCpuMilliseconds > 0);
        Assert.True(host.LastDrawCpuMilliseconds > 0);
        Assert.True(host.IsPaused);
        host.Resume();
        host.Tick(1.0 / 60);
        Assert.True(host.LastUpdateSteps > 0);
        host.Stop();
    }

    [Fact]
    public void FailureDoesNotFalselyReportSuccessfulDraw()
    {
        var game = new CountingGame { FailOnUpdate = true };
        var host = new GameHost(game, new RecordingBackend()) { ProfileFrameTimings = true };
        Assert.True(host.Start(360, 640));
        host.Tick(1.0 / 60);
        Assert.True(host.IsFaulted);
        Assert.IsType<InvalidOperationException>(host.Fault);
        Assert.Equal(0, host.LastDrawCpuMilliseconds);
        Assert.Equal(0, host.LastUpdateSteps);
        host.Stop();
    }

    [Fact]
    public void RecordingPhaseTimesDoesNotAllocateAfterWarmup()
    {
        var game = new CountingGame();
        var host = new GameHost(game, new RecordingBackend()) { ProfileFrameTimings = true };
        Assert.True(host.Start(360, 640));
        for (int i = 0; i < 30; i++) host.Tick(1.0 / 60);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 300; i++) host.Tick(1.0 / 60);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        host.Stop();
    }
}
