using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Urbe.Client;

namespace Urbe.Client.Tests;

/// <summary>
/// UC-59: startup timings with a generated vault. Opt-in only (URBE_PERF=1, URBE_PERF_N=500 picks one size; lines go
/// to stdout and to URBE_PERF_LOG when set); otherwise they return at once and cost nothing in CI.
/// </summary>
public sealed partial class HeadlessUiTests
{
    internal static void PerfLog(string line)
    {
        Console.WriteLine(line);
        if (Environment.GetEnvironmentVariable("URBE_PERF_LOG") is { Length: > 0 } log) File.AppendAllText(log, line + "\n");
    }

    internal static bool PerfWanted(int count)
    {
        if (Environment.GetEnvironmentVariable("URBE_PERF") != "1") return false;
        var only = Environment.GetEnvironmentVariable("URBE_PERF_N");
        return only is null || only == count.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>A vault of <paramref name="count"/> notes in nested folders, each linking two others.</summary>
    internal static string GenerateVault(string vault, int count)
    {
        var rng = new Random(59);
        for (int i = 0; i < count; i++)
        {
            int top = i % 12, sub = i / 12 % 4;
            var folder = Path.Combine(vault, "Pasta " + top, "Sub " + sub);
            Directory.CreateDirectory(folder);
            var text = "# Nota " + i + "\n\n" + string.Join(" ", Enumerable.Repeat("Texto de exemplo para medir.", 12)) +
                "\n\n[[Nota " + rng.Next(count) + "]] e [[Nota " + rng.Next(count) + "]]\n";
            File.WriteAllText(Path.Combine(folder, "Nota " + i + ".md"), text);
        }
        return vault;
    }

    [Theory]
    [InlineData(500)]
    [InlineData(2000)]
    public async Task Perf_StartupWithGeneratedVault(int count)
    {
        if (!PerfWanted(count)) return;
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var dir = Path.Combine(Path.GetTempPath(), "urbe-perf-" + Guid.NewGuid().ToString("N"));
            var vault = GenerateVault(Path.Combine(dir, "vault"), count);
            var selection = Path.Combine(dir, "app", "sel.json");
            var device = new MemoryDeviceStore();
            try
            {
                var sw = Stopwatch.StartNew();
                var first = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(vault), selection));
                first.Customization.Device = device;
                var window = new Window { Width = 412, Height = 900, Content = first };
                window.Show();
                await first.OpenVaultAsync();
                PerfLog($"PERF {count} first open: {sw.ElapsedMilliseconds} ms");
                await first.FlushPendingAsync();
                first.OpenNoteByPath("Pasta 3/Sub 0/Nota 51.md");
                Dispatcher.UIThread.RunJobs();
                window.Close();

                sw.Restart();
                var again = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(vault), selection));
                again.Customization.Device = device;
                var window2 = new Window { Width = 412, Height = 900, Content = again };
                window2.Show();
                var init = again.InitializeAsync();
                for (int i = 0; i < 2000 && !(again.Editor.IsVisible && again.Editor.Current is not null) && !init.IsCompleted; i++)
                {
                    await Task.Delay(1);
                    Dispatcher.UIThread.RunJobs();
                }
                PerfLog($"PERF {count} reopen: editor shown after {sw.ElapsedMilliseconds} ms (visible={again.Editor.IsVisible})");
                await init;
                PerfLog($"PERF {count} reopen: InitializeAsync done {sw.ElapsedMilliseconds} ms");
                again.Editor.IsVisible = false;
                Dispatcher.UIThread.RunJobs();
                var explorer = Stopwatch.StartNew();
                again.ShowNotes();
                Dispatcher.UIThread.RunJobs();
                window2.CaptureRenderedFrame();
                PerfLog($"PERF {count} Notas open+render {explorer.ElapsedMilliseconds} ms");
                window2.Close();
                return true;
            }
            finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
        }, CancellationToken.None));
    }
}

public sealed class LibraryOpenPerfTests
{
    [Theory]
    [InlineData(500)]
    [InlineData(1000)]
    [InlineData(2000)]
    public void Perf_LibraryOpen(int count)
    {
        if (!HeadlessUiTests.PerfWanted(count)) return;
        var dir = Path.Combine(Path.GetTempPath(), "urbe-perfc-" + Guid.NewGuid().ToString("N"));
        var vault = HeadlessUiTests.GenerateVault(dir, count);
        var files = Directory.EnumerateFiles(vault, "*.md", SearchOption.AllDirectories).OrderBy(p => p, StringComparer.Ordinal)
            .Select(p => (Path.GetRelativePath(vault, p).Replace('\\', '/'), File.ReadAllText(p), "doc_" + Guid.NewGuid().ToString("N"))).ToList();
        Directory.Delete(dir, true);
        var sw = Stopwatch.StartNew();
        var library = Urbe.Core.NoteLibrary.Open(files, null, null);
        HeadlessUiTests.PerfLog($"PERF notes {count} first Open {sw.ElapsedMilliseconds} ms");
        var record = library.Serialize();
        sw.Restart();
        Urbe.Core.NoteLibrary.Open(files, record, null);
        HeadlessUiTests.PerfLog($"PERF notes {count} reopen Open {sw.ElapsedMilliseconds} ms");
    }
}
