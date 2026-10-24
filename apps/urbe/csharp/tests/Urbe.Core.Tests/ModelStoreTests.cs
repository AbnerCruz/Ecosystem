using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using Urbe.Core.Assistant;
using Urbe.LocalAi;

namespace Urbe.Core.Tests;

/// <summary>The on-device model's file: downloaded in pieces, resumed, and kept only when it is the file that was meant.</summary>
public sealed class ModelStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "urbe-models-" + Guid.NewGuid().ToString("N"));
    private readonly byte[] _bytes = Enumerable.Range(0, 300_000).Select(i => (byte)(i * 31 % 251)).ToArray();

    private LocalModel Model(string? sha = null) => new("t", "Teste", "", "t.gguf", "https://exemplo.test/t.gguf", _bytes.Length,
        sha ?? Convert.ToHexString(SHA256.HashData(_bytes)).ToLowerInvariant(), 4096, 1000, true);

    private sealed class Server(byte[] bytes, bool honourRange = true, byte[]? wrong = null) : HttpMessageHandler
    {
        public List<long?> Ranges { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancel)
        {
            var from = request.Headers.Range?.Ranges.FirstOrDefault()?.From;
            Ranges.Add(from);
            var data = wrong ?? bytes;
            if (from is { } start && honourRange)
            {
                var part = new HttpResponseMessage(HttpStatusCode.PartialContent) { Content = new ByteArrayContent(data[(int)start..]) };
                part.Content.Headers.ContentRange = new ContentRangeHeaderValue(start, data.Length - 1, data.Length);
                return Task.FromResult(part);
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(data) });
        }
    }

    [Fact]
    public async Task ADownloadIsKeptAfterItsHashMatches()
    {
        var server = new Server(_bytes);
        var store = new ModelStore(_dir, new HttpClient(server));
        var progress = new List<long>();
        await store.InstallAsync(Model(), new Progress<(long, long)>(p => progress.Add(p.Item1)), CancellationToken.None);
        Assert.True(store.IsInstalled(Model()));
        Assert.Equal(_bytes, File.ReadAllBytes(store.PathOf(Model())));
        Assert.False(File.Exists(store.PathOf(Model()) + ".part"));
        Assert.Equal([null], server.Ranges);
        // already there: nothing is asked again
        await store.InstallAsync(Model(), null, CancellationToken.None);
        Assert.Single(server.Ranges);
    }

    [Fact]
    public async Task APartialFileIsResumedFromWhereItStopped()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllBytes(Path.Combine(_dir, "t.gguf.part"), _bytes[..120_000]);
        var server = new Server(_bytes);
        var store = new ModelStore(_dir, new HttpClient(server));
        Assert.Equal(120_000, store.PartialBytes(Model()));
        await store.InstallAsync(Model(), null, CancellationToken.None);
        Assert.Equal([120_000L], server.Ranges);
        Assert.Equal(_bytes, File.ReadAllBytes(store.PathOf(Model())));
    }

    [Fact]
    public async Task AServerThatIgnoresTheRangeStartsOver()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllBytes(Path.Combine(_dir, "t.gguf.part"), _bytes[..50_000]);
        var store = new ModelStore(_dir, new HttpClient(new Server(_bytes, honourRange: false)));
        await store.InstallAsync(Model(), null, CancellationToken.None);
        Assert.Equal(_bytes, File.ReadAllBytes(store.PathOf(Model())));
    }

    [Fact]
    public async Task AFileThatIsNotTheOneMeantIsThrownAway()
    {
        var other = (byte[])_bytes.Clone();
        other[1000] ^= 1;
        var store = new ModelStore(_dir, new HttpClient(new Server(_bytes, wrong: other)));
        var ex = await Assert.ThrowsAsync<IOException>(() => store.InstallAsync(Model(), null, CancellationToken.None));
        Assert.Contains("não confere", ex.Message);
        Assert.False(store.IsInstalled(Model()));
        Assert.False(File.Exists(store.PathOf(Model()) + ".part"));
    }

    [Fact]
    public void TheCatalogPicksTheModelThatFitsTheMemory()
    {
        Assert.Equal(LocalModelCatalog.E2B.Id, LocalModelCatalog.For(6L * 1024 * 1024 * 1024).Id);
        Assert.Equal(LocalModelCatalog.E4B.Id, LocalModelCatalog.For(16L * 1024 * 1024 * 1024).Id);
        Assert.Equal(LocalModelCatalog.E2B.Id, LocalModelCatalog.For(0).Id);
        Assert.All(LocalModelCatalog.All, m => Assert.Matches("^[0-9a-f]{64}$", m.Sha256));
    }

    public void Dispose() { try { Directory.Delete(_dir, true); } catch (IOException) { } }
}
