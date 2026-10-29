using Urbe.Client;

namespace Urbe.Client.Tests;

/// <summary>
/// DEC-0059 (no limiters): the vault opened only while it had fewer than 10 000 items, every text file under 32 MB,
/// 32 levels of folders and 256 MB of text, and a file imported had to be under 32 MB. A real vault (photos, long
/// recordings, data files) passes all of that. Each limit made the whole vault refuse to open.
/// </summary>
public sealed class FolderVaultNoLimitsTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "urbe-nolimits-" + Guid.NewGuid().ToString("N"));
    private string Vault => Path.Combine(_dir, "vault");
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public FolderVaultNoLimitsTests()
    {
        Directory.CreateDirectory(Vault);
        File.WriteAllText(Path.Combine(Vault, "Comece.md"), "# Comece\n");
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
    }

    private FolderVaultStorage Storage() => new(_ => Task.FromResult<string?>(Vault), Path.Combine(_dir, "app", "sel.json"));

    [Fact]
    public async Task AVaultWithMoreThanTenThousandFilesOpensAndListsThemAll()
    {
        var photos = Path.Combine(Vault, "Fotos");
        Directory.CreateDirectory(photos);
        for (int i = 0; i < 10_050; i++) File.WriteAllBytes(Path.Combine(photos, "f" + i + ".png"), [1]);
        var storage = Storage();
        Assert.NotNull(await storage.PickAsync(Ct));
        var listed = await storage.ListFilePathsAsync(Ct);
        Assert.Equal(10_051, listed.Count(p => !p.StartsWith('.')));
        await storage.SaveExistingTextFileAsync("Comece.md", "# Comece\n\nSalvo.\n", Ct);
        Assert.Equal("# Comece\n\nSalvo.\n", File.ReadAllText(Path.Combine(Vault, "Comece.md")));
    }

    [Fact]
    public async Task AHugeDataFileDoesNotStopTheVaultFromOpening()
    {
        using (var big = File.Create(Path.Combine(Vault, "dados.csv"))) big.SetLength(40L * 1024 * 1024);
        var storage = Storage();
        var snapshot = await storage.PickAsync(Ct);
        Assert.NotNull(snapshot);
        Assert.Contains(snapshot!.Documents, d => d.Path == "Comece.md");
        Assert.Contains("dados.csv", await storage.ListFilePathsAsync(Ct));
        await storage.SaveExistingTextFileAsync("Comece.md", "# Comece\n\nOk.\n", Ct);   // the save does not trip on it
        var read = await storage.ReadFileAsync("dados.csv", long.MaxValue, Ct);
        Assert.NotNull(read);
    }

    [Fact]
    public async Task FoldersDeeperThanThirtyTwoLevelsOpen()
    {
        var deep = Vault;
        for (int i = 0; i < 40; i++) deep = Path.Combine(deep, "n" + i);
        Directory.CreateDirectory(deep);
        File.WriteAllText(Path.Combine(deep, "Fundo.md"), "# Fundo\n");
        var snapshot = await Storage().PickAsync(Ct);
        Assert.Contains(snapshot!.Documents, d => d.Path.EndsWith("/Fundo.md", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ALongRecordingIsImported()
    {
        var storage = Storage();
        Assert.NotNull(await storage.PickAsync(Ct));
        var audio = new byte[40 * 1024 * 1024];
        audio[^1] = 7;
        await storage.CreateImportedFileAsync("Aula.m4a", audio, Ct);
        Assert.Equal(audio.Length, new FileInfo(Path.Combine(Vault, "Aula.m4a")).Length);
    }
}
