using System.Numerics;

namespace Lunet.Input;

/// <summary>Os toques do quadro atual. Valor leve, sem alocação; use em <c>foreach</c> ou por índice.</summary>
public readonly ref struct TouchCollection
{
    private readonly ReadOnlySpan<TouchPoint> _touches;

    internal TouchCollection(ReadOnlySpan<TouchPoint> touches) => _touches = touches;

    public int Count => _touches.Length;

    public TouchPoint this[int index] => _touches[index];

    /// <summary>Procura pelo id do dedo.</summary>
    public bool TryGetById(int id, out TouchPoint touch)
    {
        foreach (var t in _touches)
        {
            if (t.Id == id) { touch = t; return true; }
        }
        touch = default;
        return false;
    }

    /// <summary>Quantos dedos estão pressionados.</summary>
    public int DownCount
    {
        get
        {
            var n = 0;
            foreach (var t in _touches) if (t.IsDown) n++;
            return n;
        }
    }

    public ReadOnlySpan<TouchPoint>.Enumerator GetEnumerator() => _touches.GetEnumerator();
}

/// <summary>Ponteiro unificado: o primeiro dedo pressionado (toque) — uma abstração para jogos que só precisam de "um ponto".</summary>
public readonly record struct Pointer(bool IsDown, bool WasPressed, bool WasReleased, Vector2 Position);
