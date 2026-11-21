using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Threading;

namespace Urbe.Client.Tests;

public sealed partial class HeadlessUiTests
{
    // Opt-in (URBE_BENCH=1): what one key costs in a bare TextBox with the bench note, under each setting the Fonte uses.
    [Fact]
    public async Task Bench_FonteCostBreakdown()
    {
        if (Environment.GetEnvironmentVariable("URBE_BENCH") != "1") return;
        Assert.True(await Session.Dispatch<bool>(() =>
        {
            var note = BenchNote();
            foreach (var (name, make) in new (string, Func<TextBox>)[]
            {
                ("plain", () => new TextBox { AcceptsReturn = true }),
                ("wrap", () => new TextBox { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap }),
                ("wrap+lineheight", () => new TextBox { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, LineHeight = 24 }),
                ("wrap+lineheight+font", () => new TextBox { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, LineHeight = 24, FontFamily = UrbeTheme.UiFont }),
            })
            {
                var box = make();
                box.Text = note;
                var window = new Window { Width = 412, Height = 900, Content = new ScrollViewer { Content = box } };
                window.Show();
                box.Focus(); box.CaretIndex = note.Length / 2;
                window.KeyTextInput("a"); window.UpdateLayout();
                var sw = Stopwatch.StartNew();
                for (int i = 0; i < 60; i++) { window.KeyTextInput("a"); window.UpdateLayout(); }
                PerfLog($"FONTE {name} {sw.Elapsed.TotalMilliseconds / 60:0.00} ms/key");
                window.Close();
            }
            return true;
        }, CancellationToken.None));
    }
}
