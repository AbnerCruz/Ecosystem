using Urbe.Core.Assistant;

namespace Urbe.Core.Tests;

public sealed class ConversationResumeTests
{
    private const long Now = 1_800_000_000_000;
    private static AiConversation Chat(long updatedAgo, int messages = 1) => new()
    {
        Id = "c", Updated = Now - updatedAgo,
        Messages = [.. Enumerable.Range(0, messages).Select(_ => new AiChatMessage { Role = "user", Content = [AiChatBlock.TextBlock("oi")] })]
    };

    [Fact]
    public void RecentOrUnfinishedIsInUseOldAndEmptyIsNot()
    {
        Assert.True(ConversationResume.InUse(Chat(5 * 60_000), Now));                         // five minutes ago
        Assert.False(ConversationResume.InUse(Chat(3L * 24 * 3_600_000), Now));               // three days ago
        Assert.False(ConversationResume.InUse(Chat(60_000, messages: 0), Now));               // nothing said in it
        var queued = Chat(3L * 24 * 3_600_000); queued.QueuedMessages.Add(new AiChatMessage { Role = "user" });
        Assert.True(ConversationResume.InUse(queued, Now));                                    // a message waits its turn
        var form = Chat(3L * 24 * 3_600_000); form.Forms.Add(new FormSession { State = "pending" });
        Assert.True(ConversationResume.InUse(form, Now));                                      // a form not sent yet
    }
}
