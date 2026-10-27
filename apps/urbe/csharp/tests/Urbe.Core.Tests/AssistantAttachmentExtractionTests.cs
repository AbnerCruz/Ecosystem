using Urbe.Core.Assistant;

namespace Urbe.Core.Tests;

public sealed class AssistantAttachmentExtractionTests
{
    [Fact]
    public async Task HtmlAttachmentIsSavedAsARealVaultTextFile()
    {
        var message = new AiChatMessage { Id = "m", Role = "user", Content = [AssistantTextAttachments.ToChatBlock("pagina.html", "<h1>Olá</h1>")] };
        string? saved = null;
        var path = await AssistantAttachmentExtraction.SaveAsync(message, "pagina.html", _ => false,
            (p, text, data) => { saved = text; Assert.Null(data); return Task.CompletedTask; });
        Assert.Equal("Anexos/pagina.html", path);
        Assert.Equal("<h1>Olá</h1>", saved);
    }
}
