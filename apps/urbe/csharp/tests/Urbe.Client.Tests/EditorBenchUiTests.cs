using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Urbe.Client;
using Urbe.Core;

namespace Urbe.Client.Tests;

/// <summary>
/// Opt-in bench (URBE_BENCH=1): the editor's hot paths on a phone-sized window with a realistic note,
/// timed and photographed, so two builds can be compared on the same machine. Lines go to URBE_PERF_LOG.
/// </summary>
public sealed partial class HeadlessUiTests
{
    internal static string BenchNote()
    {
        var sb = new System.Text.StringBuilder("# Estudo de cálculo\n\n");
        for (int i = 1; i <= 60; i++)
        {
            sb.Append("## Seção ").Append(i).Append("\n\n");
            sb.Append("Parágrafo com **negrito**, *itálico*, [[Outra nota]] e #tag para o editor trabalhar de verdade, como numa nota real de estudo que quebra em várias linhas no celular.\n\n");
            sb.Append("- [ ] tarefa ").Append(i).Append("\n- [x] feita ").Append(i).Append("\n- item comum\n\n");
            if (i % 5 == 0) sb.Append("$$\\int_0^1 x^2\\,dx = \\frac{1}{3}$$\n\n");
            if (i % 7 == 0) sb.Append("> Uma citação longa que também ocupa algumas linhas na tela do celular.\n\n");
        }
        return sb.ToString();
    }

    private static async Task BenchPump(Window window, int times = 4)
    {
        for (int i = 0; i < times; i++)
        {
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
            window.CaptureRenderedFrame();
            await Task.Delay(5);
        }
        Dispatcher.UIThread.RunJobs();
    }

