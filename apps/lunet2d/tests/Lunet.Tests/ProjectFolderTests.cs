using Avalonia.Controls;
using Avalonia.VisualTree;
using Lunet.Client;
using Lunet.Client.Views;
using Lunet.Core;

namespace Lunet.Tests;

/// <summary>LUNET-536 P1: Pasta de projetos in the shell, with the previous screen's rules (ProjectFolderSync).</summary>
public sealed class ProjectFolderTests
{
    /// <summary>A folder of revision ZIPs on disk, named like the Android one (newest first).</summary>
    private sealed class DiskTree(string dir) : IProjectFolderTree
    {
        private static long _ticks = 638_000_000_000_000_000; // shared: two "devices" publish in order
        public string? Latest() => Revisions().FirstOrDefault();
        public IReadOnlyList<string> Revisions() => Directory.Exists(dir)
            ? Directory.GetFiles(dir, "lunet-revision-*.zip").Select(f => Path.GetFileName(f)!).OrderByDescending(n => n, StringComparer.Ordinal).ToList()
            : [];
        public string Publish(string archive, string digest, string? expectedRevision)
        {
            if (Latest() != expectedRevision) throw new IOException("A pasta mudou.");
            Directory.CreateDirectory(dir);
            var name = "lunet-revision-" + (++_ticks).ToString("D19") + "-" + Guid.NewGuid().ToString("N") + "-" + digest + ".zip";
            File.Copy(archive, Path.Combine(dir, name));
            return name;
        }
        public void Restore(string name, string archive, string destination)
        {
            File.Copy(Path.Combine(dir, name), archive, overwrite: true);
            ProjectFolderSnapshot.Restore(archive, name[^68..^4], destination);
        }
        public void VerifyRevision(string name, string digest)
        {
            if (!name.EndsWith("-" + digest + ".zip", StringComparison.Ordinal)) throw new IOException("Revisão diferente.");
        }
    }

