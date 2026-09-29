using System.Numerics;

namespace Lunet.Input;

/// <summary>Estado de entrada do quadro atual. Preenchido pelo host; leitura apenas para o jogo.</summary>
public sealed class InputState
{
    public const int MaxTouches = 10;
    private readonly TouchPoint[] _touches = new TouchPoint[MaxTouches];

    private readonly List<Gesture> _gestures = new(16);
    private readonly GestureRecognizer _recognizer = new();

    private readonly bool[] _keysDown = new bool[128];
    private readonly bool[] _keysPressed = new bool[128];

    public int TouchCount { get; private set; }

    /// <summary>Aceleração do aparelho em m/s² (eixos do aparelho; parado na mesa, Z ≈ 9,8). Zero sem sensor.</summary>
    public System.Numerics.Vector3 Accelerometer { get; private set; }

    public bool IsKeyDown(Keys key) => _keysDown[(int)key];

    /// <summary>Verdadeiro no primeiro passo de <c>Update</c> após a tecla ser pressionada.</summary>
    public bool IsKeyPressed(Keys key) => _keysPressed[(int)key];

    /// <summary>Usado pelo host: registra o estado de uma tecla.</summary>
    public void SetKey(Keys key, bool down)
    {
        var i = (int)key;
        if (i <= 0 || i >= _keysDown.Length) return;
        if (down && !_keysDown[i]) _keysPressed[i] = true;
        _keysDown[i] = down;
    }

    public void SetAccelerometer(System.Numerics.Vector3 value) => Accelerometer = value;

    internal void ClearPressed() => Array.Clear(_keysPressed);

    /// <summary>Gestos reconhecidos e ainda não consumidos; entregues no primeiro passo de <c>Update</c> após ocorrerem.</summary>
    public IReadOnlyList<Gesture> Gestures => _gestures;

    /// <summary>Ajustes de sensibilidade (limiares de arrasto, toque, pressão longa, deslize).</summary>
    public GestureRecognizer GestureSettings => _recognizer;

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

    internal void RecognizeGestures(double now)
    {
        if (_gestures.Count > 64) _gestures.RemoveRange(0, _gestures.Count - 64);
        _recognizer.Update(Touches, now, _gestures);
    }

    internal void ClearGestures()
    {
        _gestures.Clear();
        ClearPressed();
    }

    /// <summary>Substitui os toques atuais (usado pelo host).</summary>
    public void SetTouches(ReadOnlySpan<TouchPoint> touches)
    {
        var count = Math.Min(touches.Length, MaxTouches);
        touches[..count].CopyTo(_touches);
        TouchCount = count;
    }
}