    private static double Time(Action act, int n)
    {
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < n; i++) act();
        return sw.Elapsed.TotalMilliseconds / n;
    }

    [Fact]
    public async Task Bench_EditorHotPaths()
    {
        if (Environment.GetEnvironmentVariable("URBE_BENCH") != "1") return;
        for (int round = 0; round < 3; round++)
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            PerfLog("--- round");
            var csw = Stopwatch.StartNew();
            var view = new MainView();
            PerfLog($"BENCH mainview-ctor {csw.Elapsed.TotalMilliseconds:0.0} ms");
            var window = new Window { Width = 412, Height = 900, Content = view };
            window.Show();
            await view.LoadAsync(TutorialNotes.Load());
            Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
            PerfLog($"BENCH load+layout {csw.Elapsed.TotalMilliseconds:0.0} ms");
            await BenchPump(window);
            Snapshot(window, "bench-01-workspace.png");

            var note = BenchNote();
            var sw = Stopwatch.StartNew();
            view.Editor.Open(new CityNote("Tutorial/Estudo.md", note));
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            window.CaptureRenderedFrame();
            PerfLog($"BENCH open-visual {sw.Elapsed.TotalMilliseconds:0.0} ms ({note.Length} chars)");
            await BenchPump(window);
            Snapshot(window, "bench-02-editor-visual.png");

            // Visual: edit a block in the middle and type
            var surface = view.Editor.Surface;
            var start = surface.BlockStarts.Where(s => note.AsSpan(s).StartsWith("Parágrafo")).Skip(25).First();
            surface.BlockView(start)!.BringIntoView();
            await BenchPump(window);
            var holder = surface.BlockView(start)!;
            var text = holder.GetVisualDescendants().OfType<TextBlock>().First(t => t.Classes.Contains(MarkdownReader.TextClass));
            var at = text.TranslatePoint(new Point(40, text.LineHeight / 2), holder)!.Value;
            sw.Restart();
            surface.TapBlock(start, holder, text, at, false);
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            PerfLog($"BENCH tap-to-edit {sw.Elapsed.TotalMilliseconds:0.0} ms");
            await BenchPump(window);
            window.KeyTextInput("a");
            Dispatcher.UIThread.RunJobs();
            var visualKey = Time(() => { window.KeyTextInput("a"); window.UpdateLayout(); }, 60);
            PerfLog($"BENCH visual-key {visualKey:0.00} ms/key");
            await BenchPump(window);
            var visualFrame = Time(() => { window.KeyTextInput("b"); AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Dispatcher.UIThread.RunJobs(); window.CaptureRenderedFrame(); }, 30);
            PerfLog($"BENCH visual-key+frame {visualFrame:0.00} ms/key");
            Snapshot(window, "bench-03-editor-visual-editing.png");
            surface.FinishEdit();
            await BenchPump(window);

            // Fonte
            sw.Restart();
            view.Editor.SetMode(EditorMode.Fonte);
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            PerfLog($"BENCH switch-fonte {sw.Elapsed.TotalMilliseconds:0.0} ms");
            await BenchPump(window);
            view.Editor.Editor.Focus();
            view.Editor.Editor.CaretIndex = view.Editor.Editor.Text!.Length / 2;
            window.KeyTextInput("a");
            Dispatcher.UIThread.RunJobs();
            var fonteKey = Time(() => { window.KeyTextInput("a"); window.UpdateLayout(); }, 60);
            PerfLog($"BENCH fonte-key {fonteKey:0.00} ms/key");
            var fonteFrame = Time(() => { window.KeyTextInput("b"); AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Dispatcher.UIThread.RunJobs(); window.CaptureRenderedFrame(); }, 30);
            PerfLog($"BENCH fonte-key+frame {fonteFrame:0.00} ms/key");
            await BenchPump(window);
            Snapshot(window, "bench-04-editor-fonte.png");

            // scroll in Fonte
            var scroll = view.Editor.Editor.FindAncestorOfType<ScrollViewer>();
            if (scroll is not null)
            {
                var y = 0.0;
                var scrollFrame = Time(() => { y += 40; scroll.Offset = new Vector(0, y); AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Dispatcher.UIThread.RunJobs(); window.CaptureRenderedFrame(); }, 40);
                PerfLog($"BENCH fonte-scroll-frame {scrollFrame:0.00} ms/frame");
            }

            view.Editor.SetMode(EditorMode.Visual);
            Dispatcher.UIThread.RunJobs();
            await BenchPump(window);
            var vscroll = surface.FindAncestorOfType<ScrollViewer>();
            if (vscroll is not null)
            {
                var y = 0.0;
                var scrollFrame = Time(() => { y += 40; vscroll.Offset = new Vector(0, y); AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Dispatcher.UIThread.RunJobs(); window.CaptureRenderedFrame(); }, 40);
                PerfLog($"BENCH visual-scroll-frame {scrollFrame:0.00} ms/frame");
            }

            // switching notes back and forth
            var other = new CityNote("Tutorial/Curta.md", "# Curta\n\nTexto curto.\n");
            sw.Restart();
            for (int i = 0; i < 10; i++)
            {
                view.Editor.Open(i % 2 == 0 ? other : new CityNote("Tutorial/Estudo.md", note));
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
            }
            PerfLog($"BENCH open-switch {sw.Elapsed.TotalMilliseconds / 10:0.0} ms/open");
            await BenchPump(window);
            Snapshot(window, "bench-05-editor-short.png");

            await view.Editor.BackAsync();
            await BenchPump(window);
            Snapshot(window, "bench-06-back.png");
            window.Close();
            return true;
        }, CancellationToken.None));
    }
}

