using Hub.Core;

namespace Hub.Tests;

public sealed class ArtifactTransferSessionTests
{
    static ArtifactChoice Choice() => new("alpha", "Alpha", new(new("acme", "alpha"), "https://github.com/acme/alpha/releases", null), "v1",
        new("alpha.apk", "https://github.com/acme/alpha/releases/download/v1/alpha.apk", 10, new string('a', 64)), false);
    static ArtifactDownloadResult Verified() => new(ArtifactDownloadState.Verified, ArtifactDownloadError.None,
        "Conferido", "/private/verified.apk", 10, new string('a', 64));
    static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    static Guid Queue(ArtifactTransferSession session) => Assert.IsType<Guid>(session.Queue(Choice()));
    static Task Wait(Task task) => task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

    [Fact]
    public void RejectsInvalidMetadataAndDuplicateQueue()
    {
        var session = new ArtifactTransferSession();
        Assert.Null(session.Queue(Choice() with { Asset = Choice().Asset with { Sha256 = null } }));
        Assert.Equal(ArtifactTransferPhase.Idle, session.Status.Phase);
        var operation = Queue(session);
        Assert.Null(session.Queue(Choice()));
        Assert.Equal(operation, session.Status.Operation);
    }

    [Fact]
    public async Task DetachingAndReattachingObserverDoesNotCancelDownload()
    {
        var session = new ArtifactTransferSession();
        var operation = Queue(session);
        var started = Signal(); var finish = Signal();
        int views = 0; Action observer = () => views++;
        session.Changed += observer;
        var run = session.ExecuteAsync(operation, async (_, progress, ct) =>
        {
            progress.Report(new(4, 10)); started.SetResult();
            await finish.Task.WaitAsync(ct);
            ct.ThrowIfCancellationRequested(); return Verified();
        });
        await Wait(started.Task);
        session.Changed -= observer;
        int detached = views;
        Assert.True(session.Status.Active);
        Assert.Equal(4, session.Status.Progress!.Bytes);
        finish.SetResult(); await Wait(run);
        Assert.Equal(detached, views);
        Assert.Equal(ArtifactDownloadState.Verified, session.Status.Result!.State);
        session.Changed += observer;
        Assert.False(session.Status.Active);
        Assert.Equal("/private/verified.apk", session.Status.Result.LocalPath);
    }

    [Fact]
    public async Task CancelRunningOperationWaitsForTransferCleanupBeforeAllowingAnother()
    {
        var session = new ArtifactTransferSession(); var operation = Queue(session);
        var started = Signal(); var cancelled = Signal(); var cleanup = Signal();
        var run = session.ExecuteAsync(operation, async (_, _, ct) =>
        {
            started.SetResult();
            try { await Task.Delay(Timeout.InfiniteTimeSpan, ct); }
            catch (OperationCanceledException) { cancelled.SetResult(); }
            await cleanup.Task;
            return new(ArtifactDownloadState.Cancelled, ArtifactDownloadError.None, "Descartado");
        });
        await Wait(started.Task);
        Assert.True(session.Cancel(operation));
        await Wait(cancelled.Task);
        Assert.True(session.Status.Active); Assert.True(session.Status.Cancelling);
        Assert.Null(session.Queue(Choice()));
        cleanup.SetResult(); await Wait(run);
        Assert.False(session.Status.Active);
        Assert.Null(session.Status.Result!.LocalPath);
        Assert.NotEqual(operation, Queue(session));
    }

    [Fact]
    public async Task CancelQueuedOperationNeverInvokesTransfer()
    {
        var session = new ArtifactTransferSession(); var operation = Queue(session);
        Assert.True(session.Cancel(operation));
        int calls = 0;
        await session.ExecuteAsync(operation, (_, _, _) => { calls++; return Task.FromResult(Verified()); });
        Assert.Equal(0, calls);
        Assert.Equal(ArtifactDownloadState.Cancelled, session.Status.Result!.State);
        Assert.Null(session.Status.Result.LocalPath);
    }

