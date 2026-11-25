using System.Collections.Concurrent;

namespace Lunet;

/// <summary>Executa trabalho na thread do jogo. <see cref="Post"/> pode ser chamado de qualquer thread.</summary>
/// <example>
/// <code>
/// // De outra thread, peça ao jogo para rodar algo no início do próximo quadro.
/// Task.Run(() =&gt; dispatcher.Post(() =&gt; log.Info("carregou")));
/// </code>
/// </example>
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
    internal int RunPending(GameHost? host = null, Game? game = null)
    {
        var run = 0;
        var limit = _queue.Count; // ações postadas durante a execução ficam para o próximo quadro
        while (run < limit && _queue.TryDequeue(out var action))
        {
            if (host is not null && game is not null)
            {
                using var scope = Context.Enter(host, game, graphics: true);
                action();
            }
            else action();
            run++;
        }
        return run;
    }
}
