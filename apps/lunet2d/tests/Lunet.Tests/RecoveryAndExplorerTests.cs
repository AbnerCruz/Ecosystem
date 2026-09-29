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
    public void Journal_RetainsFiveDistinctVersions_AndCanRestoreAnEarlierOneAfterRestart()
    {
        var project = NewProject();
        var journal = new AutosaveJournal(project);
        for (var i = 1; i <= 7; i++) journal.WriteBuffer("Game.cs", $"versão {i}");
        journal.WriteBuffer("Game.cs", "versão 7"); // mesma edição não cria uma versão extra

        var reopened = new AutosaveJournal(project);
        var recovery = Assert.Single(reopened.FindRecoveries());
        var versions = reopened.FindHistory(recovery);
        Assert.Equal(["versão 7", "versão 6", "versão 5", "versão 4", "versão 3"],
            versions.Select(v => v.RecoveredText));

        reopened.Restore(versions[3]);
        Assert.Equal("versão 4", project.ReadText("Game.cs"));
        Assert.Empty(reopened.FindRecoveries());
        Assert.Empty(Directory.GetFiles(Path.Combine(project.Directory, ".lunet", "autosave")));
    }

    [Fact]
    public void Journal_DiscardsOnlyTheSavedFilesHistory()
    {
        var project = NewProject();
        var journal = new AutosaveJournal(project);
        journal.WriteBuffer("Game.cs", "g1");
        journal.WriteBuffer("Game.cs", "g2");
        journal.WriteBuffer("A.cs", "a1");
        journal.Discard("Game.cs");

        var remaining = Assert.Single(journal.FindRecoveries());
        Assert.Equal("A.cs", remaining.Path);
        Assert.Equal("a1", Assert.Single(journal.FindHistory(remaining)).RecoveredText);
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

public class EditorSessionTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "lunet-session-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void Save_IsRefusedWhenNothingWasLoaded_SoARecreatedEmptyEditorCannotWipeTheFile()
    {
        var project = new ProjectStore(_root).Create("p");
        var original = project.ReadText("Game.cs");
        var session = new EditorSession();

        // Cenário do bug: o editor foi recriado (vazio) ao voltar do Preview e o app tentou salvar.
        session.Load("Game.cs", original);
        session.Unload();
        Assert.False(session.TrySave(project, ""));
        Assert.Equal(original, project.ReadText("Game.cs"));
    }

    [Fact]
    public void Save_WritesLoadedFileAndClearsJournal()
    {
        var project = new ProjectStore(_root).Create("p");
        var journal = new AutosaveJournal(project);
        var session = new EditorSession();
        session.Load("Game.cs", project.ReadText("Game.cs"));

        Assert.True(session.IsDirty("// novo"));
        journal.WriteBuffer("Game.cs", "// novo");
        Assert.True(session.TrySave(project, "// novo", journal));
        Assert.Equal("// novo", project.ReadText("Game.cs"));
        Assert.False(session.IsDirty("// novo"));
        Assert.Empty(journal.FindRecoveries());
    }

    [Fact]
    public void Save_RecreatesFileThatWasDeletedOutsideTheEditor()
    {
        var project = new ProjectStore(_root).Create("p");
        project.WriteText("A.cs", "class A {}");
        var session = new EditorSession();
        session.Load("A.cs", "class A {}");
        project.Delete("A.cs");
        Assert.True(session.TrySave(project, "class A {}")); // texto igual, mas o arquivo sumiu: regrava
        Assert.True(File.Exists(Path.Combine(project.Directory, "A.cs")));
    }

    [Fact]
    public void Unload_ForgetsPathAndSavedText()
    {
        var session = new EditorSession();
        session.Load("A.cs", "x");
        Assert.True(session.HasFile);
        session.Unload();
        Assert.False(session.HasFile);
        Assert.False(session.IsDirty("qualquer"));
    }
}
