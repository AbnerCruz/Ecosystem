using Urbe.Client;

namespace Urbe.Client.Tests;

/// <summary>End-to-end vault storage: chat file extraction must use real disk writes, not transient chat memory.</summary>
public sealed partial class HeadlessUiTests
{
    [Fact]
    public async Task ExtractedHtmlAndPdfAreSavedAndListedInTheSelectedVault() =>
        Assert.True(await Session.Dispatch(async () =>
        {
            var (view, window, vault, dir) = await OpenCreateVault();
            try
            {
                const string html = "<!doctype html><html><script>window.alert('inert')</script><p>Olá</p></html>";
                await view.SaveChatAttachmentAsync("Anexos/pagina.html", html, null);
                Assert.Equal(html, File.ReadAllText(Path.Combine(vault, "Anexos", "pagina.html")));
                byte[] pdf = [37, 80, 68, 70, 45, 49, 46, 55, 10, 1, 2, 3];
                await view.SaveChatAttachmentAsync("Anexos/teste.pdf", null, pdf);
                Assert.Equal(pdf, File.ReadAllBytes(Path.Combine(vault, "Anexos", "teste.pdf")));
                Assert.True(((IAssistantApp)view).FileExists("Anexos/teste.pdf"));
                Assert.True(((IAssistantApp)view).FileExists("Anexos/pagina.html"));

                // Existing files are never destroyed by later saves from the chat.
                await Assert.ThrowsAnyAsync<Exception>(() =>
                    view.SaveChatAttachmentAsync("Anexos/pagina.html", "alterado", null));
                Assert.Equal(html, File.ReadAllText(Path.Combine(vault, "Anexos", "pagina.html")));
                return true;
            }
            finally
            {
                window.Close();
                try { Directory.Delete(dir, true); } catch (IOException) { }
            }
        }, CancellationToken.None));
}
