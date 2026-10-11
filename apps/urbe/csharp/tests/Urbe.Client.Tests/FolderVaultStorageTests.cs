using System.Text;
using Urbe.Client;

namespace Urbe.Client.Tests;

/// <summary>The desktop/Windows vault on a real temporary folder: open, remember, save one
/// existing note with verification, refuse anything unsafe.</summary>
public sealed class FolderVaultStorageTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "urbe-folder-vault-" + Guid.NewGuid().ToString("N"));
    private string Vault => Path.Combine(_dir, "vault");
    private string Selection => Path.Combine(_dir, "app", "native-vault.json");

    public FolderVaultStorageTests()
    {
        Directory.CreateDirectory(Path.Combine(Vault, "Bairro"));
        File.WriteAllText(Path.Combine(Vault, "Comece.md"), "# Comece\n\n[[Bairro/Um]]\n");
        File.WriteAllText(Path.Combine(Vault, "Bairro", "Um.md"), "# Um\n");
        File.WriteAllBytes(Path.Combine(Vault, "foto.png"), [1, 2, 3]);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private FolderVaultStorage Storage() => new(_ => Task.FromResult<string?>(Vault), Selection);

    [Fact]
    public async Task PickReadsTheFolderAndIsRememberedForTheNextStart()
    {
        var storage = Storage();
        var snapshot = await storage.PickAsync(Ct);
        Assert.NotNull(snapshot);
        Assert.True(storage.IsConnected);
        Assert.Contains(snapshot!.Documents, d => d.Path == "Bairro/Um.md");
        Assert.DoesNotContain(snapshot.Documents, d => d.Path == "foto.png");

        var restored = await Storage().RestoreAsync(Ct);
        Assert.NotNull(restored);
        Assert.Equal(snapshot.Documents.Select(d => d.Path).OrderBy(p => p), restored!.Documents.Select(d => d.Path).OrderBy(p => p));
    }

    [Fact]
    public async Task SavingAnExistingNoteWritesOnlyThatFileAndLeavesNoRecoveryCopy()
    {
        var storage = Storage();
        await storage.PickAsync(Ct);
        var before = Directory.GetFiles(Vault, "*", SearchOption.AllDirectories).ToDictionary(f => f, File.ReadAllBytes);
        await storage.SaveExistingNoteAsync("Bairro/Um.md", "# Um\n\nNovo texto.\n", Ct);
        Assert.Equal("# Um\n\nNovo texto.\n", File.ReadAllText(Path.Combine(Vault, "Bairro", "Um.md"), Encoding.UTF8));
        var after = Directory.GetFiles(Vault, "*", SearchOption.AllDirectories);
        Assert.Equal(before.Keys.OrderBy(f => f), after.OrderBy(f => f));   // no leftover .urbe-recovery-*
        foreach (var (file, bytes) in before)
            if (!file.EndsWith("Um.md", StringComparison.Ordinal)) Assert.Equal(bytes, File.ReadAllBytes(file));

        // a second save builds on the new baseline
        await storage.SaveExistingNoteAsync("Bairro/Um.md", "# Um\n\nDe novo.\n", Ct);
        Assert.Equal("# Um\n\nDe novo.\n", File.ReadAllText(Path.Combine(Vault, "Bairro", "Um.md")));
    }

    [Fact]
    public async Task AChangeOutsideUrbeBlocksTheSaveInsteadOfOverwriting()
    {
        var storage = Storage();
        await storage.PickAsync(Ct);
        File.WriteAllText(Path.Combine(Vault, "Comece.md"), "# Comece\n\nEditado em outro app.\n");
        await Assert.ThrowsAsync<IOException>(() => storage.SaveExistingNoteAsync("Bairro/Um.md", "x", Ct));
        Assert.Equal("# Um\n", File.ReadAllText(Path.Combine(Vault, "Bairro", "Um.md")));
        Assert.Equal("# Comece\n\nEditado em outro app.\n", File.ReadAllText(Path.Combine(Vault, "Comece.md")));
    }

    [Theory]
    [InlineData("Nova.md")]                 // creation is a later step
    [InlineData("../fora.md")]
    [InlineData("Bairro/../Comece.md")]
    [InlineData(".urbe/mapa.md")]
    [InlineData("C:/x.md")]
    [InlineData("foto.png")]
    public async Task UnsafeOrUnsupportedTargetsAreRefused(string path)
    {
        var storage = Storage();
        await storage.PickAsync(Ct);
        await Assert.ThrowsAnyAsync<Exception>(() => storage.SaveExistingNoteAsync(path, "x", Ct));
        Assert.False(File.Exists(Path.Combine(_dir, "fora.md")));
        Assert.False(File.Exists(Path.Combine(Vault, "Nova.md")));
    }

    [Fact]
    public async Task CancelledPickerKeepsTheStorageDisconnected()
    {
        var storage = new FolderVaultStorage(_ => Task.FromResult<string?>(null), Selection);
        Assert.Null(await storage.PickAsync(Ct));
        Assert.False(storage.IsConnected);
        Assert.Null(await storage.RestoreAsync(Ct));
    }

    [Fact]
    public async Task CreatingANoteAndThePersonalizationFileWritesOnlyTheNewFiles()
    {
        var storage = Storage();
        await storage.PickAsync(Ct);
        var before = Directory.GetFiles(Vault, "*", SearchOption.AllDirectories).ToDictionary(f => f, File.ReadAllBytes);
        await storage.CreateTextFileAsync("Bairro/Nova nota.md", "# Nova nota\n", Ct);
        await storage.CreateTextFileAsync("Personalização/tema.json", "{\n  \"$schema\": \"urbe-tema-1\"\n}\n", Ct);
        Assert.Equal("# Nova nota\n", File.ReadAllText(Path.Combine(Vault, "Bairro", "Nova nota.md")));
        Assert.True(File.Exists(Path.Combine(Vault, "Personalização", "tema.json")));
        foreach (var (file, bytes) in before) Assert.Equal(bytes, File.ReadAllBytes(file));
        Assert.Equal(before.Count + 2, Directory.GetFiles(Vault, "*", SearchOption.AllDirectories).Length);

        // then it is an existing file: rewritten, never created twice
        await storage.SaveExistingTextFileAsync("Personalização/tema.json", "{\n  \"tema\": \"oceano\"\n}\n", Ct);
        Assert.Contains("oceano", File.ReadAllText(Path.Combine(Vault, "Personalização", "tema.json")));
        await Assert.ThrowsAsync<IOException>(() => storage.CreateTextFileAsync("Bairro/Nova nota.md", "x", Ct));
        Assert.Equal("# Nova nota\n", File.ReadAllText(Path.Combine(Vault, "Bairro", "Nova nota.md")));
    }

    [Theory]
    [InlineData(".urbe/mapa.json")]
    [InlineData(".obsidian/app.json")]
    [InlineData("../fora.md")]
    [InlineData("foto.png")]
    [InlineData("Bairro/script.exe")]
    public async Task CreationRefusesInternalHiddenUnsafeOrBinaryPaths(string path)
    {
        var storage = Storage();
        await storage.PickAsync(Ct);
        int count = Directory.GetFiles(Vault, "*", SearchOption.AllDirectories).Length;
        await Assert.ThrowsAnyAsync<Exception>(() => storage.CreateTextFileAsync(path, "x", Ct));
        Assert.Equal(count, Directory.GetFiles(Vault, "*", SearchOption.AllDirectories).Length);
        Assert.False(File.Exists(Path.Combine(_dir, "fora.md")));
    }
}
