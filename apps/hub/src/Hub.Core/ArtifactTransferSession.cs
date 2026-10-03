namespace Hub.Core;

public enum ArtifactTransferPhase { Idle, Queued, Downloading, Finished }

/// <summary>Uma operação por processo; a Activity só observa, nunca é dona da transferência.</summary>
public sealed record ArtifactTransferStatus(Guid Operation, ArtifactTransferPhase Phase,
    ArtifactChoice? Choice = null, ArtifactDownloadProgress? Progress = null,
    ArtifactDownloadResult? Result = null, bool Cancelling = false)
{
    public bool Active => Phase is ArtifactTransferPhase.Queued or ArtifactTransferPhase.Downloading;
}

public sealed class ArtifactTransferSession
{
    readonly object _gate = new();
    ArtifactTransferStatus _status = new(Guid.Empty, ArtifactTransferPhase.Idle);
    CancellationTokenSource? _cancellation;
    string? _interruption;
    public event Action? Changed;
    public ArtifactTransferStatus Status { get { lock (_gate) return _status; } }

    public Guid? Queue(ArtifactChoice choice)
    {
        if (ArtifactDownloader.Ineligible(choice) is not null) return null;
        Guid operation;
        lock (_gate)
        {
            if (_status.Active) return null;
            operation = Guid.NewGuid();
            _cancellation = new();
            _interruption = null;
            _status = new(operation, ArtifactTransferPhase.Queued, choice);
        }
        Notify();
        return operation;
    }

    public async Task ExecuteAsync(Guid operation,
        Func<ArtifactChoice, IProgress<ArtifactDownloadProgress>, CancellationToken, Task<ArtifactDownloadResult>> transfer)
    {
        ArtifactChoice choice;
        CancellationTokenSource cancellation;
        lock (_gate)
        {
            if (_status.Operation != operation || _status.Phase != ArtifactTransferPhase.Queued) return;
            choice = _status.Choice!;
            cancellation = _cancellation!;
            _status = _status with { Phase = ArtifactTransferPhase.Downloading };
        }
        Notify();
        ArtifactDownloadResult result;
        try
        {
            result = await transfer(choice, new ProgressObserver(p => Report(operation, p)), cancellation.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        { result = new(ArtifactDownloadState.Cancelled, ArtifactDownloadError.None, "Download cancelado."); }
        catch (Exception)
        { result = new(ArtifactDownloadState.Failed, ArtifactDownloadError.BackgroundUnavailable, "Download interrompido; tente novamente."); }
        lock (_gate)
        {
            if (result.State == ArtifactDownloadState.Cancelled && _interruption is not null)
                result = result with { Message = _interruption };
            _status = _status with { Phase = ArtifactTransferPhase.Finished, Result = result, Cancelling = false };
            _cancellation = null;
        }
        cancellation.Dispose();
        Notify();
    }

    public bool Cancel(Guid operation, string? explanation = null)
    {
        CancellationTokenSource? cancellation;
        bool queued;
        lock (_gate)
        {
            if (_status.Operation != operation || !_status.Active || _status.Cancelling) return false;
            cancellation = _cancellation;
            queued = _status.Phase == ArtifactTransferPhase.Queued;
            _interruption = explanation;
            _status = queued
                ? _status with { Phase = ArtifactTransferPhase.Finished,
                    Result = new(ArtifactDownloadState.Cancelled, ArtifactDownloadError.None, explanation ?? "Download cancelado antes de iniciar.") }
                : _status with { Cancelling = true };
            if (queued) _cancellation = null;
        }
        // Fora do lock: callbacks de cancelamento podem publicar progresso.
        try { cancellation?.Cancel(); } catch (ObjectDisposedException) { }
        if (queued) cancellation?.Dispose();
        Notify();
        return true;
    }

    public void FailQueued(Guid operation, string message)
    {
        CancellationTokenSource? cancellation;
        lock (_gate)
        {
            if (_status.Operation != operation || _status.Phase != ArtifactTransferPhase.Queued) return;
            cancellation = _cancellation;
            _cancellation = null;
            _status = _status with { Phase = ArtifactTransferPhase.Finished,
                Result = new(ArtifactDownloadState.Failed, ArtifactDownloadError.BackgroundUnavailable, message) };
        }
        cancellation?.Dispose();
        Notify();
    }

    public void RecoverInterrupted()
    {
        lock (_gate)
        {
            if (_status.Phase != ArtifactTransferPhase.Idle) return;
            _status = new(Guid.Empty, ArtifactTransferPhase.Finished, Result: new(ArtifactDownloadState.Failed,
                ArtifactDownloadError.Interrupted,
                "Tentativa anterior sem resultado recuperável: o Android pode ter encerrado o processo. Selecione o APK e tente novamente."));
        }
        Notify();
    }

    void Report(Guid operation, ArtifactDownloadProgress progress)
    {
        lock (_gate)
        {
            if (_status.Operation != operation || _status.Phase != ArtifactTransferPhase.Downloading || _status.Cancelling) return;
            _status = _status with { Progress = progress };
        }
        Notify();
    }

    void Notify()
    {
        foreach (var handler in Changed?.GetInvocationList() ?? [])
            try { ((Action)handler)(); } catch (Exception) { /* Observador não controla a transferência. */ }
    }

    sealed class ProgressObserver(Action<ArtifactDownloadProgress> report) : IProgress<ArtifactDownloadProgress>
    { public void Report(ArtifactDownloadProgress value) => report(value); }
}
