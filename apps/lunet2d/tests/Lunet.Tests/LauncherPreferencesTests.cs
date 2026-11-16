using Lunet.Core;

namespace Lunet.Tests;

public sealed class LauncherPreferencesTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "lunet-home-" + Guid.NewGuid().ToString("N"));
    private string FilePath => Path.Combine(_folder, "launcher.json");

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
    }

    [Fact]
    public void NewLauncher_IsHomeWithoutOpeningOrModifyingProjects()
    {
        var store = new ProjectStore(Path.Combine(_folder, "Projects"));
        var game = store.Create("MeuJogo");
        var original = game.ReadText("Game.cs");
        var prefs = new LauncherPreferences(FilePath);

        Assert.Equal("home", prefs.Section);
        Assert.Null(prefs.LastProject);
        Assert.Empty(prefs.PinnedProjects);
        Assert.False(File.Exists(FilePath));
        Assert.Equal(original, game.ReadText("Game.cs"));
    }

    [Fact]
    public void SectionsPinsAndRecentProject_PersistAcrossRestart()
    {
        var prefs = new LauncherPreferences(FilePath);
        prefs.SelectSection("projects");
        prefs.SetPinned("JogoA", true);
        prefs.SetPinned("JogoB", true);
        prefs.MarkOpened("JogoB");
        prefs.SelectSection("studios");

        var reopened = new LauncherPreferences(FilePath);
        Assert.Equal("studios", reopened.Section);
        Assert.Equal("JogoB", reopened.LastProject);
        Assert.Equal(new[] { "JogoA", "JogoB" }, reopened.PinnedProjects);
        reopened.SetPinned("JogoA", false);
        reopened.SelectSection("explore");
        Assert.Equal(new[] { "JogoB" }, new LauncherPreferences(FilePath).PinnedProjects);
        Assert.Equal("explore", new LauncherPreferences(FilePath).Section);
    }

    [Fact]
    public void DeletingAProject_ClearsItsShortcutsWithoutTouchingOtherPins()
    {
        var prefs = new LauncherPreferences(FilePath);
        prefs.MarkOpened("Antigo");
        prefs.SetPinned("Antigo", true);
        prefs.SetPinned("Atual", true);
        prefs.ForgetProject("Antigo");

        var reopened = new LauncherPreferences(FilePath);
        Assert.Null(reopened.LastProject);
        Assert.DoesNotContain("Antigo", reopened.PinnedProjects);
        Assert.Contains("Atual", reopened.PinnedProjects);
    }

    [Fact]
    public void InvalidSectionOrName_DoesNotChangeFileOrMemory()
    {
        var prefs = new LauncherPreferences(FilePath);
        prefs.SetPinned("MeuJogo", true);
        var before = File.ReadAllText(FilePath);

        Assert.Throws<ArgumentException>(() => prefs.SelectSection("play"));
        Assert.Throws<ArgumentException>(() => prefs.SetPinned("../Jogo", true));
        Assert.Throws<ArgumentException>(() => prefs.MarkOpened("a/b"));
        Assert.Equal(before, File.ReadAllText(FilePath));
        Assert.Equal("home", prefs.Section);
        Assert.True(prefs.IsPinned("MeuJogo"));
    }

    [Fact]
    public void InvalidJson_ThrowsButKeepsOriginalFileByteForByte()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(FilePath, "{preferencias_quebradas");
        Assert.Throws<InvalidDataException>(() => new LauncherPreferences(FilePath));
        Assert.Equal("{preferencias_quebradas", File.ReadAllText(FilePath));
    }

    [Fact]
    public void MutatingReturnedPinnedArray_CannotMutatePreferences()
    {
        var prefs = new LauncherPreferences(FilePath);
        prefs.SetPinned("Fixo", true);
        var copy = prefs.PinnedProjects.ToArray();
        copy[0] = "Falso";
        Assert.True(prefs.IsPinned("Fixo"));
        Assert.False(prefs.IsPinned("Falso"));
    }
}
