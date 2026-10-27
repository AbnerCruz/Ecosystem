using System.Text;
using Urbe.Client;

namespace Urbe.Client.Tests;

/// <summary>UC-63: file bytes and the vault baseline survive extraction without overwriting user files.</summary>
public sealed class ChatAttachmentByteStorageTests
{
    [Fact]
    public async Task RawUtf16HtmlIsPreservedAndSubsequentVaultWritesStillWork()
    {
        var temp = Path.Combine(Path.GetTempPath(), "urbe-chat-raw-" + Guid.NewGuid().ToString("N"));
        var vault = Path.Combine(temp, "vault");
        Directory.CreateDirectory(vault);
        File.WriteAllText(Path.Combine(vault, "Existing.md"), "# Keep");
        try
        {
            var storage = new FolderVaultStorage(_ => Task.FromResult<string?>(vault),
                Path.Combine(temp, "settings", "folder.json"));
            Assert.NotNull(await storage.PickAsync(TestContext.Current.CancellationToken));
            var content = "<html><body>Olá</body></html>";
            var original = Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes(content)).ToArray();
            await storage.CreateImportedFileAsync("Anexos/pagina.html", original, TestContext.Current.CancellationToken);
            Assert.Equal(original, File.ReadAllBytes(Path.Combine(vault, "Anexos", "pagina.html")));
            var reopened = await storage.ReloadAsync(TestContext.Current.CancellationToken);
            Assert.Equal(content, reopened!.Documents.Single(x => x.Path == "Anexos/pagina.html").Text);
            await storage.CreateTextFileAsync("Second.md", "# Second", TestContext.Current.CancellationToken);
            Assert.Equal("# Keep", File.ReadAllText(Path.Combine(vault, "Existing.md")));
            Assert.Equal("# Second", File.ReadAllText(Path.Combine(vault, "Second.md")));
            await Assert.ThrowsAnyAsync<IOException>(() =>
                storage.CreateImportedFileAsync("Anexos/pagina.html", "replacement"u8.ToArray(), TestContext.Current.CancellationToken));
            Assert.Equal(original, File.ReadAllBytes(Path.Combine(vault, "Anexos", "pagina.html")));
            await Assert.ThrowsAnyAsync<Exception>(() =>
                storage.CreateImportedFileAsync(".urbe/secret.html", "no"u8.ToArray(), TestContext.Current.CancellationToken));
        }
        finally { try { Directory.Delete(temp, true); } catch (IOException) { } }
    }
}
