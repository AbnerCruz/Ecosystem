using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Urbe.Client;
using Urbe.Core;

namespace Urbe.Client.Tests;

/// <summary>
/// UC-84 (DEC-0053, ADD-0030): the Área de trabalho, the base of the app — projects as glass boxes with a grid of
/// their notes and files, a box that opens into its folder, the search in a box and at the root, a new project.
/// </summary>
public sealed partial class HeadlessUiTests
{
    private static string WorkspaceVault(string dir)
    {
        var vault = Path.Combine(dir, "vault");
        var curso = Path.Combine(vault, "Matemática", "curso de calculo");
        Directory.CreateDirectory(Path.Combine(curso, "Módulo 1"));
        Directory.CreateDirectory(Path.Combine(vault, "Personagens"));
        File.WriteAllText(Path.Combine(curso, "Matriz curricular.md"), "# Matriz curricular\n\nCinco módulos de cálculo, da modelagem às integrais.\n");
        File.WriteAllText(Path.Combine(curso, "Plano de Aulas.md"), "# Plano de Aulas\n\nCada aula começa por um problema concreto.\n");
        File.WriteAllText(Path.Combine(curso, "Módulo 1", "Aula 01.md"), "# Aula 01\n\nVariáveis, unidades e perguntas que podem ser modeladas.\n");
        File.WriteAllText(Path.Combine(vault, "Matemática", "Ideias.md"), "# Ideias\n\nUm curso de estatística depois.\n");
        File.WriteAllText(Path.Combine(vault, "Personagens", "Herói.md"), "# Herói\n\nAlto, de uma região do norte, criado por pescadores.\n");
        File.WriteAllText(Path.Combine(vault, "Personagens", "Vilã.md"), "# Vilã\n\nQuer o farol para si.\n");
        File.WriteAllText(Path.Combine(vault, "Comece.md"), "# Comece\n\nBem-vindo ao Urbe.\n");
        return vault;
    }

    private static Button Named(Control root, string name) =>
        root.GetLogicalDescendants().OfType<Button>().First(b => AutomationProperties.GetName(b) == name);

