using Lunet.Compiler;
using Lunet.Core;
using Lunet.Graphics;
using Lunet.Runtime;

namespace Lunet.Tests;

public sealed class FrameworkEvolutionSampleTests
{
    [Fact]
    public void BothReferenceGamesCompileAndDrawEquivalentFrames()
    {
        DirectoryInfo? root = new(AppContext.BaseDirectory);
        while (root is not null && !Directory.Exists(Path.Combine(root.FullName, "samples", "framework-evolution-context"))) root = root.Parent;
        Assert.NotNull(root);
        var store = new ProjectStore(Path.Combine(root!.FullName, "samples"));
        var compiler = new GameCompiler(new LoadedAssembliesReferenceProvider(typeof(Game).Assembly));
        var frames = new List<RecordingBackend.Batch[]>();
        foreach (var name in new[] { "framework-evolution-explicit", "framework-evolution-context" })
        {
            var project = store.Open(name);
            var compilation = compiler.Compile(project.Name, project.LoadSources().Select(s => new SourceFile(s.Path, s.Text)).ToList());
            Assert.True(compilation.Success, string.Join("\n", compilation.Diagnostics));
            using var loaded = GameLoader.Load(compilation.Assembly!, compilation.Symbols);
            var backend = new RecordingBackend();
            var host = new GameHost(loaded.Game, backend);
            Assert.True(host.Start(360, 640), host.Fault?.ToString());
            host.Tick(1.0 / 60);
            Assert.False(host.IsFaulted, host.Fault?.ToString());
            Assert.NotEmpty(backend.Batches);
            frames.Add(backend.Batches.ToArray());
            host.Stop();
        }
        Assert.Equal(frames[0].Length, frames[1].Length);
        for (int i = 0; i < frames[0].Length; i++)
        {
            Assert.Equal(frames[0][i].Texture, frames[1][i].Texture);
            Assert.Equal(frames[0][i].Vertices, frames[1][i].Vertices);
        }
    }
}
