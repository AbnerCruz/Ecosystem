using Lunet.Core;

namespace Lunet.Tests;

public sealed class SettingsAndLogTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "lunet-settings-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void Settings_RoundTrip_ClampAndSurviveCorruption()
    {
        var store = new SettingsStore(Path.Combine(_root, "nested", "settings.json"));
        Assert.Equal(14, store.Load().FontSize);

        store.Save(new EditorSettings { FontSize = 20, ShowMinimap = false, FormatOnRun = true });
        var loaded = store.Load();
        Assert.Equal(20, loaded.FontSize);
        Assert.False(loaded.ShowMinimap);
        Assert.True(loaded.FormatOnRun);
        Assert.True(loaded.ShowLineNumbers);

        store.Save(new EditorSettings { FontSize = 999 });
        Assert.Equal(EditorSettings.MaxFontSize, store.Load().FontSize);

        File.WriteAllText(store.Path, "{ isto não é json");
        Assert.Equal(14, store.Load().FontSize);
    }

    [Fact]
    public void LogExport_ListsProblemsAndConsole()
    {
        var text = LogExport.Build("Meu Jogo", "0.0.1", "Pixel 8 (Android 15)", new DateTimeOffset(2026, 9, 29, 13, 0, 0, TimeSpan.Zero),
            ["[info] iniciou", "[erro] falhou"],
            [new LogProblem("error", "CS1002", "; expected", "Game.cs", 3, 9)]);
        Assert.StartsWith("Lunet 0.0.1 — Meu Jogo\nDispositivo: Pixel 8 (Android 15)\n", text);
        Assert.Contains("== Problemas (1) ==\nerror CS1002 Game.cs(3,9): ; expected\n", text);
        Assert.Contains("== Console (2) ==\n[info] iniciou\n[erro] falhou\n", text);

        var empty = LogExport.Build("X", "1", "d", DateTimeOffset.UnixEpoch, [], []);
        Assert.Contains("(nenhum)", empty);
        Assert.Contains("(vazio)", empty);
    }
}

public sealed class GitAccountTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "lunet-account-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void Account_RoundTrips_ValidatesIdentity_AndSurvivesCorruption()
    {
        var store = new GitAccountStore(Path.Combine(_root, "git-account.json"));
        Assert.False(store.Load().CanCommit);
        store.Save(new GitAccount { Name = "Ana", Email = "ana@example.com", Token = "ghp_x" });
        var loaded = store.Load();
        Assert.True(loaded.CanCommit);
        Assert.Equal("ghp_x", loaded.Token);
        Assert.False(new GitAccount { Name = "Ana", Email = "sem-arroba" }.CanCommit);
        File.WriteAllText(Path.Combine(_root, "git-account.json"), "não é json");
        Assert.False(store.Load().CanCommit);
    }
}
