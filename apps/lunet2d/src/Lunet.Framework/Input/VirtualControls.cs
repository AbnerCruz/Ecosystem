using System.Numerics;

namespace Lunet.Input;

/// <summary>
/// Joystick virtual de tela. Fica ativo enquanto um dedo que começou dentro de <see cref="Area"/> continua pressionado.
/// Em modo flutuante, o centro passa a ser onde o dedo tocou primeiro.
/// </summary>
/// <example>
/// <code>
/// var stick = new VirtualStick(new Vector2(70, 560), 50);
/// stick.Update(input);
/// position += stick.Direction * 120 * time.DeltaSeconds;
/// </code>
/// </example>
public sealed class VirtualStick
{
    private int _touchId = -1;
    private float _deadZone = 0.15f;
    private float _responseExponent = 1f;
    private float _sensitivity = 1f;

    /// <summary>Cria um joystick de tela.</summary>
    /// <param name="center">Centro do joystick.</param>
    /// <param name="radius">Raio do movimento.</param>
    /// <param name="area">Área que ativa o toque</param>
    /// <param name="floating">Se verdadeiro, o centro passa a ser onde o dedo tocou.</param>
    public VirtualStick(Vector2 center, float radius, RectangleF? area = null, bool floating = false)
    {
        if (!float.IsFinite(radius) || radius <= 0) throw new ArgumentOutOfRangeException(nameof(radius));
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
    public float DeadZone
    {
        get => _deadZone;
        set
        {
            if (!float.IsFinite(value) || value < 0f || value > 1f)
                throw new ArgumentOutOfRangeException(nameof(value));
            _deadZone = value;
        }
    }

    /// <summary>
    /// Quando verdadeiro, a intensidade cresce continuamente de zero após a zona morta.
    /// Por padrão é falso para preservar o comportamento anterior.
    /// </summary>
    public bool RescaleDeadZone { get; set; }

    /// <summary>
    /// Curva da intensidade: 1 é linear, maior que 1 facilita movimentos finos
    /// e menor que 1 responde mais cedo. Deve ser positivo e finito.
    /// </summary>
    public float ResponseExponent
    {
        get => _responseExponent;
        set
        {
            if (!float.IsFinite(value) || value <= 0f)
                throw new ArgumentOutOfRangeException(nameof(value));
            _responseExponent = value;
        }
    }

    /// <summary>Multiplicador de intensidade, limitado ao raio. Zero desativa a saída.</summary>
    public float Sensitivity
    {
        get => _sensitivity;
        set
        {
            if (!float.IsFinite(value) || value < 0f)
                throw new ArgumentOutOfRangeException(nameof(value));
            _sensitivity = value;
        }
    }

    /// <summary>
    /// Capturar apenas um dedo na fase Pressed impede que um arrasto iniciado
    /// fora da área ative o controle. Desativado por padrão por compatibilidade.
    /// </summary>
    public bool RequireFreshPress { get; set; }

    /// <summary>Verdadeiro enquanto um dedo o controla.</summary>
    public bool IsActive => _touchId >= 0;

    /// <summary>ID do dedo capturado, ou -1 quando livre. Útil para controles simultâneos.</summary>
    public int TouchId => _touchId;

    /// <summary>Direção normalizada por <see cref="Radius"/> (comprimento 0–1); zero dentro da zona morta.</summary>
    public Vector2 Direction { get; private set; }

    /// <summary>Posição do "botão" do joystick, para desenhar.</summary>
    public Vector2 Knob => Center + Direction * Radius;

    /// <summary>Solta o dedo, zera a direção e retorna ao centro de repouso.</summary>
    public void Cancel()
    {
        _touchId = -1;
        Direction = Vector2.Zero;
        Center = HomeCenter;
    }

    /// <summary>Atualiza a direção com os toques atuais. Chame uma vez por passo.</summary>
    /// <param name="input">Estado de entrada.</param>
    public void Update(InputState input) => Update(input, -1);

    /// <summary>
    /// Atualiza o controle sem capturar <paramref name="excludedTouchId"/>.
    /// Passe o TouchId de outro controle para evitar que dois analógicos
    /// utilizem o mesmo dedo, e atualize primeiro o controle prioritário.
    /// </summary>
    /// <param name="input">Estado de entrada.</param>
    /// <param name="excludedTouchId">ID de dedo reservado, ou -1 para nenhum.</param>
    public void Update(InputState input, int excludedTouchId)
    {
        if (_touchId >= 0 && _touchId == excludedTouchId)
        {
            Cancel();
            return;
        }

        TouchPoint? mine = null;
        foreach (var t in input.Touches)
        {
            if (t.Id == _touchId)
            {
                mine = t;
                break;
            }
        }

        if (_touchId >= 0)
        {
            if (mine is not { IsDown: true })
            {
                Cancel();
                return;
            }
        }
        else
        {
            foreach (var t in input.Touches)
            {
                if (!t.IsDown || t.Id == excludedTouchId || !Area.Contains(t.Position)
                    || (RequireFreshPress && t.Phase != TouchPhase.Pressed))
                    continue;

                _touchId = t.Id;
                if (Floating) Center = t.Position;
                mine = t;
                break;
            }
            if (_touchId < 0) return;
        }

        var offset = (mine!.Value.Position - Center) / Radius;
        var length = offset.Length();
        var magnitude = MathF.Min(length, 1f);
        if (length == 0f || magnitude < DeadZone || (RescaleDeadZone && DeadZone >= 1f))
        {
            Direction = Vector2.Zero;
            return;
        }

        if (RescaleDeadZone)
            magnitude = (magnitude - DeadZone) / (1f - DeadZone);

        if (ResponseExponent != 1f)
            magnitude = MathF.Pow(magnitude, ResponseExponent);
        magnitude = MathF.Min(1f, magnitude * Sensitivity);
        Direction = offset / length * magnitude;
    }
}

/// <summary>Botão circular de tela.</summary>
/// <example>
/// <code>
/// var jump = new VirtualButton(new Circle(new Vector2(300, 560), 40));
/// jump.Update(input);
/// if (jump.WasPressed) velocity.Y = -300;
/// </code>
/// </example>
public sealed class VirtualButton
{
    private int _touchId = -1;
    private bool _wasDown;

