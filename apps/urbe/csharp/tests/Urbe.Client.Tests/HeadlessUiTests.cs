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
    public async Task NativeWorld_LifeOfTheWorldRunsOverTheCityAndPausesUnderTheEditor() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var view = new MainView();
            var window = new Window { Width = 1280, Height = 760, Content = view };
            window.Show();
            await view.LoadAsync(TutorialNotes.Load());
            await Settle(window, view);
            var world = view.World;
            world.Camera.SetZoom(1);
            await Settle(window, view);

            // night: the party becomes fireworks, the lamps and windows light up
            world.LightingMode = "noite";
            var v = world.LifeView();
            world.Life.Start("festa", v);
            Assert.NotNull(world.Life.TheParty);
            Assert.True(world.Life.TheParty!.Night);
            Assert.StartsWith("🎆 Fogos sobre ", view.LifeNoticeText);
            world.AdvanceLife(6);
            Assert.True(world.Life.Rockets.Count + world.Life.Sparks.Count + world.Life.Flashes.Count > 0, $"fogos: T={world.Life.Time} festa={world.Life.TheParty?.Name}");
            Assert.NotEmpty(world.Life.Lamps());
            Assert.NotEmpty(world.Life.Clouds);
            await Settle(window, view);
            Snapshot(window, "native-life-night.png");

            // day: rain over the city, animals and butterflies around it
            world.LightingMode = "dia";
            world.Life.Start("chuva", world.LifeView());
            world.AdvanceLife(8);
            Assert.True(world.Life.Rain.K > .2, $"chuva {world.Life.Rain.K}");
            Assert.NotEmpty(world.Life.Drops);
            Assert.True(world.Life.CurrentLight.Red < 255, "luz");
            await Settle(window, view);
            Snapshot(window, "native-life-rain.png");
            world.AdvanceLife(30);
            Assert.NotEmpty(world.Fauna.Animals);
            // the tutorial notes link to each other: villagers walk the streets between them
            Assert.Contains(world.Villagers.People, p => p.Kind == "andarilho");
            await Settle(window, view);
            Snapshot(window, "native-life-day.png");

            // v25MapaVisivel(): under the editor the city does not live
            view.Editor.Open(new CityNote("Tutorial/Comece aqui.md", "x"));
            double t = world.Life.Time;
            world.LifeTick();
            world.LifeTick();
            Assert.Equal(t, world.Life.Time);
            return true;
        }, CancellationToken.None));

    [Fact]
    public async Task CityMap_OnPhoneNavigatesOriginalWorldWithoutChangingZoom() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var view = new MainView();
            var window = new Window { Width = 412, Height = 860, Content = view };
            window.Show();
            await view.LoadAsync(TutorialNotes.Load());
            await Settle(window, view);
            view.ShowMap();
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Assert.True(view.CityMap.IsVisible);
            Assert.True(view.CityMap.HasWorld);
            Assert.True(view.CityMap.MapSize.Width > 100);
            Assert.False(view.CityMap.TryNavigate(new Point(-5, -5)));
            var zoom = view.World.Camera.Zoom;
            var lim = MapSurface.MapLimits(view.World.City!, view.World.Camera,
                view.CityMap.MapSize.Width / view.CityMap.MapSize.Height);
            var targetX = (lim.X0 + lim.X1) / 2 * Urbe.Client.World.LegacyWorld.Tile;
            var targetY = (lim.Y0 + lim.Y1) / 2 * Urbe.Client.World.LegacyWorld.Tile;
            Assert.True(view.CityMap.TryNavigate(new Point(
                view.CityMap.MapSize.Width / 2, view.CityMap.MapSize.Height / 2)));
            Assert.False(view.CityMap.IsVisible);
            Assert.Equal(zoom, view.World.Camera.Zoom);
            Assert.InRange(Math.Abs(view.World.Camera.X - targetX), 0, .01);
            Assert.InRange(Math.Abs(view.World.Camera.Y - targetY), 0, .01);
            return true;
        }, CancellationToken.None));

    [Fact]
    public void CityMap_LimitsIncludeTheCameraAndRespectAspect()
    {
        var city = new LegacyCity(new LegacyCityTerrain((_, _) => LegacyBiome.Grass),
            prefix => prefix + "1");
        var camera = new Urbe.Client.World.Camera { X = 720 * 32, Y = -400 * 32 };
        var limits = MapSurface.MapLimits(city, camera, 16.0 / 9);
        Assert.True(limits.X0 < 720 && limits.X1 > 720);
        Assert.True(limits.Y0 < -400 && limits.Y1 > -400);
        Assert.InRange(limits.Width / limits.Height, 1.7777, 1.7779);
    }

    /// <summary>
    /// A real vault opens as 1.8.4 abrirCidade: the saved .urbe/mapa.json geometry and camera are
    /// respected (fixture recorded from the original, csharp/tools/legacy-open-oracle.mjs), the
    /// terrain is the 'urbe' world even though mapa.mundo names its version, and nothing is framed.
    /// </summary>
    [Fact]
    public async Task Vault_OpensWithItsSavedMapAndCamera() => Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "csharp", "Urbe.Portable.slnx"))) dir = dir.Parent;
            using var json = System.Text.Json.JsonDocument.Parse(File.ReadAllBytes(
                Path.Combine(dir!.FullName, "csharp", "tests", "fixtures", "legacy-city", "open-vault-run-a.json")));
            var o = json.RootElement.GetProperty("mapa");
            var files = o.GetProperty("files").EnumerateObject()
                .Select(p => new VaultFile(p.Name, System.Text.Encoding.UTF8.GetBytes(p.Value.GetString()!)))
                .Append(new VaultFile(".urbe/mapa.json", System.Text.Encoding.UTF8.GetBytes(o.GetProperty("mapaJson").GetString()!)))
                .ToList();
            var view = new MainView(new SnapshotStorage(VaultReader.Read(files)));
            var window = new Window { Width = 1280, Height = 760, Content = view };
            window.Show();
            await view.InitializeAsync();
            await Settle(window, view);
            Snapshot(window, "native-vault-mapa.png");

            var city = view.World.City!;
            using var mapa = System.Text.Json.JsonDocument.Parse(o.GetProperty("mapaJson").GetString()!);
            var saved = mapa.RootElement.GetProperty("notas").GetProperty("Comece.md");
            var comece = city.Buildings.Single(b => b.Path == "Comece.md");
            Assert.Equal((saved.GetProperty("x").GetInt32(), saved.GetProperty("y").GetInt32()), (comece.X, comece.Y));
            var projetos = mapa.RootElement.GetProperty("regioes").EnumerateArray().Single(r => r.GetProperty("caminho").GetString() == "Projetos");
            Assert.Equal(projetos.GetProperty("cells").EnumerateArray().Select(c => c.GetString()!),
                city.Regions.Single(r => r.Name == "Projetos").Cells.Select(c => c.X + "," + c.Y));
            Assert.Equal(9, city.Buildings.Count(b => b.IsNote));
            Assert.Single(city.Buildings, b => !b.IsNote); // the file building of mapa.construcoes
            Assert.NotEmpty(city.Roads);
            var camera = o.GetProperty("camera");
            Assert.Equal((camera.GetProperty("x").GetDouble(), camera.GetProperty("y").GetDouble(), camera.GetProperty("z").GetDouble()),
                (view.World.Camera.X, view.World.Camera.Y, view.World.Camera.Zoom));
            Assert.Equal(LegacyCity.WorldVersion, mapa.RootElement.GetProperty("mundo").GetString());
            Assert.Equal(new LegacyWorld("urbe").Spawn.CellIndex, view.World.World!.Spawn.CellIndex);
            window.Close();
            return true;
        }, CancellationToken.None));

    private sealed class SnapshotStorage(VaultSnapshot snapshot) : IUrbeVaultStorage
    {
        public bool IsConnected => true;
        public Task<VaultSnapshot?> PickAsync(CancellationToken cancellationToken = default) => Task.FromResult<VaultSnapshot?>(snapshot);
        public Task<VaultSnapshot?> RestoreAsync(CancellationToken cancellationToken = default) => Task.FromResult<VaultSnapshot?>(snapshot);
        public Task SaveExistingNoteAsync(string path, string content, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("read-only test storage");
    }

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
