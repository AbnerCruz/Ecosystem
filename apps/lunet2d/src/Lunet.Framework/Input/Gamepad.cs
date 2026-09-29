using System.Numerics;

namespace Lunet.Input;

/// <summary>Botões de um controle; combináveis (flags).</summary>
[Flags]
public enum GamepadButtons
{
    /// <summary>Nenhum botão.</summary>
    None = 0,
    /// <summary>Botão A (baixo).</summary>
    A = 1 << 0,
    /// <summary>Botão B (direita).</summary>
    B = 1 << 1,
    /// <summary>Botão X (esquerda).</summary>
    X = 1 << 2,
    /// <summary>Botão Y (cima).</summary>
    Y = 1 << 3,
    /// <summary>Botão superior esquerdo (L1).</summary>
    LeftShoulder = 1 << 4,
    /// <summary>Botão superior direito (R1).</summary>
    RightShoulder = 1 << 5,
    /// <summary>Botão Voltar/Select.</summary>
    Back = 1 << 6,
    /// <summary>Botão Start.</summary>
    Start = 1 << 7,
    /// <summary>Clique do stick esquerdo.</summary>
    LeftStick = 1 << 8,
    /// <summary>Clique do stick direito.</summary>
    RightStick = 1 << 9,
    /// <summary>Direcional para cima.</summary>
    DPadUp = 1 << 10,
    /// <summary>Direcional para baixo.</summary>
    DPadDown = 1 << 11,
    /// <summary>Direcional para a esquerda.</summary>
    DPadLeft = 1 << 12,
    /// <summary>Direcional para a direita.</summary>
    DPadRight = 1 << 13,
}

/// <summary>Estado de um controle. Sticks em [−1, 1] (Y positivo = para baixo, como na tela); gatilhos em [0, 1].</summary>
/// <param name="IsConnected">Verdadeiro se há um controle conectado.</param>
/// <param name="Buttons">Botões pressionados no momento.</param>
/// <param name="LeftStick">Direção do stick esquerdo, de −1 a 1 em cada eixo.</param>
/// <param name="RightStick">Direção do stick direito, de −1 a 1 em cada eixo.</param>
/// <param name="LeftTrigger">Pressão do gatilho esquerdo, de 0 a 1.</param>
/// <param name="RightTrigger">Pressão do gatilho direito, de 0 a 1.</param>
public readonly record struct GamepadState(
    bool IsConnected,
    GamepadButtons Buttons,
    Vector2 LeftStick,
    Vector2 RightStick,
    float LeftTrigger,
    float RightTrigger)
{
    /// <summary>Diz se o botão (ou todos os botões combinados) está pressionado.</summary>
    /// <param name="button">Botão a testar.</param>
    /// <returns>Verdadeiro se pressionado.</returns>
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
