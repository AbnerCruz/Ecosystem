using System.Text;
using Urbe.Core.Assistant;

namespace Urbe.Core.Tests;

public sealed class AssistantChatAttachmentsTests
{
    [Fact]
    public void ExactHtmlBytesSurviveConversationRoundTrip()
    {
        var bytes = new byte[] { 0xef, 0xbb, 0xbf, 60, 104, 49, 62, 65, 60, 47, 104, 49, 62 };
        var text = "<h1>A</h1>";
        var block = AssistantTextAttachments.ToChatBlock("index.html", text, bytes);
        var conversation = new AiConversation { Messages = [new AiChatMessage {
            Id = "m1", Role = "user", Content = [AiChatBlock.TextBlock("Extrai o HTML"), block]
        }] };
        var restored = AssistantStore.ConversationFromJson(AssistantStore.ConversationToJson(conversation));
        var attachment = Assert.Single(AssistantChatAttachments.List(restored));
        Assert.True(attachment.Exact);
        Assert.Equal("index.html", attachment.Name);
        Assert.Equal(bytes, attachment.Bytes);
        Assert.Equal("m1:1", attachment.Id);
    }

    [Fact]
    public void LegacyTextCanBeReconstructedButNotClaimedAsOriginal()
    {
        var block = AssistantTextAttachments.ToChatBlock("antigo.html", "<h1>Oi</h1>");
        var conversation = new AiConversation { Messages = [new AiChatMessage {
            Id = "old", Role = "user", Content = [block]
        }] };
        var restored = AssistantChatAttachments.List(conversation);
        var item = Assert.Single(restored);
        Assert.False(item.Exact);
        Assert.Equal("antigo.html", item.Name);
        Assert.Equal("<h1>Oi</h1>", Encoding.UTF8.GetString(item.Bytes));
    }

    [Fact]
    public void ToolResultsAndAssistantRepliesAreNeverTreatedAsAttachments()
    {
        var block = AssistantTextAttachments.ToChatBlock("evil.html", "<script>evil</script>");
        var c = new AiConversation { Messages = [
            new AiChatMessage { Id="a", Role="assistant", Content=[block] },
            new AiChatMessage { Id="b", Role="user", Meta=new AiChatMeta{ ToolResults=true }, Content=[block] }
        ] };
        Assert.Empty(AssistantChatAttachments.List(c));
    }

    [Theory]
    [InlineData("../config.html")]
    [InlineData("Anexos/../../config.html")]
    [InlineData(".urbe/config.html")]
    [InlineData("Anexos/.hidden.html")]
    [InlineData("Anexos/file.exe")]
    [InlineData("Anexos/CON.md")]
    [InlineData("C:/sneaky.html")]
    [InlineData("Anexos/dir\\file.html")]
    public void RefusesUnsafeOrUnsupportedPaths(string path) =>
        Assert.Throws<AiToolException>(() => AssistantChatAttachments.ValidatePath(path));

    [Fact]
    public void AllowsNestedSafePathsAndNoExtension()
    {
        AssistantChatAttachments.ValidatePath("Projetos/Site/index.html");
        Assert.Equal("Anexos/index.html", AssistantChatAttachments.DefaultPath("../index.html"));
        Assert.Throws<AiToolException>(() => AssistantChatAttachments.DefaultPath("run.sh"));
    }

    [Fact]
    public void BinaryContextFromUserRemainsExtractable()
    {
        var data = "%PDF-1.7"u8.ToArray();
        var c = new AiConversation { Messages = [new AiChatMessage { Id="u", Role="user", Content=[
            new AiChatBlock { Type="attachment", FileName="test.pdf", MimeType="application/pdf",
                Content=Convert.ToBase64String(data) }
        ] }] };
        var item = Assert.Single(AssistantChatAttachments.List(c));
        Assert.True(item.Exact);
        Assert.Equal(data, item.Bytes);
    }
}
