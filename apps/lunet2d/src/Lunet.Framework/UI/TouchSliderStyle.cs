using Lunet.Graphics;

namespace Lunet.UI;

/// <summary>Paleta imutável de TouchSlider, sem recursos gráficos próprios.</summary>
/// <example><code>
/// var style = Lunet.UI.TouchSliderStyle.Default;
/// Color color = style.Fill;
/// </code></example>
/// <remarks>default é transparente; não é um tema global nem um formato persistente.</remarks>
public readonly struct TouchSliderStyle
{
    /// <summary>Cria as cores dos estados do slider.</summary>
    /// <param name="track">Trilho normal.</param>
    /// <param name="fill">Parte preenchida.</param>
    /// <param name="knob">Cursor normal.</param>
    /// <param name="pressedKnob">Cursor capturado.</param>
    /// <param name="disabledTrack">Trilho desabilitado.</param>
    /// <param name="disabledFill">Preenchimento desabilitado.</param>
    /// <param name="disabledKnob">Cursor desabilitado.</param>
    public TouchSliderStyle(Color track, Color fill, Color knob, Color pressedKnob, Color disabledTrack, Color disabledFill, Color disabledKnob)
    { Track = track; Fill = fill; Knob = knob; PressedKnob = pressedKnob; DisabledTrack = disabledTrack; DisabledFill = disabledFill; DisabledKnob = disabledKnob; }
    /// <summary>Trilho normal.</summary>
    public Color Track { get; }
    /// <summary>Parte preenchida.</summary>
    public Color Fill { get; }
    /// <summary>Cursor normal.</summary>
    public Color Knob { get; }
    /// <summary>Cursor capturado.</summary>
    public Color PressedKnob { get; }
    /// <summary>Trilho desabilitado.</summary>
    public Color DisabledTrack { get; }
    /// <summary>Preenchimento desabilitado.</summary>
    public Color DisabledFill { get; }
    /// <summary>Cursor desabilitado.</summary>
    public Color DisabledKnob { get; }
    /// <summary>Paleta azul com captura amarela e estado desabilitado cinza.</summary>
    public static TouchSliderStyle Default => new(new Color(50, 60, 80), new Color(35, 130, 190), Color.White, Color.Yellow, new Color(45, 45, 50), new Color(75, 75, 80), new Color(110, 110, 120));
}
