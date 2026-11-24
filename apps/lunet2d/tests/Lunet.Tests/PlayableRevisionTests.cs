using Lunet.Core;

namespace Lunet.Tests;

public sealed class PlayableRevisionTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "lunet-play-" + Guid.NewGuid().ToString("N"));
    private ProjectStore Store => new(Path.Combine(_root, "projects"));
    private string Staging => Path.Combine(_root, "staging");
    private string Cache => Path.Combine(_root, "cache");

    [Fact]
    public void RevisionChangesForSourceAssetsManifestAndToolchainButNotNotesOrFolder()
    {
        var game = Store.Create("GameA");
        using var first = PlayableRevision.Capture(game, Staging, "engine-a");
        game.WriteText("Notes/idea.md", "not executable");
        using var notes = PlayableRevision.Capture(game, Staging, "engine-a");
        Assert.Equal(first.Key, notes.Key);
        game.WriteText("Game.cs", game.ReadText("Game.cs") + "\n// changed");
        using var source = PlayableRevision.Capture(game, Staging, "engine-a");
        Assert.NotEqual(first.Key, source.Key);
        game.WriteText("Content/data.json", "{}");
        using var asset = PlayableRevision.Capture(game, Staging, "engine-a");
        Assert.NotEqual(source.Key, asset.Key);
        game.Manifest.Orientation = "landscape";
        game.WriteText("lunet.json", game.Manifest.ToJson());
        using var manifest = PlayableRevision.Capture(game, Staging, "engine-a");
        Assert.NotEqual(asset.Key, manifest.Key);
        using var engine = PlayableRevision.Capture(game, Staging, "engine-b");
        Assert.NotEqual(manifest.Key, engine.Key);
        Directory.Move(game.Directory, Path.Combine(Store.RootDirectory, "Moved"));
        using var moved = PlayableRevision.Capture(Store.Open("Moved"), Staging, "engine-a");
        Assert.Equal(manifest.Key, moved.Key);
    }

    [Fact]
    public void SnapshotDoesNotFollowLaterSourceOrAssetChanges()
    {
        var game = Store.Create("GameA");
        game.WriteText("Content/data.json", "old");
        using var revision = PlayableRevision.Capture(game, Staging, "engine");
        game.WriteText("Content/data.json", "new");
        game.WriteText("Game.cs", "changed");
        Assert.Equal("old", File.ReadAllText(Path.Combine(revision.Directory, "Content", "data.json")));
        Assert.DoesNotContain("changed", revision.Sources.Single(s => s.Path == "Game.cs").Text);
    }

    [Fact]
    public void CacheIsRevisionAndGameSpecificAndRejectsCorruption()
    {
        var game = Store.Create("GameA");
        using var original = PlayableRevision.Capture(game, Staging, "engine");
        var cache = new PlayableBuildCache(Cache);
        Assert.Null(cache.TryRead(original.GameId, original.Key));
        cache.Publish(original.GameId, original.Key, [1, 2, 3]);
        Assert.Equal(new byte[] { 1, 2, 3 }, cache.TryRead(original.GameId, original.Key));
        game.WriteText("Game.cs", "invalid source");
        using var changed = PlayableRevision.Capture(game, Staging, "engine");
        Assert.Null(cache.TryRead(changed.GameId, changed.Key));
        var another = Store.Create("GameB");
        Assert.Null(cache.TryRead(Guid.Parse(another.Manifest.GameId), original.Key));
        var file = Path.Combine(Cache, original.GameId.ToString("N"), original.Key + ".bin");
        File.WriteAllBytes(file, [9, 9, 9]);
        Assert.Null(cache.TryRead(original.GameId, original.Key));
        cache.Publish(original.GameId, original.Key, [4, 5]);
        Assert.Equal(new byte[] { 4, 5 }, cache.TryRead(original.GameId, original.Key));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }
}
