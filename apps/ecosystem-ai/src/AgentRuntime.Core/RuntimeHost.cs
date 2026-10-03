using System.Threading.Channels;

namespace AgentRuntime;

/// <summary>
/// Hospedagem dirigida por eventos (plano §5.3, T-9): o runtime não tem timer nem laço de espera ocupada. Sem submissões, o laço
/// fica suspenso aguardando a fila — custo ≈ 0, nenhuma chamada a provedor. Quem reativa o runtime é um evento (uma submissão).
/// </summary>
public sealed class RuntimeHost(AgentRunner runner)
{
    private readonly Channel<(RunRequest Request, TaskCompletionSource<RunResult> Completion)> _queue =
        Channel.CreateUnbounded<(RunRequest, TaskCompletionSource<RunResult>)>(new UnboundedChannelOptions { SingleReader = true });

    public Task<RunResult> SubmitAsync(RunRequest request)
    {
        var completion = new TaskCompletionSource<RunResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_queue.Writer.TryWrite((request, completion))) throw new InvalidOperationException("O host foi encerrado.");
        return completion.Task;
    }

    /// <summary>Encerra a fila: o laço termina depois de processar o que já foi submetido.</summary>
    public void Complete() => _queue.Writer.TryComplete();

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        await foreach (var (request, completion) in _queue.Reader.ReadAllAsync(cancellationToken))
        {
            try { completion.TrySetResult(await runner.RunAsync(request, cancellationToken)); }
            catch (OperationCanceledException) { completion.TrySetCanceled(cancellationToken); throw; }
            catch (Exception ex) { completion.TrySetException(ex); }
        }
    }
}
