using Avalonia.Threading;

namespace Urbe.Client;

/// <summary>
/// The safety net of the native app: an error nobody caught is written to a file of the app's own data (never the
/// vault) with the time, where it happened and the stack, and an error on the interface thread is marked handled so the
/// Urbe keeps running instead of closing in the middle of a note. Configurações → Relatório de erros shows and copies it.
/// </summary>
public static class CrashLog
{
    private const int MaxBytes = 64 * 1024;
    private static readonly object Gate = new();
    private static bool _installed;

    /// <summary>The log file; each head points it at its private data (Android: the app's files).</summary>
    public static string FilePath { get; set; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Urbe", "erros.log");

    /// <summary>An error was written (the interface tells the person once).</summary>
    public static event Action<Exception>? Reported;

    /// <summary>The errors written since the app opened (tests).</summary>
    public static int Count { get; private set; }

    public static void Install()
    {
        if (_installed) return;
        _installed = true;
        Dispatcher.UIThread.UnhandledException += (_, e) =>
        {
            Write(e.Exception, "interface");
            e.Handled = true;
        };
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Write(e.Exception, "tarefa");
            e.SetObserved();
        };
        AppDomain.CurrentDomain.UnhandledException += (_, e) => { if (e.ExceptionObject is Exception x) Write(x, "processo"); };
    }

    public static void Write(Exception error, string where)
    {
        lock (Gate)
        {
            Count++;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                var entry = "== " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture) + " · " + where + "\n" + error + "\n\n";
                var old = File.Exists(FilePath) ? File.ReadAllText(FilePath) : "";
                var all = old + entry;
                if (all.Length > MaxBytes) all = all[^MaxBytes..];   // the most recent errors
                File.WriteAllText(FilePath, all);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        }
        try { Dispatcher.UIThread.Post(() => Reported?.Invoke(error)); }
        catch (Exception) { }
    }

    /// <summary>The log's text, or "" when nothing was written.</summary>
    public static string Read()
    {
        try { return File.Exists(FilePath) ? File.ReadAllText(FilePath) : ""; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return ""; }
    }

    public static void Clear()
    {
        try { File.Delete(FilePath); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }
}
