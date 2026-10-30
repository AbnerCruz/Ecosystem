namespace Urbe.Client;

/// <summary>
/// CST-007: what a failure of the storage means for the person, in their words: the system's messages ("Access to
/// the path is denied", "No space left on device") say what to do, not what broke inside.
/// </summary>
public static class StorageProblems
{
    public static string Explain(Exception error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return error switch
        {
            UnauthorizedAccessException =>
                "O Urbe não tem permissão para gravar nesta pasta. Abra a pasta de novo em Configurações → Pasta no dispositivo.",
            FileNotFoundException or DirectoryNotFoundException =>
                "O arquivo ou a pasta não está mais lá: pode ter sido movido ou apagado fora do Urbe.",
            IOException io when DiskFull(io) => "Não há espaço livre no aparelho. Libere espaço e tente de novo.",
            _ => error.Message
        };
    }

    // ENOSPC (28) on Android and Linux, ERROR_DISK_FULL (112) and ERROR_HANDLE_DISK_FULL (39) on Windows
    private static bool DiskFull(IOException error) =>
        (error.HResult & 0xFFFF) is 28 or 39 or 112 ||
        error.Message.Contains("No space left", StringComparison.OrdinalIgnoreCase) ||
        error.Message.Contains("not enough space", StringComparison.OrdinalIgnoreCase);
}
