using System.IO.Compression;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Urbe.Client;

namespace Urbe.Client.Tests;

public sealed partial class HeadlessUiTests
{
    // DEC-0059: a text over 2 MB showed "Sem visualização" and a .zip listed only 400 entries
    [Fact]
    public async Task FilePreview_ABigTextOpensInPartsAndAZipListsEveryEntry() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var (view, window, vault, dir) = await OpenCreateVault();
            try
            {
                var line = "linha de registro com algum texto para ocupar espaço\n";
                var big = string.Concat(Enumerable.Repeat(line, 3 * 1024 * 1024 / line.Length + 10));
                File.WriteAllText(Path.Combine(vault, "grande.log"), big);
                using (var zip = ZipFile.Open(Path.Combine(vault, "muitos.zip"), ZipArchiveMode.Create))
                    for (int i = 0; i < 600; i++) zip.CreateEntry("item" + i + ".txt");
                var preview = view.FilePreview;

                await preview.OpenAsync("grande.log");
                Assert.Equal("text", preview.Shown);
                Assert.Equal(big, preview.ShownText);   // the whole file is the preview's
                var more = preview.GetLogicalDescendants().OfType<Button>().First(b => (b.Content as string)?.StartsWith("Mostrar mais") == true);
                int Shown() => preview.GetLogicalDescendants().OfType<SelectableTextBlock>().Sum(t => t.Text?.Length ?? 0);
                var first = Shown();
                Assert.True(first < big.Length);
                while (preview.GetLogicalDescendants().OfType<Button>().FirstOrDefault(b => (b.Content as string)?.StartsWith("Mostrar mais") == true) is { } next)
                {
                    next.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                    Dispatcher.UIThread.RunJobs();
                }
                Assert.Equal(big.Length, Shown());

                await preview.OpenAsync("muitos.zip");
                Assert.Equal("zip", preview.Shown);
                Assert.Equal(600, preview.ShownText!.Split('\n').Length);
                return true;
            }
            finally { window.Close(); try { Directory.Delete(dir, true); } catch { } }
        }, CancellationToken.None));
}
