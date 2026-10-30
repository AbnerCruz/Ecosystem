using System.Text.RegularExpressions;
using Avalonia.Threading;

namespace Urbe.Client;

/// <summary>
/// The safety net of the native app: an error nobody caught is written to a file of the app's own data (never the
/// vault) with the time, where it happened and the stack, and an error on the interface thread is marked handled so the
/// Urbe keeps running instead of closing in the middle of a note. Configurações → Relatório de erros shows and copies it.
/// CST-097: what is written is the error, not the person's content: keys and tokens, quoted text longer than a few
/// words (a piece of a note, a prompt, a reply) and the paths of the device are taken out first, and an entry older than
/// <see cref="KeepDays"/> days leaves the file. Nothing in it is sent anywhere by the Urbe.
/// </summary>
public static partial class CrashLog
{
    public const int KeepDays = 30;

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
                var entry = "== " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture) + " · " + where + "\n" + Private(error.ToString()) + "\n\n";
                var old = File.Exists(FilePath) ? Recent(File.ReadAllText(FilePath), DateTime.Now) : "";
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
    /// <summary>The error's text without what could be the person's: secrets, long quoted text, device paths.</summary>
    public static string Private(string text)
    {
        text = SecretRegex().Replace(text, m => m.Groups["name"].Success ? m.Groups["name"].Value + "=…" : "…chave…");
        text = QuotedRegex().Replace(text, m => m.Groups[1].Value + "…" + m.Groups[2].Value.Length + " caracteres…" + m.Groups[3].Value);
        return PathRegex().Replace(text, m => "…/" + m.Groups["file"].Value);
    }

    /// <summary>The entries of the log from the last <see cref="KeepDays"/> days.</summary>
    public static string Recent(string log, DateTime now)
    {
        var kept = new System.Text.StringBuilder();
        foreach (Match m in EntryRegex().Matches(log))
        {
            var start = m.Index;
            var next = m.NextMatch();
            var entry = log[start..(next.Success ? next.Index : log.Length)];
            if (DateTime.TryParseExact(m.Groups[1].Value, "yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var at) && now - at <= TimeSpan.FromDays(KeepDays))
                kept.Append(entry);
        }
        return kept.ToString();
    }

    [GeneratedRegex(@"(?i)(?:sk-[a-z0-9_-]{12,}|bearer\s+[a-z0-9._~+/=-]{12,}|(?<name>api[_-]?key|key|token|password|authorization|secret)=[^&\s""']+)")]
    private static partial Regex SecretRegex();

    // "…" or “…” or '…' longer than 40 characters
    [GeneratedRegex(@"([""“'])([^""”'\n]{40,})([""”'])")]
    private static partial Regex QuotedRegex();

    // a path of the device (Android storage, home folders, Windows drives): only the file name stays
    [GeneratedRegex(@"(?:/storage/emulated/\d+|/sdcard|/data/user/\d+|/home/[^/\s]+|/Users/[^/\s]+|[A-Za-z]:\\Users\\[^\\\s]+)[^\s:""']*?[/\\](?<file>[^/\\\s:""']+)(?=[\s:""']|$)")]
    private static partial Regex PathRegex();

    [GeneratedRegex(@"^== (\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2})", RegexOptions.Multiline)]
    private static partial Regex EntryRegex();
}
