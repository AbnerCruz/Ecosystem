namespace Lunet.Graphics;

/// <summary>Política de ajuste do espaço virtual do jogo à tela física, preservando a proporção.</summary>
/// <remarks>Fit não recorta e pode criar barras; Fill recorta bordas para cobrir toda a tela, sem distorcer sprites.</remarks>
/// <example>
/// <code>
/// Configuration.ViewportScaling = ViewportScalingMode.Fill;
/// // Em runtime: GraphicsDevice.ViewportScaling = ViewportScalingMode.Fit;
/// </code>
/// </example>
public enum ViewportScalingMode
{
    /// <summary>Mostra todo o mundo virtual (letterbox), preservando a proporção. Padrão.</summary>
    Fit = 0,

    /// <summary>Preenche a superfície física, cortando as bordas excedentes sem alterar as proporções.</summary>
    Fill = 1,
}
