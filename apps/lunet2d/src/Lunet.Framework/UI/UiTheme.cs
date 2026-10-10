using Lunet.Graphics;

namespace Lunet.UI;

/// <summary>Tema de cores imutável para controles de toque e áreas da interface.</summary>
/// <remarks>Reúne paletas de TouchButton e TouchSlider sem estado global, recursos gráficos,
/// atualização automática de controles, estilos de fonte ou arquivo persistente.
/// default é transparente; prefira Dark, Light, HighContrast ou uma instância personalizada.</remarks>
/// <example><code>
/// var theme = Lunet.UI.UiTheme.Dark;
/// var background = theme.Background;
/// var buttonStyle = theme.Button;
/// </code></example>
public readonly struct UiTheme
{
    /// <summary>Cria um conjunto de cores local; não altera controles existentes.</summary>
    /// <param name="background">Cor de fundo da tela.</param>
    /// <param name="panel">Cor dos painéis.</param>
    /// <param name="text">Cor de texto normal.</param>
    /// <param name="button">Paleta de botões, inclusive pressionado e desabilitado.</param>
    /// <param name="slider">Paleta de sliders, inclusive pressionado e desabilitado.</param>
    public UiTheme(Color background, Color panel, Color text, TouchButtonStyle button, TouchSliderStyle slider)
    {
        Background = background;
        Panel = panel;
        Text = text;
        Button = button;
        Slider = slider;
    }

    /// <summary>Cor de fundo para limpar ou preencher a tela.</summary>
    public Color Background { get; }
    /// <summary>Cor de fundo dos painéis e cards.</summary>
    public Color Panel { get; }
    /// <summary>Cor de texto geral fora dos controles.</summary>
    public Color Text { get; }
    /// <summary>Cores dos botões normais, pressionados e desabilitados.</summary>
    public TouchButtonStyle Button { get; }
    /// <summary>Cores dos sliders normais, pressionados e desabilitados.</summary>
    public TouchSliderStyle Slider { get; }

    /// <summary>Tema escuro com azul de destaque e texto claro.</summary>
    public static UiTheme Dark => new(
        new Color(18, 22, 31), new Color(30, 38, 53), new Color(244, 247, 252),
        new TouchButtonStyle(new Color(44, 91, 154), new Color(47, 128, 195),
            new Color(57, 63, 74), Color.White, new Color(178, 184, 196)),
        new TouchSliderStyle(new Color(68, 78, 97), new Color(36, 145, 213),
            Color.White, new Color(145, 221, 245),
            new Color(52, 58, 66), new Color(95, 107, 120), new Color(170, 176, 188)));

    /// <summary>Tema claro com superfícies claras e controles azuis.</summary>
    public static UiTheme Light => new(
        new Color(248, 250, 253), new Color(231, 237, 245), new Color(26, 41, 60),
        new TouchButtonStyle(new Color(37, 103, 179), new Color(21, 74, 132),
            new Color(200, 207, 217), Color.White, new Color(68, 76, 87)),
        new TouchSliderStyle(new Color(176, 188, 204), new Color(24, 103, 191),
            new Color(37, 72, 113), new Color(10, 51, 104),
            new Color(205, 211, 221), new Color(147, 158, 172), new Color(121, 134, 148)));

    /// <summary>Tema de alto contraste com texto branco no fundo e botões amarelos.</summary>
    public static UiTheme HighContrast => new(
        Color.Black, new Color(20, 20, 20), Color.White,
        new TouchButtonStyle(new Color(255, 230, 0), new Color(255, 178, 0),
            new Color(206, 206, 206), Color.Black, Color.Black),
        new TouchSliderStyle(Color.White, new Color(255, 230, 0), Color.Black,
            new Color(17, 52, 180), new Color(206, 206, 206),
            new Color(92, 92, 92), Color.Black));
}
