using Lunet.Graphics;
using Lunet.Input;
namespace Lunet.UI;
/// <summary>Controle on/off por toque com mudança somente ao soltar dentro da área.</summary>
/// <example><code>
/// var toggle = new Lunet.UI.TouchToggle(new RectangleF(24, 100, 180, 52));
/// toggle.Update(input);
/// if (toggle.WasChanged) log.Info("alternado");
/// </code></example>
/// <remarks>Reutiliza captura por dedo de TouchButton. Não arbitra sobreposições nem salva Value; sem alocações no loop.</remarks>
public sealed class TouchToggle
{
    private readonly TouchButton _button;
    /// <summary>Cria uma alternância com estado inicial.</summary>
    /// <param name="bounds">Área finita do controle.</param>
    /// <param name="value">Valor inicial ligado/desligado.</param>
    public TouchToggle(RectangleF bounds, bool value = false)
    { _button = new TouchButton(bounds); Value = value; }

    /// <summary>Área de desenho e toque; acompanha novo layout.</summary>
    public RectangleF Bounds { get => _button.Bounds; set => _button.Bounds = value; }
    /// <summary>Desabilitar cancela captura sem alterar valor.</summary>
    public bool IsEnabled { get => _button.IsEnabled; set { _button.IsEnabled = value; if (!value) WasChanged = false; } }
    /// <summary>Capturou um dedo, mesmo se saiu da área.</summary>
    public bool IsCaptured => _button.IsCaptured;
    /// <summary>Dedo capturado ainda está dentro.</summary>
    public bool IsPressed => _button.IsPressed;
    /// <summary>Estado da opção, que pode ser definido por código sem disparar evento.</summary>
    public bool Value { get; set; }
    /// <summary>Pulso de alteração somente no Update que confirmou Released válido.</summary>
    public bool WasChanged { get; private set; }

    /// <summary>Cancela a captura sem alterar valor.</summary>
    public void Cancel() { _button.Cancel(); WasChanged = false; }

    /// <summary>Processa toques e alterna somente ao soltar o dedo capturado dentro da área.</summary>
    /// <param name="input">Toques nas coordenadas de Bounds.</param>
    public void Update(InputState input)
    {
        WasChanged = false;
        _button.Update(input);
        if (_button.WasClicked) { Value = !Value; WasChanged = true; }
    }

    /// <summary>Desenha estado ligado/desligado e pressionado/desabilitado, usando as paletas existentes.</summary>
    /// <param name="batch">SpriteBatch em Begin/End.</param>
    /// <param name="font">Fonte pertencente ao jogo.</param>
    /// <param name="offText">Rótulo desligado.</param>
    /// <param name="onText">Rótulo ligado.</param>
    /// <param name="offStyle">Estilo desligado.</param>
    /// <param name="onStyle">Estilo ligado.</param>
    /// <param name="textScale">Escala finita positiva.</param>
    public void Draw(SpriteBatch batch, SpriteFont font, string offText, string onText, TouchButtonStyle offStyle, TouchButtonStyle onStyle, float textScale = 1)
    {
        ArgumentNullException.ThrowIfNull(offText);
        ArgumentNullException.ThrowIfNull(onText);
        _button.Draw(batch, font, Value ? onText : offText, Value ? onStyle : offStyle, textScale);
    }
}