    private static void Click(Button b)
    {
        b.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    [Fact]
    public async Task Workspace_ProjectsAreGlassBoxesThatOpenIntoTheirFolder() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var dir = Path.Combine(Path.GetTempPath(), "urbe-area-" + Guid.NewGuid().ToString("N"));
            var vault = WorkspaceVault(dir);
            var previous = UrbeApp.LocalStatePath;
            UrbeApp.LocalStatePath = Path.Combine(dir, "app", "notes-state.json");
            try
            {
                var view = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "app", "sel.json")));
                var window = new Window { Width = 412, Height = 860, Content = view };
                window.Show();
                await view.OpenVaultAsync();
                await Settle(window, view);
                var w = view.Workspace;
                Snapshot(window, "native-area-phone.png");

                // the root: one box per project, the loose notes apart, a new project at the end
                Assert.Equal(["Matemática", "Personagens"], w.Index.Projects.Select(p => p.Folder));
                var texts = w.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
                Assert.Contains("Soltas", texts);
                Assert.Contains("curso de calculo", texts);   // the subfolder, seen through the glass
                Assert.Contains("Um curso de estatística depois.", texts);   // a card shows the start of the note
                Assert.Contains("Novo projeto", texts);

                // a box opens into its folder, with the way back
                Click(Named(w, "Matemática"));
                Assert.Equal("Matemática", w.Folder);
                Click(Named(w, "curso de calculo"));
                Assert.Equal("Matemática/curso de calculo", w.Folder);
                texts = w.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
                Assert.Contains("Módulo 1", texts);
                Assert.Contains("Matriz curricular", texts);
                Assert.Contains("Plano de Aulas", texts);
                Snapshot(window, "native-area-phone-box.png");
                ClickButtonWithText(w, "Matemática");   // the crumb
                Assert.Equal("Matemática", w.Folder);

                // a card opens its note in the editor, over the Área de trabalho
                Click(Named(w, "Ideias"));
                await Settle(window, view);
                Assert.True(view.Editor.IsVisible);
                Assert.Equal("Matemática/Ideias.md", view.Editor.Current!.Path);
                view.Editor.IsVisible = false;
                Dispatcher.UIThread.RunJobs();
                Assert.Equal("Matemática", w.Folder);

                // the dock's Notas goes back to the projects
                view.ShowWorkspace();
                Assert.Equal("", w.Folder);
                window.Close();
                return true;
            }
            finally
            {
                UrbeApp.LocalStatePath = previous;
                try { Directory.Delete(dir, true); } catch (IOException) { }
            }
        }, CancellationToken.None));

    [Fact]
    public async Task Workspace_SearchesEveryProjectOrOneBox() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var dir = Path.Combine(Path.GetTempPath(), "urbe-area-search-" + Guid.NewGuid().ToString("N"));
            var vault = WorkspaceVault(dir);
            var previous = UrbeApp.LocalStatePath;
            UrbeApp.LocalStatePath = Path.Combine(dir, "app", "notes-state.json");
            try
            {
                var view = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "app", "sel.json")));
                var window = new Window { Width = 1280, Height = 800, Content = view };
                window.Show();
                await view.OpenVaultAsync();
                await Settle(window, view);
                var w = view.Workspace;
                Snapshot(window, "native-area-desktop.png");

                // at the root: every project, the results grouped by box
                w.Search.Text = "aula";
                w.Render();
                var cards = w.GetLogicalDescendants().OfType<Button>().Select(AutomationProperties.GetName).Where(n => n is "Aula 01" or "Plano de Aulas" or "Herói").ToList();
                Assert.Equal(["Aula 01", "Plano de Aulas"], cards.Order(StringComparer.Ordinal));
                Snapshot(window, "native-area-desktop-search.png");

                // in a box: only what is inside it
                w.Open("Personagens");
                w.Search.Text = "norte";
                w.Render();
                Assert.Contains("Nada com “norte”.", w.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text));
                w.Search.Text = "heroi";
                w.Render();
                Assert.Contains(w.GetLogicalDescendants().OfType<Button>(), b => AutomationProperties.GetName(b) == "Herói");
                window.Close();
                return true;
            }
            finally
            {
                UrbeApp.LocalStatePath = previous;
                try { Directory.Delete(dir, true); } catch (IOException) { }
            }
        }, CancellationToken.None));

    [Fact]
    public async Task Workspace_ANewProjectIsAFolderAtTheTopThatOpens() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var dir = Path.Combine(Path.GetTempPath(), "urbe-area-new-" + Guid.NewGuid().ToString("N"));
            var vault = Path.Combine(dir, "vault");
            Directory.CreateDirectory(vault);
            try
            {
                var view = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "app", "sel.json")));
                var window = new Window { Width = 412, Height = 860, Content = view };
                window.Show();
                await view.OpenVaultAsync();
                await Settle(window, view);
                // an empty vault invites to the first project
                Assert.Contains("Cada projeto é uma pasta do vault, mostrada como uma caixa: o que está dentro aparece através do vidro.",
                    view.Workspace.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text));
                Snapshot(window, "native-area-empty.png");
                await view.CreateProjectAsync("Romance");
                await Settle(window, view);
                Assert.True(Directory.Exists(Path.Combine(vault, "Romance")));
                Assert.Equal("Romance", view.Workspace.Folder);
                Assert.Contains("Pasta vazia", view.Workspace.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text));
                view.ShowWorkspace();
                Assert.Equal(["Romance"], view.Workspace.Index.Projects.Select(p => p.Folder));
                window.Close();
                return true;
            }
            finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
        }, CancellationToken.None));

    [Fact]
    public async Task Workspace_HoldingACardShowsItsInformationAndActions() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var dir = Path.Combine(Path.GetTempPath(), "urbe-area-hold-" + Guid.NewGuid().ToString("N"));
            var vault = WorkspaceVault(dir);
            var previous = UrbeApp.LocalStatePath;
            UrbeApp.LocalStatePath = Path.Combine(dir, "app", "notes-state.json");
            try
            {
                var view = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "app", "sel.json")));
                var window = new Window { Width = 412, Height = 860, Content = view };
                window.Show();
                await view.OpenVaultAsync();
                await Settle(window, view);
                var w = view.Workspace;
                // a hold (here the right click that stands for it) on a card: the sheet, and nothing opens
                var card = Named(w, "Herói");
                card.RaiseEvent(new ContextRequestedEventArgs());
                Dispatcher.UIThread.RunJobs();
                Assert.True(view.Dialogs.IsOpen);
                Assert.False(view.Editor.IsVisible);
                var texts = view.Dialogs.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text ?? "").ToList();
                Assert.Contains(texts, t => t.StartsWith("Pasta: Personagens", StringComparison.Ordinal) && t.Contains("palavras · cita 0 · citada por 0", StringComparison.Ordinal));
                foreach (var action in new[] { "Abrir", "Renomear", "Mover para", "Duplicar", "Favoritar", "Mover para a lixeira" }) Assert.Contains(action, texts);
                Snapshot(window, "native-area-hold.png");
                // Favoritar: it joins the favourites
                ClickButtonWithText(view.Dialogs, "Favoritar");
                for (int i = 0; i < 40 && view.Dialogs.IsOpen; i++) { await Task.Delay(10); Dispatcher.UIThread.RunJobs(); }
                Assert.Contains(view.Documents!.Get("Personagens/Herói.md")!.Id, view.ExplorerState.Favorites);
                // held again: Tirar dos favoritos
                Named(w, "Herói").RaiseEvent(new ContextRequestedEventArgs());
                Dispatcher.UIThread.RunJobs();
                ClickButtonWithText(view.Dialogs, "Tirar dos favoritos");
                for (int i = 0; i < 40 && view.Dialogs.IsOpen; i++) { await Task.Delay(10); Dispatcher.UIThread.RunJobs(); }
                Assert.DoesNotContain(view.Documents!.Get("Personagens/Herói.md")!.Id, view.ExplorerState.Favorites);
                // Abrir opens it
                Named(w, "Herói").RaiseEvent(new ContextRequestedEventArgs());
                Dispatcher.UIThread.RunJobs();
                ClickButtonWithText(view.Dialogs, "Abrir");
                for (int i = 0; i < 40 && !view.Editor.IsVisible; i++) { await Task.Delay(10); Dispatcher.UIThread.RunJobs(); }
                Assert.Equal("Personagens/Herói.md", view.Editor.Current!.Path);
                window.Close();
                return true;
            }
            finally
            {
                UrbeApp.LocalStatePath = previous;
                try { Directory.Delete(dir, true); } catch (IOException) { }
            }
        }, CancellationToken.None));

    [Fact]
    public async Task Workspace_FiltersByKindAndTagOrdersAndShowsAsAList() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var dir = Path.Combine(Path.GetTempPath(), "urbe-area-filter-" + Guid.NewGuid().ToString("N"));
            var vault = WorkspaceVault(dir);
            File.WriteAllText(Path.Combine(vault, "Personagens", "Herói.md"), "# Herói\n\nAlto, de uma região do norte. #protagonista\n");
            File.WriteAllBytes(Path.Combine(vault, "Personagens", "retrato.png"), [0x89, 0x50, 0x4E, 0x47]);
            var previous = UrbeApp.LocalStatePath;
            UrbeApp.LocalStatePath = Path.Combine(dir, "app", "notes-state.json");
            try
            {
                var view = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "app", "sel.json")));
                var window = new Window { Width = 412, Height = 860, Content = view };
                window.Show();
                await view.OpenVaultAsync();
                await Settle(window, view);
                var w = view.Workspace;
                List<string?> Shown() => w.GetLogicalDescendants().OfType<Button>().Where(b => b.IsEffectivelyVisible && ToolTip.GetTip(b) is string tip && tip.Contains('/', StringComparison.Ordinal) || ToolTip.GetTip(b) as string == "Comece.md")
                    .Select(AutomationProperties.GetName).ToList();

                // the box of a project: its kinds as chips; Imagens shows only the picture
                w.Open("Personagens");
                Assert.Contains(w.GetLogicalDescendants().OfType<Button>(), b => AutomationProperties.GetName(b) == "Imagens");
                Click(Named(w, "Imagens"));
                Assert.Equal(["retrato.png"], Shown());
                // a tag of the box
                Click(Named(w, "Tudo"));
                Click(Named(w, "#protagonista"));
                Assert.Equal(["Herói"], Shown());
                Click(Named(w, "#protagonista"));   // again: off
                Assert.Null(w.TagFilter);

                // the order: recentes → nome → criação
                w.Open("Matemática/curso de calculo");
                Click(Named(w, "Ordem"));
                Assert.Equal(WorkspaceOrder.Name, w.Order);
                Assert.Equal(["Matriz curricular", "Plano de Aulas"], Shown());
                // as a list: rows instead of cards
                Click(Named(w, "Ver em lista"));
                Assert.True(w.AsList);
                Assert.All(w.GetLogicalDescendants().OfType<Button>().Where(b => ToolTip.GetTip(b) is string t && t.StartsWith("Matemática/", StringComparison.Ordinal) && t.EndsWith(".md", StringComparison.Ordinal)),
                    b => Assert.Equal(56, b.MinHeight));
                Snapshot(window, "native-area-list.png");

                // at the root, a kind looks in every project, grouped by box
                w.Open("");
                w.KindFilter = WorkspaceItemKind.Image;
                Assert.Equal(["retrato.png"], Shown());
                window.Close();
                return true;
            }
            finally
            {
                UrbeApp.LocalStatePath = previous;
                try { Directory.Delete(dir, true); } catch (IOException) { }
            }
        }, CancellationToken.None));

    [Fact]
    public async Task Workspace_BoxesKeepTheirLookAndOrderInAreaJson() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var dir = Path.Combine(Path.GetTempPath(), "urbe-area-look-" + Guid.NewGuid().ToString("N"));
            var vault = WorkspaceVault(dir);
            var selection = Path.Combine(dir, "app", "sel.json");
            var previous = UrbeApp.LocalStatePath;
            UrbeApp.LocalStatePath = Path.Combine(dir, "app", "notes-state.json");
            var file = Path.Combine(vault, ".urbe", "area.json");
            async Task Until(Func<bool> done) { for (int i = 0; i < 300 && !done(); i++) { await Task.Delay(10); Dispatcher.UIThread.RunJobs(); } }
            // the app may be writing the file right now (an exclusive write): read it when it lets go
            string Read() { try { return File.Exists(file) ? File.ReadAllText(file) : ""; } catch (IOException) { return ""; } }
            try
            {
                var view = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(vault), selection));
                var window = new Window { Width = 412, Height = 860, Content = view };
                window.Show();
                await view.OpenVaultAsync();
                await Settle(window, view);
                Assert.Equal(["Matemática", "Personagens"], view.Workspace.ProjectOrder);

                // the order of the boxes
                await view.MoveBoxAsync("Personagens", -1);
                Assert.Equal(["Personagens", "Matemática"], view.Workspace.ProjectOrder);
                await Until(() => Read().Contains("\"Personagens\"", StringComparison.Ordinal));
                Assert.Contains("\"Personagens\"", Read());
                Assert.Contains("única autoridade do conteúdo", Read());

                // Aparência da caixa: colour, border, icon
                var editing = view.EditBoxAsync("Matemática");
                ClickButtonWithText(view.Dialogs, "Lilás");
                await Until(() => view.Dialogs.GetLogicalDescendants().OfType<TextBlock>().Any(t => t.Text == "Tracejada"));
                ClickButtonWithText(view.Dialogs, "Tracejada");
                await Until(() => view.Dialogs.GetLogicalDescendants().OfType<TextBlock>().Any(t => t.Text == "Livro"));
                ClickButtonWithText(view.Dialogs, "Livro");
                await editing;
                var box = view.Area.Of("Matemática")!;
                Assert.Equal(("tracejada", "livro"), (box.Border, box.Icon));
                Assert.Equal(3, view.Area.ColorOf("Matemática/curso de calculo"));   // Lilás, for the subfolders too
                await Until(() => Read().Contains("\"tracejada\"", StringComparison.Ordinal));
                Assert.Contains("\"tracejada\"", Read());
                Snapshot(window, "native-area-look.png");

                // a box keeps its order and its list
                view.Workspace.Open("Personagens");
                view.Workspace.Order = WorkspaceOrder.Name;
                view.Workspace.AsList = true;
                await Until(() => Read().Contains("\"lista\": true", StringComparison.Ordinal));
                Assert.Equal((WorkspaceOrder?)WorkspaceOrder.Name, view.Area.Of("Personagens")!.Order);
                view.Workspace.Open("");
                view.Workspace.Open("Personagens");
                Assert.True(view.Workspace.AsList);

                // a folder renamed by the app: its box follows
                Assert.True(await view.RenameFolderAsync("Matemática", "Exatas"));
                await Until(() => Read().Contains("\"Exatas\"", StringComparison.Ordinal));
                Assert.Null(view.Area.Of("Matemática"));
                Assert.Equal("livro", view.Area.Of("Exatas")!.Icon);
                window.Close();

                // and it is all there on the next opening
                var again = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(vault), selection));
                var window2 = new Window { Width = 412, Height = 860, Content = again };
                window2.Show();
                await again.InitializeAsync();
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(["Personagens", "Exatas"], again.Workspace.ProjectOrder);
                Assert.Equal("tracejada", again.Area.Of("Exatas")!.Border);
                window2.Close();
                return true;
            }
            finally
            {
                UrbeApp.LocalStatePath = previous;
                try { Directory.Delete(dir, true); } catch (IOException) { }
            }
        }, CancellationToken.None));

    [Fact]
    public async Task Sidebar_TheEdgeDragsItsWidthAndTheTreeShowsItsLevels() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var dir = Path.Combine(Path.GetTempPath(), "urbe-sidebar-" + Guid.NewGuid().ToString("N"));
            var vault = WorkspaceVault(dir);
            var previous = UrbeApp.LocalStatePath;
            UrbeApp.LocalStatePath = Path.Combine(dir, "app", "notes-state.json");
            try
            {
                var view = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "app", "sel.json")));
                var window = new Window { Width = 1280, Height = 800, Content = view };
                window.Show();
                await view.OpenVaultAsync();
                await Settle(window, view);
                Assert.Equal(MainView.SidebarWidth, view.Explorer.Bounds.Width);
                // the edge of the sidebar, dragged: wider, kept on the device
                var edge = view.Explorer.TranslatePoint(new Point(view.Explorer.Bounds.Width - 1, 400), window)!.Value;
                window.MouseDown(edge, MouseButton.Left);
                window.MouseMove(edge + new Point(60, 0), RawInputModifiers.LeftMouseButton);
                window.MouseMove(edge + new Point(120, 0), RawInputModifiers.LeftMouseButton);
                window.MouseUp(edge + new Point(120, 0), MouseButton.Left);
                await Settle(window, view);
                Assert.InRange(view.CurrentSidebarWidth, 430, 470);
                Assert.Equal(view.CurrentSidebarWidth, view.Explorer.Bounds.Width);
                Assert.Equal(view.CurrentSidebarWidth, new ExplorerState(UrbeApp.LocalStatePath).SidebarWidth);

                // a folder opened in the tree: its notes one level in, with the level's guide
                Assert.True(view.Explorer.TryEnterFolder("Personagens"));
                Dispatcher.UIThread.RunJobs();
                var nested = view.Explorer.GetLogicalDescendants().OfType<Panel>()
                    .Where(p => p.Children.OfType<Button>().Any(b => b.GetLogicalDescendants().OfType<TextBlock>().Any(t => t.Text == "Herói.md"))).ToList();
                Assert.NotEmpty(nested);
                Snapshot(window, "native-sidebar-desktop.png");
                window.Close();
                return true;
            }
            finally
            {
                UrbeApp.LocalStatePath = previous;
                try { Directory.Delete(dir, true); } catch (IOException) { }
            }
        }, CancellationToken.None));

    [Fact]
    public async Task Workspace_ACardDraggedOntoAnotherBoxMovesThere() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            // UC-84 fatia 4: with the mouse, a card dragged onto another box goes to that project
            var dir = Path.Combine(Path.GetTempPath(), "urbe-area-drag-" + Guid.NewGuid().ToString("N"));
            var vault = WorkspaceVault(dir);
            try
            {
                var view = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "app", "sel.json")));
                var window = new Window { Width = 1280, Height = 800, Content = view };
                window.Show();
                await view.OpenVaultAsync();
                await Settle(window, view);
                var card = Named(view.Workspace, "Herói");
                var box = view.Workspace.GetLogicalDescendants().OfType<Control>().First(c => c.Classes.Contains("ws-drop") && c.Tag as string == "Matemática");
                var from = card.TranslatePoint(new Point(card.Bounds.Width / 2, card.Bounds.Height / 2), window)!.Value;
                var to = box.TranslatePoint(new Point(box.Bounds.Width / 2, 40), window)!.Value;
                window.MouseDown(from, MouseButton.Left, RawInputModifiers.None);
                for (int i = 1; i <= 6; i++)
                {
                    window.MouseMove(new Point(from.X + (to.X - from.X) * i / 6, from.Y + (to.Y - from.Y) * i / 6), RawInputModifiers.LeftMouseButton);
                    Dispatcher.UIThread.RunJobs();
                }
                Snapshot(window, "native-workspace-drag.png");
                window.MouseUp(to, MouseButton.Left, RawInputModifiers.None);
                var moved = Path.Combine(vault, "Matemática", "Herói.md");
                for (int i = 0; i < 300 && !File.Exists(moved); i++) { await Task.Delay(10); Dispatcher.UIThread.RunJobs(); }
                Assert.True(File.Exists(moved));
                Assert.False(File.Exists(Path.Combine(vault, "Personagens", "Herói.md")));
                Assert.False(view.Editor.IsVisible);   // the drop opened nothing

                // a project's head dragged onto another project takes its place (kept in .urbe/area.json)
                await Settle(window, view);
                Assert.Equal(["Matemática", "Personagens"], view.Workspace.ProjectOrder);
                var head = Named(view.Workspace, "Personagens");
                var target = view.Workspace.GetLogicalDescendants().OfType<Control>().First(c => c.Classes.Contains("ws-drop") && c.Tag as string == "Matemática");
                from = head.TranslatePoint(new Point(40, head.Bounds.Height / 2), window)!.Value;
                to = target.TranslatePoint(new Point(target.Bounds.Width / 2, 40), window)!.Value;
                window.MouseDown(from, MouseButton.Left, RawInputModifiers.None);
                for (int i = 1; i <= 6; i++)
                {
                    window.MouseMove(new Point(from.X + (to.X - from.X) * i / 6, from.Y + (to.Y - from.Y) * i / 6), RawInputModifiers.LeftMouseButton);
                    Dispatcher.UIThread.RunJobs();
                }
                window.MouseUp(to, MouseButton.Left, RawInputModifiers.None);
                for (int i = 0; i < 300 && view.Workspace.ProjectOrder[0] != "Personagens"; i++) { await Task.Delay(10); Dispatcher.UIThread.RunJobs(); }
                Assert.Equal(["Personagens", "Matemática"], view.Workspace.ProjectOrder);
                Assert.Equal("", view.Workspace.Folder);   // the drop opened no box
                window.Close();
                return true;
            }
            finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
        }, CancellationToken.None));

    [Fact]
    public async Task Constellation_ShowsTheSkyFocusesAStarAndOpensItsNote() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            // UC-72: the notes as a calm night sky; the places are kept in a file derived from the notes
            var dir = Path.Combine(Path.GetTempPath(), "urbe-sky-" + Guid.NewGuid().ToString("N"));
            var vault = WorkspaceVault(dir);
            File.WriteAllText(Path.Combine(vault, "Personagens", "Herói.md"), "# Herói\n\nInimigo de [[Vilã]].\n");
            var file = Path.Combine(vault, ".urbe", "constelacao.json");
            string Read() { try { return File.Exists(file) ? File.ReadAllText(file) : ""; } catch (IOException) { return ""; } }
            try
            {
                var view = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "app", "sel.json")));
                var window = new Window { Width = 1280, Height = 800, Content = view };
                window.Show();
                await view.OpenVaultAsync();
                await Settle(window, view);
                Click(Named(view.Workspace, "Constelação"));
                for (int i = 0; i < 300 && !(view.SkyPanel.IsVisible && Read().Length > 0); i++) { await Task.Delay(10); Dispatcher.UIThread.RunJobs(); }
                Assert.True(view.SkyPanel.IsVisible);
                var kept = Read();
                Assert.Contains("única autoridade", kept);
                Assert.False(File.Exists(Path.Combine(vault, ".urbe", "mapa.json")));
                Assert.False(File.Exists(Path.Combine(vault, ".urbe", "mundo.json")));
                await Settle(window, view);
                Snapshot(window, "native-constellation.png");

                // a tap shows the star and its links, the rest dimmed; a second tap opens the note
                var hero = view.Documents!.Get("Personagens/Herói.md")!.Id;
                var sky = view.SkyPanel.Sky;
                sky.TapAt(sky.PointOf(hero)!.Value);
                Assert.Equal(hero, sky.FocusedStar);
                await Settle(window, view);
                Snapshot(window, "native-constellation-focus.png");
                sky.TapAt(sky.PointOf(hero)!.Value);
                Dispatcher.UIThread.RunJobs();
                Assert.False(view.SkyPanel.IsVisible);
                Assert.True(view.Editor.IsVisible);
                Assert.Equal("Personagens/Herói.md", view.Editor.Current?.Path);

                // opened again with the same notes: nobody moves, nothing is written
                await view.ShowConstellationAsync();
                Assert.Equal(kept, Read());
                window.Close();
                return true;
            }
            finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
        }, CancellationToken.None));

    [Fact]
    public async Task Explorer_ManyNotesMakeOnlyTheRowsNearTheScreen() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            // UC-78: three thousand notes in an open folder: only the rows near the screen are made, and scrolling makes the rest
            var explorer = new NotesExplorer { State = new ExplorerState(null) };
            var window = new Window { Width = 412, Height = 860, Content = explorer };
            window.Show();
            explorer.State!.Expand("Pasta", true);
            var watch = System.Diagnostics.Stopwatch.StartNew();
            explorer.SetNotes(Enumerable.Range(0, 3000).Select(i => new CityNote("Pasta/Nota " + i.ToString("0000", System.Globalization.CultureInfo.InvariantCulture) + ".md", "# Nota")));
            Dispatcher.UIThread.RunJobs();
            var took = watch.ElapsedMilliseconds;
            Assert.True(explorer.MadeRows < 200, "linhas feitas: " + explorer.MadeRows);
            string[] Texts() => [.. explorer.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text ?? "")];
            Assert.Contains("Nota 0000.md", Texts());
            Assert.DoesNotContain("Nota 2999.md", Texts());
            // the end of the list
            var scroll = explorer.GetLogicalDescendants().OfType<ScrollViewer>().First(s => s.Content is StackPanel);
            for (int i = 0; i < 5; i++) { scroll.Offset = new Vector(0, scroll.Extent.Height); await Task.Delay(10); Dispatcher.UIThread.RunJobs(); }
            Assert.Contains("Nota 2999.md", Texts());
            Assert.True(explorer.MadeRows < 200, "linhas feitas: " + explorer.MadeRows);
            Assert.True(explorer.TryOpenNote("Pasta/Nota 1500.md"));
            Console.WriteLine("explorer 3000 notas: " + took + " ms");
            window.Close();
            return true;
        }, CancellationToken.None));

    [Fact]
    public async Task Sidebar_ARowIsRenamedInPlace() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            // UC-83: select one row and Renomear (or F2): its name becomes a box; Enter renames, Esc leaves it
            var dir = Path.Combine(Path.GetTempPath(), "urbe-rename-" + Guid.NewGuid().ToString("N"));
            var vault = WorkspaceVault(dir);
            try
            {
                var view = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "app", "sel.json")));
                var window = new Window { Width = 1280, Height = 800, Content = view };
                window.Show();
                await view.OpenVaultAsync();
                await Settle(window, view);
                var explorer = view.Explorer;

                // a note: hold to select, then Renomear in the bar
                Assert.True(explorer.HoldNote("Comece.md"));
                Dispatcher.UIThread.RunJobs();
                Click(Named(explorer, "Renomear"));
                Assert.Equal("Comece.md", explorer.Renaming);
                var box = explorer.GetLogicalDescendants().OfType<TextBox>().First(t => AutomationProperties.GetName(t) == "Novo nome");
                Assert.Equal("Comece.md", box.Text);
                await Settle(window, view);
                Assert.Equal((0, 6), (box.SelectionStart, box.SelectionEnd));   // the name, not the extension
                Snapshot(window, "native-sidebar-rename.png");
                box.Text = "Boas-vindas.md";
                box.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });
                Assert.Null(explorer.Renaming);
                var renamed = Path.Combine(vault, "Boas-vindas.md");
                for (int i = 0; i < 300 && (!File.Exists(renamed) || File.Exists(Path.Combine(vault, "Comece.md"))); i++) { await Task.Delay(10); Dispatcher.UIThread.RunJobs(); }
                Assert.True(File.Exists(renamed));
                Assert.False(File.Exists(Path.Combine(vault, "Comece.md")));

                // a folder by its "⋯" → Renomear; Esc leaves it as it was
                Assert.True(explorer.OpenFolderMenu("Personagens"));
                for (int i = 0; i < 20 && !view.Dialogs.IsOpen; i++) { await Task.Delay(5); Dispatcher.UIThread.RunJobs(); }
                ClickButtonWithText(view.Dialogs, "Renomear");
                for (int i = 0; i < 50 && explorer.Renaming is null; i++) { await Task.Delay(5); Dispatcher.UIThread.RunJobs(); }
                Assert.Equal(NotesExplorer.FolderPrefix + "Personagens", explorer.Renaming);
                box = explorer.GetLogicalDescendants().OfType<TextBox>().First(t => AutomationProperties.GetName(t) == "Novo nome");
                box.Text = "Gente";
                box.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape });
                Assert.Null(explorer.Renaming);
                await Task.Delay(50);
                Dispatcher.UIThread.RunJobs();
                Assert.True(Directory.Exists(Path.Combine(vault, "Personagens")));
                Assert.False(Directory.Exists(Path.Combine(vault, "Gente")));
                window.Close();
                return true;
            }
            finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
        }, CancellationToken.None));
}
