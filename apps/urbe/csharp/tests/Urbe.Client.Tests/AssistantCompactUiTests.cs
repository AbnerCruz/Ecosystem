using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Urbe.Client;
using Urbe.Core;
using Urbe.Core.Assistant;

namespace Urbe.Client.Tests;

public sealed partial class HeadlessUiTests
{
    [Fact]
    public async Task NoteAgent_CompletesInContextWithoutStealingTheEditor()
        => Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var dir = Path.Combine(Path.GetTempPath(), "urbe-compact-" + Guid.NewGuid().ToString("N"));
            var vault = Path.Combine(dir, "vault");
            Directory.CreateDirectory(vault);
            File.WriteAllText(Path.Combine(vault, "Comece.md"), "# Comece\n\nUma nota.\n");
            var storage = new FolderVaultStorage(_ => Task.FromResult<string?>(vault),
                Path.Combine(dir, "app", "sel.json"));
            var before = UrbeApp.AssistantFolder;
            UrbeApp.AssistantFolder = Path.Combine(dir, "app", "assistant");
            var view = new MainView(storage);
            var window = new Window { Width = 412, Height = 900, Content = view };
            window.Show();
            try
            {
                await view.OpenVaultAsync();
                await Settle(window, view);
                var panel = view.Assistant;
                // Load the persisted configuration once; subsequent note invocations leave the full UI closed.
                panel.Open();
                panel.SelectAgent("geral");
                panel.Close();
                var model = new ScriptedModel();
                panel.Providers = new AssistantProviders(model, (_, _) => Task.CompletedTask);
                panel.Config.AgentPolicies["geral"] = "ask";
                panel.Config.Providers.Add(new AiProviderConfig("openrouter", "openrouter", ApiKey: "sk-test"));
                panel.Config.Model = "org/modelo-b";
                model.Chats.Enqueue(Sse(
                    """{"choices":[{"delta":{"content":"A nota descreve uma introdução curta."}}]}""",
                    """{"choices":[{"delta":{},"finish_reason":"stop"}]}"""));
                model.Chats.Enqueue(Sse("""{"choices":[{"delta":{"content":"Resposta verificada."},"finish_reason":"stop"}]}"""));

                var result = panel.AskInNote("Comece.md", "Comece",
                    new NoteMarks.AgentCall(0, 24, "", "Resuma a nota."));
                Assert.Null(result);
                Assert.False(panel.IsVisible);
                Assert.True(panel.CompactActive);
                Assert.True(view.AssistantMini.IsVisible);
                await panel.CurrentRun!;
                Dispatcher.UIThread.RunJobs();
                Assert.False(panel.IsVisible);
                Assert.Equal("result", panel.CompactStatus);
                Assert.True(view.AssistantMini.IsVisible);
                Assert.Contains(view.AssistantMini.GetLogicalDescendants().OfType<TextBlock>(),
                    t => t.Text == "Trabalho concluído");
                Assert.Equal("# Comece\n\nUma nota.\n",
                    File.ReadAllText(Path.Combine(vault, "Comece.md")));

                panel.DismissCompact();
                Assert.False(view.AssistantMini.IsVisible);
                Assert.True(view.Navigation.IsVisible);   // CST-043: on a phone the radial menu has the corner
                return true;
            }
            finally
            {
                UrbeApp.AssistantFolder = before;
                window.Close();
                try { Directory.Delete(dir, true); } catch (IOException) { }
            }
        }, CancellationToken.None));
}
