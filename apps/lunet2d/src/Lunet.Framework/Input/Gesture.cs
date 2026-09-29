using System.Numerics;

namespace Lunet.Input;

public enum GestureType { Tap, DoubleTap, LongPress, Drag, Swipe, Pinch, Rotate }

/// <summary>Gesto reconhecido. Posições e velocidades em coordenadas virtuais.</summary>
public readonly struct Gesture
{
    public Gesture(GestureType type, int touchId, Vector2 position, Vector2 delta, Vector2 velocity, float scale = 1f, float rotation = 0f)
    {
        Type = type; TouchId = touchId; Position = position; Delta = delta; Velocity = velocity; Scale = scale; Rotation = rotation;
    }

    public GestureType Type { get; }
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
