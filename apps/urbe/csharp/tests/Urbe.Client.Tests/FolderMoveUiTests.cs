using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Urbe.Client;

namespace Urbe.Client.Tests;

/// <summary>
/// UC-54: a whole folder moves (its "⋯" → Mover para, or held and dragged onto another folder) with every file under
/// it and its neighbourhood, and a file that is not a note is dragged onto a folder like a note, through the real
/// native UI against a real folder vault.
/// </summary>
public sealed partial class HeadlessUiTests
{
    [Fact]
    public async Task FolderMove_MovesTheSubtreeTheNeighbourhoodAndDraggedFiles() =>
        Assert.True(await Session.Dispatch(FolderMoveScenario, CancellationToken.None));

    private static async Task<bool> FolderMoveScenario()
    {
        var dir = Path.Combine(Path.GetTempPath(), "urbe-foldermove-" + Guid.NewGuid().ToString("N"));
        var vault = Path.Combine(dir, "vault");
        Directory.CreateDirectory(Path.Combine(vault, "Área", "Antigas"));
        Directory.CreateDirectory(Path.Combine(vault, "Projetos"));
        Directory.CreateDirectory(Path.Combine(vault, "Outra", "Área"));
        File.WriteAllText(Path.Combine(vault, "Comece.md"), "# Comece\n\n[[Ideias]]\n");
        File.WriteAllText(Path.Combine(vault, "Área", "Ideias.md"), "# Ideias\n");
        File.WriteAllText(Path.Combine(vault, "Área", "Antigas", "Velha.md"), "# Velha\n");
        File.WriteAllBytes(Path.Combine(vault, "Área", "foto.png"), [137, 80, 78, 71, 9]);
        File.WriteAllBytes(Path.Combine(vault, "Área", "Antigas", "doc.pdf"), [37, 80, 68, 70, 9]);
        File.WriteAllText(Path.Combine(vault, "Projetos", "Plano.md"), "# Plano\n");
        File.WriteAllText(Path.Combine(vault, "Outra", "Área", "z.md"), "# z\n");
        File.WriteAllBytes(Path.Combine(vault, "livro.pdf"), [37, 80, 68, 70]);
        var previous = UrbeApp.LocalStatePath;
        UrbeApp.LocalStatePath = Path.Combine(dir, "app", "notes-state.json");
        async Task Settled() { for (int i = 0; i < 40; i++) { await Task.Delay(10); Dispatcher.UIThread.RunJobs(); } }
        List<string?> DialogTexts(MainView v) => v.Dialogs.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
        async Task<List<string?>> Refused(MainView v, Func<Task<bool>> move)
        {
            var task = move();
            for (int i = 0; i < 100 && !v.Dialogs.IsOpen; i++) { await Task.Delay(10); Dispatcher.UIThread.RunJobs(); }
            Dispatcher.UIThread.RunJobs();
            var texts = v.Dialogs.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
            ClickButtonWithText(v.Dialogs, "Ok");
            Assert.False(await task);
            return texts;
        }
        try
        {
            var storage = new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "app", "sel.json"));
            var view = new MainView(storage);
            var window = new Window { Width = 1280, Height = 760, Content = view };
            window.Show();
            await view.OpenVaultAsync();
            await Settle(window, view);
            await UntilMapSaved(view);
            var city = view.World.City!;
            var area = city.RegionByPath("Área")!;
            var antigas = city.RegionByPath("Área/Antigas")!;
            var projetos = city.RegionByPath("Projetos")!;
            var ideias = city.Buildings.Single(b => b.IsNote && b.Path == "Área/Ideias.md");
            var (ix, iy) = (ideias.X, ideias.Y);
            bool photoBuilding = city.AttachmentBuilding("Área/foto.png") is not null;

            // the "⋯" of Área → Mover para: the list leaves Área and its sub-folders out; Projetos takes it
            view.ShowNotes();
            Dispatcher.UIThread.RunJobs();
            var e = view.Explorer;
            Assert.True(e.OpenFolderMenu("Área"));
            Dispatcher.UIThread.RunJobs();
            Assert.Contains("Mover para", DialogTexts(view));
            ClickButtonWithText(view.Dialogs, "Mover para");
            await Settled();
            var options = DialogTexts(view);
            Assert.Contains("Projetos", options);
            Assert.DoesNotContain("Área/Antigas", options);
            ClickButtonWithText(view.Dialogs, "Projetos");
            await Settled();
            for (int i = 0; i < 100 && File.Exists(Path.Combine(vault, "Área", "foto.png")); i++) await Settled();

            // every file under it, with the same bytes; the old folders are gone
            Assert.Equal("# Ideias\n", File.ReadAllText(Path.Combine(vault, "Projetos", "Área", "Ideias.md")));
            Assert.Equal("# Velha\n", File.ReadAllText(Path.Combine(vault, "Projetos", "Área", "Antigas", "Velha.md")));
            Assert.Equal(new byte[] { 137, 80, 78, 71, 9 }, File.ReadAllBytes(Path.Combine(vault, "Projetos", "Área", "foto.png")));
            Assert.Equal(new byte[] { 37, 80, 68, 70, 9 }, File.ReadAllBytes(Path.Combine(vault, "Projetos", "Área", "Antigas", "doc.pdf")));
            Assert.False(Directory.Exists(Path.Combine(vault, "Área")));

