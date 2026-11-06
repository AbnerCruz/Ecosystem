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

    [Fact]
    public async Task ReadingState_KeepsReaderDefaultsAndTotalsAcrossWritersAndReadsOlderFiles() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var dir = Path.Combine(Path.GetTempPath(), "urbe-reader-defaults-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "readers.json"), vault = new('a', 64), hash = new('b', 64);
        try
        {
            // a file of the earlier schema: no totals, no reader defaults
            File.WriteAllText(path, "{\"version\":1,\"entries\":[{\"vaultId\":\"" + vault + "\",\"path\":\"a.pdf\",\"revision\":\"" + hash +
                "\",\"format\":\"pdf\",\"location\":{\"part\":7,\"offset\":0,\"scroll\":0},\"preferences\":{\"fontSize\":18,\"lineHeight\":1.6,\"zoom\":2,\"theme\":\"system\",\"margin\":5},\"updated\":10,\"deleted\":false}]}");
            var one = new ReadingStateFile(path);
            Assert.False(one.ReadOnly);
            Assert.Equal(7, one.Restore(vault, "a.pdf", hash)!.Location.Part);
            var two = new ReadingStateFile(path);
            one.RememberDefault(ReaderKinds.Book, new(26, 2, Theme: "dark"));
            two.RememberDefault(ReaderKinds.Pdf, new(Zoom: 1.5));
            two.Remember(new(vault, "b.epub", hash, "epub", new(3, Total: 9), new(26), 20));
            Assert.True(await one.FlushAsync());
            Assert.True(await two.FlushAsync());
            var reopened = new ReadingStateFile(path);
            Assert.Equal(26, reopened.DefaultFor(ReaderKinds.Book)!.FontSize);
            Assert.Equal(1.5, reopened.PreferencesFor(VaultFileKinds.Pdf).Zoom);
            Assert.Equal("dark", reopened.PreferencesFor(VaultFileKinds.Epub).Theme);
            Assert.Null(reopened.DefaultFor(ReaderKinds.Text));
            Assert.Equal(9, reopened.Restore(vault, "b.epub", hash)!.Location.Total);
            Assert.Equal(["b.epub", "a.pdf"], reopened.Recent(vault, 5).Select(s => s.Path));
            return true;
        }
        finally { Directory.Delete(dir, true); }
    }, CancellationToken.None));
}
