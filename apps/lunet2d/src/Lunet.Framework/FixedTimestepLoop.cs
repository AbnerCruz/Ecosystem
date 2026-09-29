namespace Lunet;

/// <summary>Acumulador de passo fixo com limite de quadro. Não aloca.</summary>
public sealed class FixedTimestepLoop
{
    private readonly double _step;
    private readonly double _maxFrame;
    private double _accumulator;

    public FixedTimestepLoop(int updatesPerSecond, double maxFrameSeconds)
    {
        if (updatesPerSecond < 1) throw new ArgumentOutOfRangeException(nameof(updatesPerSecond));
        _step = 1.0 / updatesPerSecond;
        _maxFrame = maxFrameSeconds;
    }

    public double StepSeconds => _step;
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

    public void Reset()
    {
        _accumulator = 0;
        TotalSeconds = 0;
    }
}
