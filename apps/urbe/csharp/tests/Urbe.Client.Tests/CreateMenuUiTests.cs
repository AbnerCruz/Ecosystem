using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Urbe.Client;
using Urbe.Client.World;
using Urbe.Core;

namespace Urbe.Client.Tests;

/// <summary>The rest of the "Criar" (+) menu, driven through the real native UI against a real folder vault.</summary>
public sealed partial class HeadlessUiTests
{
    private static async Task<(MainView View, Window Window, string Vault, string Dir)> OpenCreateVault()
    {
        var dir = Path.Combine(Path.GetTempPath(), "urbe-create-" + Guid.NewGuid().ToString("N"));
        var vault = Path.Combine(dir, "vault");
        Directory.CreateDirectory(Path.Combine(vault, "Bairro"));
        File.WriteAllText(Path.Combine(vault, "Comece.md"), "# Comece\n\n[[Um]]\n");
        File.WriteAllText(Path.Combine(vault, "Bairro", "Um.md"), "# Um\n");
        var storage = new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "app", "sel.json"));
        var view = new MainView(storage);
        var window = new Window { Width = 1280, Height = 760, Content = view };
        window.Show();
        await view.OpenVaultAsync();
        await Settle(window, view);
        return (view, window, vault, dir);
    }

    private static async Task PickFromCreateMenu(MainView view, string item, Func<Task> next)
    {
        var menu = view.ShowCreateMenuAsync();
        Dispatcher.UIThread.RunJobs();
        ClickButtonWithText(view.Dialogs, item);
        await Task.Delay(30);
        Dispatcher.UIThread.RunJobs();
        await next();
        // a restore reopens the city, whose loading draws frames: they are ticked until the flow ends
        for (int i = 0; i < 600 && !menu.IsCompleted; i++)
        {
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(20);
        }
        if (!menu.IsCompleted)
            throw new TimeoutException("A ação não terminou. Na tela: " + string.Join(" | ", view.Dialogs.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text)));
        await menu;
    }

    /// <summary>A flow started directly (what the Explorer's Nova pasta does), with <paramref name="next"/> answering its dialog.</summary>
    private static async Task RunWithDialog(Task flow, Func<Task> next)
    {
        Dispatcher.UIThread.RunJobs();
        await Task.Delay(30);
        Dispatcher.UIThread.RunJobs();
        await next();
        for (int i = 0; i < 1500 && !flow.IsCompleted; i++)
        {
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(20);
        }
        await flow;
    }

    private static Point ScreenOf(Window window, MainView view, double tileX, double tileY) =>
        view.World.TranslatePoint(view.World.Camera.WorldToScreen(tileX * 32, tileY * 32), window)!.Value;

    [Fact]
    public async Task NewFolder_IsARegionOnTheMapAndAFolderInTheVault() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var (view, window, vault, dir) = await OpenCreateVault();
            try
            {
                var city = view.World.City!;
                int regions = city.Regions.Count;
                var before = Directory.GetFiles(vault, "*", SearchOption.AllDirectories).Order().ToArray();
                await RunWithDialog(view.CreateFolderAsync(), () =>
                {
                    Snapshot(window, "native-new-folder-dialog.png");
                    var input = view.Dialogs.GetVisualDescendants().OfType<TextBox>().Single();
                    input.Text = "  Projetos ";
                    ClickButtonWithText(view.Dialogs, "Criar pasta");
                    return Task.CompletedTask;
                });
                Assert.True(Directory.Exists(Path.Combine(vault, "Projetos")));
                Assert.Equal(regions + 1, city.Regions.Count);
                Assert.Equal("Projetos", city.Regions.Last().Name);
                Assert.Equal(before, Directory.GetFiles(vault, "*", SearchOption.AllDirectories).Order().ToArray());   // no file touched or added

                // the same name again: "Projetos (2)" (v21UniqueRegionName), never the same folder
                await RunWithDialog(view.CreateFolderAsync(), () =>
                {
                    view.Dialogs.GetVisualDescendants().OfType<TextBox>().Single().Text = "projetos";
                    ClickButtonWithText(view.Dialogs, "Criar pasta");
                    return Task.CompletedTask;
                });
                Assert.Equal("projetos (2)", city.Regions.Last().Name);
                Assert.True(Directory.Exists(Path.Combine(vault, "projetos (2)")));

                // and a note can be made inside the new neighbourhood (the folder is a real place)
                var made = city.CreateNote(city.Regions.Single(r => r.Name == "Projetos"), "Ideias", 1);
                Assert.Equal("Projetos/Ideias.md", made!.Value.House.Path);
                window.Close();
                return true;
            }
            finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
        }, CancellationToken.None));

    [Fact]
    public async Task CreateMenu_WithoutAWritableVaultCreatesNothingInMemory() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var view = new MainView();
            var window = new Window { Width = 412, Height = 860, Content = view };
            window.Show();
            await view.LoadAsync(TutorialNotes.Load());
            await Settle(window, view);
            int regions = view.World.City!.Regions.Count, houses = view.World.City.Buildings.Count;
            foreach (var item in new[] { "Construção", "Região", "Decoração", "Importar" })
                await PickFromCreateMenu(view, item, () =>
                {
                    Assert.Empty(view.Dialogs.GetVisualDescendants().OfType<TextBox>());
                    ClickButtonWithText(view.Dialogs, "Ok");
                    return Task.CompletedTask;
                });
            Assert.False(view.ToolActive);
            Assert.Equal(regions, view.World.City.Regions.Count);
            Assert.Equal(houses, view.World.City.Buildings.Count);
            window.Close();
            return true;
        }, CancellationToken.None));

    [Fact]
    public async Task DrawFolderOnTheMap_DragsARectangleNamesItAndMakesTheFolder() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var (view, window, vault, dir) = await OpenCreateVault();
            try
            {
                // life events (neblina, ...) post to the same notice pill: keep it to the tool's own messages
                view.World.Life.Options.Events = false;
                var city = view.World.City!;
                var (x0, y0) = FindDryRect(city, 6);
                view.World.Camera.X = (x0 + 3) * 32;
                view.World.Camera.Y = (y0 + 3) * 32;
                view.World.InvalidateVisual();
                Dispatcher.UIThread.RunJobs();
                int regions = city.Regions.Count;

                await PickFromCreateMenu(view, "Região", () => Task.CompletedTask);
                Assert.True(view.ToolActive);
                Assert.Equal("Arraste no mapa para delimitar a nova pasta.", view.ToolDockText);

                // too small a rectangle: "A região precisa ser maior." and the tool stays
                var a = ScreenOf(window, view, x0 + .5, y0 + .5);
                window.MouseDown(a, MouseButton.Left);
                window.MouseUp(ScreenOf(window, view, x0 + 1.5, y0 + 1.5), MouseButton.Left);
                Dispatcher.UIThread.RunJobs();
                Assert.Equal("A região precisa ser maior.", view.LifeNoticeText);
                Assert.True(view.ToolActive);
                Assert.False(view.Dialogs.IsOpen);

                // a real drag: 6x6 tiles; the camera did not pan
                double cx = view.World.Camera.X;
                window.MouseDown(a, MouseButton.Left);
                window.MouseMove(ScreenOf(window, view, x0 + 3.5, y0 + 3.5));
                window.MouseMove(ScreenOf(window, view, x0 + 5.5, y0 + 5.5));
                Snapshot(window, "native-draw-region-draft.png");
                window.MouseUp(ScreenOf(window, view, x0 + 5.5, y0 + 5.5), MouseButton.Left);
                Dispatcher.UIThread.RunJobs();
                await Task.Delay(30);
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(cx, view.World.Camera.X);
                Assert.True(view.Dialogs.IsOpen);
                var input = view.Dialogs.GetVisualDescendants().OfType<TextBox>().Single();
                input.Text = "Rascunhos";
                ClickButtonWithText(view.Dialogs, "Criar");
                await Task.Delay(100);
                Dispatcher.UIThread.RunJobs();

                var made = city.Regions.Last();
                Assert.Equal(regions + 1, city.Regions.Count);
                Assert.Equal("Rascunhos", made.Name);
                Assert.Equal((x0, y0, 6, 6), (made.X, made.Y, made.W, made.H));
                Assert.All(made.Cells, c => Assert.InRange(c.X, x0, x0 + 5));
                Assert.True(Directory.Exists(Path.Combine(vault, "Rascunhos")));
                Assert.False(view.ToolActive);
                Assert.StartsWith("Região criada com prioridade territorial", view.LifeNoticeText);
                Snapshot(window, "native-draw-region-done.png");
                window.Close();
                return true;
            }
            finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
        }, CancellationToken.None));

    private static (int X, int Y) FindDryRect(LegacyCity city, int size)
    {
        var c = city.Buildings.First();
        for (int r = 6; r < 60; r++)
        for (int dy = -r; dy <= r; dy++)
        for (int dx = -r; dx <= r; dx++)
        {
            if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r) continue;
            int x = c.X + dx, y = c.Y + dy;
            if (city.DrawRegionMask(x, y, size, size).Cells.Count == size * size && city.RegionAt(x, y) is null) return (x, y);
        }
        throw new InvalidOperationException("sem terra livre");
    }

    [Fact]
    public async Task AddBuilding_PlacesTheGhostOnTheMapAndBuildsTheNoteWhereItWasPut() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var (view, window, vault, dir) = await OpenCreateVault();
            try
            {
                var city = view.World.City!;
                // a free lot outside every neighbourhood, with road access
                (int X, int Y)? lot = null;
                var c = city.Buildings.First();
                for (int r = 6; r < 40 && lot is null; r++)
                for (int dy = -r; dy <= r && lot is null; dy++)
                for (int dx = -r; dx <= r && lot is null; dx++)
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) == r && city.CanHouse(c.X + dx, c.Y + dy)) lot = (c.X + dx, c.Y + dy);
                Assert.NotNull(lot);
                var (lx, ly) = lot!.Value;
                view.World.Camera.X = (lx + 1.5) * 32;
                view.World.Camera.Y = (ly + 1.5) * 32;
                view.World.InvalidateVisual();
                Dispatcher.UIThread.RunJobs();
                int houses = city.Buildings.Count;

                await PickFromCreateMenu(view, "Construção", () =>
                {
                    Snapshot(window, "native-building-type.png");
                    ClickButtonWithText(view.Dialogs, "Nota .md");
                    return Task.CompletedTask;
                });
                Assert.True(view.ToolActive);
                Assert.StartsWith("Posicione a construção", view.ToolDockText);
                var spot = ScreenOf(window, view, lx + .5, ly + .5);
                window.MouseDown(spot, MouseButton.Left);
                window.MouseUp(spot, MouseButton.Left);
                Dispatcher.UIThread.RunJobs();
                Assert.Equal($"Posicione a construção · {lx},{ly}", view.ToolDockText);
                Snapshot(window, "native-place-building-ghost.png");

                var ok = view.GetVisualDescendants().OfType<Button>().First(b => b.Content as string == "Construir");
                Assert.True(ok.IsEnabled);
                var confirm = view.ConfirmPlacementAsync();
                Dispatcher.UIThread.RunJobs();
                view.Dialogs.GetVisualDescendants().OfType<TextBox>().Single().Text = "Casa nova.txt";
                ClickButtonWithText(view.Dialogs, "Construir");
                await confirm;

                Assert.Equal("", File.ReadAllText(Path.Combine(vault, "Casa nova.txt")));
                var house = city.Buildings.Single(b => b.Path == "Casa nova.txt");
                Assert.Equal((lx, ly), (house.X, house.Y));
                Assert.Equal(houses + 1, city.Buildings.Count);
                Assert.Same(house, view.World.Selected);
                Assert.False(view.ToolActive);
                // the new note has its document: the map is saved with it (it used to be refused)
                await UntilMapSaved(view);
                using (var m = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(vault, ".urbe", "mapa.json"))))
                {
                    var n = m.RootElement.GetProperty("notas").GetProperty("Casa nova.txt");
                    Assert.Equal((lx, ly), (n.GetProperty("x").GetInt32(), n.GetProperty("y").GetInt32()));
                }
                window.Close();
                return true;
            }
            finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
        }, CancellationToken.None));

    /// <summary>
    /// The house panel's Copiar: tap the map, ✓, "Configurar cópia" → a NEW file with the source's text in the
    /// neighbourhood under the tile, its own document, the source untouched, and the map saved with both (owner's
    /// decision: a real copy, unlike the 1.8.4).
    /// </summary>
    [Fact]
    public async Task Copy_PlacesARealCopyOfTheNoteWhereItWasPut() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var (view, window, vault, dir) = await OpenCreateVault();
            try
            {
                var city = view.World.City!;
                await UntilMapSaved(view);
                var source = city.Buildings.Single(b => b.Path == "Bairro/Um.md");
                (int X, int Y)? lot = null;
                for (int r = 4; r < 40 && lot is null; r++)
                for (int dy = -r; dy <= r && lot is null; dy++)
                for (int dx = -r; dx <= r && lot is null; dx++)
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) == r && city.CanHouse(source.X + dx, source.Y + dy)) lot = (source.X + dx, source.Y + dy);
                var (lx, ly) = lot!.Value;
                view.World.Camera.X = (lx + 1.5) * 32;
                view.World.Camera.Y = (ly + 1.5) * 32;
                view.World.Select(source);
                Dispatcher.UIThread.RunJobs();
                ClickButtonWithText(view, "Copiar");
                await Task.Delay(30);
                Dispatcher.UIThread.RunJobs();
                Assert.True(view.ToolActive);
                Assert.StartsWith("Posicione a cópia", view.ToolDockText);
                Assert.Null(view.World.Selected);
                var spot = ScreenOf(window, view, lx + .5, ly + .5);
                window.MouseDown(spot, MouseButton.Left);
                window.MouseUp(spot, MouseButton.Left);
                Dispatcher.UIThread.RunJobs();
                var ok = view.GetVisualDescendants().OfType<Button>().First(b => b.Content as string == "Copiar" && b.IsVisible && b.IsEnabled);
                var confirm = view.ConfirmPlacementAsync();
                Dispatcher.UIThread.RunJobs();
                Assert.Contains(view.Dialogs.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "Configurar cópia");
                view.Dialogs.GetVisualDescendants().OfType<TextBox>().Single().Text = "Um cópia";
                ClickButtonWithText(view.Dialogs, "Copiar");
                await confirm;

                var region = city.RegionAt(lx, ly);
                var folder = region is null ? "" : city.RegionPaths()[region.Id] + "/";
                var copy = city.Buildings.Single(b => b.Path == folder + "Um cópia.md");
                Assert.Equal((lx, ly), (copy.X, copy.Y));
                Assert.Equal("# Um\n", File.ReadAllText(Path.Combine(vault, folder.Replace('/', Path.DirectorySeparatorChar) + "Um cópia.md")));
                Assert.Equal("# Um\n", File.ReadAllText(Path.Combine(vault, "Bairro", "Um.md")));
                Assert.Same(copy, view.World.Selected);
                Assert.NotEqual(view.Documents!.Get("Bairro/Um.md")!.Id, view.Documents.Get(copy.Path)!.Id);
                await UntilMapSaved(view);
                using (var m = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(vault, ".urbe", "mapa.json"))))
                {
                    var notas = m.RootElement.GetProperty("notas");
                    Assert.Equal((source.X, source.Y), (notas.GetProperty("Bairro/Um.md").GetProperty("x").GetInt32(), notas.GetProperty("Bairro/Um.md").GetProperty("y").GetInt32()));
                    Assert.Equal((lx, ly), (notas.GetProperty(copy.Path).GetProperty("x").GetInt32(), notas.GetProperty(copy.Path).GetProperty("y").GetInt32()));
                }
                window.Close();
                return true;
            }
            finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
        }, CancellationToken.None));

    private static ImportSelection Files(bool directory, params (string Path, string Text)[] files) =>
        new(files.Select(f => new ImportSource(f.Path, _ => ValueTask.FromResult<Stream>(new MemoryStream(Encoding.UTF8.GetBytes(f.Text))))).ToList(), directory);

    [Fact]
    public async Task Import_WritesNewDocumentsIntoTheVaultAndKeepsWhatIsThereOnConflict() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var (view, window, vault, dir) = await OpenCreateVault();
            try
            {
                var city = view.World.City!;
                int houses = city.Buildings.Count;
                view.ImportPicker = folder => Task.FromResult<ImportSelection?>(Files(folder,
                    ("Novo.md", "# Novo\n"), ("Pacote/Dentro.md", "# Dentro\n"), ("Comece.md", "# OUTRO\n"), ("Bairro/Um.md", "# Um\n"),
                    ("foto.bin", "x"), (".obsidian/app.json", "{}")));
                await PickFromCreateMenu(view, "Importar", async () =>
                {
                    ClickButtonWithText(view.Dialogs, "Pasta inteira");
                    await Task.Delay(50);
                    Dispatcher.UIThread.RunJobs();
                    Snapshot(window, "native-import-confirm.png");
                    ClickButtonWithText(view.Dialogs, "Continuar");
                    await Task.Delay(300);
                    Dispatcher.UIThread.RunJobs();
                });
                Assert.Equal("# Novo\n", File.ReadAllText(Path.Combine(vault, "Novo.md")));
                Assert.Equal("# Dentro\n", File.ReadAllText(Path.Combine(vault, "Pacote", "Dentro.md")));
                Assert.Equal("# Comece\n\n[[Um]]\n", File.ReadAllText(Path.Combine(vault, "Comece.md")));   // different content: kept
                Assert.False(File.Exists(Path.Combine(vault, "foto.bin")));
                Assert.False(Directory.Exists(Path.Combine(vault, ".obsidian")));
                Assert.Equal(houses + 2, city.Buildings.Count);
                Assert.NotNull(city.RegionByPath("Pacote"));
                Assert.StartsWith("Importação concluída. 2 arquivo(s) gravado(s); 1 preservado(s)", view.LifeNoticeText);
                window.Close();
                return true;
            }
            finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
        }, CancellationToken.None));

    [Fact]
    public async Task Import_RefusesUnsafePathsAndBackupsWithoutTouchingTheVault() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var (view, window, vault, dir) = await OpenCreateVault();
            try
            {
                var before = Directory.GetFiles(vault, "*", SearchOption.AllDirectories).Order().ToArray();
                foreach (var selection in new[] { Files(false, ("../fora.md", "x")), Files(true, (".urbe/vault.json", "{\"formatVersion\":99}"), ("n.md", "x")) })
                {
                    view.ImportPicker = _ => Task.FromResult<ImportSelection?>(selection);
                    await PickFromCreateMenu(view, "Importar", async () =>
                    {
                        ClickButtonWithText(view.Dialogs, "Arquivos");
                        await Task.Delay(50);
                        Dispatcher.UIThread.RunJobs();
                        Assert.Contains("Não foi possível importar", view.Dialogs.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text));
                        ClickButtonWithText(view.Dialogs, "Ok");
                    });
                }
                Assert.Equal(before, Directory.GetFiles(vault, "*", SearchOption.AllDirectories).Order().ToArray());
                window.Close();
                return true;
            }
            finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
        }, CancellationToken.None));

    [Fact]
    public async Task Import_RestoresAVaultFolderWithABackupOfWhatItReplaces() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var (view, window, vault, dir) = await OpenCreateVault();
            try
            {
                await view.FlushMapAsync();
                // the copy of the vault as it is now (what "Exportar"/a copied folder gives)
                var copy = Directory.GetFiles(vault, "*", SearchOption.AllDirectories)
                    .Select(f => (Path: Path.GetRelativePath(vault, f).Replace('\\', '/'), Bytes: File.ReadAllBytes(f))).ToList();
                Assert.Contains(copy, f => f.Path == ".urbe/mapa.json");
                // the vault changes afterwards
                File.WriteAllText(Path.Combine(vault, "Comece.md"), "# Mudou\n");
                File.WriteAllText(Path.Combine(vault, "Extra.md"), "# Extra\n");
                await view.OpenVaultAsync();
                await Settle(window, view);
                view.ImportPicker = _ => Task.FromResult<ImportSelection?>(new ImportSelection(
                    copy.Select(f => new ImportSource(f.Path, _ => ValueTask.FromResult<Stream>(new MemoryStream(f.Bytes)))).ToList(), true));
                await PickFromCreateMenu(view, "Importar", async () =>
                {
                    ClickButtonWithText(view.Dialogs, "Pasta inteira");
                    await Task.Delay(80);
                    Dispatcher.UIThread.RunJobs();
                    Assert.Contains("Vault para restauração", view.Dialogs.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text));
                    ClickButtonWithText(view.Dialogs, "Continuar");
                    await Task.Delay(150);
                    Dispatcher.UIThread.RunJobs();
                    Assert.Contains("Restaurar este vault?", view.Dialogs.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text));
                    Snapshot(window, "native-import-restore.png");
                    ClickButtonWithText(view.Dialogs, "Restaurar com backup");
                    await Task.Delay(400);
                    Dispatcher.UIThread.RunJobs();
                });
                Assert.Equal("# Comece\n\n[[Um]]\n", File.ReadAllText(Path.Combine(vault, "Comece.md")));
                Assert.False(File.Exists(Path.Combine(vault, "Extra.md")));
                Assert.False(File.Exists(Path.Combine(vault, ".urbe", "import.v1.json")));
                var backup = Directory.GetDirectories(Path.Combine(vault, ".urbe", "backup"), "import-*").Single();
                Assert.Equal("# Mudou\n", File.ReadAllText(Path.Combine(backup, "files", "Comece.md")));
                Assert.Equal("# Extra\n", File.ReadAllText(Path.Combine(backup, "files", "Extra.md")));
                Assert.StartsWith("Vault restaurado. ", view.LifeNoticeText);
                Assert.Null(view.World.City!.Buildings.FirstOrDefault(b => b.Path == "Extra.md"));

                // Backups do vault: the import's own backup brings the replaced vault back (and is itself backed up first)
                await RunWithDialog(view.ShowBackupsAsync(), async () =>
                {
                    Assert.Contains("Antes de importar · 3 arquivo(s)", view.Dialogs.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text));
                    Snapshot(window, "native-backups-list.png");
                    ClickButtonWithText(view.Dialogs, "Antes de importar · 3 arquivo(s)");
                    await Task.Delay(120);
                    Dispatcher.UIThread.RunJobs();
                    ClickButtonWithText(view.Dialogs, "Continuar");
                    await Task.Delay(150);
                    Dispatcher.UIThread.RunJobs();
                    ClickButtonWithText(view.Dialogs, "Restaurar com backup");
                    await Task.Delay(400);
                    Dispatcher.UIThread.RunJobs();
                });
                Assert.Equal("# Mudou\n", File.ReadAllText(Path.Combine(vault, "Comece.md")));
                Assert.Equal("# Extra\n", File.ReadAllText(Path.Combine(vault, "Extra.md")));
                Assert.Equal(2, Directory.GetDirectories(Path.Combine(vault, ".urbe", "backup"), "import-*").Length);
                Assert.StartsWith("Vault restaurado. ", view.LifeNoticeText);
                window.Close();
                return true;
            }
            finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
        }, CancellationToken.None));
}
