using System.Numerics;

namespace Lunet.Graphics;

/// <summary>Configuração imutável de vida, velocidade radial, gravidade e aparência das partículas.</summary>
/// <example><code>var effect = new ParticleSettings(lifetimeSeconds: 1.5, minSpeed: 40, maxSpeed: 120, gravity: new Vector2(0, 100));</code></example>
public sealed class ParticleSettings
{
    /// <summary>Configura um efeito radial; valores numéricos devem ser finitos.</summary>
    /// <param name="lifetimeSeconds">Vida positiva em segundos.</param>
    /// <param name="minSpeed">Velocidade mínima não negativa, em unidades por segundo.</param>
    /// <param name="maxSpeed">Velocidade máxima, pelo menos minSpeed.</param>
    /// <param name="gravity">Aceleração constante; padrão zero.</param>
    /// <param name="startSize">Tamanho inicial do quad em unidades do jogo, não negativo.</param>
    /// <param name="endSize">Tamanho final, não negativo.</param>
    /// <param name="startColor">Cor inicial; padrão branco.</param>
    /// <param name="endColor">Cor final; padrão branco transparente.</param>
    public ParticleSettings(double lifetimeSeconds = 1, float minSpeed = 50, float maxSpeed = 150, Vector2? gravity = null,
        float startSize = 8, float endSize = 0, Color? startColor = null, Color? endColor = null)
    {
        if (!double.IsFinite(lifetimeSeconds) || lifetimeSeconds <= 0) throw new ArgumentOutOfRangeException(nameof(lifetimeSeconds));
        if (!float.IsFinite(minSpeed) || minSpeed < 0) throw new ArgumentOutOfRangeException(nameof(minSpeed));
        if (!float.IsFinite(maxSpeed) || maxSpeed < minSpeed) throw new ArgumentOutOfRangeException(nameof(maxSpeed));
        if (!float.IsFinite(startSize) || startSize < 0) throw new ArgumentOutOfRangeException(nameof(startSize));
        if (!float.IsFinite(endSize) || endSize < 0) throw new ArgumentOutOfRangeException(nameof(endSize));
        var g = gravity ?? Vector2.Zero;
        if (!float.IsFinite(g.X) || !float.IsFinite(g.Y)) throw new ArgumentOutOfRangeException(nameof(gravity));
        LifetimeSeconds = lifetimeSeconds;
        MinSpeed = minSpeed; MaxSpeed = maxSpeed;
        Gravity = g; StartSize = startSize; EndSize = endSize;
        StartColor = startColor ?? Color.White;
        EndColor = endColor ?? Color.White.WithAlpha(0);
    }
    /// <summary>Vida em segundos.</summary>
    public double LifetimeSeconds { get; }
    /// <summary>Velocidade radial mínima.</summary>
    public float MinSpeed { get; }
    /// <summary>Velocidade radial máxima.</summary>
    public float MaxSpeed { get; }
    /// <summary>Aceleração constante.</summary>
    public Vector2 Gravity { get; }
    /// <summary>Tamanho inicial do quad.</summary>
    public float StartSize { get; }
    /// <summary>Tamanho final do quad.</summary>
    public float EndSize { get; }
    /// <summary>Cor inicial.</summary>
    public Color StartColor { get; }
    /// <summary>Cor final.</summary>
    public Color EndColor { get; }
}
