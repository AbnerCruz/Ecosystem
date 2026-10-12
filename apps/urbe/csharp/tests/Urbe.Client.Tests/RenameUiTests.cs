using Avalonia;
using Avalonia.Input;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using Urbe.Client;

namespace Urbe.Client.Tests;

/// <summary>"Renomear" from the editor's "Mais ações", driven through the real native UI against a real folder vault.</summary>
public sealed partial class HeadlessUiTests
{
    private static async Task UntilMapSaved(MainView v)
    {
        for (int i = 0; i < 300 && (v.MapSave!.HasPending || v.MapSave.IsBusy); i++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(20);
        }
        Dispatcher.UIThread.RunJobs();
        Assert.False(v.MapSave!.HasPending);
        Assert.Equal("saved", v.MapSave.State);
    }

    private static async Task RenameThroughTheEditorMenu(MainView view, string typed)
    {
        var more = view.Editor.GetLogicalDescendants().OfType<Button>().Single(b => ToolTip.GetTip(b) as string == "Mais ações");
        more.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
        ClickButtonWithText(view.Dialogs, "Renomear");
        await Task.Delay(30);
        Dispatcher.UIThread.RunJobs();
        var input = view.Dialogs.GetVisualDescendants().OfType<TextBox>().Single();
        input.Text = typed;
        ClickButtonWithText(view.Dialogs, "Renomear");
        for (int i = 0; i < 100 && view.Dialogs.IsOpen; i++) { await Task.Delay(10); Dispatcher.UIThread.RunJobs(); }
        for (int i = 0; i < 20; i++) { await Task.Delay(10); Dispatcher.UIThread.RunJobs(); }
    }

