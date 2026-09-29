namespace Lunet;

/// <summary>Tempo de uma atualização. <see cref="DeltaSeconds"/> é sempre o passo fixo configurado.</summary>
public readonly struct GameTime
{
    public GameTime(double totalSeconds, float deltaSeconds, float interpolation)
    {
        TotalSeconds = totalSeconds;
        DeltaSeconds = deltaSeconds;
        Interpolation = interpolation;
    }

    /// <summary>Tempo simulado desde o início do jogo.</summary>
    public double TotalSeconds { get; }

    /// <summary>Duração do passo de atualização (fixa nas atualizações; tempo real do quadro no desenho).</summary>
    public float DeltaSeconds { get; }

    /// <summary>Fração (0–1) entre a última atualização e a próxima, útil para interpolar o desenho.</summary>
    public float Interpolation { get; }
}