            // the neighbourhood itself changed parent, with its sub-neighbourhood; the house kept its lot
            Assert.Same(area, city.RegionByPath("Projetos/Área"));
            Assert.Same(antigas, city.RegionByPath("Projetos/Área/Antigas"));
            Assert.Equal(projetos.Id, area.ParentId);
            Assert.Null(city.RegionByPath("Área"));
            Assert.Equal("Projetos/Área/Ideias.md", ideias.Path);
            Assert.Equal(area.Id, ideias.RegionId);
            Assert.Equal((ix, iy), (ideias.X, ideias.Y));
            Assert.Contains(city.Buildings, b => b.Path == "Projetos/Área/Antigas/Velha.md" && b.RegionId == antigas.Id);
            if (photoBuilding) Assert.NotNull(city.AttachmentBuilding("Projetos/Área/foto.png"));
            Assert.NotNull(view.Documents!.Get("Projetos/Área/Ideias.md"));
            Assert.Null(view.Documents.Get("Área/Ideias.md"));

            // never into itself or below it, never onto a folder with that name
            Assert.Contains("Uma pasta não pode ir para dentro dela mesma.", await Refused(view, () => view.MoveFolderAsync("Projetos", "Projetos/Área")));
            Assert.Contains("Já existe “Área” em “Outra”.", await Refused(view, () => view.MoveFolderAsync("Projetos/Área", "Outra")));
            Assert.True(File.Exists(Path.Combine(vault, "Projetos", "Área", "Ideias.md")));
            Assert.True(File.Exists(Path.Combine(vault, "Outra", "Área", "z.md")));

            // hold Outra and drag it onto Projetos: it moves with what is in it
            view.ShowNotes();
            e.ClearSelection();
            e.HoldDelay = TimeSpan.FromMilliseconds(5);
            await Settled();
            Control RowOf(string text) => e.GetVisualDescendants().OfType<Button>()
                .First(b => b.GetLogicalDescendants().OfType<TextBlock>().Any(t => t.Text == text) && b.MinHeight == 48);
            Point Centre(Control c) => c.TranslatePoint(new Point(c.Bounds.Width / 2, c.Bounds.Height / 2), window)!.Value;
            async Task Drag(string what, string onto)
            {
                var from = Centre(RowOf(what));
                window.MouseDown(from, MouseButton.Left);
                await Task.Delay(40);
                Dispatcher.UIThread.RunJobs();
                var to = Centre(RowOf(onto));
                window.MouseMove(new Point(from.X, (from.Y + to.Y) / 2), RawInputModifiers.LeftMouseButton);
                window.MouseMove(to, RawInputModifiers.LeftMouseButton);
                Dispatcher.UIThread.RunJobs();
                Assert.True(e.Dragging);
                window.MouseUp(to, MouseButton.Left);
                await Settled();
            }
            await Drag("Outra", "Projetos");
            for (int i = 0; i < 100 && !File.Exists(Path.Combine(vault, "Projetos", "Outra", "Área", "z.md")); i++) await Settled();
            Assert.True(File.Exists(Path.Combine(vault, "Projetos", "Outra", "Área", "z.md")));
            Assert.False(Directory.Exists(Path.Combine(vault, "Outra")));
            Assert.Equal(projetos.Id, city.RegionByPath("Projetos/Outra")!.ParentId);

            // a file that is not a note: held and let go shows its options; dragged onto a folder it moves there
            view.ShowNotes();
            await Settled();
            var pdf = Centre(RowOf("livro.pdf"));
            window.MouseDown(pdf, MouseButton.Left);
            await Task.Delay(40);
            Dispatcher.UIThread.RunJobs();
            Assert.False(view.Dialogs.IsOpen);
            window.MouseUp(pdf, MouseButton.Left);
            await Settled();
            Assert.Contains("Renomear", DialogTexts(view));
            ClickButtonWithText(view.Dialogs, "Visualizar");
            for (int i = 0; i < 40 && view.FilePreview.Current != "livro.pdf"; i++) { await Task.Delay(10); Dispatcher.UIThread.RunJobs(); }
            view.FilePreview.Close();
            view.ShowNotes();
            await Settled();
            await Drag("livro.pdf", "Projetos");
            for (int i = 0; i < 100 && File.Exists(Path.Combine(vault, "livro.pdf")); i++) await Settled();
            Assert.Equal(new byte[] { 37, 80, 68, 70 }, File.ReadAllBytes(Path.Combine(vault, "Projetos", "livro.pdf")));
            Assert.False(File.Exists(Path.Combine(vault, "livro.pdf")));
            await UntilMapSaved(view);
            window.Close();

