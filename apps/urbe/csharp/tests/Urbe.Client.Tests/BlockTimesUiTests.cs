using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Urbe.Client;
using Urbe.Core;

namespace Urbe.Client.Tests;

public sealed partial class HeadlessUiTests
{
    [Fact]
    public async Task Editor_ShowsWhenEachBlockLastChangedWhenAsked() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        // CST-025: the time of each block's last edit, from the note's history, beside the block
        var dir = Path.Combine(Path.GetTempPath(), "urbe-block-times-" + Guid.NewGuid().ToString("N"));
        var vault = Path.Combine(dir, "vault");
        Directory.CreateDirectory(vault);
        const string now = "# Título\n\nAntigo.\n\nNovo.\n";
        File.WriteAllText(Path.Combine(vault, "N.md"), now);
        var storage = new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "app", "sel.json"));
        var view = new MainView(storage);
        view.Customization.Device = new MemoryDeviceStore();
        var window = new Window { Width = 412, Height = 860, Content = view };
        window.Show();
        try
        {
            await view.OpenVaultAsync();
            await Settle(window, view);
            var id = view.Documents!.Get("N.md")!.Id;
            var at = new DateTimeOffset(DateTime.Today.AddHours(9).AddMinutes(15));
            view.History.Capture(id, "N.md", "# Título\n\nAntigo.\n", now, at.ToUnixTimeMilliseconds());
            string[] Times() => view.Editor.GetVisualDescendants().OfType<TextBlock>().Where(t => t.Classes.Contains("urbe-block-time")).Select(t => t.Text ?? "").ToArray();

            view.OpenNoteByPath("N.md");
            for (int i = 0; i < 20; i++) { await Task.Delay(10); Dispatcher.UIThread.RunJobs(); }
            Assert.Empty(Times());   // off by default: reading stays clean

            view.Customization.BlockTimes = true;
            for (int i = 0; i < 20; i++) { await Task.Delay(10); Dispatcher.UIThread.RunJobs(); }
            var times = Times();
            // the new paragraph took the time of that edit; the older blocks, with no known creation date, show nothing
            Assert.Equal(["09:15"], times);
            // the time takes no width from the text (observation of 15/11/2026: a column of times squeezed every block on
            // the phone): the block drawn beside it is as wide as the blocks without a time
            foreach (var label in view.Editor.GetVisualDescendants().OfType<TextBlock>().Where(t => t.Classes.Contains("urbe-block-time")))
            {
                var holder = Assert.IsType<Grid>(label.Parent);
                Assert.Empty(holder.ColumnDefinitions);
                var drawn = holder.Children.First(c => !ReferenceEquals(c, label));
                Assert.Equal(holder.Bounds.Width, drawn.Bounds.Width + drawn.Margin.Left + drawn.Margin.Right, 1);
            }
            Snapshot(window, "native-block-times.png");
            return true;
        }
        finally { window.Close(); try { Directory.Delete(dir, true); } catch (IOException) { } }
    }, CancellationToken.None));
}
