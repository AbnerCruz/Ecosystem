using Avalonia.Controls;
using Avalonia.LogicalTree;
using Lunet.Client;
using Lunet.Client.Shell;
using Lunet.Core;

namespace Lunet.Tests;

/// <summary>Renaming, duplicating and deleting games without losing data (core and Home of the shell).</summary>
public sealed class ProjectManageTests
{
    private static string NewRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "lunet-manage-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    [Fact]
    public void Rename_KeepsTheApkIdentity_AndRefusesATakenName()
    {
        var root = NewRoot();
        try
        {
            var store = new ProjectStore(root);
            var original = store.Create("Nave");
            store.Create("Outro");
            var (gameId, package) = (original.Manifest.GameId, original.Manifest.PackageId);
            Assert.Throws<ProjectException>(() => store.Rename("Nave", "Outro"));
            Assert.Throws<ProjectException>(() => store.Rename("Nave", "com espaço"));
            var renamed = store.Rename("Nave", "Foguete");
            Assert.Equal("Foguete", renamed.Name);
            Assert.Equal(gameId, renamed.Manifest.GameId);
            Assert.Equal(package, renamed.Manifest.PackageId);
            Assert.Equal(["Foguete", "Outro"], store.List());
            Assert.Equal("foguete", store.Rename("Foguete", "foguete").Name); // case only
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void Duplicate_CopiesTheFilesWithANewIdentity_WithoutCachesOrHistory()
    {
        var root = NewRoot();
        try
        {
            var store = new ProjectStore(root);
            var original = store.Create("Nave");
            original.WriteText("Content/fase.json", "{\"nivel\":1}");
            Directory.CreateDirectory(Path.Combine(original.Directory, ".lunet", "saves"));
            File.WriteAllText(Path.Combine(original.Directory, ".lunet", "saves", "slot.json"), "{}");
            var copy = store.Duplicate("Nave");
            Assert.Equal("Nave-copia", copy.Name);
            Assert.Equal("{\"nivel\":1}", copy.ReadText("Content/fase.json"));
            Assert.Equal(original.ReadText(original.Manifest.EntryPoint), copy.ReadText(copy.Manifest.EntryPoint));
            Assert.NotEqual(original.Manifest.GameId, copy.Manifest.GameId);
            Assert.NotEqual(original.Manifest.PackageId, copy.Manifest.PackageId);
            Assert.False(Directory.Exists(Path.Combine(copy.Directory, ".lunet")));
            Assert.Null(store.FindIdentityConflict(copy.Manifest, copy.Directory));
            Assert.Equal("Nave-copia-2", store.Duplicate("Nave").Name);
            Assert.False(Directory.Exists(Path.Combine(root, ".lunet-imports")) && Directory.EnumerateFileSystemEntries(Path.Combine(root, ".lunet-imports")).Any());
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void Trash_KeepsTheGameUntilPurged_AndRestoresUnderAFreeName()
    {
        var root = NewRoot();
        try
        {
            var store = new ProjectStore(root);
            var game = store.Create("Nave");
            game.WriteText("Notes/ideia.md", "chefe no fim");
            var trashed = store.MoveToTrash("Nave");
            Assert.Empty(store.List()); // gone from the games, not from the disk
            var listed = Assert.Single(store.Trash());
            Assert.Equal("Nave", listed.Name);
            Assert.Equal(trashed.Id, listed.Id);

            store.Create("Nave"); // the name was taken meanwhile
            var restored = store.Restore(trashed.Id);
            Assert.Equal("Nave-2", restored.Name);
            Assert.Equal("chefe no fim", restored.ReadText("Notes/ideia.md"));
            Assert.Empty(store.Trash());

            var again = store.MoveToTrash("Nave-2");
            store.Purge(again.Id);
            Assert.Empty(store.Trash());
            Assert.Equal(["Nave"], store.List());
            Assert.Throws<ProjectException>(() => store.Restore(again.Id));
            Assert.Throws<ProjectException>(() => store.Purge("../Nave~1"));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void Home_NewGameSheet_ActionsUndoAndTrash_WorkByTaps()
    {
        var root = NewRoot();
        try
        {
            UiSession.Run(() =>
            {
                var shell = new LunetShell(root);
                var window = new Window { Width = 412, Height = 915, Content = shell };
                window.Show();
                var home = shell.Home;

                home.ShowCreate();
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Assert.NotNull(home.Sheet);
                var sheet = home.Sheet!;
                var name = Find<TextBox>(sheet, "ProjectName");
                Assert.Equal("Jogo-1", name.Text);
                name.Text = "Moedas";
                Click(Find<Button>(sheet, "Template-CoinCatcher"));
                Click(Find<Button>(home.Sheet!, "Orientation-portrait"));
                Click(Find<Button>(home.Sheet!, "CreateGame"));
                Assert.Null(home.Sheet);
                Assert.Equal(ShellLevel.Workspace, shell.State.Level);
                Assert.Equal("Moedas", shell.State.Project);
                Assert.Equal("portrait", shell.Workspace.Project!.Manifest.Orientation);
                Assert.Contains("Coin", shell.Workspace.Project.ReadText(shell.Workspace.Project.Manifest.EntryPoint));

                shell.GoHome();
                home.ShowActions("Moedas");
                Click(Find<Button>(home.Sheet!, "Action-Duplicar"));
                Assert.Equal(["Moedas", "Moedas-copia"], home.Shown);

                home.ShowRename("Moedas-copia");
                Find<TextBox>(home.Sheet!, "ProjectName").Text = "Moedas";
                var save = home.Sheet!.GetLogicalDescendants().OfType<Button>().First(b => Avalonia.Automation.AutomationProperties.GetName(b) == "Salvar");
                Click(save);
                Assert.NotNull(home.Sheet); // taken name: the sheet stays, with the reason
                Find<TextBox>(home.Sheet!, "ProjectName").Text = "Moedas2";
                Click(save);
                Assert.Null(home.Sheet);
                Assert.Equal(["Moedas", "Moedas2"], home.Shown);

                home.Delete("Moedas2");
                Assert.Equal(["Moedas"], home.Shown);
                Assert.Contains("lixeira", home.SnackbarText);
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Click(Find<Button>(window, "Undo"));
                Assert.Equal(["Moedas", "Moedas2"], home.Shown);

                home.Delete("Moedas2");
                home.ShowTrash();
                var id = shell.Workspace.Store.Trash()[0].Id;
                var purge = Find<Button>(home.Sheet!, "Purge-" + id);
                Click(purge);
                Assert.Single(shell.Workspace.Store.Trash()); // first tap only arms
                Click(Find<Button>(home.Sheet!, "Purge-" + id));
                Assert.Empty(shell.Workspace.Store.Trash());
                Assert.True(shell.Back()); // Back closes the sheet before anything else
                Assert.Null(home.Sheet);
                window.Close();
            });
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void Shell_ExportsAZipThatImportsBackAsANewGame_SavingTheOpenOneFirst()
    {
        var root = NewRoot();
        try
        {
            UiSession.Run(() =>
            {
                var shell = new LunetShell(root);
                var window = new Window { Width = 412, Height = 915, Content = shell };
                window.Show();
                shell.CreateProject();
                var name = shell.State.Project!;
                shell.WorkspaceView.Code.Surface.Text = "// editado antes de exportar\n" + shell.WorkspaceView.Code.Surface.Text;
                using var zip = new MemoryStream();
                shell.ExportProjectZip(name, zip); // not saved yet: export saves the open game first
                zip.Position = 0;
                var imported = shell.ImportProjectZip(zip);
                Assert.NotEqual(name, imported);
                var copy = shell.Workspace.Store.Open(imported);
                Assert.StartsWith("// editado antes de exportar", copy.ReadText(copy.Manifest.EntryPoint));
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                shell.GoHome();
                Assert.Contains(imported, shell.Home.Shown);
                Assert.Throws<Lunet.Core.ProjectException>(() => shell.ImportProjectZip(new MemoryStream([1, 2, 3])));
                window.Close();
            });
        }
        finally { Directory.Delete(root, true); }
    }

    private static T Find<T>(Control root, string name) where T : Control =>
        root.GetLogicalDescendants().OfType<T>().First(c => c.Name == name);

    private static void Click(Button button) =>
        button.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
}
