using System.IO.Compression;
using Lunet.Core;
using Lunet.Compiler;
using Lunet.Git;

namespace Lunet.Tests;

public sealed class ProjectFolderSnapshotTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

    [Fact]
    public void RoundTripKeepsIdentityAssetsNotesAndGit()
    {
        var source = new ProjectStore(Path.Combine(_directory, "Projects"));
        var project = source.Create("ação");
        project.WriteText("Notas/fase.md", "Próxima missão");
        var git = GitRepository.Init(project.Directory);
        git.StageAll();
        var commit = git.Commit("Primeira revisão", new GitSignature("Lunet", "dev@example.com", DateTimeOffset.UnixEpoch));
        var bytes = new byte[1024 * 1024];
        new Random(510).NextBytes(bytes);
        var asset = Path.Combine(project.Directory, "Content", "map.bin");
        File.WriteAllBytes(asset, bytes);
        File.WriteAllText(Path.Combine(_directory, "private-settings.json"), "private");

        var archive = Path.Combine(_directory, "revision.zip");
        var digest = ProjectFolderSnapshot.Capture(source.RootDirectory, archive);
        Assert.Equal(digest, ProjectFolderSnapshot.Capture(source.RootDirectory, Path.Combine(_directory, "retry.zip")));
        var workingCopy = Path.Combine(_directory, "copy");
        ProjectFolderSnapshot.Restore(archive, digest, workingCopy);
        var restored = new ProjectStore(workingCopy).Open("ação");
        Assert.Equal(project.Manifest.GameId, restored.Manifest.GameId);
        Assert.Equal(project.Manifest.PackageId, restored.Manifest.PackageId);
        Assert.Equal("Próxima missão", restored.ReadText("Notas/fase.md"));
        Assert.Equal(commit, GitRepository.Open(restored.Directory).Head);
        var compilation = new GameCompiler(new LoadedAssembliesReferenceProvider(typeof(Game).Assembly))
            .Compile("restored_game", restored.LoadSources().Select(file => new SourceFile(file.Path, file.Text)).ToArray());
        Assert.True(compilation.Success);
        using var exported = new MemoryStream();
        restored.ExportZip(exported);
        exported.Position = 0;
        var imported = new ProjectStore(Path.Combine(_directory, "import")).ImportZip(exported);
        Assert.Equal("Próxima missão", imported.ReadText("Notas/fase.md"));
        Assert.Equal(bytes, File.ReadAllBytes(Path.Combine(restored.Directory, "Content", "map.bin")));
        Assert.False(File.Exists(Path.Combine(workingCopy, "private-settings.json")));
        Assert.True(File.Exists(asset));
    }

    [Fact]
    public void CorruptArchiveCannotActivatePrivateCopy()
    {
        var source = new ProjectStore(Path.Combine(_directory, "Projects"));
        source.Create("Game");
        var archive = Path.Combine(_directory, "revision.zip");
        var digest = ProjectFolderSnapshot.Capture(source.RootDirectory, archive);
        using (var writer = File.AppendText(archive)) writer.WriteLine("interrupted");
        var copy = Path.Combine(_directory, "copy");
        Assert.Throws<InvalidDataException>(() => ProjectFolderSnapshot.Restore(archive, digest, copy));
        Assert.False(Directory.Exists(copy));
        Assert.Equal("Game", Assert.Single(source.List()));
    }

    [Fact]
    public void EvenValidDigestCannotRestoreTraversalEntries()
    {
        Directory.CreateDirectory(_directory);
        var archive = Path.Combine(_directory, "revision.zip");
        using (var zip = ZipFile.Open(archive, ZipArchiveMode.Create))
            zip.CreateEntry("../outside.txt");
        var copy = Path.Combine(_directory, "copy");
        Assert.Throws<InvalidDataException>(() => ProjectFolderSnapshot.Restore(archive, ProjectFolderSnapshot.Digest(archive), copy));
        Assert.False(Directory.Exists(copy));
    }

    [Fact]
    public void GitRemoteWithEmbeddedCredentialsNeverPublishes()
    {
        var source = new ProjectStore(Path.Combine(_directory, "Projects"));
        var project = source.Create("Game");
        project.WriteText(".git/config", "[remote \"origin\"]\n    url = https://user:password@example.com/game\n");
        var archive = Path.Combine(_directory, "revision.zip");
        Assert.Throws<IOException>(() => ProjectFolderSnapshot.Capture(source.RootDirectory, archive));
        Assert.Contains("password", project.ReadText(".git/config"));
        using var zip = ZipFile.OpenRead(archive);
        Assert.DoesNotContain(zip.Entries, entry => entry.Length > 0 && entry.FullName.EndsWith("/.git/config"));
    }

    [Fact]
    public void ExistingSettingsLoadWithDarkThemeAndWithoutMovingProjects()
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "settings.json");
        File.WriteAllText(path, """{"FontSize":19,"ShowLineNumbers":false}""");
        var store = new SettingsStore(path);
        var legacy = store.Load();
        Assert.Equal(19, legacy.FontSize);
        Assert.False(legacy.ShowLineNumbers);
        Assert.Equal(WorkspaceTheme.Dark, legacy.Theme);
        Assert.False(legacy.UseSharedProjectFolder);

        legacy.Theme = WorkspaceTheme.Light;
        legacy.Font = EditorFont.Serif;
        legacy.FirstUseStep = 2;
        store.Save(legacy);
        var restored = store.Load();
        Assert.Equal(WorkspaceTheme.Light, restored.Theme);
        Assert.Equal(EditorFont.Serif, restored.Font);
        Assert.Equal(2, restored.FirstUseStep);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }
}
