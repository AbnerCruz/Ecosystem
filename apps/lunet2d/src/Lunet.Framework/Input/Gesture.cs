using System.Numerics;

namespace Lunet.Input;

/// <summary>Tipos de gesto reconhecidos por <see cref="GestureRecognizer"/>.</summary>
/// <summary>Dois dedos girando um em torno do outro.</summary>
/// <summary>Dois dedos se aproximando ou afastando.</summary>
/// <summary>Arrasto rápido que termina ao soltar.</summary>
/// <summary>Dedo arrastando (um gesto por movimento).</summary>
/// <summary>Dedo parado por mais de meio segundo.</summary>
/// <summary>Dois toques rápidos no mesmo lugar.</summary>
/// <summary>Toque curto.</summary>
public enum GestureType
{
    /// <summary>Toque curto.</summary>
    Tap,
    /// <summary>Dois toques rápidos no mesmo lugar.</summary>
    DoubleTap,
    /// <summary>Dedo parado por mais de meio segundo.</summary>
    LongPress,
    /// <summary>Dedo arrastando (um gesto por movimento).</summary>
    Drag,
    /// <summary>Arrasto rápido que termina ao soltar.</summary>
    Swipe,
    /// <summary>Dois dedos se aproximando ou afastando.</summary>
    Pinch,
    /// <summary>Dois dedos girando um em torno do outro.</summary>
    Rotate,
}

/// <summary>Gesto reconhecido. Posições e velocidades em coordenadas virtuais.</summary>
public readonly struct Gesture
{
    /// <summary>Cria um gesto.</summary>
    /// <param name="type">Tipo.</param>
    /// <param name="touchId">Id do dedo.</param>
    /// <param name="position">Posição.</param>
    /// <param name="delta">Movimento.</param>
    /// <param name="velocity">Velocidade.</param>
    /// <param name="scale">Razão de pinça.</param>
    /// <param name="rotation">Rotação em radianos.</param>
    public Gesture(GestureType type, int touchId, Vector2 position, Vector2 delta, Vector2 velocity, float scale = 1f, float rotation = 0f)
    {
        Type = type; TouchId = touchId; Position = position; Delta = delta; Velocity = velocity; Scale = scale; Rotation = rotation;
    }

    /// <summary>Tipo do gesto.</summary>
    public GestureType Type { get; }
    /// <summary>Id do dedo que gerou o gesto.</summary>
    public int TouchId { get; }

    /// <summary>Posição atual (Drag), final (Swipe), central entre os dedos (Pinch/Rotate) ou do toque (Tap/DoubleTap/LongPress).</summary>
    public Vector2 Position { get; }

    /// <summary>Movimento desde o gesto Drag anterior (Drag) ou total (Swipe).</summary>
    public Vector2 Delta { get; }

    /// <summary>Unidades virtuais por segundo (Swipe).</summary>
    public Vector2 Velocity { get; }

    /// <summary>Pinch: razão entre a distância atual e a anterior dos dedos (&gt;1 afastando, &lt;1 aproximando).</summary>
    public float Scale { get; }

    /// <summary>Rotate: variação do ângulo entre os dedos, em radianos, desde o gesto anterior.</summary>
    public float Rotation { get; }
}
