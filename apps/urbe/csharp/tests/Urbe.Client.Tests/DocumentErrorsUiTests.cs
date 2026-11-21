using Avalonia.Controls;
using Urbe.Client;
using Urbe.Core.Assistant;

namespace Urbe.Client.Tests;

public sealed partial class HeadlessUiTests
{
    /// <summary>#963 case 01: a document the agent cannot read says why, with the file it probably meant.</summary>
    [Fact]
    public async Task Documents_AMissingPathSaysWhyAndSuggestsTheFile() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var dir = Path.Combine(Path.GetTempPath(), "urbe-docerr-" + Guid.NewGuid().ToString("N"));
        var vault = Path.Combine(dir, "vault");
        Directory.CreateDirectory(Path.Combine(vault, "Livros"));
        File.WriteAllBytes(Path.Combine(vault, "Livros", "Livro (1).pdf"), "%PDF-1.4\n"u8.ToArray());
        var view = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "app", "sel.json")));
        var window = new Window { Width = 412, Height = 860, Content = view };
        window.Show();
        try
        {
            await view.OpenVaultAsync();
            await Settle(window, view);
            IAssistantApp app = view;
            var missing = await Assert.ThrowsAsync<AiToolException>(() => app.ReadDocumentBytesAsync("Livro.pdf", CancellationToken.None));
            Assert.Contains("Não há arquivo em \"Livro.pdf\" no vault", missing.Message);
            Assert.Contains("Livros/Livro (1).pdf", missing.Message);
            Assert.Contains("extract_attachment", missing.Message);
            Assert.Contains("list_notes não serve", missing.Message);
            Assert.NotNull(await app.ReadDocumentBytesAsync("Livros/Livro (1).pdf", CancellationToken.None));
            return true;
        }
        finally { window.Close(); try { Directory.Delete(dir, true); } catch (IOException) { } }
    }, CancellationToken.None));
}
