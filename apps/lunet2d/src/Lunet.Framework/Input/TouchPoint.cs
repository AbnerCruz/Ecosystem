using System.Numerics;

namespace Lunet.Input;

public enum TouchPhase { Pressed, Moved, Released, Cancelled }

/// <summary>Um dedo na tela. <see cref="Position"/> está em coordenadas virtuais do jogo.</summary>
public readonly struct TouchPoint
{
    public TouchPoint(int id, TouchPhase phase, Vector2 position)
    {
        Id = id; Phase = phase; Position = position;
    }

    public int Id { get; }
    public TouchPhase Phase { get; }
    public Vector2 Position { get; }
    public bool IsDown => Phase is TouchPhase.Pressed or TouchPhase.Moved;
}