    [Fact]
    public async Task DuplicateServiceStartExecutesTransferOnce()
    {
        var session = new ArtifactTransferSession(); var operation = Queue(session); var finish = Signal(); int calls = 0;
        var first = session.ExecuteAsync(operation, async (_, _, _) => { calls++; await finish.Task; return Verified(); });
        await session.ExecuteAsync(operation, (_, _, _) => { calls++; return Task.FromResult(Verified()); });
        finish.SetResult(); await Wait(first);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task OldCancelAndProgressCannotAffectNewOperation()
    {
        var session = new ArtifactTransferSession(); var old = Queue(session); IProgress<ArtifactDownloadProgress>? oldProgress = null;
        await session.ExecuteAsync(old, (_, p, _) => { oldProgress = p; return Task.FromResult(Verified()); });
        var current = Queue(session); var finish = Signal();
        var run = session.ExecuteAsync(current, async (_, _, _) => { await finish.Task; return Verified(); });
        Assert.False(session.Cancel(old));
        oldProgress!.Report(new(999, 1000));
        Assert.Null(session.Status.Progress); Assert.False(session.Status.Cancelling);
        finish.SetResult(); await Wait(run);
        Assert.Equal(current, session.Status.Operation);
    }

    [Fact]
    public async Task PlatformInterruptionCancelsWithoutClaimingVerifiedFile()
    {
        var session = new ArtifactTransferSession(); var operation = Queue(session); var started = Signal();
        var run = session.ExecuteAsync(operation, async (_, _, ct) =>
        { started.SetResult(); await Task.Delay(Timeout.InfiniteTimeSpan, ct); return Verified(); });
        await Wait(started.Task);
        session.Cancel(operation, "O Android interrompeu o serviço."); await Wait(run);
        Assert.Equal(ArtifactDownloadState.Cancelled, session.Status.Result!.State);
        Assert.Equal("O Android interrompeu o serviço.", session.Status.Result.Message);
        Assert.Null(session.Status.Result.LocalPath);
    }

    [Fact]
    public async Task TransferFailureAndObserverFailureDoNotStrandSession()
    {
        var session = new ArtifactTransferSession(); session.Changed += () => throw new InvalidOperationException("tela destruída");
        var operation = Queue(session);
        await session.ExecuteAsync(operation, (_, _, _) => throw new IOException("falha inesperada"));
        Assert.False(session.Status.Active);
        Assert.Equal(ArtifactDownloadState.Failed, session.Status.Result!.State);
        Assert.Null(session.Status.Result.LocalPath);
        Queue(session);
    }

    [Fact]
    public async Task ServiceStartDeniedMakesQueuedStateRetryableWithoutNetwork()
    {
        var session = new ArtifactTransferSession(); var operation = Queue(session);
        session.FailQueued(operation, "Serviço bloqueado.");
        await session.ExecuteAsync(operation, (_, _, _) => throw new Exception("não deve iniciar"));
        Assert.False(session.Status.Active); Assert.Equal("Serviço bloqueado.", session.Status.Result!.Message);
        Queue(session);
        session.FailQueued(operation, "evento atrasado");
        Assert.True(session.Status.Active);
    }

    [Fact]
    public void ProcessRecoveryDoesNotReuseVerifiedFileOrResumeAndCannotOverwriteActiveState()
    {
        var session = new ArtifactTransferSession(); session.RecoverInterrupted();
        Assert.False(session.Status.Active);
        Assert.Equal(ArtifactDownloadError.Interrupted, session.Status.Result!.Error);
        Assert.Null(session.Status.Result.LocalPath); Assert.Null(session.Status.Choice);
        var operation = Queue(session); session.RecoverInterrupted();
        Assert.True(session.Status.Active); Assert.Equal(operation, session.Status.Operation);
    }
}
