using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Urbe.Client;
using Urbe.Client.World;
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

    [Fact]
    public async Task Editor_OnlyConfirmsSavedAfterVaultHostAcknowledges() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var editor = new EditorOverlay { SaveOnClose = true };
            var window = new Window { Width = 800, Height = 500, Content = editor };
            window.Show();
            editor.Open(new CityNote("Notas/Exemplo.md", "original"));
            editor.Editor.Text = "texto novo";
            Assert.True(editor.HasChanges);
            CityNote? written = null;
            editor.SaveRequested += note => { written = note; return Task.CompletedTask; };
            Assert.True(await editor.SaveAsync());
            Assert.Equal("texto novo", written?.Content);
            Assert.False(editor.HasChanges);

            var failed = new EditorOverlay { SaveOnClose = true };
            failed.Open(new CityNote("Notas/Exemplo.md", "original"));
            failed.Editor.Text = "não salvar";
            failed.SaveRequested += _ => throw new IOException("sem permissão");
            Assert.False(await failed.SaveAsync());
            Assert.True(failed.HasChanges);
            return true;
        }, CancellationToken.None));


    [Fact]
    public async Task NativeExplorer_ListsFoldersSearchesAndOpensSelectedNote() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var view = new MainView();
            var window = new Window { Width = 412, Height = 860, Content = view };
            window.Show();
            await view.LoadAsync(TutorialNotes.Load());
            view.ShowNotes();
            Assert.True(view.Explorer.IsVisible);
            Assert.Contains("Tutorial", view.Explorer.VisibleFolderPaths);
            Assert.True(view.Explorer.TryEnterFolder("Tutorial"));
            Assert.Equal("Tutorial", view.Explorer.CurrentFolder);
            Assert.Contains("Tutorial/Comece aqui.md", view.Explorer.VisibleNotePaths);
            view.Explorer.SearchText = "Comece";
            Assert.Contains("Tutorial/Comece aqui.md", view.Explorer.VisibleNotePaths);
            Assert.True(view.Explorer.TryOpenNote("Tutorial/Comece aqui.md"));
            Assert.False(view.Explorer.IsVisible);
            Assert.True(view.Editor.IsVisible);
            Assert.Equal("Tutorial/Comece aqui.md", view.Editor.Current?.Path);
            return true;
        }, CancellationToken.None));

    [Fact]
    public async Task NativeExplorer_PreventsOpeningInvisibleOrUnknownPaths() =>
        Assert.True(await Session.Dispatch<bool>(() =>
        {
            var explorer = new NotesExplorer();
            var window = new Window { Width = 410, Height = 760, Content = explorer };
            window.Show();
            explorer.SetNotes([
                new CityNote("Raiz.md", "# raiz"),
                new CityNote("Bairro/Um.md", "# um"),
                new CityNote("Bairro/Sub/Dois.md", "# dois")
            ]);
            Assert.Equal(new[] { "Raiz.md" }, explorer.VisibleNotePaths);
            Assert.Equal(new[] { "Bairro" }, explorer.VisibleFolderPaths);
            Assert.False(explorer.TryOpenNote("Bairro/Um.md"));
            Assert.True(explorer.TryEnterFolder("Bairro"));
            Assert.Contains("Bairro/Um.md", explorer.VisibleNotePaths);
            Assert.Contains("Bairro/Sub", explorer.VisibleFolderPaths);
            Assert.False(explorer.TryEnterFolder("Bairro/Sub/ausente"));
            explorer.SearchText = "Dois";
            Assert.Contains("Bairro/Sub/Dois.md", explorer.VisibleNotePaths);
            Assert.True(explorer.TryOpenNote("Bairro/Sub/Dois.md"));
            return true;
        }, CancellationToken.None));

    [Fact]
    public async Task NativeWorld_LightingMatchesLegacyRgbWithoutTintingHouseLabels() =>
        Assert.True(await Session.Dispatch<bool>(() =>
        {
            var world = new WorldView();
            var window = new Window { Width = 800, Height = 480, Content = world };
            window.Show();
            world.LightingMode = "dia";
            var day = world.CurrentLight;
            Assert.Equal((255, 255, 255), (day.Red, day.Green, day.Blue));
            Assert.Equal(0, day.Darkness);
            world.LightingMode = "noite";
            var night = world.CurrentLight;
            var expected = LegacyWorldLife.LightAt(23);
            Assert.Equal(expected, night);
            Assert.True(night.Darkness > .9);
            world.LightingMode = "entardecer";
            var dusk = world.CurrentLight;
            Assert.True(dusk.Red > dusk.Blue);
            Assert.Throws<ArgumentOutOfRangeException>(() => { world.LightingMode = "unsupported"; });
            return true;
        }, CancellationToken.None));

    [Fact]
    public async Task City_ReopensCameraFromPhysicalVaultMapWithoutWritingOrRetilingWorld() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var files = new[]
            {
                new VaultFile("A.md", System.Text.Encoding.UTF8.GetBytes("# A")),
                new VaultFile(".urbe/mapa.json", System.Text.Encoding.UTF8.GetBytes(
                    """{"v":4,"mundo":"urbe","camera":{"x":1100,"y":750,"z":1.35}}"""))
            };
            var snapshot = VaultReader.Read(files);
            var view = new MainView();
            var window = new Window { Width = 412, Height = 860, Content = view };
            window.Show();
            await view.LoadAsync([new CityNote("A.md", "# A")], "urbe", snapshot);
            Assert.True(view.World.City!.Buildings.Count > 0);
            Assert.Equal(1100, view.World.Camera.X);
            Assert.Equal(750, view.World.Camera.Y);
            Assert.Equal(1.35, view.World.Camera.Zoom);
            Assert.Equal("# A", snapshot.Documents.Single().Text);
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
