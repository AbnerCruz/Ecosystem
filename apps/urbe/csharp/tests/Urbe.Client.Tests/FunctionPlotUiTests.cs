using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Urbe.Client;
using Urbe.Client.Ui;

namespace Urbe.Client.Tests;

public sealed partial class HeadlessUiTests
{
    [Fact]
    public async Task Editor_AGraphBlockIsDrawnAndATapEditsItsText() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            // CST-069: ```grafico in the Visual mode is the graph of its functions
            var surface = new VisualSurface();
            var window = new Window { Width = 412, Height = 760, Content = new ScrollViewer { Content = surface } };
            window.Show();
            try
            {
                const string block = "```grafico\ntítulo: Ondas\ny = sin(x)\ny = x^2/8 - 1\ny = 2 +\nx: -6..6\ny: -2..3\n```";
                surface.SetText("Antes\n\n" + block + "\n\nDepois\n");
                for (int i = 0; i < 5; i++) { await Task.Delay(10); Dispatcher.UIThread.RunJobs(); }
                var plot = surface.GetVisualDescendants().OfType<FunctionPlotView>().Single();
                Assert.Equal(2, plot.Drawing.Curves.Count);
                Assert.Single(plot.Drawing.Errors);
                Assert.True(plot.Bounds.Width > 300 && plot.Bounds.Height > 200, plot.Bounds.ToString());
                Snapshot(window, "native-function-plot.png");

                // the block's text opens for editing, like a code block
                var start = surface.Text.IndexOf("```grafico", StringComparison.Ordinal);
                typeof(VisualSurface).GetMethod("Edit", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(surface, [start]);
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(block, surface.GetVisualDescendants().OfType<TextBox>().Single().Text);
                return true;
            }
            finally { window.Close(); }
        }, CancellationToken.None));
}
