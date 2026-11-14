using System.Diagnostics;
using System.Text;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Threading;
using Lunet.Host;

namespace Lunet.Desktop;

/// <summary>
/// <c>Lunet --measure [pasta]</c> (LUNET-482): the numbers the ADR 0012 spike asks for, reproducible on any machine:
/// shell start to first rendered frame, opening a project, compiling, preview frame cost, and scrolling a long file list.
/// Uses the same window and workspace as the interactive app, rendered offscreen (Avalonia headless + Skia).
/// </summary>
public static class SpikeMeasurement
{
    public static int Run(string root)
    {
        var report = new StringBuilder();
        var total = Stopwatch.StartNew();
        AppBuilder.Configure<MeasuredApp>().UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false }).SetupWithoutStarting();
        report.AppendLine($"inicialização do Avalonia: {total.ElapsedMilliseconds} ms");

        var workspace = new WorkspaceSession(root);
        if (!workspace.Projects().Contains("Medicao")) workspace.Create("Medicao");
        workspace.Open("Medicao");
        // A long list for the scrolling measurement.
        for (int i = 0; i < 400; i++) workspace.Write($"Data/item{i:000}.json", "{}");

        var window = new MainWindow(root) { Width = 1280, Height = 800 };
        var shown = Stopwatch.StartNew();
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        window.CaptureRenderedFrame();
        report.AppendLine($"janela até o primeiro quadro: {shown.ElapsedMilliseconds} ms");

        var compile = Stopwatch.StartNew();
        var result = workspace.Compile();
        report.AppendLine($"compilação Roslyn do projeto inicial: {compile.ElapsedMilliseconds} ms (sucesso: {result.Success})");
        compile.Restart();
        workspace.Compile();
        report.AppendLine($"recompilação (quente): {compile.ElapsedMilliseconds} ms");

        foreach (var sprites in new[] { 0, 500, 2000 })
        {
            workspace.Write(workspace.Project!.Manifest.EntryPoint, StressGame(sprites));
            using var game = workspace.Run(640, 360)!;
            var frames = new List<double>();
            for (int i = 0; i < 120; i++)
            {
                var f = Stopwatch.StartNew();
                game.Frame(1 / 60.0);
                frames.Add(f.Elapsed.TotalMilliseconds);
            }
            frames.Sort();
            report.AppendLine($"quadro da prévia (CPU, 640×360, {sprites} sprites 16×16): mediana {frames[60]:0.00} ms, p95 {frames[114]:0.00} ms");
        }

        var files = new Avalonia.Controls.ListBox { ItemsSource = workspace.Files(), Height = 600 };
        var listWindow = new Avalonia.Controls.Window { Content = files, Width = 300, Height = 600 };
        listWindow.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        var scroll = Stopwatch.StartNew();
        for (int i = 0; i < workspace.Files().Count; i += 20)
        {
            files.ScrollIntoView(i);
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            listWindow.CaptureRenderedFrame();
        }
        int steps = (workspace.Files().Count + 19) / 20;
        report.AppendLine($"rolagem de {workspace.Files().Count} arquivos: {scroll.Elapsed.TotalMilliseconds / steps:0.0} ms por passo renderizado ({steps} passos)");
        workspace.Write(workspace.Project!.Manifest.EntryPoint, StressGame(300));
        window.OpenAndRun("Medicao");
        for (int i = 0; i < 30; i++) { window.Step(); Avalonia.Threading.Dispatcher.UIThread.RunJobs(); }
        var shot = Path.Combine(root, "spike-desktop.png");
#pragma warning disable CS0618 // the encoder-options overload is not needed for a plain PNG
        window.CaptureRenderedFrame()?.Save(shot);
#pragma warning restore CS0618
        report.AppendLine($"captura da janela: {shot}");
        report.AppendLine($"total: {total.ElapsedMilliseconds} ms · {Environment.OSVersion} · {Environment.ProcessorCount} núcleos · .NET {Environment.Version}");
        Console.Write(report);
        return result.Success ? 0 : 1;
    }

    private static string StressGame(int sprites) => $$"""
        using System.Numerics; using Lunet; using Lunet.Graphics;
        public sealed class Stress : Game
        {
            private SpriteBatch _b = null!; private Texture2D _t = null!; private float _time;
            protected override void Initialize() { Configuration.VirtualWidth = 640; Configuration.VirtualHeight = 360; }
            protected override void LoadContent() { _b = new SpriteBatch(GraphicsDevice); _t = Texture2D.CreateCircle(GraphicsDevice, 16, Color.White); }
            protected override void Update(GameTime time) => _time += (float)time.DeltaSeconds;
            protected override void Draw(GameTime time)
            {
                GraphicsDevice.Clear(Color.CornflowerBlue);
                _b.Begin();
                for (int i = 0; i < {{sprites}}; i++)
                    _b.Draw(_t, new Vector2((i * 37 + _time * 60) % 624, (i * 53) % 344), Color.White);
                _b.End();
            }
        }
        """;

    private sealed class MeasuredApp : Application
    {
        public override void Initialize() => Styles.Add(new Avalonia.Themes.Fluent.FluentTheme());
    }
}
