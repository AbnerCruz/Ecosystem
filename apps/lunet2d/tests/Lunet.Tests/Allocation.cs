namespace Lunet.Tests;

/// <summary>
/// Measures steady-state allocations reliably. <see cref="GC.GetAllocatedBytesForCurrentThread"/> counts everything
/// on the thread, and the test runner's shared threads also run other work (and the runtime's tier-up of hot
/// methods), which made "does not allocate" tests fail now and then on CI with a few KB that were not theirs. Here the
/// warm-up and the measurement run on a thread of their own and the smallest of three runs is kept: a real
/// allocation of the code under test shows in every run (iterations × bytes), a one-off runtime event does not.
/// </summary>
public static class Allocation
{
    public static long Steady(Action warmup, Action work, int runs = 3)
    {
        long allocated = long.MaxValue;
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                warmup();
                for (var run = 0; run < runs && allocated != 0; run++)
                {
                    var before = GC.GetAllocatedBytesForCurrentThread();
                    work();
                    allocated = Math.Min(allocated, GC.GetAllocatedBytesForCurrentThread() - before);
                }
            }
            catch (Exception e) { failure = e; }
        });
        thread.Start();
        thread.Join();
        if (failure is not null) throw new InvalidOperationException("A medição de alocação falhou: " + failure.Message, failure);
        return allocated;
    }
}

public sealed class AllocationTests
{
    private static object? _sink;

    [Fact]
    public void Steady_StillSeesARealAllocation_AndZeroWhenThereIsNone()
    {
        Assert.True(Allocation.Steady(() => { }, () => { for (var i = 0; i < 100; i++) _sink = new byte[64]; }) >= 100 * 64);
        var x = 0;
        Assert.Equal(0, Allocation.Steady(() => { for (var i = 0; i < 1000; i++) x += i; }, () => { for (var i = 0; i < 1000; i++) x += i; }));
        GC.KeepAlive(_sink);
    }
}
