namespace Lunet;

/// <summary>Tempo fornecido ao código do jogo, em segundos, durante a atualização e o desenho.</summary>
/// <example>
/// <code>
/// position += velocity * time.DeltaSeconds;
/// double seconds = time.TotalSeconds;
/// </code>
/// </example>
public readonly struct GameTime
{
    /// <summary>Cria um valor de tempo.</summary>
    /// <param name="totalSeconds">Tempo simulado total.</param>
    /// <param name="deltaSeconds">Duração do passo ou quadro.</param>
    /// <param name="interpolation">Fração entre a última atualização e a próxima.</param>
    public GameTime(double totalSeconds, float deltaSeconds, float interpolation)
    {
        TotalSeconds = totalSeconds;
        DeltaSeconds = deltaSeconds;
        Interpolation = interpolation;
    }

    /// <summary>Tempo simulado acumulado desde o início do jogo, em segundos. Avança com os passos executados.</summary>
    public double TotalSeconds { get; }

    /// <summary>Duração em segundos. Em Update, é o passo fixo configurado (1/60 s por padrão). Em Draw, é o intervalo real do quadro.</summary>
    public float DeltaSeconds { get; }

    /// <summary>Fração (0–1) entre a última atualização e a próxima, útil para interpolar o desenho.</summary>
    public float Interpolation { get; }
}
