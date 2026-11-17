using Urbe.Client;

namespace Urbe.Client.Tests;

/// <summary>CST-180: an export is written whole and read back; a file that does not match is an error.</summary>
public sealed class ExportFilesTests
{
    /// <summary>A place to save: what is there, how a read-back looks (or fails).</summary>
    private sealed class Place(byte[] existing, Func<byte[], byte[]>? corrupt = null, bool readable = true)
    {
        public MemoryStream Store { get; } = Expandable(existing);
        private static MemoryStream Expandable(byte[] bytes) { var m = new MemoryStream(); m.Write(bytes); return m; }
        public Task<Stream> OpenWrite() { Store.Position = 0; return Task.FromResult<Stream>(new Kept(Store)); }
        public Task<Stream> OpenRead()
        {
            if (!readable) throw new NotSupportedException("no read-back here");
            var data = Store.ToArray();
            return Task.FromResult<Stream>(new MemoryStream(corrupt?.Invoke(data) ?? data));
        }
        public Task Save(byte[] bytes) => ExportFiles.WriteAndCheckAsync(OpenWrite, OpenRead, bytes);

        private sealed class Kept(Stream inner) : Stream
        {
            public override bool CanRead => inner.CanRead; public override bool CanSeek => inner.CanSeek; public override bool CanWrite => inner.CanWrite;
            public override long Length => inner.Length; public override long Position { get => inner.Position; set => inner.Position = value; }
            public override void Flush() => inner.Flush();
            public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
            public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);
            public override void SetLength(long value) => inner.SetLength(value);
            public override void Write(byte[] buffer, int offset, int count) => inner.Write(buffer, offset, count);
        }
    }

    [Fact]
    public async Task AShorterFileOverALongerOneLeavesNothingBehind()
    {
        var place = new Place(new byte[5000]);
        var html = "<html>curto</html>"u8.ToArray();
        await place.Save(html);
        Assert.Equal(html, place.Store.ToArray());
    }

    [Fact]
    public async Task AFileThatDoesNotMatchIsAnErrorAndAnUnreadableOneIsTrusted()
    {
        var bytes = "<html>publicação</html>"u8.ToArray();
        var error = await Assert.ThrowsAsync<IOException>(() => new Place([], corrupt: d => d[..^3]).Save(bytes));
        Assert.Contains("não confere", error.Message);
        await new Place([], readable: false).Save(bytes);   // no read-back: written, not checked
    }
}
