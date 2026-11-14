using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Urbe.Client;
using Urbe.Core;

namespace Urbe.Client.Tests;

/// <summary>Opt-in (URBE_PROFILE=fonte|visual): types into a long note for a profiler to look at.</summary>
public sealed class EditorTypingProfileTests
{
    [Fact]
    public async Task Profile_Typing()
    {
        var mode = Environment.GetEnvironmentVariable("URBE_PROFILE");
        if (mode is null) return;
        Assert.True(await HeadlessUiTests.Session.Dispatch<bool>(async () =>
        {
            var view = new MainView();
            var window = new Window { Width = 412, Height = 900, Content = view };
            window.Show();
            await view.LoadAsync(TutorialNotes.Load());
            var note = HeadlessUiTests.BenchNote();
            view.Editor.Open(new CityNote("Tutorial/Estudo.md", note));
            Dispatcher.UIThread.RunJobs();
            if (mode == "fonte")
            {
                view.Editor.SetMode(EditorMode.Fonte);
                Dispatcher.UIThread.RunJobs();
                view.Editor.Editor.Focus();
                view.Editor.Editor.CaretIndex = view.Editor.Editor.Text!.Length / 2;
            }
            else
            {
                var surface = view.Editor.Surface;
                var start = surface.BlockStarts.Where(s => note.AsSpan(s).StartsWith("Parágrafo")).Skip(25).First();
                var holder = surface.BlockView(start)!;
                surface.TapBlock(start, holder, holder, new Avalonia.Point(40, 10), false);
                Dispatcher.UIThread.RunJobs();
            }
            var sw = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < 400; i++)
            {
                window.KeyTextInput(i % 7 == 6 ? " " : "a");
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                Dispatcher.UIThread.RunJobs();
            }
            Console.WriteLine($"PROFILE {mode} {sw.Elapsed.TotalMilliseconds / 400:0.00} ms/key");
            window.Close();
            await Task.Yield();
            return true;
        }, CancellationToken.None));
    }
}
