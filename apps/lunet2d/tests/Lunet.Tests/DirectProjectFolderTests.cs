using Lunet.Core;

namespace Lunet.Tests;

public sealed class DirectProjectFolderTests
{
    [Fact]
    public void MigrationPreservesIdentityAndFilesAndTheChosenFolderIsTheSourceOfTruth()
    {
        var root = Directory.CreateTempSubdirectory("lunet-direct-").FullName;
        try
        {
            var internalStore = new ProjectStore(Path.Combine(root, "internal"));
            var target = new ProjectStore(new Folder(new DiskProjectFiles(Path.Combine(root, "chosen"))));
            var game = internalStore.Create("Game");
            game.CreateDirectory("Scripts/Empty");
            game.WriteBytes("Content/example.bin", [1, 2, 3]);
            var settings = new ProjectFolderSync(root, root).ConnectDirect(new EditorSettings(), "folder", target.Root, internalStore.Root, null);
            Assert.Equal("folder", settings.DirectProjectTree);
            Assert.False(settings.UseSharedProjectFolder);
            ProjectFolderMigration.Migrate(internalStore, target); // a retry of an already verified migration
            var opened = target.Open("Game");
            Assert.Equal(game.Manifest.GameId, opened.Manifest.GameId);
            Assert.True(opened.Files.DirectoryExists("Scripts/Empty"));
            Assert.Equal(new byte[] { 1, 2, 3 }, opened.ReadBytes("Content/example.bin"));

            using var captured = PlayableRevision.Capture(opened, Path.Combine(root, "preview"), "test", TestContext.Current.CancellationToken);
            Assert.Equal(game.Manifest.GameId, captured.GameId.ToString());
            opened.WriteText(game.Manifest.EntryPoint, "// edição na pasta");
            Assert.NotEqual(game.ReadText(game.Manifest.EntryPoint), opened.ReadText(game.Manifest.EntryPoint));
            Assert.Throws<IOException>(() => ProjectFolderMigration.Migrate(internalStore, target));
            Assert.Equal("// edição na pasta", target.Open("Game").ReadText(game.Manifest.EntryPoint));
            Assert.False(settings.Normalized().UseSharedProjectFolder);
            Assert.Equal("folder", settings.DirectProjectTree);

            using var zip = new MemoryStream();
            opened.ExportZip(zip);
            zip.Position = 0;
            var imported = target.ImportZip(zip);
            Assert.NotEqual(opened.Manifest.GameId, imported.Manifest.GameId);
            Assert.Equal("// edição na pasta", imported.ReadText(game.Manifest.EntryPoint));
            var duplicate = target.Duplicate("Game");
            Assert.Equal("// edição na pasta", duplicate.ReadText(game.Manifest.EntryPoint));
            Assert.Equal("Novo", target.Rename("Game", "Novo").Name);
            var trash = target.MoveToTrash("Novo");
            Assert.DoesNotContain("Novo", target.List());
            Assert.Equal("Novo", target.Restore(trash.Id).Name);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    private sealed class Folder(DiskProjectFiles disk) : IProjectFiles
    {
        public string Location => "provider:" + disk.Location;
        public string? LocalDirectory => null;
        public IReadOnlyList<ProjectFileEntry> List(string folder) => disk.List(folder);
        public bool FileExists(string path) => disk.FileExists(path);
        public bool DirectoryExists(string path) => disk.DirectoryExists(path);
        public byte[] Read(string path) => disk.Read(path);
        public void Write(string path, byte[] bytes) => disk.Write(path, bytes);
        public void CreateDirectory(string path) => disk.CreateDirectory(path);
        public void Move(string from, string to) => disk.Move(from, to);
        public void Delete(string path) => disk.Delete(path);
        public IProjectFiles Sub(string folder) => new Folder((DiskProjectFiles)disk.Sub(folder));
    }
}