public sealed partial class HeadlessUiTests
{
    [Fact]
    public async Task Bench_TypingProfile()
    {
        var mode = Environment.GetEnvironmentVariable("URBE_BENCH_TYPE");
        if (mode is null) return;
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var view = new MainView();
            var window = new Window { Width = 412, Height = 900, Content = view };
            window.Show();
            await view.LoadAsync(TutorialNotes.Load());
            var note = BenchNote();
            view.Editor.Open(new CityNote("Tutorial/Estudo.md", note));
            await BenchPump(window);
            if (mode == "leave")
            {
                var dir = Path.Combine(Path.GetTempPath(), "urbe-bench-" + Guid.NewGuid().ToString("N"));
                var vault = GenerateVault(Path.Combine(dir, "vault"), 500);
                File.WriteAllText(Path.Combine(vault, "Estudo.md"), note);
                var big = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "sel.json")));
                window.Content = big;
                await big.OpenVaultAsync();
                await BenchPump(window);
                var n2 = int.Parse(Environment.GetEnvironmentVariable("URBE_BENCH_N") ?? "10");
                double total = 0, ui = 0;
                for (int i = 0; i < n2; i++)
                {
                    big.OpenNoteByPath("Estudo.md");
                    await BenchPump(window);
                    big.Editor.Editor.Text = big.Editor.Editor.Text + "x";
                    Dispatcher.UIThread.RunJobs();
                    var sw = Stopwatch.StartNew();
                    var back = big.Editor.BackAsync();
                    ui += sw.Elapsed.TotalMilliseconds;
                    await back;
                    total += sw.Elapsed.TotalMilliseconds;
                    await BenchPump(window);
                }
                PerfLog($"PROFILE leave ui {ui / n2:0.0} ms, saved {total / n2:0.0} ms");
                window.Close();
                return true;
            }
            if (mode == "notes")
            {
                var dir = Path.Combine(Path.GetTempPath(), "urbe-bench-" + Guid.NewGuid().ToString("N"));
                var vault = GenerateVault(Path.Combine(dir, "vault"), 2000);
                var big = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "sel.json")));
                window.Content = big;
                await big.OpenVaultAsync();
                await BenchPump(window);
                var n1 = int.Parse(Environment.GetEnvironmentVariable("URBE_BENCH_N") ?? "20");
                var each1 = Time(() => { big.ShowNotes(); Dispatcher.UIThread.RunJobs(); window.CaptureRenderedFrame(); big.Explorer.IsVisible = false; Dispatcher.UIThread.RunJobs(); }, n1);
                PerfLog($"PROFILE notes {each1:0.00} ms/open");
                window.Close();
                return true;
            }
            if (mode == "open")
            {
                var other = new CityNote("Tutorial/Curta.md", "# Curta\n\nTexto.\n");
                var n0 = int.Parse(Environment.GetEnvironmentVariable("URBE_BENCH_N") ?? "40");
                var each0 = Time(() => { view.Editor.Open(other); Dispatcher.UIThread.RunJobs(); view.Editor.Open(new CityNote("Tutorial/Estudo.md", note)); Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); }, n0);
                PerfLog($"PROFILE open {each0:0.00} ms/open");
                window.Close();
                return true;
            }
            if (mode == "fonte")
            {
                view.Editor.SetMode(EditorMode.Fonte);
                await BenchPump(window);
                view.Editor.Editor.Focus();
                view.Editor.Editor.CaretIndex = note.Length / 2;
            }
            else
            {
                var surface = view.Editor.Surface;
                var start = surface.BlockStarts.Where(s => note.AsSpan(s).StartsWith("Parágrafo")).Skip(25).First();
                surface.BlockView(start)!.BringIntoView();
                await BenchPump(window);
                var holder = surface.BlockView(start)!;
                var text = holder.GetVisualDescendants().OfType<TextBlock>().First(t => t.Classes.Contains(MarkdownReader.TextClass));
                surface.TapBlock(start, holder, text, text.TranslatePoint(new Point(40, text.LineHeight / 2), holder)!.Value, false);
                await BenchPump(window);
            }
            var n = int.Parse(Environment.GetEnvironmentVariable("URBE_BENCH_N") ?? "400");
            var each = Time(() => { window.KeyTextInput("a"); AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Dispatcher.UIThread.RunJobs(); window.CaptureRenderedFrame(); }, n);
            PerfLog($"PROFILE {mode} {each:0.00} ms/key");
            window.Close();
            return true;
        }, CancellationToken.None));
    }
}
