using System.Numerics;

namespace Lunet.Input;

/// <summary>Estado de entrada do quadro atual. Preenchido pelo host; leitura apenas para o jogo.</summary>
public sealed class InputState
{
    public const int MaxTouches = 10;
    private readonly TouchPoint[] _touches = new TouchPoint[MaxTouches];

    public int TouchCount { get; private set; }

    public ReadOnlySpan<TouchPoint> Touches => _touches.AsSpan(0, TouchCount);

    /// <summary>Primeiro toque ativo, se houver.</summary>
    public bool TryGetPrimaryTouch(out TouchPoint touch)
    {
        for (var i = 0; i < TouchCount; i++)
        {
            if (_touches[i].IsDown) { touch = _touches[i]; return true; }
        }
        touch = default;
        return false;
    }

    /// <summary>Posição do primeiro toque ativo, se houver.</summary>
    public bool TryGetPointer(out Vector2 position)
    {
        if (TryGetPrimaryTouch(out var touch)) { position = touch.Position; return true; }
        position = default;
        return false;
    }

    /// <summary>Substitui os toques atuais (usado pelo host).</summary>
    public void SetTouches(ReadOnlySpan<TouchPoint> touches)
    {
        var count = Math.Min(touches.Length, MaxTouches);
        touches[..count].CopyTo(_touches);
        TouchCount = count;
    }
}
