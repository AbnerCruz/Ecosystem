using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Urbe.Client;

namespace Urbe.Client.Tests;

public sealed partial class HeadlessUiTests
{
    // CST-129: "demora para salvar e sair do editor"

    [Fact]
    public async Task Leave_ANoteOnlyReadLeavesTheAreaAsItWas() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var (view, window, dir, _) = await OpenRealVaultAsync(("Casa/Lista.md", "# Lista\n\n- pão\n"), ("Trabalho/Plano.md", "# Plano\n"));
        try
        {
            TextBlock Box(string name = "Casa") => view.Workspace.GetLogicalDescendants().OfType<TextBlock>().First(t => t.Text == name);
            var before = Box();
            var other = Box("Trabalho");
            view.OpenNoteByPath("Casa/Lista.md");
            await Settle(window, view);
            await view.Editor.BackAsync();
            await Settle(window, view);
            Assert.Same(before, Box());   // nothing a card shows changed: the Área de trabalho was not built again

            view.OpenNoteByPath("Casa/Lista.md");
            await Settle(window, view);
            view.Editor.Editor.Text = "# Lista\n\n- café\n- pão\n";
            await view.Editor.BackAsync();
            await Settle(window, view);
            Assert.NotSame(before, Box());   // an edit shows on the cards
            Assert.Same(other, Box("Trabalho"));   // only on its project's box
            Assert.Contains(view.Workspace.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text?.Contains("café") == true);
            return true;
        }
        finally { window.Close(); try { Directory.Delete(dir, true); } catch { } }
    }, CancellationToken.None));

    [Fact]
    public async Task Leave_TheScreenAnswersWhileTheVaultIsCheckedAndWritten() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        // the check of the whole folder before and after the write ran on the screen's thread between its two reads
        var dir = Path.Combine(Path.GetTempPath(), "urbe-leave-" + Guid.NewGuid().ToString("N"));
        var vault = Path.Combine(dir, "vault");
        for (int i = 0; i < 1500; i++)
        {
            Directory.CreateDirectory(Path.Combine(vault, "Pasta " + i % 30));
            File.WriteAllText(Path.Combine(vault, "Pasta " + i % 30, "Nota " + i + ".md"), "# Nota " + i + "\n\n" + string.Concat(Enumerable.Repeat("Texto da nota com [[link]] e #tag. ", 20)) + "\n");
        }
        var book = new System.Text.StringBuilder("# Livro\n\n");
        for (int i = 0; i < 600; i++) book.Append("## Capítulo ").Append(i).Append("\n\nTexto **forte** e [[Nota ").Append(i % 50).Append("]] com mais palavras para encher a linha.\n\n- [ ] tarefa\n\n");
        File.WriteAllText(Path.Combine(vault, "Livro.md"), book.ToString());
        var storage = new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "app", "sel.json"));
        try
        {
            Assert.NotNull(await storage.PickAsync());
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var save = storage.SaveExistingTextFileAsync("Livro.md", book + "Novo.\n");
            long last = 0, gap = 0;
            void Beat()
            {
                var now = sw.ElapsedMilliseconds;
                gap = Math.Max(gap, now - last);
                last = now;
                if (!save.IsCompleted) Dispatcher.UIThread.Post(Beat, DispatcherPriority.Input);
            }
            Beat();
            while (!save.IsCompleted) await Task.Delay(1);
            await save;
            var total = sw.ElapsedMilliseconds;
            Assert.EndsWith("Novo.\n", File.ReadAllText(Path.Combine(vault, "Livro.md")));
            Assert.True(gap < Math.Max(40, total / 3), $"the screen stood still {gap} ms of a save of {total} ms");
            return true;
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }, CancellationToken.None));
}
