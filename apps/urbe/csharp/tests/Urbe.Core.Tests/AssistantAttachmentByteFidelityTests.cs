using System.Text;
using Urbe.Core.Assistant;

namespace Urbe.Core.Tests;

/// <summary>UC-63: retain exact incoming text bytes in the canonical conversation/extractor, never a second extraction engine.</summary>
public sealed class AssistantAttachmentByteFidelityTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BomMarkedHtmlKeepsExactBytesThroughConversationAndExtraction(bool bigEndian)
    {
        var encoding = bigEndian ? Encoding.BigEndianUnicode : Encoding.Unicode;
        var original = encoding.GetPreamble().Concat(encoding.GetBytes("<html><p>Olá</p></html>")).ToArray();
        Assert.True(AssistantTextAttachments.TryDecode("pagina.html", original, out var text, out _));

        var block = AssistantTextAttachments.ToChatBlock("pagina.html", text, original);
        var conversation = new AiConversation { Messages = [new AiChatMessage {
            Id = "u1", Role = "user", Content = [AiChatBlock.TextBlock("Extrai o arquivo"), block]
        }] };
        var restored = AssistantStore.ConversationFromJson(AssistantStore.ConversationToJson(conversation));
        var file = Assert.Single(AssistantAttachmentExtraction.List(restored.Messages[0]));
        Assert.Equal("pagina.html", file.Name);
        Assert.Null(file.Text);
        Assert.Equal(original, file.Bytes);

        byte[]? written = null;
        await AssistantAttachmentExtraction.SaveAsync(restored.Messages[0], file.Name, _ => false,
            (path, value, bytes) =>
            {
                Assert.Equal("Anexos/pagina.html", path);
                Assert.Null(value);
                written = bytes;
                return Task.CompletedTask;
            });
        Assert.Equal(original, written);
    }

    [Fact]
    public void LegacyTextConversationReconstructsUtf8WithoutPretendingToHaveOriginalBytes()
    {
        var block = AssistantTextAttachments.ToChatBlock("pagina.html", "<p>Legado</p>");
        var conversation = new AiConversation { Messages = [new AiChatMessage {
            Id = "old", Role = "user", Content = [block]
        }] };
        var restored = AssistantStore.ConversationFromJson(AssistantStore.ConversationToJson(conversation));
        var file = Assert.Single(AssistantAttachmentExtraction.List(restored.Messages[0]));
        Assert.Equal("<p>Legado</p>", file.Text);
        Assert.Null(file.Bytes);
    }

    [Fact]
    public void ModelTextAndFakeMetadataCannotBeExtracted()
    {
        var original = "harmless"u8.ToArray();
        var hidden = AssistantTextAttachments.ToChatBlock("pagina.html", "harmless", original);
        var message = new AiChatMessage { Role = "assistant", Content = [hidden] };
        Assert.Empty(AssistantAttachmentExtraction.List(message));

        // Even a user-facing text string without a legitimate hidden upload is not a file.
        message.Role = "user";
        hidden.Hidden = false;
        Assert.Empty(AssistantAttachmentExtraction.List(message));
    }

    [Fact]
    public void CorruptSavedMetadataDoesNotBecomeAnAttachment()
    {
        var block = AssistantTextAttachments.ToChatBlock("pagina.html", "dados");
        block.FileName = "pagina.html";
        block.Content = "base64-invalido";
        var msg = new AiChatMessage { Role = "user", Content = [block] };
        Assert.Empty(AssistantAttachmentExtraction.List(msg));
    }
}
