using Lunet.Core;

namespace Lunet.Tests;

public class RecoveryAndExplorerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "lunet-rec-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private LunetProject NewProject() => new ProjectStore(_root).Create("p");

    [Fact]
    public void Journal_OffersUnsavedBufferAfterCrash_AndRestoreWritesIt()
    {
        var project = NewProject();
        var journal = new AutosaveJournal(project);
        journal.WriteBuffer("Game.cs", "// texto não salvo\n");

        // "Crash": novo diário, mesma pasta.
        var recoveries = new AutosaveJournal(project).FindRecoveries();
        var recovery = Assert.Single(recoveries);
        Assert.Equal("Game.cs", recovery.Path);
        Assert.Equal("// texto não salvo\n", recovery.RecoveredText);
        Assert.Contains("class P", recovery.DiskText);

        journal.Restore(recovery);
        Assert.Equal("// texto não salvo\n", project.ReadText("Game.cs"));
        Assert.Empty(journal.FindRecoveries());
    }

    [Fact]
    public void Journal_DiscardAfterSave_LeavesNothingToRecover()
    {
        var project = NewProject();
        var journal = new AutosaveJournal(project);
        journal.WriteBuffer("Game.cs", "x");
        project.WriteText("Game.cs", "x");
        journal.Discard("Game.cs");
        Assert.Empty(journal.FindRecoveries());
    }

    [Fact]
    public void Journal_IgnoresBuffersIdenticalToDisk_AndTruncatedOnes()
    {
        var project = NewProject();
        var journal = new AutosaveJournal(project);
        journal.WriteBuffer("Game.cs", project.ReadText("Game.cs"));
        File.WriteAllText(Path.Combine(project.Directory, ".lunet", "autosave", "broken.buf"), "sem-quebra-de-linha");
        Assert.Empty(journal.FindRecoveries());
        Assert.Empty(Directory.GetFiles(Path.Combine(project.Directory, ".lunet", "autosave")));
    }

    [Fact]
    public void Journal_RecoversFileDeletedOnDisk_AndRejectsEscapingPaths()
    {
        var project = NewProject();
        project.WriteText("Code/A.cs", "class A {}");
        var journal = new AutosaveJournal(project);
        journal.WriteBuffer("Code/A.cs", "class A { int x; }");
        project.Delete("Code/A.cs");
        var recovery = Assert.Single(journal.FindRecoveries());
        Assert.Null(recovery.DiskText);
        Assert.Throws<ProjectException>(() => journal.WriteBuffer("../x.cs", "x"));
    }

    [Fact]
    public void Journal_KeepsSeparateBuffersPerFile()
    {
        var project = NewProject();
        var journal = new AutosaveJournal(project);
        project.WriteText("A.cs", "a");
        journal.WriteBuffer("A.cs", "a1");
        journal.WriteBuffer("Game.cs", "g1");
        journal.WriteBuffer("A.cs", "a2");
        var all = journal.FindRecoveries();
        Assert.Equal(["A.cs", "Game.cs"], all.Select(r => r.Path));
        Assert.Equal("a2", all[0].RecoveredText);
    }

    [Fact]
    public void Tree_ListsFoldersFirstWithDepth_AndHidesCache()
    {
        var project = NewProject();
        project.WriteText("Code/Player.cs", "x");
        project.WriteText("Code/Ai/Brain.cs", "x");
        project.WriteText(".lunet/cache/z.bin", "x");
        var tree = project.ListTree();
        Assert.DoesNotContain(tree, e => e.Path.StartsWith(".lunet"));
        Assert.Equal(["Code", "Code/Ai", "Code/Ai/Brain.cs", "Code/Player.cs", "Content", "Game.cs", "lunet.json"], tree.Select(e => e.Path));
        Assert.Equal(2, tree.Single(e => e.Path == "Code/Ai/Brain.cs").Depth);
        Assert.True(tree.Single(e => e.Path == "Code").IsDirectory);
    }

    [Fact]
    public void RenameDeleteAndCreateDirectory_WorkWithinProject()
    {
        var project = NewProject();
        project.WriteText("Code/A.cs", "x");
        project.CreateDirectory("Content/Audio");
        project.Rename("Code/A.cs", "Code/B.cs");
        Assert.Equal("x", project.ReadText("Code/B.cs"));
        project.Rename("Code", "Logic");
        Assert.Equal("x", project.ReadText("Logic/B.cs"));
        project.Delete("Logic");
        Assert.DoesNotContain(project.ListTree(), e => e.Path.StartsWith("Logic"));
        Assert.Throws<ProjectException>(() => project.CreateDirectory("Content/Audio"));
    }

    [Fact]
    public void Explorer_ProtectsManifestEntryPointAndEscapes()
    {
        var project = NewProject();
        Assert.Throws<ProjectException>(() => project.Delete("lunet.json"));
        Assert.Throws<ProjectException>(() => project.Delete("Game.cs"));
        Assert.Throws<ProjectException>(() => project.Rename("Game.cs", "Main.cs"));
        Assert.Throws<ProjectException>(() => project.Rename("Content", "../out"));
        Assert.Throws<ProjectException>(() => project.Delete(".lunet"));
        project.WriteText("X.cs", "x");
        Assert.Throws<ProjectException>(() => project.Rename("X.cs", "Content"));
    }
}