    /// <summary>Cria um botão de tela.</summary>
    /// <param name="area">Área circular do botão.</param>
    public VirtualButton(Circle area) => Area = area;

    /// <summary>Área circular do botão.</summary>
    public Circle Area { get; }
    /// <summary>ID do dedo capturado, ou -1 quando livre.</summary>
    public int TouchId => _touchId;

    /// <summary>
    /// Exige um Pressed dentro da área para iniciar captura; por padrão,
    /// permite capturar dedos já movidos para preservar jogos existentes.
    /// </summary>
    public bool RequireFreshPress { get; set; }

    /// <summary>Verdadeiro enquanto um dedo o segura.</summary>
    public bool IsDown { get; private set; }

    /// <summary>Verdadeiro no primeiro <see cref="Update"/> depois de pressionado.</summary>
    public bool WasPressed { get; private set; }

    /// <summary>Solta o dedo e zera o estado, inclusive o evento de pressão.</summary>
    public void Cancel()
    {
        _touchId = -1;
        _wasDown = false;
        IsDown = false;
        WasPressed = false;
    }

    /// <summary>Atualiza o estado com os toques atuais. Chame uma vez por passo.</summary>
    /// <param name="input">Estado de entrada.</param>
    public void Update(InputState input) => Update(input, -1);

    /// <summary>Atualiza ignorando um dedo reservado para outro controle.</summary>
    /// <param name="input">Estado de entrada.</param>
    /// <param name="excludedTouchId">ID reservado, ou -1 para nenhum.</param>
    public void Update(InputState input, int excludedTouchId)
    {
        if (_touchId >= 0 && _touchId == excludedTouchId)
        {
            Cancel();
            return;
        }

        var down = false;
        foreach (var t in input.Touches)
        {
            if (!t.IsDown || t.Id == excludedTouchId) continue;
            if (t.Id == _touchId
                || (_touchId < 0 && Area.Contains(t.Position)
                    && (!RequireFreshPress || t.Phase == TouchPhase.Pressed)))
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
