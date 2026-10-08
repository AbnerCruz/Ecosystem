namespace Lunet.Graphics;

/// <summary>Playback independente de um clip; avance em Update e desenhe Texture/Source pelo SpriteBatch.</summary>
/// <example><code>
/// var sheet = new SpriteSheet(texture, 16, 16);
/// var animator = new SpriteAnimator(SpriteAnimationClip.FromSheet(sheet, new[] { 0, 1 }, 0.1));
/// animator.Update(1.0 / 60);
/// batch.Draw(animator.Clip.Texture, position, animator.Source, Color.White);
/// </code></example>
/// <remarks>Sem alocação por Update. Deltas grandes pulam direto para o quadro atual, sem percorrer ciclos. Não dispara eventos para quadros pulados. Atualize na thread do jogo.</remarks>
public sealed class SpriteAnimator
{
    /// <summary>Cria um animador em reprodução no primeiro quadro.</summary>
    /// <param name="clip">Sequência compartilhada.</param>
    public SpriteAnimator(SpriteAnimationClip clip) => Clip = clip ?? throw new ArgumentNullException(nameof(clip));
    /// <summary>Clip atual.</summary>
    public SpriteAnimationClip Clip { get; private set; }
    /// <summary>Tempo no ciclo atual; no one-shot completo é igual à duração.</summary>
    public double PositionSeconds { get; private set; }
    /// <summary>Índice do quadro exibido.</summary>
    public int FrameIndex { get; private set; }
    /// <summary>Região do quadro exibido.</summary>
    public RectangleF Source => Clip.Frame(FrameIndex).Source;
    /// <summary>Se Update avança o relógio.</summary>
    public bool IsPlaying { get; private set; } = true;
    /// <summary>Se chegou ao fim de um clip sem loop; pausar não completa o clip.</summary>
    public bool IsComplete => !Clip.Loop && PositionSeconds >= Clip.DurationSeconds;

    /// <summary>Avança pelo tempo do jogo, em segundos.</summary>
    /// <param name="deltaSeconds">Delta finito e não negativo; zero não muda o estado.</param>
    public void Update(double deltaSeconds)
    {
        if (!double.IsFinite(deltaSeconds) || deltaSeconds < 0) throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
        if (!IsPlaying || deltaSeconds == 0) return;
        var duration = Clip.DurationSeconds;
        var remaining = duration - PositionSeconds;
        if (Clip.Loop)
        {
            var delta = deltaSeconds % duration;
            PositionSeconds = delta >= remaining ? delta - remaining : PositionSeconds + delta;
            if (PositionSeconds >= duration) PositionSeconds = 0;
        }
        else if (deltaSeconds >= remaining)
        {
            PositionSeconds = duration;
            IsPlaying = false;
        }
        else
        {
            PositionSeconds += deltaSeconds;
            if (PositionSeconds >= duration) IsPlaying = false;
        }
        FrameIndex = Clip.FindFrame(PositionSeconds);
    }
    /// <summary>Pausa sem perder posição.</summary>
    public void Pause() => IsPlaying = false;
    /// <summary>Retoma a posição pausada; um one-shot completo só volta com Restart/Play.</summary>
    public void Resume() => IsPlaying = !IsComplete;
    /// <summary>Recomeça no primeiro quadro e retoma reprodução.</summary>
    public void Restart()
    {
        PositionSeconds = 0;
        FrameIndex = 0;
        IsPlaying = true;
    }
    /// <summary>Troca o clip e reinicia; o mesmo clip continua sem reiniciar por padrão.</summary>
    /// <param name="clip">Novo clip.</param>
    /// <param name="restart">Forçar reinício mesmo se for o clip atual.</param>
    public void Play(SpriteAnimationClip clip, bool restart = false)
    {
        ArgumentNullException.ThrowIfNull(clip);
        if (!restart && ReferenceEquals(Clip, clip)) return;
        Clip = clip;
        Restart();
    }
}
