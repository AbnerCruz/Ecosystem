using Avalonia.Controls;
using Avalonia.LogicalTree;
using Urbe.Client;
using Urbe.Core;
using Urbe.Core.Assistant;

namespace Urbe.Client.Tests;

public sealed partial class HeadlessUiTests
{
    [Fact]
    public async Task Assistant_ARunShowsWhereItsTimeWentAndAgentsAreSummarized() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        // CST-094/096: under the answer, the run's measures; in Custos e consumo, each agent's runs together
        var folder = Path.Combine(Path.GetTempPath(), "urbe-metrics-" + Guid.NewGuid().ToString("N"));
        var before = UrbeApp.AssistantFolder;
        UrbeApp.AssistantFolder = folder;
        var view = new MainView();
        var window = new Window { Width = 412, Height = 900, Content = view };
        window.Show();
        try
        {
            await view.LoadAsync(TutorialNotes.Load());
            var panel = view.Assistant;
            panel.Open();
            panel.SelectAgent("geral");
            panel.Config.Providers.Add(new AiProviderConfig("p", "openrouter", ApiKey: "test"));
            panel.Config.ProviderId = "p";
            panel.Config.Model = "test-model";
            var model = new ScriptedModel();
            panel.Providers = new AssistantProviders(model, (_, _) => Task.CompletedTask);
            model.Chats.Enqueue(Sse("""{"choices":[{"delta":{"content":"Resposta."}}]}""", """{"choices":[{"delta":{},"finish_reason":"stop"}]}"""));
            panel.Send("Oi");
            await panel.CurrentRun!;
            await Settle(window, view);
            string[] Texts() => panel.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text ?? "").ToArray();
            Assert.Contains(Texts(), t => t.StartsWith("Levou ", StringComparison.Ordinal));
            Assert.True(panel.Conversation.Runs[^1].Efficiency.Completed);

            ClickButtonWithText(panel, panel.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text ?? "").First(t => t.StartsWith("Custos") || t.Contains("acumulados")));
            await Settle(window, view);
            Assert.Contains(Texts(), t => t == "Desempenho por agente" || t == "DESEMPENHO POR AGENTE");
            Assert.Contains(Texts(), t => t.Contains("1 execução · mediana"));

            // turned off: the next runs keep no times
            panel.Config.MeasureRuns = false;
            model.Chats.Enqueue(Sse("""{"choices":[{"delta":{"content":"De novo."}}]}""", """{"choices":[{"delta":{},"finish_reason":"stop"}]}"""));
            panel.Send("Mais");
            await panel.CurrentRun!;
            Assert.Equal(0, panel.Conversation.Runs[^1].Efficiency.TotalMs);
            return true;
        }
        finally { window.Close(); UrbeApp.AssistantFolder = before; try { Directory.Delete(folder, true); } catch (IOException) { } }
    }, CancellationToken.None));
}
