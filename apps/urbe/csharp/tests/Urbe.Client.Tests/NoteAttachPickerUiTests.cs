using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Urbe.Client;
using Urbe.Core.Assistant;

namespace Urbe.Client.Tests;

public sealed partial class HeadlessUiTests
{
    [Theory]
    [InlineData("flashcards", "Flashcards", "Física para o ensino médio", false, true, null, "web_search")]
    [InlineData("podcast", "Podcast", null, true, true, "Curto", "inferir o tema")]
    [InlineData("vídeo", "Vídeo", "Órbitas", true, true, "Médio", "como base para o tema")]
    [InlineData("podcast", "Podcast", "Ondas", false, false, "Longo", "search_notes")]
    [InlineData("vídeo", "Vídeo", "Marés", false, true, "Flexível", "web_search")]
    public async Task Chat_CreationWaitsUntilTheUserSubmitsTheBrief(string menuEntry, string kind, string? topic,
        bool attached, bool web, string? length, string expectedInstruction) => Assert.True(await Session.Dispatch(async () =>
    {
        var folder = Path.Combine(Path.GetTempPath(), "numa-creation-brief-" + Guid.NewGuid().ToString("N"));
        var vault = Path.Combine(folder, "vault");
        Directory.CreateDirectory(vault);
        var before = UrbeApp.AssistantFolder;
        UrbeApp.AssistantFolder = Path.Combine(folder, "assistente");
        if (attached)
            new AssistantStore(new FolderAssistantStorage(UrbeApp.AssistantFolder)).SaveDrafts(
                new Dictionary<string, (string, List<AiChatBlock>)>
                {
                    ["geral"] = ("", [new AiChatBlock { Hidden = true, FileName = "fonte.md", Text = "Fonte sobre órbitas." }])
                });
        var view = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(folder, "sel.json")));
        try
        {
            await view.OpenVaultAsync();
            var panel = view.Assistant;
            panel.Open();
            panel.SelectAgent("geral");
            panel.Config.Web.Enabled = web;
            var model = new ScriptedModel();
            panel.Providers = new AssistantProviders(model, (_, _) => Task.CompletedTask);
            panel.Config.Providers.Add(new AiProviderConfig("openrouter", "openrouter", ApiKey: "teste"));
            panel.Conversation.ProviderId = "openrouter";
            panel.Conversation.Model = "org/m";
            model.Chats.Enqueue(Sse("""{"choices":[{"delta":{"content":"Recebi o briefing."},"finish_reason":"stop"}]}"""));

            var menu = panel.GetLogicalDescendants().OfType<Button>().ToList();
            Assert.Single(menu, button => AutomationProperties.GetName(button) == "Anexar arquivos");
            Assert.DoesNotContain(menu, button => AutomationProperties.GetName(button) == "Nota do vault");
            menu.Single(b => AutomationProperties.GetName(b) == "Gerar " + menuEntry)
                .RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            Assert.Equal("Gerar " + kind, panel.ViewTitle);
            Assert.Empty(model.Sent);

            var fields = panel.GetLogicalDescendants().OfType<TextBox>().ToList();
            if (!attached)
            {
                ClickButtonWithText(panel, "Enviar configurações e gerar");
                Assert.Empty(model.Sent);
                Assert.Equal("Gerar " + kind, panel.ViewTitle);
            }
            fields.Single(box => box.PlaceholderText == "Tema, objetivo ou público").Text = topic;
            var durations = panel.GetLogicalDescendants().OfType<ComboBox>()
                .Where(c => AutomationProperties.GetName(c) == "Duração da produção").ToList();
            if (length is null)
                Assert.Empty(durations);
            else
            {
                var duration = Assert.Single(durations);
                Assert.Equal(["Curto", "Médio", "Longo", "Flexível"], duration.Items.Cast<string>());
                Assert.Equal("Flexível", duration.SelectedItem);
                duration.SelectedItem = length;
            }
            ClickButtonWithText(panel, "Enviar configurações e gerar");
            await panel.CurrentRun!;
            var request = panel.Conversation.Messages.First(message => message.Role == "user");
            Assert.Contains(expectedInstruction, request.Content[0].Text);
            if (topic is not null) Assert.Contains(topic, request.Content[0].Text);
            if (length is not null) Assert.Contains("Duração: " + length, request.Content[0].Text);
            Assert.Equal(attached, request.Content.Any(block => block.Text == "Fonte sobre órbitas."));
            Assert.NotEmpty(model.Sent);
            return true;
        }
        finally
        {
            UrbeApp.AssistantFolder = before;
            try { Directory.Delete(folder, true); } catch (IOException) { }
        }
    }, CancellationToken.None));
}