            // reopened, the map keeps the moved neighbourhoods where their folders are now
            var again = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "app", "sel.json")));
            var window2 = new Window { Width = 1280, Height = 760, Content = again };
            window2.Show();
            await again.OpenVaultAsync();
            await Settle(window2, again);
            var reopened = again.World.City!;
            // (region ids are the session's; the same neighbourhood is the same ground under its new parent)
            var area2 = reopened.RegionByPath("Projetos/Área")!;
            var antigas2 = reopened.RegionByPath("Projetos/Área/Antigas")!;
            Assert.Equal((area.X, area.Y, area.W, area.H), (area2.X, area2.Y, area2.W, area2.H));
            Assert.Equal((antigas.X, antigas.Y, antigas.W, antigas.H), (antigas2.X, antigas2.Y, antigas2.W, antigas2.H));
            Assert.Equal(reopened.RegionByPath("Projetos")!.Id, area2.ParentId);
            Assert.Null(reopened.RegionByPath("Área"));
            Assert.Null(reopened.RegionByPath("Outra"));
            Assert.Contains(reopened.Buildings, b => b.Path == "Projetos/Área/Ideias.md" && b.RegionId == area2.Id && (b.X, b.Y) == (ix, iy));
            window2.Close();
            return true;
        }
        finally
        {
            UrbeApp.LocalStatePath = previous;
            try { Directory.Delete(dir, true); } catch (IOException) { }
        }
    }

    /// <summary>A file the vault refuses to move stops the folder move: what already moved stays valid, the rest stays
    /// where it was, and nothing is lost.</summary>
    [Fact]
    public async Task FolderMove_StopsAndReportsWhenAFileFailsMidway() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var dir = Path.Combine(Path.GetTempPath(), "urbe-foldermove-fail-" + Guid.NewGuid().ToString("N"));
            var vault = Path.Combine(dir, "vault");
            Directory.CreateDirectory(Path.Combine(vault, "Pasta"));
            Directory.CreateDirectory(Path.Combine(vault, "Destino"));
            File.WriteAllText(Path.Combine(vault, "Comece.md"), "# Comece\n");
            File.WriteAllText(Path.Combine(vault, "Pasta", "A.md"), "# A\n");
            File.WriteAllText(Path.Combine(vault, "Pasta", "B.md"), "# B\n");
            File.WriteAllText(Path.Combine(vault, "Destino", "D.md"), "# D\n");
            // a name the vault never moves (a name ending in a space is unsafe): the third file fails
            File.WriteAllBytes(Path.Combine(vault, "Pasta", "ruim.png "), [1, 2, 3]);
            var previous = UrbeApp.LocalStatePath;
            UrbeApp.LocalStatePath = Path.Combine(dir, "app", "notes-state.json");
            try
            {
                var view = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "app", "sel.json")));
                var window = new Window { Width = 1280, Height = 760, Content = view };
                window.Show();
                await view.OpenVaultAsync();
                await Settle(window, view);
                await UntilMapSaved(view);
                var city = view.World.City!;
                var pasta = city.RegionByPath("Pasta")!;

                var task = view.MoveFolderAsync("Pasta", "Destino");
                for (int i = 0; i < 200 && !view.Dialogs.IsOpen; i++) { await Task.Delay(10); Dispatcher.UIThread.RunJobs(); }
                var message = string.Join("\n", view.Dialogs.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text));
                Assert.Contains("Não foi possível mover a pasta", message);
                Assert.Contains("“Pasta/ruim.png ” não foi movido", message);
                Assert.Contains("2 de 3 arquivos já estão em “Destino/Pasta”; os outros continuam em “Pasta”. Nenhum arquivo foi perdido.", message);
                ClickButtonWithText(view.Dialogs, "Ok");
                Assert.False(await task);

                Assert.Equal("# A\n", File.ReadAllText(Path.Combine(vault, "Destino", "Pasta", "A.md")));
                Assert.Equal("# B\n", File.ReadAllText(Path.Combine(vault, "Destino", "Pasta", "B.md")));
                Assert.False(File.Exists(Path.Combine(vault, "Pasta", "A.md")));
                Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(Path.Combine(vault, "Pasta", "ruim.png ")));
                // the moved houses changed neighbourhood as moved notes do; the old one stays for what is left
                var moved = city.RegionByPath("Destino/Pasta");
                Assert.NotNull(moved);
                Assert.Contains(city.Buildings, b => b.Path == "Destino/Pasta/A.md" && b.RegionId == moved!.Id);
                Assert.Same(pasta, city.RegionByPath("Pasta"));
                Assert.NotNull(view.Documents!.Get("Destino/Pasta/B.md"));
                await UntilMapSaved(view);
                window.Close();
                return true;
            }
            finally
            {
                UrbeApp.LocalStatePath = previous;
                try { Directory.Delete(dir, true); } catch (IOException) { }
            }
        }, CancellationToken.None));
}
