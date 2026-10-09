using Lunet.Input;

namespace Lunet.UI;

/// <summary>Área de conteúdo vertical rolável por toque, com arraste e inércia limitada.</summary>
/// <example><code>
/// var area = new Lunet.UI.TouchScrollArea(new RectangleF(12, 72, 336, 400), 900);
/// area.Update(input, (float)time.DeltaSeconds);
/// if (area.IsDragging) log.Info("rolando");
/// </code></example>
/// <remarks>Use SpriteBatch.Begin(clip: area.Bounds) e subtraia area.OffsetY das posições dos filhos.
/// Não consome eventos nem arbitra botões filhos; use IsDragging e WasDragged para evitar cliques durante arraste.
/// Não cria texturas nem altera dados de projetos. Sem alocações no Update.</remarks>
public sealed class TouchScrollArea
{
    private readonly int[] _previousDownIds = new int[InputState.MaxTouches];
    private int _previousCount;
    private RectangleF _bounds;
    private float _contentHeight;
    private float _offsetY;
    private int _touchId;
    private bool _captured;
    private bool _dragging;
    private bool _enabled = true;
    private float _pressY, _lastY;
    private double _velocity;

    /// <summary>Cria área com limites finitos e altura de conteúdo finita não negativa.</summary>
    /// <param name="bounds">Retângulo de rolagem em coordenadas virtuais.</param>
    /// <param name="contentHeight">Altura vertical total do conteúdo.</param>
    public TouchScrollArea(RectangleF bounds, float contentHeight)
    {
        Bounds = bounds;
        ContentHeight = contentHeight;
    }

    /// <summary>Área do painel; redimensionar limita automaticamente o deslocamento.</summary>
    public RectangleF Bounds
    {
        get => _bounds;
        set
        {
            if (!float.IsFinite(value.X) || !float.IsFinite(value.Y)
                || !float.IsFinite(value.Width) || !float.IsFinite(value.Height)
                || value.Width < 0 || value.Height < 0
                || !float.IsFinite(value.Right) || !float.IsFinite(value.Bottom))
                throw new ArgumentOutOfRangeException(nameof(value));
            _bounds = value;
            ClampOffset();
        }
    }

    /// <summary>Altura completa do conteúdo; alterar recalcula os limites da rolagem.</summary>
    public float ContentHeight
    {
        get => _contentHeight;
        set
        {
            if (!float.IsFinite(value) || value < 0)
                throw new ArgumentOutOfRangeException(nameof(value));
            _contentHeight = value;
            ClampOffset();
        }
    }

    /// <summary>Máximo deslocamento vertical permitido, em unidades virtuais.</summary>
    public float MaximumOffsetY => Math.Max(0, _contentHeight - _bounds.Height);

    /// <summary>Distância positiva já rolada desde o topo do conteúdo.</summary>
    public float OffsetY => _offsetY;

    /// <summary>Desabilitar cancela o gesto e a inércia, preservando a posição atual.</summary>
    public bool IsEnabled
    {
        get => _enabled;
        set
        {
            _enabled = value;
            if (!value) Cancel();
        }
    }

    /// <summary>Um dedo iniciado dentro da área está reservado para a rolagem.</summary>
    public bool IsCaptured => _captured;

    /// <summary>O dedo ultrapassou o limiar de 6 unidades e move o conteúdo.</summary>
    public bool IsDragging => _dragging;

    /// <summary>Pulso no Update em que um arraste terminou com Released; útil para inibir clique de filhos.</summary>
    public bool WasDragged { get; private set; }

    /// <summary>Define posição de rolagem e zera a inércia, respeitando o topo e a base.</summary>
    /// <param name="offsetY">Distância finita desejada desde o topo.</param>
    public void ScrollTo(float offsetY)
    {
        if (!float.IsFinite(offsetY)) throw new ArgumentOutOfRangeException(nameof(offsetY));
        _velocity = 0;
        SetOffset(offsetY);
    }

