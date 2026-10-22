using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Urbe.Client;

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
}
