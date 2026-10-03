using System.Net;
using System.Security.Cryptography;
using Hub.Core;

namespace Hub.Tests;

public sealed class ArtifactDownloadTests
{
    static CancellationToken T => TestContext.Current.CancellationToken;
    static readonly byte[] Bytes = Enumerable.Range(0, 150000).Select(i => (byte)(i % 251)).ToArray();

    static ArtifactChoice Choice(byte[]? bytes = null)
    {
        bytes ??= Bytes;
        return new("alpha", "Alpha", new(new("acme", "alpha"), "https://github.com/acme/alpha/releases", null), "v1",
            new("alpha.apk", "https://github.com/acme/alpha/releases/download/v1/alpha.apk", bytes.LongLength,
                Convert.ToHexStringLower(SHA256.HashData(bytes))), false);
    }

    sealed class Folder : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "hub-download-test-" + Guid.NewGuid().ToString("N"));
        public string Verified => Path.Combine(Root, "hub-verified.apk");
        public void AssertNoPartial() => Assert.Empty(Directory.Exists(Root) ? Directory.GetFiles(Root, "*.part") : []);
        public void Dispose() { if (Directory.Exists(Root)) Directory.Delete(Root, true); }
    }

    sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> response) : HttpMessageHandler
    {
        public int Calls;
        public HttpRequestMessage? Request;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        { Interlocked.Increment(ref Calls); Request = request; return response(request, ct); }
    }

    sealed class StreamContentWithoutLength(Func<Stream> stream) : HttpContent
    {
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
        protected override Task SerializeToStreamAsync(Stream target, TransportContext? context) => stream().CopyToAsync(target);
        protected override Task<Stream> CreateContentReadStreamAsync() => Task.FromResult(stream());
        protected override Task<Stream> CreateContentReadStreamAsync(CancellationToken ct) => Task.FromResult(stream());
    }

    sealed class BlockAfterChunk(byte[] chunk, Exception? fail = null) : Stream
    {
        int _reads;
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            if (Interlocked.Increment(ref _reads) == 1)
            {
                chunk.CopyTo(buffer);
                Started.TrySetResult();
                return chunk.Length;
            }
            if (fail is not null) throw fail;
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            return 0;
        }
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() => throw new NotSupportedException();
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    sealed class Observe(Action<ArtifactDownloadProgress> observe) : IProgress<ArtifactDownloadProgress>
    { public void Report(ArtifactDownloadProgress value) => observe(value); }

    sealed class CountingStream(byte[] bytes) : MemoryStream(bytes)
    {
        public long BytesRead { get; private set; }
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
        {
            var read = await base.ReadAsync(buffer, ct);
            BytesRead += read;
            return read;
        }
    }

    static HttpResponseMessage Response(HttpContent? content = null, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = content ?? new ByteArrayContent(Bytes) };

    static Handler Fake(Func<HttpRequestMessage, HttpResponseMessage>? response = null) =>
        new((r, _) => Task.FromResult(response?.Invoke(r) ?? Response()));

    [Fact]
    public async Task PublishesOnlyVerifiedBytesWithPublicGetAndBoundedProgress()
    {
        using var folder = new Folder();
        using var handler = Fake();
        using var http = new HttpClient(handler);
        var progress = new List<ArtifactDownloadProgress>();
        var choice = Choice() with { Asset = Choice().Asset with { Sha256 = Choice().Asset.Sha256!.ToUpperInvariant() } };
        var result = await new ArtifactDownloader(http, folder.Root).DownloadAsync(choice, new Observe(progress.Add), T);
        Assert.Equal(ArtifactDownloadState.Verified, result.State);
        Assert.Equal(ArtifactDownloadError.None, result.Error);
        Assert.Equal(folder.Verified, result.LocalPath);
        Assert.Equal(Bytes.LongLength, result.Bytes);
        Assert.Equal(Choice().Asset.Sha256, result.Sha256);
        Assert.Equal(Bytes, await File.ReadAllBytesAsync(result.LocalPath!, T));
        folder.AssertNoPartial();
        Assert.True(progress.Count >= 3);
        Assert.Equal(Bytes.LongLength, progress[^1].Bytes);
        Assert.All(progress, p => Assert.InRange(p.Bytes, 1, p.ExpectedBytes));
        Assert.Equal(HttpMethod.Get, handler.Request!.Method);
        Assert.Null(handler.Request.Headers.Authorization);
        Assert.Equal("identity", handler.Request.Headers.AcceptEncoding.Single().Value);
        Assert.Contains("tamanho e SHA-256 conferidos", result.Message);
        Assert.DoesNotContain("assinatura conferida", result.Message);
    }

    [Fact]
    public async Task CorruptBytesAreDiscardedAndPreviouslyVerifiedFileIsPreserved()
    {
        using var folder = new Folder();
        Directory.CreateDirectory(folder.Root);
        await File.WriteAllTextAsync(folder.Verified, "anterior", T);
        var corrupt = Bytes.ToArray(); corrupt[777] ^= 1;
        using var http = new HttpClient(Fake(_ => Response(new ByteArrayContent(corrupt))));
        var result = await new ArtifactDownloader(http, folder.Root).DownloadAsync(Choice(), ct: T);
        Assert.Equal(ArtifactDownloadError.ChecksumMismatch, result.Error);
        Assert.Null(result.LocalPath);
        Assert.Equal("anterior", await File.ReadAllTextAsync(folder.Verified, T));
        folder.AssertNoPartial();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    public async Task RejectsDivergentContentLengthBeforeCreatingPartial(int delta)
    {
        using var folder = new Folder();
        using var http = new HttpClient(Fake(_ => Response(new ByteArrayContent(new byte[Bytes.Length + delta]))));
        var result = await new ArtifactDownloader(http, folder.Root).DownloadAsync(Choice(), ct: T);
        Assert.Equal(ArtifactDownloadError.SizeMismatch, result.Error);
        Assert.False(File.Exists(folder.Verified));
        folder.AssertNoPartial();
    }

    [Theory]
    [InlineData(-1, ArtifactDownloadError.SizeMismatch)]
    [InlineData(1, ArtifactDownloadError.SizeMismatch)]
    [InlineData(0, ArtifactDownloadError.None)]
    public async Task DoesNotTrustMissingContentLength(int delta, ArtifactDownloadError expected)
    {
        using var folder = new Folder();
        byte[] payload = delta == 0 ? Bytes : new byte[Bytes.Length + delta];
        using var http = new HttpClient(Fake(_ => Response(new StreamContentWithoutLength(() => new MemoryStream(payload)))));
        var result = await new ArtifactDownloader(http, folder.Root).DownloadAsync(Choice(), ct: T);
        Assert.Equal(expected, result.Error);
        Assert.Equal(delta == 0, File.Exists(folder.Verified));
        folder.AssertNoPartial();
    }

    [Fact]
    public async Task StreamingResponseCannotExceedTheLimitEvenWithoutLengthHeader()
    {
        using var folder = new Folder();
        var payload = new byte[1000000];
        using var stream = new CountingStream(payload);
        using var http = new HttpClient(Fake(_ => Response(new StreamContentWithoutLength(() => stream))));
        var result = await new ArtifactDownloader(http, folder.Root, maxBytes: 4).DownloadAsync(Choice(new byte[4]), ct: T);
        Assert.Equal(ArtifactDownloadError.TooLarge, result.Error);
        Assert.Equal(5, stream.BytesRead); // limita leitura a tamanho esperado + 1, sem drenar a resposta
        Assert.False(File.Exists(folder.Verified));
        folder.AssertNoPartial();
    }

    [Fact]
    public async Task CancellationDuringBodyDiscardsPartialAndKeepsOldVerifiedFile()
    {
        using var folder = new Folder();
        Directory.CreateDirectory(folder.Root);
        await File.WriteAllTextAsync(folder.Verified, "anterior", T);
        using var stream = new BlockAfterChunk(new byte[3]);
        using var http = new HttpClient(Fake(_ => Response(new StreamContentWithoutLength(() => stream))));
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(T);
        var wrote = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var downloading = new ArtifactDownloader(http, folder.Root).DownloadAsync(Choice(), new Observe(_ => wrote.TrySetResult()), cancel.Token);
        await wrote.Task.WaitAsync(T);
        Assert.Single(Directory.GetFiles(folder.Root, "*.part"));
        cancel.Cancel();
        var result = await downloading;
        Assert.Equal(ArtifactDownloadState.Cancelled, result.State);
        Assert.Null(result.LocalPath);
        Assert.Equal("anterior", await File.ReadAllTextAsync(folder.Verified, T));
        folder.AssertNoPartial();
    }

    [Fact]
    public async Task DeadlineCoversStreamingBodyAfterHeadersArrived()
    {
        using var folder = new Folder();
        using var stream = new BlockAfterChunk(new byte[3]);
        using var http = new HttpClient(Fake(_ => Response(new StreamContentWithoutLength(() => stream))));
        var result = await new ArtifactDownloader(http, folder.Root, timeout: TimeSpan.FromSeconds(1)).DownloadAsync(Choice(), ct: T);
        Assert.True(stream.Started.Task.IsCompleted);
        Assert.Equal(ArtifactDownloadError.Timeout, result.Error);
        Assert.False(File.Exists(folder.Verified));
        folder.AssertNoPartial();
    }

    [Fact]
    public async Task CancelledBeforeStartingDoesNotSendRequest()
    {
        using var folder = new Folder(); using var handler = Fake(); using var http = new HttpClient(handler);
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        var result = await new ArtifactDownloader(http, folder.Root).DownloadAsync(Choice(), ct: cancel.Token);
        Assert.Equal(ArtifactDownloadState.Cancelled, result.State);
        Assert.Equal(0, handler.Calls);
        Assert.False(Directory.Exists(folder.Root));
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.PartialContent)]
    public async Task HttpFailureNeverPublishesPartial(HttpStatusCode status)
    {
        using var folder = new Folder(); using var http = new HttpClient(Fake(_ => Response(status: status)));
        var result = await new ArtifactDownloader(http, folder.Root).DownloadAsync(Choice(), ct: T);
        Assert.Equal(ArtifactDownloadError.Http, result.Error);
        Assert.Contains(((int)status).ToString(), result.Message);
        Assert.False(File.Exists(folder.Verified)); folder.AssertNoPartial();
    }

    [Fact]
    public async Task NetworkFailureIsExplicit()
    {
        using var folder = new Folder();
        using var handler = new Handler((_, _) => Task.FromException<HttpResponseMessage>(new HttpRequestException("offline")));
        using var http = new HttpClient(handler);
        var result = await new ArtifactDownloader(http, folder.Root).DownloadAsync(Choice(), ct: T);
        Assert.Equal(ArtifactDownloadError.Network, result.Error);
        Assert.Null(result.LocalPath); folder.AssertNoPartial();
    }

    [Fact]
    public async Task InterruptedResponseDiscardsBytesAlreadyWritten()
    {
        using var folder = new Folder(); using var stream = new BlockAfterChunk(new byte[3], new IOException("interrompido"));
        using var http = new HttpClient(Fake(_ => Response(new StreamContentWithoutLength(() => stream))));
        var result = await new ArtifactDownloader(http, folder.Root).DownloadAsync(Choice(), ct: T);
        Assert.Equal(ArtifactDownloadState.Failed, result.State);
        Assert.Null(result.LocalPath); folder.AssertNoPartial(); Assert.False(File.Exists(folder.Verified));
    }

    [Fact]
    public async Task StorageFailureDoesNotReadOrDeleteAnUnrelatedFile()
    {
        using var folder = new Folder(); using var handler = Fake(); using var http = new HttpClient(handler);
        await File.WriteAllTextAsync(folder.Root, "não é pasta", T);
        try
        {
            var result = await new ArtifactDownloader(http, folder.Root).DownloadAsync(Choice(), ct: T);
            Assert.Equal(ArtifactDownloadError.Storage, result.Error);
            Assert.Equal(0, handler.Calls);
            Assert.Equal("não é pasta", await File.ReadAllTextAsync(folder.Root, T));
        }
        finally { File.Delete(folder.Root); }
    }

    [Fact]
    public async Task ReplacesOldVerifiedArtifactAndClearsAbandonedPartialsWithoutDeletingOtherFiles()
    {
        using var folder = new Folder(); Directory.CreateDirectory(folder.Root);
        await File.WriteAllTextAsync(folder.Verified, "anterior", T);
        await File.WriteAllTextAsync(Path.Combine(folder.Root, "hub-old.part"), "abandonado", T);
        await File.WriteAllTextAsync(Path.Combine(folder.Root, "keep.txt"), "preservado", T);
        using var http = new HttpClient(Fake()); var downloader = new ArtifactDownloader(http, folder.Root);
        Assert.Equal(ArtifactDownloadState.Verified, (await downloader.DownloadAsync(Choice(), ct: T)).State);
        Assert.Equal(ArtifactDownloadState.Verified, (await downloader.DownloadAsync(Choice(), ct: T)).State);
        Assert.Equal(Bytes, await File.ReadAllBytesAsync(folder.Verified, T));
        Assert.Equal("preservado", await File.ReadAllTextAsync(Path.Combine(folder.Root, "keep.txt"), T));
        Assert.Equal(2, Directory.GetFiles(folder.Root).Length); folder.AssertNoPartial();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("sha256:abcd")]
    [InlineData("gggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggg")]
    public async Task MissingOrInvalidDigestNeverDownloads(string? digest)
    {
        using var folder = new Folder(); using var handler = Fake(); using var http = new HttpClient(handler);
        var c = Choice(); c = c with { Asset = c.Asset with { Sha256 = digest } };
        var result = await new ArtifactDownloader(http, folder.Root).DownloadAsync(c, ct: T);
        Assert.Equal(ArtifactDownloadError.MissingChecksum, result.Error);
        Assert.Equal(0, handler.Calls); Assert.False(Directory.Exists(folder.Root));
    }

    [Theory]
    [InlineData("http://github.com/acme/alpha/releases/download/v1/alpha.apk")]
    [InlineData("https://example.invalid/alpha.apk")]
    [InlineData("https://github.com/acme/other/releases/download/v1/alpha.apk")]
    [InlineData("https://github.com/acme/alpha/releases/download/v2/alpha.apk")]
    [InlineData("https://github.com/acme/alpha/releases/download/v1/other.apk")]
    [InlineData("https://github.com/acme/alpha/releases/download/v1/alpha.apk?token=abc")]
    [InlineData("https://github.com/acme/alpha/releases/download/v1/alpha.apk#fragment")]
    public async Task RejectsUrlOutsideTheDeclaredRepositoryTagAndAsset(string url)
    {
        using var folder = new Folder(); using var handler = Fake(); using var http = new HttpClient(handler);
        var c = Choice(); c = c with { Asset = c.Asset with { Url = url } };
        var result = await new ArtifactDownloader(http, folder.Root).DownloadAsync(c, ct: T);
        Assert.Equal(ArtifactDownloadError.InvalidMetadata, result.Error);
        Assert.Equal(0, handler.Calls); Assert.False(Directory.Exists(folder.Root));
    }

    [Fact]
    public void RejectsOtherProductsPrefixAndRemotePathTraversal()
    {
        var c = Choice();
        Assert.Equal(ArtifactDownloadError.InvalidMetadata, ArtifactDownloader.Ineligible(c with { Channel = c.Channel with { TagPrefix = "other-v" } })!.Error);
        Assert.Equal(ArtifactDownloadError.InvalidMetadata, ArtifactDownloader.Ineligible(c with { Asset = c.Asset with { Name = "../alpha.apk" } })!.Error);
        Assert.Equal(ArtifactDownloadError.InvalidMetadata, ArtifactDownloader.Ineligible(c with { Tag = "../v1" })!.Error);
        Assert.Equal(ArtifactDownloadError.InvalidMetadata, ArtifactDownloader.Ineligible(c with { Asset = c.Asset with { Name = null! } })!.Error);
        Assert.Equal(ArtifactDownloadError.InvalidMetadata, ArtifactDownloader.Ineligible(c with { Asset = c.Asset with { Size = 0 } })!.Error);
        Assert.Equal(ArtifactDownloadError.TooLarge, ArtifactDownloader.Ineligible(c with { Asset = c.Asset with { Size = ArtifactDownloader.DefaultMaxBytes + 1 } })!.Error);
    }

    [Fact]
    public void RepositoryCasingAndEncodedTagDoNotChangeIdentity()
    {
        var c = Choice();
        c = c with { Tag = "v1/test", Asset = c.Asset with { Name = "alpha debug.apk",
            Url = "https://github.com/Acme/Alpha/releases/download/v1%2Ftest/alpha%20debug.apk" } };
        Assert.Null(ArtifactDownloader.Ineligible(c));
    }

    [Fact]
    public async Task PublicDownloadDoesNotInheritAnApiToken()
    {
        using var folder = new Folder(); using var handler = Fake(); using var http = new HttpClient(handler);
        http.DefaultRequestHeaders.Authorization = new("Bearer", "fictional-test-token");
        var result = await new ArtifactDownloader(http, folder.Root).DownloadAsync(Choice(), ct: T);
        Assert.Equal(ArtifactDownloadState.Failed, result.State); Assert.Equal(0, handler.Calls);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RejectsHttpsDowngradeOrUnexpectedCompression(bool downgrade)
    {
        using var folder = new Folder();
        using var http = new HttpClient(Fake(_ =>
        {
            var r = Response();
            if (downgrade) r.RequestMessage = new(HttpMethod.Get, "http://example.invalid/alpha.apk");
            else r.Content.Headers.ContentEncoding.Add("gzip");
            return r;
        }));
        var result = await new ArtifactDownloader(http, folder.Root).DownloadAsync(Choice(), ct: T);
        Assert.Equal(ArtifactDownloadError.InvalidMetadata, result.Error);
        Assert.Null(result.LocalPath); folder.AssertNoPartial();
    }
}
