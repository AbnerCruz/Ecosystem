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

    [Fact]
    public async Task Editor_AGraphsSliderMovesItsCurvesAndWritesTheValueOnRelease() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            // CST-069: a = 1 em 0..5 is a slider under the graph; the curve follows it as it moves, and the note keeps
            // the value where the slider was let go — without the block opening for editing
            var surface = new VisualSurface();
            var window = new Window { Width = 412, Height = 760, Content = new ScrollViewer { Content = surface } };
            window.Show();
            try
            {
                surface.SetText("```grafico\ny = a*x\na = 1 em 0..5\nx: 0..2\n```\n");
                for (int i = 0; i < 5; i++) { await Task.Delay(10); Dispatcher.UIThread.RunJobs(); }
                var plot = surface.GetVisualDescendants().OfType<FunctionPlotView>().Single();
                var slider = surface.GetVisualDescendants().OfType<Slider>().Single();
                Assert.Equal((0, 5, 1), (slider.Minimum, slider.Maximum, slider.Value));
                Assert.Contains(surface.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "a = 1");
                double At1() => plot.Drawing.Curves[0].Segments.SelectMany(seg => seg).MinBy(p => Math.Abs(p.X - 1))!.Y;
                Assert.Equal(1, At1(), 3);

                slider.Value = 3;   // the finger moves
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(3, At1(), 3);
                Assert.Contains(surface.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "a = 3");
                Assert.Contains("a = 1 em 0..5", surface.Text);   // nothing written while it moves
                Snapshot(window, "native-function-plot-slider.png");

                // a tap on the slider is the slider's, not the block's
                var press = new Avalonia.Input.PointerPressedEventArgs(slider, new Avalonia.Input.Pointer(1, Avalonia.Input.PointerType.Touch, true), slider,
                    new Avalonia.Point(10, 10), 0, new Avalonia.Input.PointerPointProperties(Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PointerUpdateKind.LeftButtonPressed), Avalonia.Input.KeyModifiers.None);
                slider.RaiseEvent(new Avalonia.Input.TappedEventArgs(Avalonia.Input.InputElement.TappedEvent, press));
                Dispatcher.UIThread.RunJobs();
                Assert.Empty(surface.GetVisualDescendants().OfType<TextBox>());

                // let go: the value goes into the note
                slider.RaiseEvent(new Avalonia.Input.KeyEventArgs { RoutedEvent = Avalonia.Input.InputElement.KeyUpEvent, Key = Avalonia.Input.Key.Right, Source = slider });
                for (int i = 0; i < 3; i++) { await Task.Delay(10); Dispatcher.UIThread.RunJobs(); }
                Assert.Equal("```grafico\ny = a*x\na = 3 em 0..5\nx: 0..2\n```\n", surface.Text);
                Assert.Equal(3, surface.GetVisualDescendants().OfType<Slider>().Single().Value);
                return true;
            }
            finally { window.Close(); }
        }, CancellationToken.None));
}
