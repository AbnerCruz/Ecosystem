using Lunet.Graphics;

namespace Lunet.UI;

/// <summary>Paleta imutável de um TouchButton; não contém texturas, fonte ou recursos a liberar.</summary>
/// <example><code>
/// var colors = Lunet.UI.TouchButtonStyle.Default;
/// Color pressed = colors.PressedBackground;
/// </code></example>
/// <remarks>default tem todas as cores transparentes; use Default para uma paleta visível.
/// É um estilo local de botão, sem tema global, skins, hierarquia ou formato persistente.</remarks>
public readonly struct TouchButtonStyle
{
    /// <summary>Cria a paleta dos estados do botão.</summary>
    /// <param name="background">Fundo normal.</param>
    /// <param name="pressedBackground">Fundo pressionado.</param>
    /// <param name="disabledBackground">Fundo desabilitado.</param>
    /// <param name="foreground">Texto habilitado.</param>
    /// <param name="disabledForeground">Texto desabilitado.</param>
    public TouchButtonStyle(Color background, Color pressedBackground, Color disabledBackground, Color foreground, Color disabledForeground)
    { Background = background; PressedBackground = pressedBackground; DisabledBackground = disabledBackground; Foreground = foreground; DisabledForeground = disabledForeground; }

    /// <summary>Fundo normal.</summary>
    public Color Background { get; }
    /// <summary>Fundo pressionado.</summary>
    public Color PressedBackground { get; }
    /// <summary>Fundo desabilitado.</summary>
    public Color DisabledBackground { get; }
    /// <summary>Texto habilitado.</summary>
    public Color Foreground { get; }
    /// <summary>Texto desabilitado.</summary>
    public Color DisabledForeground { get; }
    /// <summary>Paleta azul com texto branco e estado desabilitado cinza.</summary>
    public static TouchButtonStyle Default => new(new Color(50, 75, 120), new Color(35, 140, 190), new Color(50, 55, 65), Color.White, new Color(140, 145, 155));
}
