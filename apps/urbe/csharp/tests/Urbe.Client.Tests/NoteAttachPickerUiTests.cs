using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Urbe.Client;
using Urbe.Core.Assistant;

namespace Urbe.Client.Tests;

public sealed partial class HeadlessUiTests
{
    [Fact]
    public async Task Chat_CreationWaitsUntilTheUserSubmitsTheBrief() => Assert.True(await Session.Dispatch(async () =>
    {
        var folder = Path.Combine(Path.GetTempPath(), "numa-creation-brief-" + Guid.NewGuid().ToString("N"));
        var vault = Path.Combine(folder, "vault");
        Directory.CreateDirectory(vault);
        var before = UrbeApp.AssistantFolder;
        UrbeApp.AssistantFolder = Path.Combine(folder, "assistente");
        var view = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(folder, "sel.json")));
        try
        {
            await view.OpenVaultAsync();
            var panel = view.Assistant;
            panel.Open();
            panel.SelectAgent("geral");
            var model = new ScriptedModel();
            panel.Providers = new AssistantProviders(model, (_, _) => Task.CompletedTask);
            panel.Config.Providers.Add(new AiProviderConfig("openrouter", "openrouter", ApiKey: "teste"));
            panel.Conversation.ProviderId = "openrouter";
            panel.Conversation.Model = "org/m";
            model.Chats.Enqueue(Sse("""{"choices":[{"delta":{"content":"Recebi o briefing."},"finish_reason":"stop"}]}"""));

            var menu = panel.GetLogicalDescendants().OfType<Button>().ToList();
            Assert.Single(menu, button => AutomationProperties.GetName(button) == "Anexar arquivos");
            Assert.DoesNotContain(menu, button => AutomationProperties.GetName(button) == "Nota do vault");
            menu.Single(b => AutomationProperties.GetName(b) == "Gerar flashcards")
                .RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            Assert.Equal("Gerar Flashcards", panel.ViewTitle);
            Assert.Empty(model.Sent);

            var fields = panel.GetLogicalDescendants().OfType<TextBox>().ToList();
            fields.Single(box => box.PlaceholderText == "Assunto, objetivo e público").Text = "Física para o ensino médio";
            fields.Single(box => box.PlaceholderText == "Quantidade, dificuldade e tópicos").Text = "Dois cartões sobre órbitas";
            ClickButtonWithText(panel, "Enviar configurações e gerar");
            await panel.CurrentRun!;
            var prompt = panel.Conversation.Messages.First(message => message.Role == "user").Content[0].Text;
            Assert.Contains("Física para o ensino médio", prompt);
            Assert.Contains("Dois cartões sobre órbitas", prompt);
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
