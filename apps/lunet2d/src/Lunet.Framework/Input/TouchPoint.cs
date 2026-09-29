using System.Numerics;

namespace Lunet.Input;

/// <summary>Fase de um dedo na tela.</summary>
/// <summary>O sistema cancelou o toque.</summary>
/// <summary>O dedo saiu da tela.</summary>
/// <summary>O dedo está na tela.</summary>
/// <summary>O dedo acabou de tocar a tela.</summary>
public enum TouchPhase
{
    /// <summary>O dedo acabou de tocar a tela.</summary>
    Pressed,
    /// <summary>O dedo está na tela.</summary>
    Moved,
    /// <summary>O dedo saiu da tela.</summary>
    Released,
    /// <summary>O sistema cancelou o toque.</summary>
    Cancelled,
}

/// <summary>Um dedo na tela. <see cref="Position"/> está em coordenadas virtuais do jogo.</summary>
public readonly struct TouchPoint
{
    /// <summary>Cria um toque.</summary>
    /// <param name="id">Id do dedo.</param>
    /// <param name="phase">Fase.</param>
    /// <param name="position">Posição em coordenadas virtuais.</param>
    public TouchPoint(int id, TouchPhase phase, Vector2 position)
    {
        Id = id; Phase = phase; Position = position;
    }

    /// <summary>Id do dedo, estável enquanto ele está na tela.</summary>
    public int Id { get; }
    /// <summary>Fase do toque.</summary>
    public TouchPhase Phase { get; }
    /// <summary>Posição em coordenadas virtuais.</summary>
    public Vector2 Position { get; }
    /// <summary>Verdadeiro se o dedo está na tela (Pressed ou Moved).</summary>
    public bool IsDown => Phase is TouchPhase.Pressed or TouchPhase.Moved;
}
