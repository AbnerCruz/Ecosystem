using System.Text.Json;
using Lunet.Core;

namespace Lunet.Tests;

public sealed class LauncherContinuityTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "lunet-home-f2-" + Guid.NewGuid().ToString("N"));
    private ProjectStore Projects => new(Path.Combine(_root, "Projects"));
    private string PreferencesPath => Path.Combine(_root, "launcher-preferences.json");

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void LegacyNameOnlyPreferences_MigrateToStableGameIdWithoutChangingProjects()
    {
        var project = Projects.Create("Alpha");
        var source = project.ReadText("Game.cs");
        Directory.CreateDirectory(_root);
        File.WriteAllText(PreferencesPath,
            """{"Version":1,"Section":"projects","LastProject":"Alpha","PinnedProjects":["Alpha"]}""");
        var prefs = new LauncherPreferences(PreferencesPath);
        var catalog = LauncherProjectCatalog.Scan(Projects);
        prefs.UpgradeProjectReferences(catalog);

        Assert.Equal(project.Manifest.GameId, prefs.LastProjectId);
        Assert.True(prefs.IsPinnedProject(project.Manifest.GameId));
        Assert.Single(prefs.RecentProjectIds);
        Assert.Empty(prefs.PinnedProjects);
        Assert.Equal("projects", prefs.Section);
        Assert.Equal(source, project.ReadText("Game.cs"));
        Assert.Equal(project.Manifest.GameId, new LauncherPreferences(PreferencesPath).LastProjectId);
    }

    [Fact]
    public void FolderRename_ContinuesSameGameAndPinWithoutEditingManifest()
    {
        var project = Projects.Create("Antigo");
        var id = project.Manifest.GameId;
        var prefs = new LauncherPreferences(PreferencesPath);
        prefs.MarkOpenedProject("Antigo", id);
        prefs.SetPinnedProject("Antigo", id, true);
        Directory.Move(project.Directory, Path.Combine(Projects.RootDirectory, "Renomeado"));

        var catalog = LauncherProjectCatalog.Scan(Projects);
        Assert.Equal("Renomeado", LauncherProjectCatalog.UniqueNameForId(catalog, id));
        Assert.True(new LauncherPreferences(PreferencesPath).IsPinnedProject(id));
        Assert.Equal(id, Projects.Open("Renomeado").Manifest.GameId);
        Assert.Null(LauncherProjectCatalog.UniqueIdForName(catalog, "Antigo"));
    }

    [Fact]
    public void RecentProjects_AreOrderedBoundedAndPersisted()
    {
        var prefs = new LauncherPreferences(PreferencesPath);
        var ids = Enumerable.Range(0, 10).Select(_ => Guid.NewGuid().ToString("D")).ToArray();
        for (var i = 0; i < 10; i++) prefs.MarkOpenedProject("Jogo" + i, ids[i]);
        prefs.MarkOpenedProject("Jogo0", ids[0]);
        var recent = new LauncherPreferences(PreferencesPath).RecentProjectIds;

        Assert.Equal(8, recent.Count);
        Assert.Equal(ids[0], recent[0]);
        Assert.Equal(ids[9], recent[1]);
        Assert.DoesNotContain(ids[1], recent);
    }

    [Fact]
    public void MissingOrDuplicateGameId_NeverResolvesToWrongProject()
    {
        var first = Projects.Create("Primeiro");
        var second = Projects.Create("Segundo");
        var manifest = Path.Combine(second.Directory, ProjectStore.ManifestFileName);
        var content = File.ReadAllText(manifest).Replace(second.Manifest.GameId, first.Manifest.GameId);
        File.WriteAllText(manifest, content);
        var catalog = LauncherProjectCatalog.Scan(Projects);

        Assert.Null(LauncherProjectCatalog.UniqueNameForId(catalog, first.Manifest.GameId));
        Assert.Null(LauncherProjectCatalog.UniqueNameForId(catalog, Guid.NewGuid().ToString("D")));
        Assert.Null(LauncherProjectCatalog.UniqueNameForId(catalog, "not-a-gameid"));
    }

    [Fact]
    public void MalformedManifestAndMissingGameId_AreNotInventedOrRewritten()
    {
        var broken = Projects.Create("Quebrado");
        var path = Path.Combine(broken.Directory, ProjectStore.ManifestFileName);
        File.WriteAllText(path, "{corrompido");
        var legacy = Projects.Create("Legado");
        var legacyManifest = Path.Combine(legacy.Directory, ProjectStore.ManifestFileName);
        using (var document = JsonDocument.Parse(File.ReadAllText(legacyManifest)))
        {
            var data = document.RootElement.EnumerateObject().Where(x => x.Name != "gameId")
                .ToDictionary(x => x.Name, x => x.Value.Clone());
            File.WriteAllText(legacyManifest, JsonSerializer.Serialize(data));
        }
        var originalBroken = File.ReadAllText(path);
        var beforeLegacy = File.ReadAllText(legacyManifest);
        var catalog = LauncherProjectCatalog.Scan(Projects);
        Assert.Empty(catalog);
        Assert.Equal(originalBroken, File.ReadAllText(path));
        Assert.Equal(beforeLegacy, File.ReadAllText(legacyManifest));
    }

    [Fact]
    public void DeletedProject_ClearsItsIdAndKeepsOtherPins()
    {
        var a = Projects.Create("A");
        var b = Projects.Create("B");
        var prefs = new LauncherPreferences(PreferencesPath);
        prefs.MarkOpenedProject("A", a.Manifest.GameId);
        prefs.SetPinnedProject("A", a.Manifest.GameId, true);
        prefs.SetPinnedProject("B", b.Manifest.GameId, true);
        Projects.Delete("A");
        prefs.ForgetProject("A", a.Manifest.GameId);

        var restored = new LauncherPreferences(PreferencesPath);
        Assert.Null(restored.LastProjectId);
        Assert.False(restored.IsPinnedProject(a.Manifest.GameId));
        Assert.True(restored.IsPinnedProject(b.Manifest.GameId));
        Assert.DoesNotContain(a.Manifest.GameId, restored.RecentProjectIds);
    }
}
