using System.Numerics;

namespace Lunet.Input;

[Flags]
public enum GamepadButtons
{
    None = 0,
    A = 1 << 0, B = 1 << 1, X = 1 << 2, Y = 1 << 3,
    LeftShoulder = 1 << 4, RightShoulder = 1 << 5,
    Back = 1 << 6, Start = 1 << 7,
    LeftStick = 1 << 8, RightStick = 1 << 9,
    DPadUp = 1 << 10, DPadDown = 1 << 11, DPadLeft = 1 << 12, DPadRight = 1 << 13,
}

/// <summary>Estado de um controle. Sticks em [−1, 1] (Y positivo = para baixo, como na tela); gatilhos em [0, 1].</summary>
public readonly record struct GamepadState(
    bool IsConnected,
    GamepadButtons Buttons,
    Vector2 LeftStick,
    Vector2 RightStick,
    float LeftTrigger,
    float RightTrigger)
{
    public bool IsDown(GamepadButtons button) => (Buttons & button) == button && button != GamepadButtons.None;

    /// <summary>Aplica zona morta radial aos sticks (padrão do Android tem ruído perto de zero).</summary>
    public GamepadState WithDeadZone(float deadZone = 0.15f) =>
        this with { LeftStick = Dead(LeftStick, deadZone), RightStick = Dead(RightStick, deadZone) };

    private static Vector2 Dead(Vector2 stick, float zone)
    {
        var length = stick.Length();
        if (length < zone) return Vector2.Zero;
        var scaled = (length - zone) / (1f - zone);
        return stick / length * MathF.Min(1f, scaled);
    }
}
