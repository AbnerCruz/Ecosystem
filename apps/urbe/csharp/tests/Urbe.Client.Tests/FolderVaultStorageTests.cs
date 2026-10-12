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

    [Fact]
    public async Task MovingANoteKeepsItsExactBytesAndRemovesOnlyTheOldName()
    {
        var bom = new byte[] { 0xef, 0xbb, 0xbf }.Concat(Encoding.UTF8.GetBytes("# Um\r\n")).ToArray();
        File.WriteAllBytes(Path.Combine(Vault, "Bairro", "Um.md"), bom);
        var storage = Storage();
        await storage.PickAsync(Ct);
        var before = Directory.GetFiles(Vault, "*", SearchOption.AllDirectories).ToDictionary(f => f, File.ReadAllBytes);
        await storage.MoveTextFileAsync("Bairro/Um.md", "Bairro/Primeiro.md", Ct);
        Assert.False(File.Exists(Path.Combine(Vault, "Bairro", "Um.md")));
        Assert.Equal(bom, File.ReadAllBytes(Path.Combine(Vault, "Bairro", "Primeiro.md")));
        foreach (var (file, bytes) in before)
            if (!file.EndsWith("Um.md", StringComparison.Ordinal)) Assert.Equal(bytes, File.ReadAllBytes(file));
        Assert.Equal(before.Count, Directory.GetFiles(Vault, "*", SearchOption.AllDirectories).Length);   // no temporary copy left

        // then it is the file at its new name: saved there, and moved again (another extension)
        await storage.SaveExistingNoteAsync("Bairro/Primeiro.md", "# Primeiro\n", Ct);
        await storage.MoveTextFileAsync("Bairro/Primeiro.md", "Bairro/Primeiro.txt", Ct);
        Assert.Equal("# Primeiro\n", File.ReadAllText(Path.Combine(Vault, "Bairro", "Primeiro.txt")));
        await Assert.ThrowsAsync<FileNotFoundException>(() => storage.MoveTextFileAsync("Bairro/Primeiro.md", "Bairro/X.md", Ct));
    }

    [Fact]
    public async Task AChangeOfCaseOnlyRenamesTheSameFile()
    {
        var storage = Storage();
        await storage.PickAsync(Ct);
        await storage.MoveTextFileAsync("Comece.md", "comece.md", Ct);
        var names = Directory.GetFiles(Vault).Select(Path.GetFileName).ToArray();
        Assert.Contains("comece.md", names);
        Assert.DoesNotContain("Comece.md", names);
        Assert.DoesNotContain(names, n => n!.Contains(".urbe-move-", StringComparison.Ordinal));
        Assert.Equal("# Comece\n\n[[Bairro/Um]]\n", File.ReadAllText(Path.Combine(Vault, "comece.md")));
    }

    [Theory]
    [InlineData("Comece.md", "Bairro/Um.md")]        // never over another file
    [InlineData("Comece.md", "bairro/UM.md")]        // nor over one that differs only in case
    [InlineData("Comece.md", "Comece.md")]
    [InlineData("Comece.md", "../fora.md")]
    [InlineData("Comece.md", ".urbe/x.md")]
    [InlineData("Comece.md", "Comece.png")]
    [InlineData("foto.png", "foto2.png")]
    [InlineData("Nada.md", "Algo.md")]
    public async Task UnsafeMovesAreRefusedAndTouchNothing(string from, string to)
    {
        var storage = Storage();
        await storage.PickAsync(Ct);
        var before = Directory.GetFiles(Vault, "*", SearchOption.AllDirectories).ToDictionary(f => f, File.ReadAllBytes);
        await Assert.ThrowsAnyAsync<Exception>(() => storage.MoveTextFileAsync(from, to, Ct));
        var after = Directory.GetFiles(Vault, "*", SearchOption.AllDirectories);
        Assert.Equal(before.Keys.Order(), after.Order());
        foreach (var (file, bytes) in before) Assert.Equal(bytes, File.ReadAllBytes(file));
        Assert.False(File.Exists(Path.Combine(_dir, "fora.md")));
    }

    [Fact]
    public async Task AChangeOutsideUrbeBlocksTheMove()
    {
        var storage = Storage();
        await storage.PickAsync(Ct);
        File.WriteAllText(Path.Combine(Vault, "Bairro", "Um.md"), "# Um\n\nEditado em outro app.\n");
        await Assert.ThrowsAsync<IOException>(() => storage.MoveTextFileAsync("Bairro/Um.md", "Bairro/Dois.md", Ct));
        Assert.True(File.Exists(Path.Combine(Vault, "Bairro", "Um.md")));
        Assert.False(File.Exists(Path.Combine(Vault, "Bairro", "Dois.md")));
    }

    private static string TrashOf(string path, string content, string? earlier = null)
    {
        var store = new Urbe.Core.DocumentStore();
        var doc = store.Upsert(new Urbe.Core.DocumentInput { Path = path, Content = content });
        return Urbe.Core.LegacyTrash.Read(earlier, null, out _).ExportWith(doc, 1760000000000);
    }

    [Fact]
    public async Task TrashingANoteWritesTheTrashFirstAndRemovesOnlyThatFile()
    {
        var storage = Storage();
        await storage.PickAsync(Ct);
        var before = Directory.GetFiles(Vault, "*", SearchOption.AllDirectories).ToDictionary(f => f, File.ReadAllBytes);
        var trash = TrashOf("Bairro/Um.md", "# Um\n");
        await storage.TrashTextFileAsync("Bairro/Um.md", trash, Ct);
        Assert.False(File.Exists(Path.Combine(Vault, "Bairro", "Um.md")));
        Assert.Equal(trash, File.ReadAllText(Path.Combine(Vault, ".urbe", "trash.v2.json")));
        foreach (var (file, bytes) in before)
            if (!file.EndsWith("Um.md", StringComparison.Ordinal)) Assert.Equal(bytes, File.ReadAllBytes(file));
        Assert.Equal(before.Count, Directory.GetFiles(Vault, "*", SearchOption.AllDirectories).Length);   // -1 note +1 trash, no copies left

        // a second note: the trash file is replaced (no recovery copy left) and keeps the first item
        var second = TrashOf("Comece.md", "# Comece\n\n[[Bairro/Um]]\n", trash);
        await storage.TrashTextFileAsync("Comece.md", second, Ct);
        Assert.Equal(second, File.ReadAllText(Path.Combine(Vault, ".urbe", "trash.v2.json")));
        Assert.Contains("Bairro/Um.md", second);
        Assert.Equal(["foto.png", "trash.v2.json"], Directory.GetFiles(Vault, "*", SearchOption.AllDirectories).Select(Path.GetFileName).Order().ToArray()!);
    }

    [Fact]
    public async Task ATrashWithoutTheCurrentTextOrOfAnotherVersionRemovesNothing()
    {
        var storage = Storage();
        await storage.PickAsync(Ct);
        // the trash does not hold this text
        await Assert.ThrowsAsync<InvalidDataException>(() => storage.TrashTextFileAsync("Bairro/Um.md", TrashOf("Bairro/Um.md", "outro texto"), Ct));
        await Assert.ThrowsAsync<InvalidDataException>(() => storage.TrashTextFileAsync("Bairro/Um.md", TrashOf("Comece.md", "# Um\n"), Ct));
        await Assert.ThrowsAsync<InvalidDataException>(() => storage.TrashTextFileAsync("Bairro/Um.md", "{}", Ct));
        Assert.True(File.Exists(Path.Combine(Vault, "Bairro", "Um.md")));
        Assert.False(File.Exists(Path.Combine(Vault, ".urbe", "trash.v2.json")));

        // a trash file of a future version is preserved: nothing is deleted
        Directory.CreateDirectory(Path.Combine(Vault, ".urbe"));
        File.WriteAllText(Path.Combine(Vault, ".urbe", "trash.v2.json"), "{\"version\":3,\"items\":[]}");
        var reopened = Storage();
        await reopened.PickAsync(Ct);
        await Assert.ThrowsAsync<InvalidOperationException>(() => reopened.TrashTextFileAsync("Bairro/Um.md", TrashOf("Bairro/Um.md", "# Um\n"), Ct));
        Assert.True(File.Exists(Path.Combine(Vault, "Bairro", "Um.md")));
        Assert.Equal("{\"version\":3,\"items\":[]}", File.ReadAllText(Path.Combine(Vault, ".urbe", "trash.v2.json")));
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

    [Fact]
    public async Task CreatingAFolderMakesOnlyThatFolderAndAcceptsOneThatExists()
    {
        var storage = Storage();
        await storage.PickAsync(Ct);
        var before = Directory.GetFiles(Vault, "*", SearchOption.AllDirectories).ToDictionary(f => f, File.ReadAllBytes);
        await storage.CreateDirectoryAsync("Projetos/Sub pasta", Ct);
        await storage.CreateDirectoryAsync("Bairro", Ct);   // already there: fine
        Assert.True(Directory.Exists(Path.Combine(Vault, "Projetos", "Sub pasta")));
        Assert.Equal(before.Keys.Order(), Directory.GetFiles(Vault, "*", SearchOption.AllDirectories).Order());
        foreach (var (file, bytes) in before) Assert.Equal(bytes, File.ReadAllBytes(file));
        // the vault stays writable: the baseline did not change
        await storage.CreateTextFileAsync("Projetos/Nota.md", "# N\n", Ct);
        Assert.Equal("# N\n", File.ReadAllText(Path.Combine(Vault, "Projetos", "Nota.md")));
    }

    [Theory]
    [InlineData(".urbe/x")]
    [InlineData(".obsidian")]
    [InlineData("../fora")]
    [InlineData("/abs")]
    [InlineData("a\\b")]
    [InlineData("c:/x")]
    [InlineData("a//b")]
    [InlineData("")]
    [InlineData("Comece.md")]
    [InlineData("Comece.md/Dentro")]
    public async Task FolderCreationRefusesHiddenUnsafeOrFileNames(string path)
    {
        var storage = Storage();
        await storage.PickAsync(Ct);
        var dirs = Directory.GetDirectories(Vault, "*", SearchOption.AllDirectories).Length;
        await Assert.ThrowsAnyAsync<Exception>(() => storage.CreateDirectoryAsync(path, Ct));
        Assert.Equal(dirs, Directory.GetDirectories(Vault, "*", SearchOption.AllDirectories).Length);
        Assert.False(Directory.Exists(Path.Combine(_dir, "fora")));
    }

    private const string Map1 = "{\n \"v\": 4,\n \"app\": \"Urbe\",\n \"camera\": {\n  \"x\": 1152,\n  \"y\": 800.5,\n  \"z\": 1.15\n },\n \"notas\": {\n  \"Área/Ideias.md\": {\n   \"x\": 3\n  }\n },\n \"salvo\": \"2025-10-09T08:53:20.000Z\"\n}";
    private const string Map2 = "{\n \"v\": 4,\n \"app\": \"Urbe\",\n \"camera\": {\n  \"x\": 1200,\n  \"y\": 800.5,\n  \"z\": 1.15\n },\n \"notas\": {},\n \"salvo\": \"2025-10-09T08:54:20.000Z\"\n}";

    [Fact]
    public async Task SavingTheMapWritesOnlyMapaJsonWithTheExactBytesAndNoRecoveryCopy()
    {
        var storage = Storage();
        await storage.PickAsync(Ct);
        var before = Directory.GetFiles(Vault, "*", SearchOption.AllDirectories).ToDictionary(f => f, File.ReadAllBytes);
        var mapa = Path.Combine(Vault, ".urbe", "mapa.json");

        await storage.SaveMapAsync(Map1, Ct);                       // created (with .urbe/)
        Assert.Equal(Encoding.UTF8.GetBytes(Map1), File.ReadAllBytes(mapa)); // no BOM, no reformatting, "Á" raw
        await storage.SaveMapAsync(Map2, Ct);                       // replaced on the new baseline
        Assert.Equal(Encoding.UTF8.GetBytes(Map2), File.ReadAllBytes(mapa));

        var after = Directory.GetFiles(Vault, "*", SearchOption.AllDirectories);
        Assert.Equal(before.Keys.Append(mapa).OrderBy(f => f, StringComparer.Ordinal), after.OrderBy(f => f, StringComparer.Ordinal));
        foreach (var (file, bytes) in before) Assert.Equal(bytes, File.ReadAllBytes(file));

        // notes keep being saved after the map changed the folder
        await storage.SaveExistingNoteAsync("Bairro/Um.md", "# Um\n\nDepois do mapa.\n", Ct);
        Assert.Equal(Encoding.UTF8.GetBytes(Map2), File.ReadAllBytes(mapa));
    }

    [Fact]
    public async Task AMapThatTheCanonicalWriterWouldReformatIsRefused()
    {
        var storage = Storage();
        await storage.PickAsync(Ct);
        // 2-space indentation is not JSON.stringify(map, null, 1): the plan would write other bytes
        await Assert.ThrowsAsync<InvalidDataException>(() => storage.SaveMapAsync("{\n  \"v\": 4\n}", Ct));
        await Assert.ThrowsAnyAsync<Exception>(() => storage.SaveMapAsync("[1]", Ct));
        Assert.False(Directory.Exists(Path.Combine(Vault, ".urbe")));
    }

    [Fact]
    public async Task AChangeOutsideUrbeBlocksTheMapSave()
    {
        Directory.CreateDirectory(Path.Combine(Vault, ".urbe"));
        File.WriteAllText(Path.Combine(Vault, ".urbe", "mapa.json"), Map1);
        var storage = Storage();
        await storage.PickAsync(Ct);
        File.WriteAllText(Path.Combine(Vault, ".urbe", "mapa.json"), Map1.Replace("1152", "7"));   // another device synced
        await Assert.ThrowsAsync<IOException>(() => storage.SaveMapAsync(Map2, Ct));
        Assert.Equal(Map1.Replace("1152", "7"), File.ReadAllText(Path.Combine(Vault, ".urbe", "mapa.json")));

        // a new note outside Urbe also blocks it
        var other = Storage();
        await other.PickAsync(Ct);
        File.WriteAllText(Path.Combine(Vault, "Fora.md"), "# Fora\n");
        await Assert.ThrowsAsync<IOException>(() => other.SaveMapAsync(Map2, Ct));
        Assert.Equal(Map1.Replace("1152", "7"), File.ReadAllText(Path.Combine(Vault, ".urbe", "mapa.json")));
    }

    [Theory]
    [InlineData("{\n \"v\": 5,\n \"notas\": {}\n}")]          // a map of a future version
    [InlineData("{\n \"v\": \"quatro\"\n}")]                   // an unknown version
    public async Task AReadOnlyMapIsNeverRewritten(string existing)
    {
        Directory.CreateDirectory(Path.Combine(Vault, ".urbe"));
        File.WriteAllText(Path.Combine(Vault, ".urbe", "mapa.json"), existing);
        var storage = Storage();
        var snapshot = await storage.PickAsync(Ct);
        Assert.True(snapshot!.IsMapReadOnly);
        await Assert.ThrowsAsync<InvalidOperationException>(() => storage.SaveMapAsync(Map2, Ct));
        Assert.Equal(existing, File.ReadAllText(Path.Combine(Vault, ".urbe", "mapa.json")));
    }

    [Fact]
    public async Task AVaultOfAFutureFormatRefusesTheMap()
    {
        Directory.CreateDirectory(Path.Combine(Vault, ".urbe"));
        File.WriteAllText(Path.Combine(Vault, ".urbe", "vault.json"), "{\"formatVersion\":99}");
        var storage = Storage();
        var snapshot = await storage.PickAsync(Ct);
        Assert.True(snapshot!.IsReadOnly);
        await Assert.ThrowsAsync<InvalidOperationException>(() => storage.SaveMapAsync(Map2, Ct));
        Assert.False(File.Exists(Path.Combine(Vault, ".urbe", "mapa.json")));
    }
}
