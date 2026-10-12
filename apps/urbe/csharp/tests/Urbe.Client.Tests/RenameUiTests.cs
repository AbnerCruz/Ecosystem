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
    /// Notas (mobile-ui.js): holding a note 430 ms selects it and shows the bar ("1 nota"), the release opens nothing, a tap
    /// while selecting toggles, a tap without a selection opens the note; Limpar seleção ends it.
    /// </summary>
    [Fact]
    public async Task Notes_HoldingSelectsAndATapOpens() =>
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
            var p = Centre("Um");
            window.MouseDown(p, MouseButton.Left);
            for (int i = 0; i < 70; i++) { await Task.Delay(10); Dispatcher.UIThread.RunJobs(); }
            window.MouseUp(p, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(["Um.md"], view.Explorer.SelectedPaths);
            Assert.False(view.Editor.IsVisible);
            Assert.Contains(view.Explorer.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "1 nota");
            Snapshot(window, "native-notes-selection.png");

            // a tap while selecting toggles (2 notas), the bar's Limpar seleção ends it
            view.Explorer.HoldDelay = TimeSpan.FromSeconds(30);   // taps: wall time must not decide on a loaded machine
            var q = Centre("Dois");
            window.MouseDown(q, MouseButton.Left);
            window.MouseUp(q, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(["Um.md", "Dois.md"], view.Explorer.SelectedPaths);
            Assert.Contains(view.Explorer.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "2 notas");
            view.Explorer.GetLogicalDescendants().OfType<Button>().Single(b => ToolTip.GetTip(b) as string == "Limpar seleção")
                .RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            Assert.Empty(view.Explorer.SelectedPaths);

            // without a selection a tap opens the note
            q = Centre("Dois");
            window.MouseDown(q, MouseButton.Left);
            window.MouseUp(q, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            Assert.True(view.Editor.IsVisible);
            Assert.Equal("Dois.md", view.Editor.Current!.Path);
            window.Close();
            return true;
        }, CancellationToken.None));

    /// <summary>
    /// The selection bar on a real vault: Mover para pasta (the file moves, the house joins the neighbourhood and keeps its
    /// lot; a name the folder has is refused), Duplicar ("Um cópia.md", its house 4 tiles away), Favoritar and the
    /// Favoritos and Recentes views, kept on the device.
    /// </summary>
    [Fact]
    public async Task Notes_SelectionBarMovesDuplicatesAndFavorites() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var dir = Path.Combine(Path.GetTempPath(), "urbe-notesbar-" + Guid.NewGuid().ToString("N"));
            var vault = Path.Combine(dir, "vault");
            Directory.CreateDirectory(Path.Combine(vault, "Bairro"));
            Directory.CreateDirectory(Path.Combine(vault, "Outro"));
            File.WriteAllText(Path.Combine(vault, "Comece.md"), "# Comece\n\n[[Um]]\n");
            File.WriteAllText(Path.Combine(vault, "Bairro", "Um.md"), "# Um\n");
            File.WriteAllText(Path.Combine(vault, "Outro", "Comece.md"), "# Outro Comece\n");
            var mapa = Path.Combine(vault, ".urbe", "mapa.json");
            var previous = UrbeApp.LocalStatePath;
            UrbeApp.LocalStatePath = Path.Combine(dir, "app", "notes-state.json");
            async Task Settled() { for (int i = 0; i < 40; i++) { await Task.Delay(10); Dispatcher.UIThread.RunJobs(); } }
            try
            {
                var view = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "app", "sel.json")));
                var window = new Window { Width = 1280, Height = 760, Content = view };
                window.Show();
                await view.OpenVaultAsync();
                await Settle(window, view);
                await UntilMapSaved(view);
                var city = view.World.City!;
                var house = city.Buildings.Single(b => b.Path == "Bairro/Um.md");
                var outro = city.Regions.Single(r => r.Name == "Outro");
                var lot = (house.X, house.Y);
                var id = view.Documents!.Get("Bairro/Um.md")!.Id;
                void Bar(string tip) => view.Explorer.GetLogicalDescendants().OfType<Button>().Single(b => ToolTip.GetTip(b) as string == tip)
                    .RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

                // Mover para pasta → Outro
                view.ShowNotes();
                Assert.True(view.Explorer.TryEnterFolder("Bairro"));
                Assert.True(view.Explorer.HoldNote("Bairro/Um.md"));
                Bar("Mover para pasta");
                await Settled();
                Assert.Contains(view.Dialogs.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "Fora de qualquer pasta");
                ClickButtonWithText(view.Dialogs, "Outro");
                await Settled();
                Assert.False(File.Exists(Path.Combine(vault, "Bairro", "Um.md")));
                Assert.Equal("# Um\n", File.ReadAllText(Path.Combine(vault, "Outro", "Um.md")));
                Assert.Equal(outro.Id, house.RegionId);
                Assert.Equal(lot, (house.X, house.Y));                       // the 1.8.4 keeps the lot
                Assert.Equal(id, view.Documents.Get("Outro/Um.md")!.Id);
                Assert.Empty(view.Explorer.SelectedPaths);

                // Comece → Outro, where Comece.md exists: explorer.move takes "Comece (2).md", nothing is overwritten
                Assert.True(view.Explorer.HoldNote("Comece.md"));
                Bar("Mover para pasta");
                await Settled();
                ClickButtonWithText(view.Dialogs, "Outro");
                await Settled();
                Assert.Equal("# Outro Comece\n", File.ReadAllText(Path.Combine(vault, "Outro", "Comece.md")));
                Assert.Equal("# Comece\n\n[[Um]]\n", File.ReadAllText(Path.Combine(vault, "Outro", "Comece (2).md")));

                // Duplicar Um
                Assert.True(view.Explorer.TryEnterFolder("Outro"));
                Assert.True(view.Explorer.HoldNote("Outro/Um.md"));
                Bar("Duplicar");
                await Settled();
                Assert.Equal("# Um\n", File.ReadAllText(Path.Combine(vault, "Outro", "Um cópia.md")));
                var copy = city.Buildings.Single(b => b.Path == "Outro/Um cópia.md");
                Assert.Equal((house.X + 4, house.Y + 4), (copy.X, copy.Y));

                // Favoritar, then Notas → ⋯ → Favoritos; opening it puts it in Recentes
                Assert.True(view.Explorer.TryEnterFolder("Outro"));
                Assert.True(view.Explorer.HoldNote("Outro/Um cópia.md"));
                Bar("Favoritar");
                Dispatcher.UIThread.RunJobs();
                view.ShowCollection("favorites");
                Assert.Equal("Favoritos", view.Explorer.CollectionTitle);
                Assert.Equal(["Outro/Um cópia.md"], view.Explorer.VisibleNotePaths);
                Assert.True(view.Explorer.TryOpenNote("Outro/Um cópia.md"));
                Assert.True(view.Editor.IsVisible);
                view.Editor.IsVisible = false;
                view.ShowCollection("recent");
                Assert.Equal(["Outro/Um cópia.md"], view.Explorer.VisibleNotePaths);
                Assert.True(File.Exists(UrbeApp.LocalStatePath));

                await UntilMapSaved(view);
                using (var m = System.Text.Json.JsonDocument.Parse(File.ReadAllText(mapa)))
                {
                    var notas = m.RootElement.GetProperty("notas");
                    Assert.Equal(lot, (notas.GetProperty("Outro/Um.md").GetProperty("x").GetInt32(), notas.GetProperty("Outro/Um.md").GetProperty("y").GetInt32()));
                    Assert.True(notas.TryGetProperty("Outro/Um cópia.md", out _));
                }
                window.Close();

                // the favourites are on the device: a new app finds them
                var again = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "app", "sel.json")));
                var window2 = new Window { Width = 1280, Height = 760, Content = again };
                window2.Show();
                await again.OpenVaultAsync();
                await Settle(window2, again);
                again.ShowCollection("favorites");
                Assert.Equal(["Outro/Um cópia.md"], again.Explorer.VisibleNotePaths);
                window2.Close();
                return true;
            }
            finally
            {
                UrbeApp.LocalStatePath = previous;
                try { Directory.Delete(dir, true); } catch (IOException) { }
            }
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

    /// <summary>
    /// Notas → Mais opções → Lixeira: Restaurar brings the file and a house back, ✕ Excluir de vez and Esvaziar ask first
    /// and only rewrite .urbe/trash.v2.json; a new app on the folder finds the restored note.
    /// </summary>
    [Fact]
    public async Task Trash_RestoresPurgesAndEmptiesFromNotes() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var dir = Path.Combine(Path.GetTempPath(), "urbe-trashview-" + Guid.NewGuid().ToString("N"));
            var vault = Path.Combine(dir, "vault");
            Directory.CreateDirectory(Path.Combine(vault, "Bairro"));
            File.WriteAllText(Path.Combine(vault, "Comece.md"), "# Comece\n");
            File.WriteAllText(Path.Combine(vault, "Bairro", "Um.md"), "# Um\n\nTexto.\n");
            File.WriteAllText(Path.Combine(vault, "Bairro", "Dois.md"), "# Dois\n");
            File.WriteAllText(Path.Combine(vault, "Bairro", "Tres.md"), "# Tres\n");
            var trashFile = Path.Combine(vault, ".urbe", "trash.v2.json");
            MainView? view = null;
            async Task Settled() { for (int i = 0; i < 40; i++) { await Task.Delay(10); Dispatcher.UIThread.RunJobs(); } }
            try
            {
                view = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "app", "sel.json")));
                var window = new Window { Width = 1280, Height = 760, Content = view };
                window.Show();
                await view.OpenVaultAsync();
                await Settle(window, view);
                await UntilMapSaved(view);
                foreach (var p in new[] { "Bairro/Um.md", "Bairro/Dois.md", "Bairro/Tres.md" }) await view.DeleteNoteAsync(p, confirmed: true);
                Assert.False(File.Exists(Path.Combine(vault, "Bairro", "Um.md")));

                // Notas → ⋯ → Lixeira
                view.ShowNotes();
                Dispatcher.UIThread.RunJobs();
                var more = view.Explorer.GetLogicalDescendants().OfType<Button>().Single(b => ToolTip.GetTip(b) as string == "Mais opções");
                more.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                Dispatcher.UIThread.RunJobs();
                Assert.Contains(view.Dialogs.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "3 item(ns)");
                ClickButtonWithText(view.Dialogs, "Lixeira");
                await Settled();
                Assert.True(view.Explorer.ShowingTrash);
                Assert.Contains(view.Explorer.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "3 item(ns) · restaure ou exclua de vez");
                Snapshot(window, "native-trash-view.png");

                // Restaurar "Um": the file with its text, a house in Bairro, one item less
                var um = view.Explorer.GetLogicalDescendants().OfType<Border>()
                    .First(b => b.GetLogicalDescendants().OfType<TextBlock>().Any(t => t.Text == "Bairro/Um.md"));
                ClickButtonWithText(um, "Restaurar");
                await Settled();
                Assert.Equal("# Um\n\nTexto.\n", File.ReadAllText(Path.Combine(vault, "Bairro", "Um.md")));
                Assert.Contains(view.World.City!.Buildings, b => b.Path == "Bairro/Um.md");
                Assert.DoesNotContain("Bairro/Um.md", File.ReadAllText(trashFile));
                Assert.Contains(view.Explorer.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "2 item(ns) · restaure ou exclua de vez");

                // ✕ Excluir de vez "Dois": asks, then only the trash changes
                var dois = view.Explorer.GetLogicalDescendants().OfType<Border>()
                    .First(b => b.GetLogicalDescendants().OfType<TextBlock>().Any(t => t.Text == "Bairro/Dois.md"));
                dois.GetLogicalDescendants().OfType<Button>().Single(b => ToolTip.GetTip(b) as string == "Excluir de vez")
                    .RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                await Settled();
                Assert.Contains(view.Dialogs.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "“Dois” será apagada para sempre. Não dá para desfazer.");
                ClickButtonWithText(view.Dialogs, "Excluir de vez");
                await Settled();
                Assert.DoesNotContain("Bairro/Dois.md", File.ReadAllText(trashFile));
                Assert.Contains("Bairro/Tres.md", File.ReadAllText(trashFile));

                // Esvaziar
                ClickButtonWithText(view.Explorer, "Esvaziar");
                await Settled();
                Assert.Contains(view.Dialogs.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "Todos os 1 itens serão apagados para sempre. Não dá para desfazer.");
                ClickButtonWithText(view.Dialogs, "Esvaziar");
                await Settled();
                Assert.Equal("{\n \"version\": 2,\n \"items\": []\n}", File.ReadAllText(trashFile));
                Assert.Contains(view.Explorer.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "A lixeira está vazia");
                await UntilMapSaved(view);
                window.Close();

                var reopened = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "app2", "sel.json")));
                var window2 = new Window { Width = 1280, Height = 760, Content = reopened };
                window2.Show();
                await reopened.OpenVaultAsync();
                await Settle(window2, reopened);
                Assert.Equal(["Bairro/Um.md", "Comece.md"], reopened.World.City!.Buildings.Where(b => b.IsNote).Select(b => b.Path).Order(StringComparer.Ordinal));
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