    /// <summary>Interrompe captura e inércia sem zerar o deslocamento.</summary>
    public void Cancel()
    {
        _captured = false;
        _dragging = false;
        WasDragged = false;
        _velocity = 0;
    }

    /// <summary>Processa toques por ID, soltura, limites e inércia usando delta do passo fixo.</summary>
    /// <param name="input">Estado dos dedos em coordenadas virtuais da área.</param>
    /// <param name="deltaSeconds">Delta finito não negativo, em segundos; não pressupõe 60 Hz.</param>
    public void Update(InputState input, float deltaSeconds)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!float.IsFinite(deltaSeconds) || deltaSeconds < 0)
            throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
        WasDragged = false;
        bool justReleased = false;

        if (!_enabled || _bounds.Width == 0 || _bounds.Height == 0 || MaximumOffsetY == 0)
            Cancel();
        else if (_captured)
        {
            if (!input.TouchCollection.TryGetById(_touchId, out var touch)
                || !float.IsFinite(touch.Position.X) || !float.IsFinite(touch.Position.Y)
                || (!touch.IsDown && touch.Phase != TouchPhase.Released))
                Cancel();
            else
            {
                double movement = (double)touch.Position.Y - _lastY;
                double total = (double)touch.Position.Y - _pressY;
                if (!_dragging && Math.Abs(total) >= 6)
                {
                    _dragging = true;
                    movement = total; // recupera todo o deslocamento desde Pressed.
                    _velocity = 0;
                }
                if (_dragging)
                {
                    double prior = _offsetY;
                    SetOffset(prior - movement);
                    // Não projeta inércia quando o dedo pressiona contra um limite.
                    if (movement != 0)
                    {
                        if (deltaSeconds > 0 && _offsetY != prior)
                            _velocity = Math.Clamp(-movement / deltaSeconds, -6000d, 6000d);
                        else
                            _velocity = 0;
                    }
                    else if (touch.Phase != TouchPhase.Released)
                    {
                        // Dedo parado reduz a velocidade; Released sem deslocamento não a apaga.
                        _velocity *= Math.Exp(-16d * deltaSeconds);
                    }
                }
                _lastY = touch.Position.Y;
                if (touch.Phase == TouchPhase.Released)
                {
                    WasDragged = _dragging;
                    _captured = false;
                    _dragging = false;
                    justReleased = true;
                    if (!WasDragged) _velocity = 0;
                }
            }
        }
        else
        {
            foreach (var touch in input.Touches)
            {
                if (touch.Phase != TouchPhase.Pressed || WasDown(touch.Id)
                    || !float.IsFinite(touch.Position.X) || !float.IsFinite(touch.Position.Y)
                    || !_bounds.Contains(touch.Position))
                    continue;

                _touchId = touch.Id;
                _captured = true;
                _dragging = false;
                _pressY = _lastY = touch.Position.Y;
                _velocity = 0;
                break;
            }
        }

        if (!_captured && !justReleased && _enabled && deltaSeconds > 0 && _velocity != 0)
        {
            double before = _offsetY;
            // Integral exata da velocidade exponencial: distância consistente em 60/90/120 Hz.
            double decay = Math.Exp(-16d * deltaSeconds);
            SetOffset(before + _velocity * (1d - decay) / 16d);
            _velocity *= decay;
            if (Math.Abs(_velocity) < 2d || _offsetY == before)
                _velocity = 0;
        }

        _previousCount = 0;
        foreach (var t in input.Touches)
            if (t.IsDown) _previousDownIds[_previousCount++] = t.Id;
    }

    private void ClampOffset() => SetOffset(_offsetY);

    private void SetOffset(double offset)
        => _offsetY = (float)Math.Clamp(offset, 0d, MaximumOffsetY);

    private bool WasDown(int id)
    {
        for (int i = 0; i < _previousCount; i++)
            if (_previousDownIds[i] == id) return true;
        return false;
    }
}
