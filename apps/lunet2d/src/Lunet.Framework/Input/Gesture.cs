using System.Numerics;

namespace Lunet.Input;

public enum GestureType { Tap, LongPress, Drag, Swipe }

/// <summary>Gesto reconhecido. Posições e velocidades em coordenadas virtuais.</summary>
public readonly struct Gesture
{
    public Gesture(GestureType type, int touchId, Vector2 position, Vector2 delta, Vector2 velocity)
    {
        Type = type; TouchId = touchId; Position = position; Delta = delta; Velocity = velocity;
    }

    public GestureType Type { get; }
    public int TouchId { get; }

    /// <summary>Posição atual (Drag), final (Swipe) ou do toque (Tap/LongPress).</summary>
    public Vector2 Position { get; }

    /// <summary>Movimento desde o gesto Drag anterior (Drag) ou total (Swipe).</summary>
    public Vector2 Delta { get; }

    /// <summary>Unidades virtuais por segundo (Swipe).</summary>
    public Vector2 Velocity { get; }
}
