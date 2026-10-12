using Urbe.Client;

namespace Urbe.Client.Tests;

/// <summary>
/// The timing of the 1.8.4 map save (app.js marcarSinc + workspace.js schedule/flush/flushPending),
/// with the timers under the test's control.
/// </summary>
public sealed class MapSaveSchedulerTests
{
    private sealed class Clock
    {
        public readonly List<(TimeSpan Delay, TaskCompletionSource Done)> Timers = [];
        /// <summary>A timer that fires runs what waits on it at once (no thread-pool hop), so the order is exact.</summary>
        public bool Inline { get; init; }
        public Task Delay(TimeSpan t)
        {
            var done = new TaskCompletionSource(Inline ? TaskCreationOptions.None : TaskCreationOptions.RunContinuationsAsynchronously);
            Timers.Add((t, done));
            return done.Task;
        }
    }

    private static async Task Until(Func<bool> condition)
    {
        for (int i = 0; i < 200 && !condition(); i++) await Task.Delay(5, TestContext.Current.CancellationToken);
        Assert.True(condition());
    }

    [Fact]
    public async Task ASyncWaits120ThenTheSaveWaits900WithoutRestartingAndWritesOnlyChanges()
    {
        var clock = new Clock();
        string map = "A";
        var written = new List<string>();
        var s = new MapSaveScheduler(() => map, t => { written.Add(t); return Task.CompletedTask; }, onDisk: null, clock.Delay);

        s.MarkSync();
        s.MarkSync();                                        // coalesced (sincTimer already set)
        Assert.Single(clock.Timers);
        Assert.Equal(MapSaveScheduler.SyncDelay, clock.Timers[0].Delay);
        Assert.True(s.HasPending);
        clock.Timers[0].Done.SetResult();
        await Until(() => clock.Timers.Count == 2);
        Assert.Equal(MapSaveScheduler.SaveDelay, clock.Timers[1].Delay);
        Assert.Equal("dirty", s.State);
        s.Schedule();                                        // timer already running: not restarted
        Assert.Equal(2, clock.Timers.Count);
        clock.Timers[1].Done.SetResult();
        await Until(() => s.State == "saved");
        Assert.Equal(["A"], written);

        // the same map again: flush runs, nothing is written (snapshot equal)
        s.Schedule();
        clock.Timers[2].Done.SetResult();
        await Until(() => !s.HasPending && s.State == "saved");
        Assert.Equal(["A"], written);

        // a refused map is not written
        map = null!;
        s.Schedule();
        clock.Timers[3].Done.SetResult();
        await Until(() => s.State == "refused");
        Assert.Equal(["A"], written);
    }

    [Fact]
    public async Task FlushWritesAPendingSaveAtOnceAndTheOldTimerDoesNothing()
    {
        var clock = new Clock();
        int produced = 0;
        var written = new List<string>();
        var s = new MapSaveScheduler(() => "B" + ++produced, t => { written.Add(t); return Task.CompletedTask; }, onDisk: "B0", clock.Delay);
        await s.FlushAsync();                                // nothing pending: nothing happens
        Assert.Empty(written);

        s.Schedule();
        await s.FlushAsync();                                // pagehide/visibilitychange
        Assert.Equal(["B1"], written);
        clock.Timers[0].Done.SetResult();                    // the 900 ms timer fires later
        await Task.Delay(30, TestContext.Current.CancellationToken);
        Assert.Equal(["B1"], written);
        Assert.False(s.HasPending);
    }

    [Fact]
    public async Task ARequestDuringAWriteMakesTheFlushLoopAgain()
    {
        // the timer's continuation must run before the write is released; with an inline timer it does, without guessing
        // a delay (30 ms was not always enough on a loaded machine and the flush wrote a third time)
        var clock = new Clock { Inline = true };
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int produced = 0;
        var written = new List<string>();
        var s = new MapSaveScheduler(() => "C" + ++produced, async t => { written.Add(t); if (written.Count == 1) await gate.Task; },
            onDisk: null, clock.Delay);
        var first = s.FlushAsync();                          // nothing pending
        s.Schedule();
        var flushing = s.FlushAsync();
        await Until(() => written.Count == 1 && s.IsBusy);
        s.Schedule();                                        // a change while writing
        clock.Timers[^1].Done.SetResult();                   // its timer fires: busy → pending, synchronously
        Assert.True(s.HasPending);
        gate.SetResult();
        await flushing;
        await first;
        Assert.Equal(["C1", "C2"], written);
        Assert.Equal("saved", s.State);
    }

    [Fact]
    public async Task AFailedWriteIsReportedAndTheNextSaveTriesAgain()
    {
        var clock = new Clock();
        bool fail = true;
        var written = new List<string>();
        var s = new MapSaveScheduler(() => "D", t => fail ? throw new IOException("disco") : Task.Run(() => written.Add(t)), null, clock.Delay);
        s.Schedule();
        await Assert.ThrowsAsync<IOException>(s.FlushAsync);
        Assert.Equal("error", s.State);
        Assert.Equal("disco", s.Problem);
        fail = false;
        s.Schedule();
        clock.Timers[^1].Done.SetResult();
        await Until(() => s.State == "saved");
        Assert.Equal(["D"], written);
    }
}
