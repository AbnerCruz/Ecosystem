using Lunet.Graphics;

namespace Lunet;

/// <summary>Configuração de um jogo: passo de atualização e resolução virtual.</summary>
/// <example>
/// <code>
/// Configuration.VirtualWidth = 720;
/// Configuration.VirtualHeight = 1280;
/// Configuration.UpdatesPerSecond = 60;
/// Configuration.ViewportScaling = ViewportScalingMode.Fill;
/// </code>
/// </example>
public sealed class GameConfiguration
{
    /// <summary>Atualizações por segundo. Padrão: 60.</summary>
    public int UpdatesPerSecond { get; set; } = 60;

    /// <summary>Largura da resolução virtual em que o jogo desenha.</summary>
    public int VirtualWidth { get; set; } = 360;

    /// <summary>Altura da resolução virtual em que o jogo desenha.</summary>
    public int VirtualHeight { get; set; } = 640;

    /// <summary>Ajuste de tela: Fit (padrão retrocompatível) ou Fill (sem barras, com recorte).</summary>
    public ViewportScalingMode ViewportScaling { get; set; } = ViewportScalingMode.Fit;

    /// <summary>Maior tempo real (s) processado por quadro; protege contra a espiral da morte.</summary>
    public double MaxFrameSeconds { get; set; } = 0.25;

    internal void Validate()
    {
        if (UpdatesPerSecond is < 1 or > 1000) throw new InvalidOperationException("UpdatesPerSecond deve estar entre 1 e 1000.");
        if (VirtualWidth < 1 || VirtualHeight < 1) throw new InvalidOperationException("A resolução virtual deve ser positiva.");
        if (MaxFrameSeconds <= 0) throw new InvalidOperationException("MaxFrameSeconds deve ser positivo.");
        if (ViewportScaling is not (ViewportScalingMode.Fit or ViewportScalingMode.Fill))
            throw new InvalidOperationException("ViewportScaling deve ser Fit ou Fill.");
    }
}
