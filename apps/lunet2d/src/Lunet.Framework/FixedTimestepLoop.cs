namespace Lunet;

/// <summary>Acumulador de passo fixo com limite de quadro. Não aloca.</summary>
public sealed class FixedTimestepLoop
{
    private readonly double _step;
    private readonly double _maxFrame;
    private double _accumulator;

    /// <summary>Cria o acumulador de passo fixo.</summary>
    /// <param name="updatesPerSecond">Atualizações por segundo.</param>
    /// <param name="maxFrameSeconds">Maior tempo real aceito por quadro.</param>
    public FixedTimestepLoop(int updatesPerSecond, double maxFrameSeconds)
    {
        if (updatesPerSecond < 1) throw new ArgumentOutOfRangeException(nameof(updatesPerSecond));
        _step = 1.0 / updatesPerSecond;
        _maxFrame = maxFrameSeconds;
    }

    /// <summary>Duração de um passo fixo, em segundos.</summary>
    public double StepSeconds => _step;
    /// <summary>Tempo simulado total, em segundos.</summary>
    public double TotalSeconds { get; private set; }

    /// <summary>Fração 0–1 do próximo passo já acumulada.</summary>
    public float Interpolation => (float)(_accumulator / _step);

    /// <summary>Avança o tempo real e devolve quantos passos fixos devem rodar.</summary>
    public int Advance(double elapsedSeconds)
    {
        if (elapsedSeconds < 0) elapsedSeconds = 0;
        if (elapsedSeconds > _maxFrame) elapsedSeconds = _maxFrame;
        _accumulator += elapsedSeconds;
        var steps = 0;
        while (_accumulator >= _step)
        {
            _accumulator -= _step;
            steps++;
        }
        return steps;
    }

    /// <summary>Marca a execução de um passo, avançando o tempo simulado.</summary>
    public GameTime CompleteStep()
    {
        TotalSeconds += _step;
        return new GameTime(TotalSeconds, (float)_step, 0f);
    }

    /// <summary>Zera o tempo acumulado e o tempo total.</summary>
    public void Reset()
    {
        _accumulator = 0;
        TotalSeconds = 0;
    }
}
