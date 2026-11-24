using Lunet.Core;
using Lunet.Compiler;
using Lunet.Runtime;

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

    [Fact]
    public void RealCompilerColdWarmFailureAndIndependentGameIds()
    {
        var store = Store;
        var first = store.Create("GameA");
        var second = store.Create("GameB", ProjectTemplate.Blank, "landscape");
        var compiler = new GameCompiler(new LoadedAssembliesReferenceProvider(typeof(Game).Assembly));
        var cache = new PlayableBuildCache(Cache);
        using var original = PlayableRevision.Capture(first, Staging, "engine");
        var compiled = compiler.Compile("first", original.Sources.Select(s => new SourceFile(s.Path, s.Text)).ToArray());
        Assert.True(compiled.Success, string.Join("\n", compiled.Diagnostics));
        cache.Publish(original.GameId, original.Key, compiled.Assembly!);
        using var warm = PlayableRevision.Capture(first, Staging, "engine");
        Assert.Equal(original.Key, warm.Key);
        using (var game = GameLoader.Load(cache.TryRead(warm.GameId, warm.Key)!))
            Assert.NotNull(game.Game);
        using var landscape = PlayableRevision.Capture(second, Staging, "engine");
        Assert.Equal("landscape", landscape.Manifest.Orientation);
        Assert.NotEqual(warm.GameId, landscape.GameId);
        Assert.Null(cache.TryRead(landscape.GameId, landscape.Key));
        var secondBuild = compiler.Compile("second", landscape.Sources.Select(s => new SourceFile(s.Path, s.Text)).ToArray());
        Assert.True(secondBuild.Success, string.Join("\n", secondBuild.Diagnostics));
        first.WriteText("Game.cs", "this is not C#");
        using var broken = PlayableRevision.Capture(first, Staging, "engine");
        var failed = compiler.Compile("broken", broken.Sources.Select(s => new SourceFile(s.Path, s.Text)).ToArray());
        Assert.False(failed.Success);
        Assert.Null(cache.TryRead(broken.GameId, broken.Key));
        Assert.NotNull(cache.TryRead(original.GameId, original.Key));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }
}
