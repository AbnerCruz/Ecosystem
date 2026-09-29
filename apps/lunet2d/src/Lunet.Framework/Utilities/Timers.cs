namespace Lunet;

/// <summary>Referência a um timer criado por <see cref="Timers"/>; use para cancelar.</summary>
public sealed class TimerHandle
{
    internal TimerHandle(double interval, bool repeat, Action callback)
    {
        Remaining = interval;
        Interval = interval;
        Repeat = repeat;
        Callback = callback;
    }

    internal double Remaining;
    internal double Interval { get; }
    internal bool Repeat { get; }
    internal Action Callback { get; }
    /// <summary>Verdadeiro enquanto o timer não foi cancelado nem terminou.</summary>
    public bool IsActive { get; internal set; } = true;

    /// <summary>Cancela o timer.</summary>
    public void Cancel() => IsActive = false;
}

/// <summary>Timers no tempo do jogo (param quando o jogo está pausado). Avançados pelo host a cada passo fixo.</summary>
public sealed class Timers
{
    private readonly List<TimerHandle> _timers = new();
    private readonly List<TimerHandle> _pendingAdd = new();

    /// <summary>Executa a ação uma vez depois do tempo dado (no tempo do jogo).</summary>
    /// <param name="seconds">Espera em segundos, maior que zero.</param>
    /// <param name="callback">Ação a executar.</param>
    /// <returns>Referência para cancelar.</returns>
    public TimerHandle After(float seconds, Action callback) => Add(seconds, repeat: false, callback);

    /// <summary>Executa a ação repetidamente a cada intervalo.</summary>
    /// <param name="seconds">Intervalo em segundos, maior que zero.</param>
    /// <param name="callback">Ação a executar.</param>
    /// <returns>Referência para cancelar.</returns>
    public TimerHandle Every(float seconds, Action callback) => Add(seconds, repeat: true, callback);

    private TimerHandle Add(float seconds, bool repeat, Action callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        if (seconds <= 0) throw new ArgumentOutOfRangeException(nameof(seconds), "O intervalo deve ser positivo.");
        var handle = new TimerHandle(seconds, repeat, callback);
        _pendingAdd.Add(handle); // timers criados dentro de um callback só contam a partir do próximo passo
        return handle;
    }

    /// <summary>Quantos timers ainda estão ativos.</summary>
    public int ActiveCount => _timers.Count(t => t.IsActive) + _pendingAdd.Count(t => t.IsActive);

    internal void Update(double deltaSeconds)
    {
        if (_pendingAdd.Count > 0) { _timers.AddRange(_pendingAdd); _pendingAdd.Clear(); }
        for (var i = 0; i < _timers.Count; i++)
        {
            var t = _timers[i];
            if (!t.IsActive) continue;
            t.Remaining -= deltaSeconds;
            while (t.IsActive && t.Remaining <= 0)
            {
                t.Callback();
                if (t.Repeat) t.Remaining += t.Interval;
                else t.IsActive = false;
            }
        }
        _timers.RemoveAll(t => !t.IsActive);
    }
}
