using Avalonia.Controls;
using Avalonia.LogicalTree;
using Urbe.Client;
using Urbe.Core;
using Urbe.Core.Assistant;

namespace Urbe.Client.Tests;

/// <summary>
/// CST-012: "interromper uma execução deve atualizar a interface e encerrar o trabalho correspondente". A run stopped
/// while the answer is arriving ends at once: what arrived stays, nothing that comes after shows, the button is "Enviar"
/// again and the chat says it was interrupted, with Continuar.
/// </summary>
public sealed partial class HeadlessUiTests
{
    [Fact]
    public async Task Assistant_StoppingARunEndsItAndTheScreenSaysSo() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var before = UrbeApp.AssistantFolder;
        UrbeApp.AssistantFolder = null;
        var view = new MainView();
        var window = new Window { Width = 412, Height = 900, Content = view };
        window.Show();
        try
        {
            await view.LoadAsync(TutorialNotes.Load());
            var panel = view.Assistant; panel.Open(); panel.SelectAgent("geral");
            panel.Config.Providers.Add(new AiProviderConfig("p", "openrouter", ApiKey: "test"));
            panel.Config.ProviderId = "p"; panel.Config.Model = "test-model";
            var model = new GatedModel(); panel.Providers = new AssistantProviders(model, (_, _) => Task.CompletedTask);
            panel.Send("Escreva um texto longo.");
            var run = panel.CurrentRun!;
            await model.WriteAsync("""{"choices":[{"delta":{"content":"Começo do texto."}}]}""");
            await WaitFor(window, view, () => ShownIn(panel).Contains("Começo do texto.") ? panel : null);
            Assert.Contains(panel.GetLogicalDescendants().OfType<Button>(), b => Avalonia.Automation.AutomationProperties.GetName(b) == "Parar");

            panel.StopCompact();
            var ended = await Task.WhenAny(run, Task.Delay(3000));
            Assert.Same(run, ended);   // the run ends now, not when the provider finishes
            await model.WriteAsync("""{"choices":[{"delta":{"content":" Depois da parada."}}]}""");
            await model.EndAsync();
            for (int i = 0; i < 5; i++) await Settle(window, view);

            Assert.Equal("aborted", panel.Conversation.Runs[^1].Stopped);
            var shown = ShownIn(panel);
            Assert.Contains("Começo do texto.", shown);
            Assert.DoesNotContain("Depois da parada.", shown);
            Assert.Contains("Interrompido.", shown);
            Assert.Contains(panel.GetLogicalDescendants().OfType<Button>(), b => Avalonia.Automation.AutomationProperties.GetName(b) == "Enviar");
            Assert.DoesNotContain(panel.GetLogicalDescendants().OfType<Button>(), b => Avalonia.Automation.AutomationProperties.GetName(b) == "Parar");
            return true;
        }
        finally { window.Close(); UrbeApp.AssistantFolder = before; }
    }, CancellationToken.None));
}
