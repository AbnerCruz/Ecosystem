using System.Diagnostics;
using Avalonia.Controls;
using Lunet.Client.Views;

namespace Lunet.Tests;

/// <summary>
/// G04 / H3: typing latency through the Android keyboard path (AndroidImeSimulator) in a long file, in this runner.
/// The numbers are written to the test output; they are not phone numbers (no claim about the moto g32). The test only
/// fails on a gross regression (p95 above 50 ms here), and checks that a key never reads the whole file.
/// </summary>
public sealed class TypingLatencyTests
{
    [Fact]
    public void TypingInALongFile_StaysFast_AndTheKeyboardSeesAWindowNotTheFile()
    {
        UiSession.Run(() =>
        {
            var lines = string.Join("\n", Enumerable.Range(0, 3000).Select(i => $"    var valor{i} = Calcular({i}) * 2; // linha {i}"));
            var code = new CodeSurface();
            var window = new Window { Width = 412, Height = 800, Content = code.View };
            window.Show();
            code.Text = lines;
            code.Caret = lines.IndexOf("linha 1500", StringComparison.Ordinal);
            code.Focus();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            var ime = new AndroidImeSimulator(window, AndroidImeSimulator.ClientOf(code.Editor.TextArea)!);
            for (var i = 0; i < 20; i++) ime.CommitText("a"); // warm-up
            var samples = new List<double>();
            var watch = new Stopwatch();
            foreach (var ch in "posicao do jogador atualizada no quadro seguinte com velocidade".Repeat(3))
            {
                watch.Restart();
                ime.CommitText(ch.ToString());
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                samples.Add(watch.Elapsed.TotalMilliseconds);
            }
            samples.Sort();
            double P(double q) => samples[(int)Math.Min(samples.Count - 1, Math.Round(q * (samples.Count - 1)))];
            TestContext.Current.TestOutputHelper?.WriteLine($"teclas={samples.Count} p50={P(0.5):0.00} ms p95={P(0.95):0.00} ms max={samples[^1]:0.00} ms (runner, não aparelho)");
            Assert.True(P(0.95) < 50, $"p95 {P(0.95):0.0} ms");
            Assert.True(ime.Text.Length < 4 * CodeInputClient.Reach); // the keyboard is given a window, never the 150 KB file
            window.Close();
        });
    }
}

internal static class StringRepeat
{
    public static string Repeat(this string s, int n) => string.Concat(Enumerable.Repeat(s, n));
}
