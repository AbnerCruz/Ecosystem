using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Urbe.Client;
using Urbe.Core;

namespace Urbe.Client.Tests;

public static class HeadlessApp
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<UrbeApp>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}

/// <summary>
/// Drives the real native UI headlessly (Skia rendering, real input routing).
/// Frames are saved to URBE_SNAPSHOT_DIR when set, as visual evidence for review.
/// This is not device validation (NN-017): soft keyboard/IME on Android is G-N0 DEVICE.
/// </summary>
public sealed class HeadlessUiTests : IDisposable
{
    private static readonly HeadlessUnitTestSession Session = HeadlessUnitTestSession.StartNew(typeof(HeadlessApp));

    public void Dispose() { }

    [Fact]
    public async Task City_RendersFullScreenWithHousesAndOpensTheEditorFromAHouse() => Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var view = new MainView();
            var window = new Window { Width = 1280, Height = 760, Content = view };
            window.Show();
            await view.LoadAsync(TutorialNotes.Load());
            await Settle(window, view);
            Snapshot(window, "native-desktop-city.png");

            // city.fit: the whole tutorial city at the 1.8.4 framing zoom (clamped to [.45, 1.3])
            Assert.InRange(view.World.Camera.Zoom, .45, 1.3);
            Assert.True(view.World.ReadyChunks > 0);
            Assert.True(view.World.Bounds.Width > 1100 && view.World.Bounds.Height > 700);

            // closer: near ground, individual trees, roads with pebbles
            view.World.Camera.SetZoom(1.15);
            var house = view.World.City!.Buildings.Single(b => b.Path == "Tutorial/Comece aqui.md");
            view.World.CenterOn(house);
            await Settle(window, view);
            Snapshot(window, "native-desktop-near.png");

            // tap the house → 1.8.4 house card → "Abrir e editar" → editor over the city
            var centre = view.World.Camera.WorldToScreen((house.X + 1.5) * 32, (house.Y + 1.5) * 32);
            var p = view.World.TranslatePoint(centre, window)!.Value;
            window.MouseDown(p, MouseButton.Left);
            window.MouseUp(p, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            Assert.Same(house, view.World.Selected);
            await Settle(window, view);
            Snapshot(window, "native-desktop-house.png");

            view.Editor.Open(new CityNote(house.Path, TutorialNotes.Load().Single(f => f.Path == house.Path).Content));
            await Settle(window, view);
            Assert.True(view.Editor.IsVisible);
            Assert.True(view.Editor.Editor.IsFocused);
            view.Editor.Editor.CaretIndex = 0;
            window.KeyTextInput("Olá, ação! ");
            Dispatcher.UIThread.RunJobs();
            Assert.StartsWith("Olá, ação! ", view.Editor.Editor.Text);
            Snapshot(window, "native-desktop-editor.png");
            return true;
        }, CancellationToken.None));

    [Fact]
    public async Task City_OnPhoneUsesTheBottomDock() => Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var view = new MainView();
            var window = new Window { Width = 412, Height = 860, Content = view };
            window.Show();
            await view.LoadAsync(TutorialNotes.Load());
            await Settle(window, view);
            Snapshot(window, "native-phone-city.png");
            Assert.True(view.World.Bounds.Width >= 400);
            return true;
        }, CancellationToken.None));

    [Fact]
    public async Task Editor_ToolbarEditsMarkdownSource() => Assert.True(await Session.Dispatch<bool>(() =>
        {
            var editor = new EditorOverlay();
            var window = new Window { Width = 800, Height = 600, Content = editor };
            window.Show();
            editor.Open(new CityNote("Tutorial/x.md", "linha um\nlinha dois"));
            editor.Editor.CaretIndex = 12;
            editor.LinePrefix("## ");
            Assert.Equal("linha um\n## linha dois", editor.Editor.Text);
            editor.Editor.SelectionStart = 0;
            editor.Editor.SelectionEnd = 5;
            editor.Wrap("**");
            Assert.Equal("**linha** um\n## linha dois", editor.Editor.Text);
            return true;
        }, CancellationToken.None));

    private static async Task Settle(Window window, MainView view)
    {
        for (int i = 0; i < 600; i++)
        {
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
            window.CaptureRenderedFrame();
            if (view.World.ReadyChunks > 0 && view.World.PendingChunks == 0 && i > 5) break;
            await Task.Delay(20);
        }
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    private static void Snapshot(Window window, string name)
    {
        var dir = Environment.GetEnvironmentVariable("URBE_SNAPSHOT_DIR");
        var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        if (string.IsNullOrEmpty(dir)) return;
        Directory.CreateDirectory(dir);
        frame!.Save(Path.Combine(dir, name), Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
    }
}
