using Urbe.Client;
using Urbe.Core;

namespace Urbe.Client.Tests;

/// <summary>The folder host as ImportTransaction sees it: a missing file is absent (not empty), writes are verified.</summary>
public sealed class ImportStorageTests
{
    [Fact]
    public async Task FolderImportStorage_ReadsListsWritesAndRemoves()
    {
        var dir = Path.Combine(Path.GetTempPath(), "urbe-importer-" + Guid.NewGuid().ToString("N"));
        var vault = Path.Combine(dir, "vault");
        Directory.CreateDirectory(vault);
        File.WriteAllText(Path.Combine(vault, "Nota.md"), "# Nota\n");
        try
        {
            var storage = new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "app", "sel.json"));
            Assert.NotNull(await storage.PickAsync(TestContext.Current.CancellationToken));
            var importer = storage.ImportStorage!;
            Assert.Null(await importer.ReadAsync(ImportTransaction.JournalPath, TestContext.Current.CancellationToken));
            await using (await importer.AcquireExclusiveAsync(TestContext.Current.CancellationToken))
            {
                await importer.WriteAsync(".urbe/backup/x/files/Nota.md", "# Nota\n"u8.ToArray(), TestContext.Current.CancellationToken);
                await importer.WriteAsync("Nota.md", "# Outra\n"u8.ToArray(), TestContext.Current.CancellationToken);
            }
            Assert.Contains(".urbe/backup/x/files/Nota.md", await importer.ListAsync(TestContext.Current.CancellationToken));
            Assert.Equal("# Outra\n", File.ReadAllText(Path.Combine(vault, "Nota.md")));
            await using (await importer.AcquireExclusiveAsync(TestContext.Current.CancellationToken))
                await importer.RemoveAsync(".urbe/backup/x/files/Nota.md", TestContext.Current.CancellationToken);
            Assert.Null(await importer.ReadAsync(".urbe/backup/x/files/Nota.md", TestContext.Current.CancellationToken));
            // the host saw the import: a note save after it is not refused as an outside change
            await storage.SaveExistingTextFileAsync("Nota.md", "# Terceira\n", TestContext.Current.CancellationToken);
            Assert.Equal("# Terceira\n", File.ReadAllText(Path.Combine(vault, "Nota.md")));
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
    }
}
