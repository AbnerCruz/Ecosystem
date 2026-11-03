using Urbe.Client;
using Urbe.Core;

namespace Urbe.Client.Tests;

public sealed partial class HeadlessUiTests
{
    [Fact]
    public async Task ReadingState_VerifiesWritesMergesSourcesAndPreservesFutureFiles() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var dir = Path.Combine(Path.GetTempPath(), "urbe-reader-state-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "readers.json"), vault = new('a', 64), hash = new('b', 64);
        try
        {
            var one = new ReadingStateFile(path);
            var two = new ReadingStateFile(path);
            one.Remember(new(vault, "a.pdf", hash, "pdf", new(200), new(), 10));
            two.Remember(new(vault, "b.epub", hash, "epub", new(5), new(24), 20));
            Assert.True(await one.FlushAsync());
            Assert.True(await two.FlushAsync());
            var restored = new ReadingStateFile(path);
            Assert.Equal(200, restored.Restore(vault, "a.pdf", hash)!.Location.Part);
            Assert.Equal(24, restored.Restore(vault, "b.epub", hash)!.Preferences.FontSize);
            restored.Move(vault, "b.epub", "moved.epub");
            Assert.True(await restored.FlushAsync());
            var moved = new ReadingStateFile(path);
            Assert.Null(moved.Restore(vault, "b.epub", hash));
            Assert.Equal(5, moved.Restore(vault, "moved.epub", hash)!.Location.Part);
            var future = "{\"version\":99,\"entries\":[]}";
            File.WriteAllText(path, future);
            var protectedState = new ReadingStateFile(path);
            Assert.True(protectedState.ReadOnly);
            Assert.False(await protectedState.FlushAsync());
            Assert.Equal(future, File.ReadAllText(path));
            return true;
        }
        finally { Directory.Delete(dir, true); }
    }, CancellationToken.None));
}
