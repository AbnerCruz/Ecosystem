namespace Lunet;

public enum LogLevel { Info, Warning, Error }

/// <summary>Registro do jogo. A IDE (ou qualquer host) assina <see cref="Written"/>.</summary>
public sealed class GameLog
{
    public event Action<LogLevel, string>? Written;

    public void Info(string message) => Write(LogLevel.Info, message);
    public void Warning(string message) => Write(LogLevel.Warning, message);
    public void Error(string message) => Write(LogLevel.Error, message);

    public void Write(LogLevel level, string message) => Written?.Invoke(level, message);
}