    /// <summary>
    /// The file is moved in the vault with the text being edited, the old name disappears, the editor and the
    /// house follow, the document keeps its id, and .urbe/mapa.json is rewritten with the new path; a name taken in
    /// the same neighbourhood becomes " (2)"; reopening the folder finds the house at the same place.
    /// </summary>
    [Fact]
    public async Task Rename_MovesTheFileAndTheMapFollows() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var dir = Path.Combine(Path.GetTempPath(), "urbe-rename-" + Guid.NewGuid().ToString("N"));
            var vault = Path.Combine(dir, "vault");
            Directory.CreateDirectory(Path.Combine(vault, "Bairro"));
            File.WriteAllText(Path.Combine(vault, "Comece.md"), "# Comece\n\n[[Um]]\n");
            File.WriteAllText(Path.Combine(vault, "Bairro", "Um.md"), "# Um\n");
            File.WriteAllText(Path.Combine(vault, "Bairro", "Dois.md"), "# Dois\n");
            File.WriteAllBytes(Path.Combine(vault, "foto.png"), [1, 2, 3]);
            var mapa = Path.Combine(vault, ".urbe", "mapa.json");
            try
            {
                var storage = new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "app", "sel.json"));
                var view = new MainView(storage);
                var window = new Window { Width = 1280, Height = 760, Content = view };
                window.Show();
                await view.OpenVaultAsync();
                await Settle(window, view);
                await UntilMapSaved(view);
                var house = view.World.City!.Buildings.Single(b => b.Path == "Bairro/Um.md");
                var id = view.Documents!.Get("Bairro/Um.md")!.Id;
                var place = (house.X, house.Y);

                // editing, not yet saved: the text goes with the file
                view.OpenNoteByPath("Bairro/Um.md");
                Dispatcher.UIThread.RunJobs();
                view.Editor.SetMode(EditorMode.Fonte);
                view.Editor.Editor.Text = "# Um\n\nTexto novo.\n";
                Dispatcher.UIThread.RunJobs();
                await RenameThroughTheEditorMenu(view, "Primeiro");

                Assert.False(File.Exists(Path.Combine(vault, "Bairro", "Um.md")));
                Assert.Equal("# Um\n\nTexto novo.\n", File.ReadAllText(Path.Combine(vault, "Bairro", "Primeiro.md")));
                Assert.Equal("Bairro/Primeiro.md", house.Path);
                Assert.Equal("Primeiro", house.Name);
                Assert.Equal("Bairro/Primeiro.md", view.Editor.Current!.Path);
                Assert.Equal(id, view.Documents.Get("Bairro/Primeiro.md")!.Id);
                Assert.Null(view.Documents.Get("Bairro/Um.md"));
                await UntilMapSaved(view);
                using (var m = System.Text.Json.JsonDocument.Parse(File.ReadAllText(mapa)))
                {
                    var notas = m.RootElement.GetProperty("notas");
                    Assert.False(notas.TryGetProperty("Bairro/Um.md", out _));
                    var p = notas.GetProperty("Bairro/Primeiro.md");
                    Assert.Equal(id, p.GetProperty("id").GetString());
                    Assert.Equal(place, (p.GetProperty("x").GetInt32(), p.GetProperty("y").GetInt32()));
                }

                // a name another note of the neighbourhood has: v21UniqueName gives " (2)", nothing is overwritten
                await RenameThroughTheEditorMenu(view, "Dois");
                Assert.Equal("Bairro/Dois (2).md", house.Path);
                Assert.Equal("# Dois\n", File.ReadAllText(Path.Combine(vault, "Bairro", "Dois.md")));
                Assert.Equal("# Um\n\nTexto novo.\n", File.ReadAllText(Path.Combine(vault, "Bairro", "Dois (2).md")));
                await UntilMapSaved(view);
                var files = Directory.GetFiles(vault, "*", SearchOption.AllDirectories)
                    .Select(f => Path.GetRelativePath(vault, f).Replace('\\', '/')).Order(StringComparer.Ordinal);
                Assert.Equal([".urbe/mapa.json", "Bairro/Dois (2).md", "Bairro/Dois.md", "Comece.md", "foto.png"], files);
                Snapshot(window, "native-rename-editor.png");
                window.Close();

                // a new app on the same folder: the renamed house where it was
                var reopened = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "app2", "sel.json")));
                var window2 = new Window { Width = 1280, Height = 760, Content = reopened };
                window2.Show();
                await reopened.OpenVaultAsync();
                await Settle(window2, reopened);
                var again = reopened.World.City!.Buildings.Single(b => b.Path == "Bairro/Dois (2).md");
                Assert.Equal(place, (again.X, again.Y));
                Assert.Equal(id, reopened.Documents!.Get("Bairro/Dois (2).md")!.Id);
                window2.Close();
                return true;
            }
            finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
        }, CancellationToken.None));

    /// <summary>
    /// Explorer → hold a note → "Mover para…": into another neighbourhood the file moves and the map follows; to the
    /// root the house moves and the file stays in its folder (as the 1.8.4); onto a name the destination already
    /// has, nothing is overwritten and nothing moves.
    /// </summary>
    [Fact]
    public async Task Move_HoldInTheExplorerMovesTheHouseAndItsFile() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var dir = Path.Combine(Path.GetTempPath(), "urbe-move-" + Guid.NewGuid().ToString("N"));
            var vault = Path.Combine(dir, "vault");
            Directory.CreateDirectory(Path.Combine(vault, "Bairro"));
            Directory.CreateDirectory(Path.Combine(vault, "Outro"));
            File.WriteAllText(Path.Combine(vault, "Comece.md"), "# Comece\n\n[[Um]]\n");
            File.WriteAllText(Path.Combine(vault, "Bairro", "Um.md"), "# Um\n");
            File.WriteAllText(Path.Combine(vault, "Outro", "Comece.md"), "# Outro Comece\n");
            var mapa = Path.Combine(vault, ".urbe", "mapa.json");
            MainView? view = null;
            async Task Pick(string item)
            {
                for (int i = 0; i < 50 && !view!.Dialogs.IsOpen; i++) { await Task.Delay(10); Dispatcher.UIThread.RunJobs(); }
                ClickButtonWithText(view!.Dialogs, item);
                for (int i = 0; i < 40; i++) { await Task.Delay(10); Dispatcher.UIThread.RunJobs(); }
            }
            try
            {
                var storage = new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "app", "sel.json"));
                view = new MainView(storage);
                var window = new Window { Width = 1280, Height = 760, Content = view };
                window.Show();
                await view.OpenVaultAsync();
                await Settle(window, view);
                await UntilMapSaved(view);
                var city = view.World.City!;
                var house = city.Buildings.Single(b => b.Path == "Bairro/Um.md");
                var outro = city.Regions.Single(r => r.Name == "Outro");
                var id = view.Documents!.Get("Bairro/Um.md")!.Id;

                view.ShowNotes();
                Assert.True(view.Explorer.TryEnterFolder("Bairro"));
                Assert.True(view.Explorer.HoldNote("Bairro/Um.md"));
                Dispatcher.UIThread.RunJobs();
                Snapshot(window, "native-move-menu.png");
                await Pick("Outro");
                Assert.False(File.Exists(Path.Combine(vault, "Bairro", "Um.md")));
                Assert.Equal("# Um\n", File.ReadAllText(Path.Combine(vault, "Outro", "Um.md")));
                Assert.Equal(outro.Id, house.RegionId);
                Assert.Equal("Outro/Um.md", house.Path);
                Assert.True(outro.Has(house.X, house.Y));
                Assert.Equal(id, view.Documents.Get("Outro/Um.md")!.Id);
                await UntilMapSaved(view);
                using (var m = System.Text.Json.JsonDocument.Parse(File.ReadAllText(mapa)))
                {
                    var p = m.RootElement.GetProperty("notas").GetProperty("Outro/Um.md");
                    Assert.Equal((house.X, house.Y), (p.GetProperty("x").GetInt32(), p.GetProperty("y").GetInt32()));
                }

                // to the root: the house leaves the neighbourhood, the file stays in its folder
                Assert.True(view.Explorer.TryEnterFolder("Outro"));
                Assert.True(view.Explorer.HoldNote("Outro/Um.md"));
                await Pick("vault");
                Assert.Null(house.RegionId);
                Assert.Equal("# Um\n", File.ReadAllText(Path.Combine(vault, "Outro", "Um.md")));
                await UntilMapSaved(view);

                // onto a name the destination has: refused, nothing overwritten or moved
                var comece = city.Buildings.Single(b => b.Path == "Comece.md");
                var before = (comece.X, comece.Y, comece.RegionId);
                Assert.True(view.Explorer.HoldNote("Comece.md"));
                await Pick("Outro");
                Assert.Contains(view.Dialogs.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "Não foi possível mover");
                ClickButtonWithText(view.Dialogs, "Ok");
                Assert.Equal("# Outro Comece\n", File.ReadAllText(Path.Combine(vault, "Outro", "Comece.md")));
                Assert.True(File.Exists(Path.Combine(vault, "Comece.md")));
                Assert.Equal(before, (comece.X, comece.Y, comece.RegionId));
                window.Close();
                return true;
            }
            finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
        }, CancellationToken.None));

    /// <summary>v21BindEntry: pressing a note row 520 ms opens "Mover para…" and the release opens nothing; a short tap opens the note.</summary>
    [Fact]
    public async Task Explorer_HoldingANoteRowAsksWhereToMoveItAndATapOpensIt() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var view = new MainView();
            var window = new Window { Width = 412, Height = 860, Content = view };
            window.Show();
            await view.LoadAsync([new CityNote("Um.md", "# Um\n"), new CityNote("Dois.md", "# Dois\n")]);
            await Settle(window, view);
            view.ShowNotes();
            Dispatcher.UIThread.RunJobs();
            window.CaptureRenderedFrame();
            Point Centre(string name)
            {
                var row = view.Explorer.GetVisualDescendants().OfType<Button>()
                    .First(b => b.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == name));
                return row.TranslatePoint(new Point(row.Bounds.Width / 2, row.Bounds.Height / 2), window)!.Value;
            }
            var held = new List<string>();
            view.Explorer.NoteHeld += n => held.Add(n.Path);
            var p = Centre("Um");
            window.MouseDown(p, MouseButton.Left);
            for (int i = 0; i < 70; i++) { await Task.Delay(10); Dispatcher.UIThread.RunJobs(); }
            window.MouseUp(p, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(["Um.md"], held);
            Assert.True(view.Dialogs.IsOpen);
            Assert.False(view.Editor.IsVisible);
            ClickButtonWithText(view.Dialogs, "Raiz");   // the Tutorial: the move asks for a vault and changes nothing
            for (int i = 0; i < 20; i++) { await Task.Delay(10); Dispatcher.UIThread.RunJobs(); }
            ClickButtonWithText(view.Dialogs, "Ok");

            // a tap: wall time between press and release must not decide the test on a loaded machine
            view.Explorer.HoldDelay = TimeSpan.FromSeconds(30);
            var q = Centre("Dois");
            window.MouseDown(q, MouseButton.Left);
            window.MouseUp(q, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(["Um.md"], held);
            Assert.True(view.Editor.IsVisible);
            Assert.Equal("Dois.md", view.Editor.Current!.Path);
            window.Close();
            return true;
        }, CancellationToken.None));

    /// <summary>
    /// Excluir from the house panel and "Mover para a lixeira" from the editor: the 1.8.4 confirmation, the file gone,
    /// .urbe/trash.v2.json holding both notes, the houses gone from the city and the map; reopening keeps them deleted.
    /// Cancelling the confirmation deletes nothing.
    /// </summary>
    [Fact]
    public async Task Delete_MovesTheNoteToTheTrashFromThePanelAndTheEditor() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var dir = Path.Combine(Path.GetTempPath(), "urbe-trash-" + Guid.NewGuid().ToString("N"));
            var vault = Path.Combine(dir, "vault");
            Directory.CreateDirectory(Path.Combine(vault, "Bairro"));
            File.WriteAllText(Path.Combine(vault, "Comece.md"), "# Comece\n\n[[Um]]\n");
            File.WriteAllText(Path.Combine(vault, "Bairro", "Um.md"), "# Um\n");
            File.WriteAllText(Path.Combine(vault, "Bairro", "Dois.md"), "# Dois\n");
            var trashFile = Path.Combine(vault, ".urbe", "trash.v2.json");
            MainView? view = null;
            async Task Settled()
            {
                for (int i = 0; i < 40; i++) { await Task.Delay(10); Dispatcher.UIThread.RunJobs(); }
            }
            try
            {
                var storage = new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "app", "sel.json"));
                view = new MainView(storage);
                var window = new Window { Width = 1280, Height = 760, Content = view };
                window.Show();
                await view.OpenVaultAsync();
                await Settle(window, view);
                await UntilMapSaved(view);
                var city = view.World.City!;

                // the house panel: Excluir → "Mover para a lixeira?" → Cancelar keeps everything
                var um = city.Buildings.Single(b => b.Path == "Bairro/Um.md");
                view.World.Select(um);
                Dispatcher.UIThread.RunJobs();
                ClickButtonWithText(view, "Excluir");
                await Settled();
                Assert.Contains(view.Dialogs.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "“Um.md” poderá ser restaurada pela Lixeira, em Notas.");
                Snapshot(window, "native-trash-confirm.png");
                ClickButtonWithText(view.Dialogs, "Cancelar");
                await Settled();
                Assert.True(File.Exists(Path.Combine(vault, "Bairro", "Um.md")));
                Assert.Contains(um, city.Buildings);

                // again, confirmed
                view.World.Select(um);
                Dispatcher.UIThread.RunJobs();
                ClickButtonWithText(view, "Excluir");
                await Settled();
                ClickButtonWithText(view.Dialogs, "Mover para a lixeira");
                await Settled();
                Assert.False(File.Exists(Path.Combine(vault, "Bairro", "Um.md")));
                Assert.DoesNotContain(um, city.Buildings);
                Assert.Null(view.Documents!.Get("Bairro/Um.md"));
                Assert.Contains("\"originalPath\": \"Bairro/Um.md\"", File.ReadAllText(trashFile));

                // the editor: Mais ações → Mover para a lixeira
                view.OpenNoteByPath("Bairro/Dois.md");
                Dispatcher.UIThread.RunJobs();
                var more = view.Editor.GetLogicalDescendants().OfType<Button>().Single(b => ToolTip.GetTip(b) as string == "Mais ações");
                more.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                Dispatcher.UIThread.RunJobs();
                ClickButtonWithText(view.Dialogs, "Mover para a lixeira");
                await Settled();
                ClickButtonWithText(view.Dialogs, "Mover para a lixeira");
                await Settled();
                Assert.False(view.Editor.IsVisible);
                Assert.False(File.Exists(Path.Combine(vault, "Bairro", "Dois.md")));
                var trash = File.ReadAllText(trashFile);
                Assert.Contains("\"content\": \"# Um\\n\"", trash);
                Assert.Contains("\"content\": \"# Dois\\n\"", trash);
                await UntilMapSaved(view);
                using (var m = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(vault, ".urbe", "mapa.json"))))
                    Assert.Equal(["Comece.md"], m.RootElement.GetProperty("notas").EnumerateObject().Select(p => p.Name));
                window.Close();

                // a new app on the same folder: still deleted, the trash as it was
                var reopened = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "app2", "sel.json")));
                var window2 = new Window { Width = 1280, Height = 760, Content = reopened };
                window2.Show();
                await reopened.OpenVaultAsync();
                await Settle(window2, reopened);
                Assert.Equal(["Comece.md"], reopened.World.City!.Buildings.Where(b => b.IsNote).Select(b => b.Path));
                Assert.Equal(trash, File.ReadAllText(trashFile));
                window2.Close();
                return true;
            }
            finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
        }, CancellationToken.None));

    [Fact]
    public async Task Rename_WithoutAWritableVaultRenamesNothing() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var view = new MainView();
            var window = new Window { Width = 412, Height = 860, Content = view };
            window.Show();
            await view.LoadAsync(TutorialNotes.Load());
            await Settle(window, view);
            var house = view.World.City!.Buildings.First(b => b.IsNote);
            var (path, name) = (house.Path, house.Name);
            var rename = view.RenameNoteAsync(path);
            Dispatcher.UIThread.RunJobs();
            Assert.Empty(view.Dialogs.GetVisualDescendants().OfType<TextBox>());
            ClickButtonWithText(view.Dialogs, "Ok");
            await rename;
            Assert.Equal((path, name), (house.Path, house.Name));
            window.Close();
            return true;
        }, CancellationToken.None));
}
