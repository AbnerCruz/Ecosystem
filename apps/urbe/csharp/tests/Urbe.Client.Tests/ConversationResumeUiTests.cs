using Avalonia.Controls;
using Urbe.Client;
using Urbe.Core.Assistant;

namespace Urbe.Client.Tests;

public sealed partial class HeadlessUiTests
{
    /// <summary>
    /// CST-145: coming back to an agent after a restart continues the conversation still in use (minutes ago), and
    /// opens a new chat when the last one stopped long ago (it stays in Conversas).
    /// </summary>
    [Theory]
    [InlineData(5, true)]
    [InlineData(3 * 24 * 60, false)]
    public async Task Assistant_AConversationInUseIsResumedAfterARestart(int minutesAgo, bool resumed) =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var folder = Path.Combine(Path.GetTempPath(), "urbe-resume-" + Guid.NewGuid().ToString("N"));
            var before = UrbeApp.AssistantFolder;
            UrbeApp.AssistantFolder = folder;
            var view = new MainView();
            var window = new Window { Width = 412, Height = 900, Content = view };
            window.Show();
            try
            {
                await view.LoadAsync(TutorialNotes.Load());
                var vault = ((IAssistantApp)view).VaultName;
                var store = new AssistantStore(new FolderAssistantStorage(folder));
                var updated = DateTimeOffset.UtcNow.AddMinutes(-minutesAgo).ToUnixTimeMilliseconds();
                store.SaveConversation(new AiConversation
                {
                    Id = "em-uso", AgentId = "geral", Vault = vault, Title = "Plano da viagem", Updated = updated,
                    Messages = [new() { Role = "user", Content = [AiChatBlock.TextBlock("Vamos planejar a viagem")] }]
                }, updated);
                view.Assistant.Open();   // the app (re)starts: the panel loads now
                view.Assistant.SelectAgent("geral");
                if (resumed)
                {
                    Assert.Equal("em-uso", view.Assistant.Conversation.Id);
                    Assert.Equal("Vamos planejar a viagem", view.Assistant.Conversation.Messages.Single().Content[0].Text);
                }
                else
                {
                    Assert.NotEqual("em-uso", view.Assistant.Conversation.Id);
                    Assert.Empty(view.Assistant.Conversation.Messages);
                    Assert.NotNull(store.GetConversation("em-uso"));   // still in Conversas
                }
                return true;
            }
            finally { window.Close(); UrbeApp.AssistantFolder = before; try { Directory.Delete(folder, true); } catch (IOException) { } }
        }, CancellationToken.None));
}
