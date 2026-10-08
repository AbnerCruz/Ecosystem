using System.Numerics;
using Lunet.Graphics;

namespace Lunet;

/// <summary>Curvas normalizadas de aceleração/desaceleração de uma transição.</summary>
/// <example><code>var tween = new Tween(0.5, Ease.OutQuad);</code></example>
public enum Ease
{
    /// <summary>Velocidade constante.</summary>
    Linear,
    /// <summary>Aceleração quadrática.</summary>
    InQuad,
    /// <summary>Desaceleração quadrática.</summary>
    OutQuad,
    /// <summary>Acelera até a metade e desacelera depois.</summary>
    InOutQuad,
    /// <summary>Curva cúbica com velocidade zero nas duas pontas.</summary>
    SmoothStep
}

/// <summary>Relógio de transição controlado pelo jogo; interpola valores sem callbacks ou alocações por quadro.</summary>
/// <example><code>
/// var tween = new Tween(0.5, Ease.OutQuad);
/// tween.Update(1.0 / 60);
/// Vector2 next = tween.Value(Vector2.Zero, new Vector2(100, 200));
/// </code></example>
/// <remarks>Crie uma vez e chame Restart para reutilizar. Não há scheduler global: o jogo decide o que avançar, pausar e aplicar. Cor interpola canais RGBA de 8 bits, sem correção gamma. Duração zero já começa completa.</remarks>
public sealed class Tween
{
    /// <summary>Cria uma transição em execução.</summary>
    /// <param name="durationSeconds">Duração finita e não negativa.</param>
    /// <param name="ease">Curva de interpolação.</param>
    public Tween(double durationSeconds, Ease ease = Ease.Linear)
    {
        if (!double.IsFinite(durationSeconds) || durationSeconds < 0) throw new ArgumentOutOfRangeException(nameof(durationSeconds));
        if (ease < Ease.Linear || ease > Ease.SmoothStep) throw new ArgumentOutOfRangeException(nameof(ease));
        DurationSeconds = durationSeconds;
        Ease = ease;
        Restart();
    }
    /// <summary>Duração em segundos.</summary>
    public double DurationSeconds { get; }
    /// <summary>Curva aplicada ao progresso.</summary>
    public Ease Ease { get; }
    /// <summary>Tempo transcorrido, limitado à duração.</summary>
    public double ElapsedSeconds { get; private set; }
    /// <summary>Se chegou ao valor final.</summary>
    public bool IsComplete => ElapsedSeconds >= DurationSeconds;
    /// <summary>Se Update avança o relógio.</summary>
    public bool IsPlaying { get; private set; }
    /// <summary>Progresso linear de 0 a 1; duração zero retorna 1.</summary>
    public float Progress => DurationSeconds == 0 ? 1 : (float)(ElapsedSeconds / DurationSeconds);
    /// <summary>Progresso de 0 a 1 depois da curva.</summary>
    public float Amount
    {
        get
        {
            var t = Progress;
            return Ease switch
            {
                Ease.InQuad => t * t,
                Ease.OutQuad => t * (2 - t),
                Ease.InOutQuad => t < 0.5f ? 2 * t * t : 1 - 2 * (1 - t) * (1 - t),
                Ease.SmoothStep => t * t * (3 - 2 * t),
                _ => t
            };
        }
    }
    /// <summary>Avança até o fim, preservando exatamente o endpoint.</summary>
    /// <param name="deltaSeconds">Tempo finito e não negativo.</param>
    public void Update(double deltaSeconds)
    {
        if (!double.IsFinite(deltaSeconds) || deltaSeconds < 0) throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
        if (!IsPlaying || deltaSeconds == 0) return;
        if (deltaSeconds >= DurationSeconds - ElapsedSeconds)
        {
            ElapsedSeconds = DurationSeconds;
            IsPlaying = false;
        }
        else
        {
            ElapsedSeconds += deltaSeconds;
            if (IsComplete) IsPlaying = false;
        }
    }
    /// <summary>Pausa sem alterar valores.</summary>
    public void Pause() => IsPlaying = false;
    /// <summary>Retoma se a transição ainda não está completa.</summary>
    public void Resume() => IsPlaying = !IsComplete;
    /// <summary>Reutiliza o relógio desde o início; duração zero continua completa.</summary>
    public void Restart()
    {
        ElapsedSeconds = 0;
        IsPlaying = DurationSeconds > 0;
    }
    /// <summary>Interpola um número; valores iniciais/finais devem ser finitos.</summary>
    /// <param name="from">Valor inicial.</param>
    /// <param name="to">Valor final.</param>
    /// <returns>Valor no progresso atual.</returns>
    public float Value(float from, float to)
    {
        if (!float.IsFinite(from)) throw new ArgumentOutOfRangeException(nameof(from), "Endpoint deve ser finito.");
        if (!float.IsFinite(to)) throw new ArgumentOutOfRangeException(nameof(to), "Endpoint deve ser finito.");
        var t = Amount;
        return t == 0 ? from : t == 1 ? to : (float)((1.0 - t) * from + (double)t * to);
    }
    /// <summary>Interpola posição, escala ou outro vetor de dois componentes finitos.</summary>
    /// <param name="from">Vetor inicial.</param>
    /// <param name="to">Vetor final.</param>
    /// <returns>Vetor no progresso atual.</returns>
    public Vector2 Value(Vector2 from, Vector2 to) => new(Value(from.X, to.X), Value(from.Y, to.Y));
    /// <summary>Interpola canais RGBA, arredondando para o byte mais próximo.</summary>
    /// <param name="from">Cor inicial.</param>
    /// <param name="to">Cor final.</param>
    /// <returns>Cor no progresso atual.</returns>
    public Color Value(Color from, Color to) => new((byte)MathF.Round(Value(from.R, to.R)), (byte)MathF.Round(Value(from.G, to.G)),
        (byte)MathF.Round(Value(from.B, to.B)), (byte)MathF.Round(Value(from.A, to.A)));
}
