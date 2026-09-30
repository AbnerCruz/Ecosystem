namespace Lunet;

/// <summary>Gravidade de uma mensagem do <see cref="GameLog"/>.</summary>
/// <summary>Erro.</summary>
/// <summary>Aviso: algo estranho, mas o jogo segue.</summary>
/// <summary>Mensagem informativa.</summary>
public enum LogLevel
{
    /// <summary>Mensagem informativa.</summary>
    Info,
    /// <summary>Aviso: algo estranho, mas o jogo segue.</summary>
    Warning,
    /// <summary>Erro.</summary>
    Error,
}

/// <summary>Registro do jogo. A IDE (ou qualquer host) assina <see cref="Written"/>.</summary>
/// <example>
/// <code>
/// Log.Info("Fase carregada");
/// Log.Warning("Pouca memória");
/// Log.Written += (level, message) =&gt; Console.WriteLine($"[{level}] {message}");
/// </code>
/// </example>
public sealed class GameLog
{
    /// <summary>Ocorre a cada mensagem registrada. O Lunet assina este evento para preencher o Console.</summary>
    public event Action<LogLevel, string>? Written;

    /// <summary>Registra uma mensagem informativa.</summary>
    /// <param name="message">Texto da mensagem.</param>
    public void Info(string message) => Write(LogLevel.Info, message);
    /// <summary>Registra um aviso.</summary>
    /// <param name="message">Texto do aviso.</param>
    public void Warning(string message) => Write(LogLevel.Warning, message);
    /// <summary>Registra um erro.</summary>
    /// <param name="message">Texto do erro.</param>
    public void Error(string message) => Write(LogLevel.Error, message);

    /// <summary>Registra uma mensagem com a gravidade dada.</summary>
    /// <param name="level">Gravidade.</param>
    /// <param name="message">Texto da mensagem.</param>
    public void Write(LogLevel level, string message) => Written?.Invoke(level, message);
}
