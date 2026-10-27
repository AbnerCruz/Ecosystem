using System.Text;
using Urbe.Client;

namespace Urbe.Client.Tests;

/// <summary>A real vault and the same byte writer used by Android SAF: no replacement, fidelity, and subsequent writes.</summary>
public sealed class ChatAttachmentStorageTests
{
    [Fact]
    public async Task ImportedUtf16HtmlIsByteExactAndFollowingNoteStillWrites()
    {
        var temp = Path.Combine(Path.GetTempPath(), "urbe-chat-file-" + Guid.NewGuid().ToString("N"));
        var vault = Path.Combine(temp, "vault");
        Directory.CreateDirectory(vault);
        File.WriteAllText(Path.Combine(vault, "Existente.md"), "# Original");
        try
        {
            var storage = new FolderVaultStorage(_ => Task.FromResult<string?>(vault),
                Path.Combine(temp, "app", "choice.json"));
            Assert.NotNull(await storage.PickAsync(TestContext.Current.CancellationToken));
            var original = Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes(
                "<html><body>Olá</body></html>")).ToArray();
            await storage.CreateImportedFileAsync("Anexos/página.html", original, TestContext.Current.CancellationToken);
            Assert.Equal(original, File.ReadAllBytes(Path.Combine(vault, "Anexos", "página.html")));
            var reopened = await storage.ReloadAsync(TestContext.Current.CancellationToken);
            Assert.Equal("<html><body>Olá</body></html>",
                reopened!.Documents.Single(d => d.Path == "Anexos/página.html").Text);
            await storage.CreateTextFileAsync("Nova.md", "# Nova", TestContext.Current.CancellationToken);
            Assert.Equal("# Nova", File.ReadAllText(Path.Combine(vault, "Nova.md")));
            Assert.Equal("# Original", File.ReadAllText(Path.Combine(vault, "Existente.md")));
            await Assert.ThrowsAnyAsync<IOException>(() =>
                storage.CreateImportedFileAsync("Anexos/página.html", "replacement"u8.ToArray(), TestContext.Current.CancellationToken));
            Assert.Equal(original, File.ReadAllBytes(Path.Combine(vault, "Anexos", "página.html")));
            await Assert.ThrowsAnyAsync<Exception>(() =>
                storage.CreateImportedFileAsync(".urbe/config.html", "oops"u8.ToArray(), TestContext.Current.CancellationToken));
            Assert.False(File.Exists(Path.Combine(vault, ".urbe", "config.html")));
        }
        finally { try { Directory.Delete(temp, true); } catch (IOException) { } }
    }
}
