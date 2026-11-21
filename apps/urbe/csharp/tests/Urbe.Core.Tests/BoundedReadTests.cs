using Urbe.Core.Assistant;

namespace Urbe.Core.Tests;

/// <summary>#954: attaching a PDF must not allocate its 512 MiB limit; memory follows the file's real size.</summary>
public sealed class BoundedReadTests
{
    private sealed class Unseekable(byte[] data) : MemoryStream(data) { public override bool CanSeek => false; }

    [Fact]
    public async Task ReadingASmallFileWithAHugeLimitAllocatesAboutTheFile()
    {
        var data = new byte[3 * 1024 * 1024];
        Random.Shared.NextBytes(data);
        foreach (var stream in new Stream[] { new MemoryStream(data), new Unseekable(data) })
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            var read = await AssistantTextAttachments.ReadBoundedAsync(stream, AssistantDocumentTools.MaxFileBytes, TestContext.Current.CancellationToken);
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.Equal(data, read);
            Assert.True(allocated < 32L * 1024 * 1024, "allocated " + allocated / 1024 / 1024 + " MB");
        }
    }

    [Fact]
    public async Task AFileOverTheLimitIsRefusedWithoutReadingItAll()
    {
        Assert.Null(await AssistantTextAttachments.ReadBoundedAsync(new MemoryStream(new byte[1001]), 1000, TestContext.Current.CancellationToken));
        Assert.Null(await AssistantTextAttachments.ReadBoundedAsync(new Unseekable(new byte[1001]), 1000, TestContext.Current.CancellationToken));
        Assert.Equal(1000, (await AssistantTextAttachments.ReadBoundedAsync(new Unseekable(new byte[1000]), 1000, TestContext.Current.CancellationToken))!.Length);
    }
}
