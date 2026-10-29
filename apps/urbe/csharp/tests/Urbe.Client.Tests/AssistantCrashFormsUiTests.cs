using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.Interactivity;
using Urbe.Core;
using Urbe.Client.Ui;
using Avalonia.LogicalTree;
using Urbe.Client;
using Urbe.Core.Assistant;
using System.Text.Json.Nodes;
namespace Urbe.Client.Tests;
public sealed partial class HeadlessUiTests
{
    [Theory]
    [InlineData("{\"steps\":[{\"step\":42,\"status\":false},\"invalid\",null]}")]
    [InlineData("[]")]
    [InlineData("\"invalid\"")]
    public async Task Activity_InvalidModelPlanDoesNotCrashExpansionOrRefresh(string payload) => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var before = UrbeApp.AssistantFolder;
        UrbeApp.AssistantFolder = null;
        var view = new MainView();
        var window = new Window { Width = 412, Height = 900, Content = view };
        window.Show();
        try
        {
            await view.LoadAsync(TutorialNotes.Load());
            var panel = view.Assistant;
            panel.Open();
            panel.Config.Providers.Add(new AiProviderConfig("p", "openrouter"));
            panel.Conversation.Messages.Add(new AiChatMessage { Id = "q", Role = "user", Content = [AiChatBlock.TextBlock("Planeje")] });
            panel.Conversation.Messages.Add(new AiChatMessage { Id = "a", Role = "assistant", Content = [new AiChatBlock { Type = "tool_use", Id = "bad-plan", Name = "update_plan", Input = JsonNode.Parse(payload) }] });
            panel.Conversation.Messages.Add(new AiChatMessage { Role = "user", Meta = new() { ToolResults = true }, Content = [new AiChatBlock { Type = "tool_result", ToolUseId = "bad-plan", Content = "Plano recebido" }] });
            panel.RenderAll();
            var fold = panel.Log.GetLogicalDescendants().OfType<Expander>().First();
            fold.IsExpanded = true;
            await Settle(window, view);
            var stage = ((Panel)fold.Content!).Children.Select(c => c is Ui.Deferred d ? d.View : c).OfType<Border>().First().Child as Expander;
            stage!.IsExpanded = true;
            await Settle(window, view);
            panel.RenderAll();
            await Settle(window, view);
            return true;
        }
        finally { window.Close(); UrbeApp.AssistantFolder = before; }
    }, CancellationToken.None));

    [Fact]
    public async Task SecondMessageIsQueuedAndActuallySentInOrder() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var before = UrbeApp.AssistantFolder;
        UrbeApp.AssistantFolder = null;
        var view = new MainView();
        var window = new Window { Width = 412, Height = 900, Content = view };
        window.Show();
        try
        {
            await view.LoadAsync(TutorialNotes.Load());
            var panel = view.Assistant;
            panel.Open(); panel.SelectAgent("geral");
            panel.Config.Providers.Add(new AiProviderConfig("p", "openrouter", ApiKey: "test"));
            panel.Config.ProviderId = "p"; panel.Config.Model = "test-model";
            var model = new ParallelModel();
            panel.Providers = new AssistantProviders(model, (_, _) => Task.CompletedTask);
            panel.Send("Primeira mensagem");
            var run = panel.CurrentRun!;
            panel.Input.Text = "Segunda mensagem";
            var send = panel.GetLogicalDescendants().OfType<Button>().Single(b => Avalonia.Automation.AutomationProperties.GetName(b) == "Enviar após a resposta atual");
            send.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Equal("Segunda mensagem", Assert.Single(panel.Conversation.QueuedMessages).Content[0].Text);
            Assert.False(run.IsCompleted);
            model.Release.TrySetResult(true);
            await run.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(2, model.Started);
            Assert.Empty(panel.Conversation.QueuedMessages);
            Assert.Equal(["Primeira mensagem", "Segunda mensagem"], panel.Conversation.Messages.Where(m => m.Role == "user" && m.Meta?.ToolResults != true).Select(m => m.Content[0].Text));
            Assert.Equal(2, panel.Conversation.Runs.Count);
            return true;
        }
        finally { window.Close(); UrbeApp.AssistantFolder = before; }
    }, CancellationToken.None));

    [Fact]
    public async Task FormToolPersistsDraftAndContinuesAfterExplicitSubmit() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var folder = Path.Combine(Path.GetTempPath(), "urbe-forms-" + Guid.NewGuid().ToString("N"));
        var before = UrbeApp.AssistantFolder; UrbeApp.AssistantFolder = folder;
        var view = new MainView();
        var window = new Window { Width = 412, Height = 900, Content = view }; window.Show();
        try
        {
            await view.LoadAsync(TutorialNotes.Load());
            var panel = view.Assistant; panel.Open(); panel.SelectAgent("geral");
            panel.Config.Providers.Add(new AiProviderConfig("p", "openrouter", ApiKey: "test"));
            panel.Config.ProviderId = "p"; panel.Config.Model = "test-model";
            var model = new ScriptedModel(); panel.Providers = new AssistantProviders(model, (_, _) => Task.CompletedTask);
            model.Chats.Enqueue(Sse("""{"choices":[{"delta":{"tool_calls":[{"index":0,"id":"form-one","function":{"name":"request_form","arguments":"{\"title\":\"Ficha\",\"fields\":[{\"id\":\"nome\",\"label\":\"Nome\",\"type\":\"text\",\"required\":true}]}"}}]}}]}""",
                """{"choices":[{"delta":{},"finish_reason":"tool_calls"}]}"""));
            panel.Send("Quero preencher uma ficha"); await panel.CurrentRun!;
            Assert.Equal("needs_input", panel.Conversation.Runs.Last().Stopped);
            var renderer = panel.Log.GetLogicalDescendants().OfType<FormRenderer>().Single();
            var field = renderer.GetLogicalDescendants().OfType<TextBox>().Single();
            field.Text = "Arven";
            await Settle(window, view);
            Snapshot(window, "native-assistant-form-phone.png");
            var id = panel.Conversation.Id;
            Assert.Equal("Arven", (string?)new AssistantStore(new FolderAssistantStorage(folder)).GetConversation(id)!.Forms.Single().Values["nome"]);
            panel.RenderAll(); Assert.Same(renderer, panel.Log.GetLogicalDescendants().OfType<FormRenderer>().Single());
            model.Chats.Enqueue(Sse("""{"choices":[{"delta":{"content":"Recebi Arven."}}]}""", """{"choices":[{"delta":{},"finish_reason":"stop"}]}"""));
            var source = ((IAssistantApp)view).Documents!.List().First();
            renderer.Session.Definition.SourcePath = source.Path;
            renderer.Session.SourceHash = FormSession.Hash("versão anterior");
            renderer.SubmitButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Equal("pending", renderer.Session.State);
            Assert.Contains("mudou", renderer.Status.Text);
            renderer.Session.SourceHash = FormSession.Hash(source.Content);
            renderer.SubmitButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); await panel.CurrentRun!;
            Assert.Equal("submitted", panel.Conversation.Forms.Single().State);
            Assert.Contains(panel.Conversation.Messages, m => m.Role == "user" && m.Content.Any(b => b.Text?.Contains("Arven") == true));
            Assert.DoesNotContain(panel.Log.GetLogicalDescendants(), c => c is FormRenderer);
            return true;
        }
        finally { window.Close(); UrbeApp.AssistantFolder = before; try { Directory.Delete(folder, true); } catch (IOException) { } }
    }, CancellationToken.None));
}
