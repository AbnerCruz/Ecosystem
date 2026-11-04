using Avalonia.Threading;
using Urbe.Client;
using Urbe.Core.Assistant;

namespace Urbe.Client.Tests;

public sealed partial class HeadlessUiTests
{
    /// <summary>
    /// A study book attached to the chat (owner, 04/11: "a IA não aceita livros pdf grandes") goes to the vault and the
    /// message carries a reference; the agent's document tools read it page by page and search it. A short PDF still
    /// travels whole.
    /// </summary>
    [Fact]
    public async Task Assistente_ABigBookIsKeptInTheVaultAndReadByParts() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var (view, window, vault, dir) = await OpenCreateVault();
        try
        {
            var fixtures = Path.Combine(AppContext.BaseDirectory, "fixtures");
            var book = File.ReadAllBytes(Path.Combine(fixtures, "book-300.pdf"));
            var shortPdf = File.ReadAllBytes(Path.Combine(fixtures, "book-20.pdf"));
            Assert.True(await AssistantPanel.IsBookAsync(book));
            Assert.False(await AssistantPanel.IsBookAsync(shortPdf));

            var panel = view.Assistant;
            await panel.AttachBookAsync("Cálculo I.pdf", book);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(book, File.ReadAllBytes(Path.Combine(vault, "Livros", "Cálculo I.pdf")));
            var block = Assert.Single(panel.PendingAttachments);
            Assert.Equal("text", block.Type);
            Assert.Equal("Livros/Cálculo I.pdf", block.FileName);
            Assert.Contains("300 páginas", block.Text);
            Assert.Contains("NÃO foi enviado inteiro", block.Text);
            Assert.True(block.Content.Length == 0);   // no bytes in the message

            // the agent's tools reach it by its vault path
            var bytes = await ((IAssistantApp)view).ReadDocumentBytesAsync("Livros/Cálculo I.pdf");
            Assert.NotNull(bytes);
            var page = AssistantDocumentTools.Page(bytes!, 150);
            Assert.Equal(300, page.Pages);
            Assert.Equal(150, page.Page);

            // a second book with the same name never overwrites the first
            await panel.AttachBookAsync("Cálculo I.pdf", book);
            Assert.True(File.Exists(Path.Combine(vault, "Livros", "Cálculo I (2).pdf")));
            return true;
        }
        finally { window.Close(); try { Directory.Delete(dir, true); } catch (IOException) { } }
    }, CancellationToken.None));
}
