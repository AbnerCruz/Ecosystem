using System.Numerics;
using Lunet.Graphics;
using Lunet.Input;

namespace Lunet.UI;

/// <summary>Slider horizontal por toque, com intervalo finito e passo opcional.</summary>
/// <example><code>
/// var slider = new Lunet.UI.TouchSlider(new RectangleF(24, 100, 312, 48), 0, 100, 50, 10);
/// slider.Update(input);
/// if (slider.WasChanged) log.Info("valor " + slider.Value);
/// </code></example>
/// <remarks>Um dedo é capturado ao começar dentro. O arraste fora é limitado ao intervalo;
/// Cancel/disable não desfazem valores já aplicados. Atualize inclusive desabilitado.
/// Sem consumo de input, foco ou arbitragem de controles sobrepostos; sem alocação no loop.</remarks>
public sealed class TouchSlider
{
    private readonly int[] _previousIds = new int[InputState.MaxTouches];
    private int _previousCount;
    private int _touchId;
    private bool _captured;
    private bool _enabled = true;
    private RectangleF _bounds;
    private float _value;

    /// <summary>Cria um slider; o valor inicial é limitado e quantizado.</summary>
    /// <param name="bounds">Área finita não negativa no espaço do input.</param>
    /// <param name="minimum">Limite inferior finito.</param>
    /// <param name="maximum">Limite superior finito, estritamente maior que minimum.</param>
    /// <param name="value">Valor inicial finito.</param>
    /// <param name="step">Passo finito não negativo; zero é contínuo. A grade parte de minimum.</param>
    /// <param name="knobWidth">Largura finita positiva do cursor, reduzida se Bounds for menor.</param>
    public TouchSlider(RectangleF bounds, float minimum = 0, float maximum = 1, float value = 0, float step = 0, float knobWidth = 20)
    {
        if (!float.IsFinite(minimum)) throw new ArgumentOutOfRangeException(nameof(minimum));
        if (!float.IsFinite(maximum) || maximum <= minimum) throw new ArgumentOutOfRangeException(nameof(maximum));
        if (!float.IsFinite(step) || step < 0) throw new ArgumentOutOfRangeException(nameof(step));
        if (!float.IsFinite(knobWidth) || knobWidth <= 0) throw new ArgumentOutOfRangeException(nameof(knobWidth));
        Minimum = minimum; Maximum = maximum; Step = step; KnobWidth = knobWidth;
        Bounds = bounds; Value = value;
    }

    /// <summary>Limite inferior, imutável.</summary>
    public float Minimum { get; }
    /// <summary>Limite superior, imutável.</summary>
    public float Maximum { get; }
    /// <summary>Passo da grade relativa ao mínimo; zero é contínuo.</summary>
    public float Step { get; }
    /// <summary>Largura desejada do cursor.</summary>
    public float KnobWidth { get; }
    /// <summary>Valor limitado/quantizado. Atribuição por código não publica WasChanged.</summary>
    public float Value
    {
        get => _value;
        set { if (!float.IsFinite(value)) throw new ArgumentOutOfRangeException(nameof(value)); _value = Quantize(value); }
    }
    /// <summary>Fração 0–1 do valor, calculada em double para intervalos extremos.</summary>
    public float NormalizedValue => (float)(((double)_value - Minimum) / ((double)Maximum - Minimum));
    /// <summary>Área para desenho/toque. Atualize antes de Update após mudar o layout.</summary>
    public RectangleF Bounds
    {
        get => _bounds;
        set
        {
            if (!float.IsFinite(value.X) || !float.IsFinite(value.Y) || !float.IsFinite(value.Width) || !float.IsFinite(value.Height)
                || value.Width < 0 || value.Height < 0 || !float.IsFinite(value.Right) || !float.IsFinite(value.Bottom))
                throw new ArgumentOutOfRangeException(nameof(value));
            _bounds = value; // mantém captura ativa: sliders seguem o dedo durante ajustes de layout.
        }
    }
    /// <summary>Desabilitar cancela a captura imediatamente, conservando o valor.</summary>
    public bool IsEnabled { get => _enabled; set { _enabled = value; if (!value) Cancel(); } }
    /// <summary>O slider acompanha um dedo, inclusive fora dos limites.</summary>
    public bool IsCaptured => _captured;
    /// <summary>Pulso do Update em que o toque mudou o valor final; o próximo Update limpa.</summary>
    public bool WasChanged { get; private set; }

