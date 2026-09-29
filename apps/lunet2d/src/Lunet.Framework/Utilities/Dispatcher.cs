using System.Collections.Concurrent;

namespace Lunet;

/// <summary>Executa trabalho na thread do jogo. <see cref="Post"/> pode ser chamado de qualquer thread.</summary>
public sealed class Dispatcher
{
    private readonly ConcurrentQueue<Action> _queue = new();

    /// <summary>Agenda uma ação para rodar na thread do jogo, no início do próximo quadro. Pode ser chamado de qualquer thread.</summary>
    /// <param name="action">Ação a executar.</param>
    public void Post(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        _queue.Enqueue(action);
    }

    /// <summary>Roda o que está pendente (o host chama uma vez por quadro). Devolve quantas ações rodaram.</summary>
    internal int RunPending()
    {
        var run = 0;
        var limit = _queue.Count; // ações postadas durante a execução ficam para o próximo quadro
        while (run < limit && _queue.TryDequeue(out var action))
        {
            action();
            run++;
        }
        return run;
    }
}
