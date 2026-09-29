using System.Numerics;

namespace Lunet.Input;

/// <summary>
/// Joystick virtual de tela. Fica ativo enquanto um dedo que começou dentro de <see cref="Area"/> continua pressionado.
/// Em modo flutuante, o centro passa a ser onde o dedo tocou primeiro.
/// </summary>
public sealed class VirtualStick
{
    private int _touchId = -1;

    /// <summary>Cria um joystick de tela.</summary>
    /// <param name="center">Centro do joystick.</param>
    /// <param name="radius">Raio do movimento.</param>
    /// <param name="area">Área que ativa o toque</param>
    /// <param name="floating">Se verdadeiro, o centro passa a ser onde o dedo tocou.</param>
    public VirtualStick(Vector2 center, float radius, RectangleF? area = null, bool floating = false)
    {
        if (radius <= 0) throw new ArgumentOutOfRangeException(nameof(radius));
        HomeCenter = center;
        Center = center;
        Radius = radius;
        Area = area ?? new RectangleF(center.X - radius, center.Y - radius, radius * 2, radius * 2);
        Floating = floating;
    }

    /// <summary>Centro de repouso.</summary>
    public Vector2 HomeCenter { get; }
    /// <summary>Centro atual (segue o dedo no modo flutuante).</summary>
    public Vector2 Center { get; private set; }
    /// <summary>Raio do movimento.</summary>
    public float Radius { get; }
    /// <summary>Área que ativa o joystick.</summary>
    public RectangleF Area { get; }
    /// <summary>Se o centro segue o primeiro toque.</summary>
    public bool Floating { get; }
    /// <summary>Fração do raio ignorada perto do centro.</summary>
    public float DeadZone { get; set; } = 0.15f;

    /// <summary>Verdadeiro enquanto um dedo o controla.</summary>
    public bool IsActive => _touchId >= 0;

    /// <summary>Direção normalizada por <see cref="Radius"/> (comprimento 0–1); zero dentro da zona morta.</summary>
    public Vector2 Direction { get; private set; }

    /// <summary>Posição do "botão" do joystick, para desenhar.</summary>
    public Vector2 Knob => Center + Direction * Radius;

    /// <summary>Atualiza a direção com os toques atuais. Chame uma vez por passo.</summary>
    /// <param name="input">Estado de entrada.</param>
    public void Update(InputState input)
    {
        TouchPoint? mine = null;
        foreach (var t in input.Touches)
        {
            if (t.Id == _touchId) mine = t;
        }

        if (_touchId >= 0)
        {
            if (mine is not { IsDown: true })
            {
                _touchId = -1;
                Direction = Vector2.Zero;
                Center = HomeCenter;
                return;
            }
        }
        else
        {
            foreach (var t in input.Touches)
            {
                if (!t.IsDown || !Area.Contains(t.Position)) continue;
                _touchId = t.Id;
                if (Floating) Center = t.Position;
                mine = t;
                break;
            }
            if (_touchId < 0) return;
        }

        var offset = (mine!.Value.Position - Center) / Radius;
        var length = offset.Length();
        if (length > 1f) offset /= length;
        Direction = offset.Length() < DeadZone ? Vector2.Zero : offset;
    }
}

/// <summary>Botão circular de tela.</summary>
public sealed class VirtualButton
{
    private int _touchId = -1;
    private bool _wasDown;

    /// <summary>Cria um botão de tela.</summary>
    /// <param name="area">Área circular do botão.</param>
    public VirtualButton(Circle area) => Area = area;

    /// <summary>Área circular do botão.</summary>
    public Circle Area { get; }
    /// <summary>Verdadeiro enquanto um dedo o segura.</summary>
    public bool IsDown { get; private set; }

    /// <summary>Verdadeiro no primeiro <see cref="Update"/> depois de pressionado.</summary>
    public bool WasPressed { get; private set; }

    /// <summary>Atualiza o estado com os toques atuais. Chame uma vez por passo.</summary>
    /// <param name="input">Estado de entrada.</param>
    public void Update(InputState input)
    {
        var down = false;
        foreach (var t in input.Touches)
        {
            if (!t.IsDown) continue;
            if (t.Id == _touchId || (_touchId < 0 && Area.Contains(t.Position)))
            {
                _touchId = t.Id;
                down = true;
                break;
            }
        }
        if (!down) _touchId = -1;
        IsDown = down;
        WasPressed = down && !_wasDown;
        _wasDown = down;
    }
}