    /// <summary>Cancela sem desfazer o valor; conserva histórico para ignorar Pressed retido.</summary>
    public void Cancel() { _captured = false; WasChanged = false; }

    /// <summary>Limites atuais do cursor, incluindo sua largura reduzida em áreas estreitas.</summary>
    /// <returns>RectangleF no mesmo espaço de Bounds.</returns>
    public RectangleF GetKnobBounds()
    {
        float width = Math.Min(KnobWidth, _bounds.Width);
        return new((float)(_bounds.X + (double)(_bounds.Width - width) * NormalizedValue), _bounds.Y, width, _bounds.Height);
    }

    /// <summary>Atualiza por ID; captura somente Pressed novo dentro, e ignora área sem percurso horizontal.</summary>
    /// <param name="input">Snapshot do host. Toques devem estar no mesmo espaço de Bounds.</param>
    /// <remarks>Released aplica a posição final e solta. Cancelled, dedo ausente ou posição não finita
    /// cancelam sem alterar o último valor. Cada Update produz no máximo um pulso WasChanged.</remarks>
    public void Update(InputState input)
    {
        ArgumentNullException.ThrowIfNull(input); WasChanged = false;
        float travel = Math.Max(0, _bounds.Width - KnobWidth);
        if (!_enabled || _bounds.Height == 0 || travel == 0) _captured = false;
        else if (_captured)
        {
            if (!input.TouchCollection.TryGetById(_touchId, out var touch) || !Finite(touch.Position)
                || (!touch.IsDown && touch.Phase != TouchPhase.Released)) _captured = false;
            else { Apply(touch.Position.X, travel); if (touch.Phase == TouchPhase.Released) _captured = false; }
        }
        else
        {
            foreach (var touch in input.Touches)
            {
                if (touch.Phase != TouchPhase.Pressed || WasDown(touch.Id) || !Finite(touch.Position) || !_bounds.Contains(touch.Position)) continue;
                _touchId = touch.Id; _captured = true; Apply(touch.Position.X, travel); break;
            }
        }
        _previousCount = 0;
        foreach (var touch in input.Touches) if (touch.IsDown) _previousIds[_previousCount++] = touch.Id;
    }

    private void Apply(float x, float travel)
    {
        double t = Math.Clamp(((double)x - _bounds.X - KnobWidth / 2d) / travel, 0, 1);
        float next = Quantize(Minimum + ((double)Maximum - Minimum) * t);
        WasChanged = next != _value; _value = next;
    }
    private float Quantize(double value)
    {
        if (value <= Minimum) return Minimum;
        if (value >= Maximum) return Maximum;
        if (Step > 0) value = Minimum + Math.Round((value - Minimum) / Step, MidpointRounding.AwayFromZero) * Step;
        return (float)Math.Clamp(value, Minimum, Maximum);
    }
    private bool WasDown(int id)
    { for (int i = 0; i < _previousCount; i++) if (_previousIds[i] == id) return true; return false; }
    private static bool Finite(Vector2 p) => float.IsFinite(p.X) && float.IsFinite(p.Y);

    /// <summary>Desenha trilho, preenchimento e cursor entre SpriteBatch.Begin/End.</summary>
    /// <param name="batch">SpriteBatch do jogo, com sua câmera/clip/state atuais.</param>
    /// <param name="style">Paleta; use TouchSliderStyle.Default para cores visíveis.</param>
    /// <remarks>Zero área não desenha; não cria fonte/textura e não desenha rótulo.</remarks>
    public void Draw(SpriteBatch batch, TouchSliderStyle style)
    {
        ArgumentNullException.ThrowIfNull(batch);
        if (_bounds.Width == 0 || _bounds.Height == 0) return;
        var knob = GetKnobBounds(); float trackHeight = Math.Min(8, _bounds.Height);
        var track = new RectangleF(_bounds.X + knob.Width / 2, _bounds.Center.Y - trackHeight / 2, _bounds.Width - knob.Width, trackHeight);
        batch.FillRect(track, _enabled ? style.Track : style.DisabledTrack);
        batch.FillRect(new(track.X, track.Y, track.Width * NormalizedValue, track.Height), _enabled ? style.Fill : style.DisabledFill);
        batch.FillRect(knob, !_enabled ? style.DisabledKnob : _captured ? style.PressedKnob : style.Knob);
    }
}
