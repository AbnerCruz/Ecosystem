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

    [Fact]
    public async Task BinaryPdfIsCopiedByteForByte()
    {
        byte[] original = [37, 80, 68, 70, 45, 49, 46, 55, 10, 1, 2, 3];
        var message = new AiChatMessage { Id = "m2", Role = "user", Content = [
            new AiChatBlock { Type = "attachment", FileName = "amostra.pdf",
                MimeType = "application/pdf", Content = Convert.ToBase64String(original) }] };
        byte[]? saved = null;
        await AssistantAttachmentExtraction.SaveAsync(message, "amostra.pdf", _ => false,
            (p, text, bytes) => { Assert.Null(text); saved = bytes; return Task.CompletedTask; });
        Assert.Equal(original, saved);
    }

    [Fact]
    public async Task ExistingFileIsNotOverwrittenAndFailuresAreNotReportedAsSaved()
    {
        var m = new AiChatMessage { Id = "m3", Role = "user",
            Content = [AssistantTextAttachments.ToChatBlock("pagina.html", "texto")] };
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Anexos/pagina.html" };
        var path = await AssistantAttachmentExtraction.SaveAsync(m, "pagina.html", existing.Contains,
            (p, text, _) => { Assert.DoesNotContain(p, existing); return Task.CompletedTask; });
        Assert.Equal("Anexos/pagina (2).html", path);
        await Assert.ThrowsAsync<IOException>(() => AssistantAttachmentExtraction.SaveAsync(m,
            "pagina.html", _ => false, (_, _, _) => throw new IOException("offline")));
    }

    [Fact]
    public void CorruptBytesAndInventedAttachmentsCannotBeExtracted()
    {
        var fake = new AiChatMessage { Role = "user", Content = [
            new AiChatBlock { Type = "attachment", FileName = "amostra.pdf",
                MimeType = "application/pdf", Content = Convert.ToBase64String("not-a-pdf"u8.ToArray()) }] };
        Assert.Empty(AssistantAttachmentExtraction.List(fake));
        var message = new AiChatMessage { Role = "assistant", Content = [
            AssistantTextAttachments.ToChatBlock("pagina.html", "um texto")] };
        Assert.Empty(AssistantAttachmentExtraction.List(message));
    }

    [Fact]
    public void UnsupportedExtensionsAreNotPresentedAsExtractable()
    {
        var m = new AiChatMessage { Role = "user", Content = [
            AssistantTextAttachments.ToChatBlock("instalador.exe", "MZ")] };
        Assert.Empty(AssistantAttachmentExtraction.List(m));
    }

}
