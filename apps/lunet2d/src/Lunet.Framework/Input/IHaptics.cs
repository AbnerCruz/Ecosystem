namespace Lunet.Input;

/// <summary>Vibração do aparelho.</summary>
/// <example>
/// <code>
/// haptics.Vibrate(30, 0.5f);
/// </code>
/// </example>
public interface IHaptics
{
    /// <summary>Vibra por <paramref name="milliseconds"/> ms com intensidade 0–1 (aparelhos sem controle de amplitude ignoram a intensidade).</summary>
    /// <param name="milliseconds">Duração da vibração, em milissegundos.</param>
    /// <param name="intensity">Intensidade de 0 a 1 (quando o aparelho suporta).</param>
    void Vibrate(int milliseconds, float intensity = 1f);

    /// <summary>Interrompe a vibração.</summary>
    void Cancel();
}

/// <summary>Sem vibração (padrão em testes e em aparelhos sem motor).</summary>
/// <example>
/// <code>
/// IHaptics none = new NullHaptics();
/// none.Vibrate(50);
/// </code>
/// </example>
public sealed class NullHaptics : IHaptics
{
    /// <summary>Não vibra.</summary>
    /// <param name="milliseconds">Ignorado.</param>
    /// <param name="intensity">Ignorado.</param>
    public void Vibrate(int milliseconds, float intensity = 1f) { }
    /// <summary>Não faz nada.</summary>
    public void Cancel() { }
}