    private static string Game(string root, string name)
    {
        var dir = Path.Combine(root, name);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "lunet.json"), "{\"title\":\"" + name + "\"}");
        File.WriteAllText(Path.Combine(dir, "Game.cs"), "class Game {}");
        return dir;
    }

    [Fact]
    public void Sync_ConnectPublishesTheGames_SaveRefusesAForeignRevision_RestoreAndSwapKeepEveryCopy()
    {
        var files = Path.Combine(Path.GetTempPath(), "lunet-folder-" + Guid.NewGuid().ToString("N"));
        try
        {
            var cache = Path.Combine(files, "cache");
            Directory.CreateDirectory(cache);
            var sync = new ProjectFolderSync(files, cache);
            Game(sync.PrivateProjects, "Nave");
            var tree = new DiskTree(Path.Combine(files, "chosen"));

            var s = sync.Connect(new EditorSettings(), "content://pasta", tree);
            Assert.True(s.UseSharedProjectFolder);
            Assert.Single(tree.Revisions());
            Assert.True(File.Exists(Path.Combine(sync.ProjectsRoot(s), "Nave", "Game.cs")));   // working copy of the games
            Assert.True(Directory.Exists(Path.Combine(sync.PrivateProjects, "Nave")));         // internal games untouched

            File.WriteAllText(Path.Combine(sync.ProjectsRoot(s), "Nave", "Game.cs"), "class Game { int vida; }");
            s = sync.SaveRevision(s, tree);
            Assert.Equal(2, tree.Revisions().Count);
            Assert.Equal(tree.Latest(), s.ProjectRevision);

            // Another device published meanwhile: saving refuses instead of overwriting it.
            var elsewhere = Path.Combine(files, "elsewhere");
            Game(elsewhere, "Outro");
            var foreign = Path.Combine(cache, "foreign.zip");
            var foreignDigest = ProjectFolderSnapshot.Capture(elsewhere, foreign);
            new DiskTree(Path.Combine(files, "chosen")).Publish(foreign, foreignDigest, tree.Latest());
            File.WriteAllText(Path.Combine(sync.ProjectsRoot(s), "Nave", "Game.cs"), "class Game { int vida = 3; }");
            Assert.Throws<IOException>(() => sync.SaveRevision(s, tree));

            // Recover the first revision into another copy; the current one becomes "previous" and comes back with Swap.
            var first = tree.Revisions()[^1];
            var current = s.ProjectWorkingCopy;
            var restored = sync.Restore(s, tree, first);
            Assert.Equal(current, restored.PreviousProjectWorkingCopy);
            Assert.Equal("class Game {}", File.ReadAllText(Path.Combine(sync.ProjectsRoot(restored), "Nave", "Game.cs")));
            var back = sync.Swap(restored)!;
            Assert.Equal(current, back.ProjectWorkingCopy);
            Assert.Equal("class Game { int vida = 3; }", File.ReadAllText(Path.Combine(sync.ProjectsRoot(back), "Nave", "Game.cs")));

            // Internal games again, then resume the working copy.
            var internalGames = sync.SetActive(back, false)!;
            Assert.Equal(sync.PrivateProjects, sync.ProjectsRoot(internalGames));
            Assert.Equal(sync.ProjectsRoot(back), sync.ProjectsRoot(sync.SetActive(internalGames, true)!));

            // A different folder while the current copy has unpublished work: refused, nothing changes.
            Assert.Throws<IOException>(() => sync.Connect(back, "content://outra", new DiskTree(Path.Combine(files, "outra"))));
        }
        finally { if (Directory.Exists(files)) Directory.Delete(files, true); }
    }

    [Fact]
    public void Ajustes_PastaDeProjetos_ChoosesAFolderInTheShell_AndReopensOverIt()
    {
        var files = Path.Combine(Path.GetTempPath(), "lunet-folder-ui-" + Guid.NewGuid().ToString("N"));
        try
        {
            UiSession.Run(() =>
            {
                var cache = Path.Combine(files, "cache");
                Directory.CreateDirectory(cache);
                var sync = new ProjectFolderSync(files, cache);
                Game(sync.PrivateProjects, "Nave");
                var tree = new DiskTree(Path.Combine(files, "chosen"));
                var released = new List<string>();
                var reopened = 0;
                var shell = new LunetShell(sync.PrivateProjects) { SettingsPath = Path.Combine(files, "settings.json") };
                shell.ProjectFolder = new ProjectFolderHost(sync,
                    () => Task.FromResult<(string, IProjectFolderTree)?>(("content://pasta", tree)),
                    _ => tree, released.Add, () => reopened++);
                var window = new Window { Width = 412, Height = 915, Content = shell };
                window.Show();
                var settings = shell.Home.ShowSettings();
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                window.GetVisualDescendants().OfType<Button>().First(b => b.Name == "ProjectFolder").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                var folder = Assert.IsType<ProjectFolderView>(settings.Folder);
                var connect = folder.Connect();
                var until = DateTime.UtcNow.AddSeconds(30);
                while (!connect.IsCompleted && DateTime.UtcNow < until) { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); Thread.Sleep(5); }
                connect.GetAwaiter().GetResult();
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Assert.True(shell.Settings.UseSharedProjectFolder);
                Assert.Equal("content://pasta", new SettingsStore(Path.Combine(files, "settings.json")).Load().ProjectTree); // saved
                Assert.Equal(1, reopened);                                                                                 // shell reopens over the copy
                Assert.Empty(released);
                Assert.Contains(window.GetVisualDescendants().OfType<Button>(), b => b.Name == "FolderSave");
                Assert.True(shell.Back());                                                                                 // back to Ajustes
                Assert.Null(settings.Folder);
                window.Close();
            });
        }
        finally { if (Directory.Exists(files)) Directory.Delete(files, true); }
    }
}
