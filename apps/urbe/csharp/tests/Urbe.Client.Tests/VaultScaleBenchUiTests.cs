using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Urbe.Client;

namespace Urbe.Client.Tests;

/// <summary>
/// CST-099: opening, selecting, moving and deleting measured on vaults of two sizes (200 and 2,000 notes), through the
/// real interface and a real folder. The times are printed (headless diagnostic, no claim about a phone); the budgets
/// only catch what grows by orders of magnitude, such as a write that reads the whole vault again (15/11/2026).
/// </summary>
public sealed partial class HeadlessUiTests
{
    [Theory]
    [InlineData(4, 50)]
    [InlineData(40, 50)]
    public async Task Bench_VaultOperationsAtScale(int folders, int perFolder) => Assert.True(await Session.Dispatch(async () =>
    {
        var dir = Path.Combine(Path.GetTempPath(), "urbe-scale-" + Guid.NewGuid().ToString("N"));
        var vault = Path.Combine(dir, "vault");
        for (int f = 0; f < folders; f++)
        {
            Directory.CreateDirectory(Path.Combine(vault, $"Pasta {f}"));
            for (int i = 0; i < perFolder; i++)
                File.WriteAllText(Path.Combine(vault, $"Pasta {f}", $"Nota {i}.md"), $"# Nota {f}-{i}\n\nTexto com [[Nota {(i + 1) % perFolder}]].\n" + new string('x', 800));
        }
        Directory.CreateDirectory(Path.Combine(vault, "Destino"));
        var previous = UrbeApp.LocalStatePath;
        UrbeApp.LocalStatePath = Path.Combine(dir, "app", "s.json");
        var view = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "app", "sel.json")));
        var window = new Window { Width = 412, Height = 900, Content = view };
        window.Show();
        var n = folders * perFolder;
        void Report(string what, long ms, long budget)
        {
            Console.WriteLine($"  vault.{what} {n} notas: {ms} ms");
            Assert.True(ms < budget, $"{what} com {n} notas levou {ms} ms (limite {budget} ms)");
        }
        try
        {
            var sw = Stopwatch.StartNew();
            await view.OpenVaultAsync();
            await Settle(window, view);
            Report("open", sw.ElapsedMilliseconds, 60_000);
            view.ShowNotes();
            view.Explorer.TryEnterFolder("Pasta 0");
            Dispatcher.UIThread.RunJobs();
            sw.Restart();
            view.Explorer.HoldNote("Pasta 0/Nota 0.md");
            for (int i = 1; i < 30; i++) view.Explorer.TryOpenNote($"Pasta 0/Nota {i}.md");
            Report("select30", sw.ElapsedMilliseconds, 2_000);
            var chosen = view.Explorer.SelectedPaths;
            sw.Restart();
            await view.MoveNotesToAsync(chosen, "Destino");
            Report("move30", sw.ElapsedMilliseconds, 30_000);
            Assert.Equal(30, Directory.GetFiles(Path.Combine(vault, "Destino")).Length);
            view.Explorer.ClearSelection();

            var trash = Enumerable.Range(0, 30).Select(i => $"Pasta 1/Nota {i}.md").ToList();
            var deleting = view.SelectionActionAsync("delete", trash);
            sw.Restart();
            long confirm = -1;
            for (int i = 0; i < 2000 && !deleting.IsCompleted; i++)
            {
                await Task.Delay(5);
                Dispatcher.UIThread.RunJobs();
                if (confirm < 0 && view.Dialogs.GetLogicalDescendants().OfType<Button>().FirstOrDefault(b => b.Content as string == "Mover para a lixeira") is { } yes)
                {
                    confirm = sw.ElapsedMilliseconds;
                    sw.Restart();
                    yes.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                }
            }
            await deleting;
            Report("deleteConfirmShown30", confirm, 10_000);
            Report("delete30", sw.ElapsedMilliseconds, 30_000);
            Assert.Equal(perFolder - 30, Directory.GetFiles(Path.Combine(vault, "Pasta 1")).Length);

            sw.Restart();
            Assert.False(await view.RefreshFromDiskAsync());
            Report("refreshUnchanged", sw.ElapsedMilliseconds, 30_000);
            return true;
        }
        finally
        {
            window.Close();
            UrbeApp.LocalStatePath = previous;
            try { Directory.Delete(dir, true); } catch { }
        }
    }, CancellationToken.None));
}
