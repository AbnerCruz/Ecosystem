using System.Numerics;

namespace Lunet.Input;

/// <summary>Os toques do quadro atual. Valor leve, sem alocação; use em <c>foreach</c> ou por índice.</summary>
/// <example>
/// <code>
/// foreach (var touch in input.TouchCollection)
/// {
///     if (touch.IsDown) position = touch.Position;
/// }
/// </code>
/// </example>
public readonly ref struct TouchCollection
{
    private readonly ReadOnlySpan<TouchPoint> _touches;

    internal TouchCollection(ReadOnlySpan<TouchPoint> touches) => _touches = touches;

    /// <summary>Quantidade de toques.</summary>
    public int Count => _touches.Length;

    /// <summary>Toque pelo índice.</summary>
    /// <param name="index">Posição na coleção.</param>
    /// <returns>O toque.</returns>
    public TouchPoint this[int index] => _touches[index];

    /// <summary>Procura pelo id do dedo.</summary>
    /// <param name="id">Identificador do dedo.</param>
    /// <param name="touch">Recebe o toque encontrado.</param>
    /// <returns>Verdadeiro se o dedo está na lista.</returns>
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

    /// <summary>Permite usar foreach sem alocar memória.</summary>
    /// <returns>O enumerador.</returns>
    public ReadOnlySpan<TouchPoint>.Enumerator GetEnumerator() => _touches.GetEnumerator();
}

/// <summary>Ponteiro unificado: o primeiro dedo pressionado (toque) — uma abstração para jogos que só precisam de "um ponto".</summary>
/// <param name="IsDown">Verdadeiro enquanto o primeiro dedo está pressionado.</param>
/// <param name="WasPressed">Verdadeiro no primeiro passo de atualização depois de o dedo tocar a tela.</param>
/// <param name="WasReleased">Verdadeiro no primeiro passo de atualização depois de o dedo sair da tela.</param>
/// <param name="Position">Posição do dedo em coordenadas virtuais.</param>
/// <example>
/// <code>
/// Pointer pointer = input.Pointer;
/// if (pointer.WasPressed) log.Info("toque em " + pointer.Position);
/// </code>
/// </example>
public readonly record struct Pointer(bool IsDown, bool WasPressed, bool WasReleased, Vector2 Position);
