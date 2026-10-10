using Lunet.Graphics;
using Lunet.Input;

namespace Lunet.UI;

/// <summary>Botão retangular: captura um dedo e confirma clique ao soltar dentro dos limites.</summary>
/// <example><code>
/// var button = new Lunet.UI.TouchButton(new RectangleF(20, 100, 160, 48));
/// button.Update(input);
/// if (button.WasClicked) log.Info("clicou");
/// </code></example>
/// <remarks>Atualize uma vez por passo, inclusive desabilitado. Usa IDs/fases de TouchPoint,
/// sem consumir input nem arbitrar controles sobrepostos. Cancel em OnPause/ao ocultar.
/// Não captura dedos que já estavam pressionados no Update anterior. Sem alocação no loop.</remarks>
public sealed class TouchButton
{
    private readonly int[] _previousDownIds = new int[InputState.MaxTouches];
    private int _previousCount;
    private int _touchId;
    private bool _captured;
    private bool _enabled = true;
    private RectangleF _bounds;

    /// <summary>Cria um botão com área finita não negativa.</summary>
    /// <param name="bounds">Área no mesmo espaço dos toques (normalmente coordenadas virtuais).</param>
    public TouchButton(RectangleF bounds) => Bounds = bounds;

    /// <summary>Área para desenho e toque. Altere antes de Update ao recalcular layout; não faz clipping.</summary>
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
            if (_bounds != value)
            {
                _bounds = value;
                Cancel(); // impedir clique de um dedo capturado antes do relayout.
            }
        }
    }

    /// <summary>Desabilitar cancela imediatamente a captura e o clique; reabilitar não retoma um dedo já acompanhado.</summary>
    public bool IsEnabled
    {
        get => _enabled;
        set { _enabled = value; if (!value) Cancel(); }
    }
    /// <summary>Há um dedo capturado, mesmo arrastado para fora do botão.</summary>
    public bool IsCaptured => _captured;
    /// <summary>O dedo capturado está pressionado dentro dos limites no último Update.</summary>
    public bool IsPressed { get; private set; }
    /// <summary>Verdadeiro só no Update que recebeu Released do dedo capturado dentro dos limites.</summary>
    public bool WasClicked { get; private set; }

    /// <summary>Cancela a interação sem clique; conserva o histórico de dedos para não recapturar um toque repetido.</summary>
    public void Cancel() { _captured = false; IsPressed = false; WasClicked = false; }

    /// <summary>Atualiza a captura/estado; desaparecimento ou Cancelled cancela sem clique.</summary>
    /// <param name="input">Snapshot do host com no máximo InputState.MaxTouches; posições no espaço de Bounds.</param>
    public void Update(InputState input)
    {
        ArgumentNullException.ThrowIfNull(input);
        WasClicked = false; IsPressed = false;
        if (_enabled)
        {
            if (_captured)
            {
                if (!input.TouchCollection.TryGetById(_touchId, out var touch)) _captured = false;
                else
                {
                    bool inside = _bounds.Contains(touch.Position);
                    if (touch.Phase == TouchPhase.Released)
                    { WasClicked = inside; _captured = false; }
                    else if (!touch.IsDown || !float.IsFinite(touch.Position.X) || !float.IsFinite(touch.Position.Y))
                        _captured = false;
                    else IsPressed = inside;
                }
                // Não transfira a interação para outro dedo no mesmo passo.
            }
            else
            {
                foreach (var touch in input.Touches)
                {
                    if (touch.Phase != TouchPhase.Pressed || WasDown(touch.Id) || !_bounds.Contains(touch.Position)) continue;
                    _touchId = touch.Id; _captured = true; IsPressed = true; break;
                }
            }
        }
        _previousCount = 0;
        foreach (var touch in input.Touches)
            if (touch.IsDown) _previousDownIds[_previousCount++] = touch.Id;
    }

    private bool WasDown(int id)
    {
        for (int i = 0; i < _previousCount; i++) if (_previousDownIds[i] == id) return true;
        return false;
    }

    /// <summary>Desenha fundo e texto centralizado usando o estado atual. Chame entre Begin/End.</summary>
    /// <param name="batch">SpriteBatch do jogo.</param>
    /// <param name="font">Fonte pertencente ao jogo; não é criada nem liberada pelo botão.</param>
    /// <param name="text">Texto; não faz wrap, ajuste automático nem recorte.</param>
    /// <param name="style">Cores dos estados; use TouchButtonStyle.Default para o estilo inicial.</param>
    /// <param name="textScale">Escala finita positiva do texto.</param>
    /// <remarks>Zero área não desenha. Reutiliza câmera, clipping e batching do SpriteBatch atual.</remarks>
    public void Draw(SpriteBatch batch, SpriteFont font, string text, TouchButtonStyle style, float textScale = 1)
    {
        ArgumentNullException.ThrowIfNull(batch); ArgumentNullException.ThrowIfNull(font); ArgumentNullException.ThrowIfNull(text);
        if (!float.IsFinite(textScale) || textScale <= 0) throw new ArgumentOutOfRangeException(nameof(textScale));
        if (_bounds.Width == 0 || _bounds.Height == 0) return;
        var size = font.Measure(text, textScale);
        if (!float.IsFinite(size.X) || !float.IsFinite(size.Y)) throw new OverflowException("O texto não cabe em coordenadas float.");
        batch.FillRect(_bounds, !_enabled ? style.DisabledBackground : IsPressed ? style.PressedBackground : style.Background);
        batch.DrawString(font, text, _bounds.Center - size / 2, _enabled ? style.Foreground : style.DisabledForeground, textScale);
    }
}
