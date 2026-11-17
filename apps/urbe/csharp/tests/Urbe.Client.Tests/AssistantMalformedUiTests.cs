using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Urbe.Client;
using Urbe.Core.Assistant;

namespace Urbe.Client.Tests;

public sealed partial class HeadlessUiTests
{
    /// <summary>
    /// CST-136: broken arguments, an unknown tool, a huge result and a huge answer never take the chat down: each
    /// failure is named to the agent, the note is untouched, the long answer is all there and the panel still works.
    /// </summary>
    [Fact]
    public async Task Assistant_MalformedAndHugeStepsKeepTheChatWorking() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var dir = Path.Combine(Path.GetTempPath(), "urbe-malformed-" + Guid.NewGuid().ToString("N"));
            var vault = Path.Combine(dir, "vault");
            Directory.CreateDirectory(vault);
            File.WriteAllText(Path.Combine(vault, "Um.md"), "# Um\n\n- velho\n");
            File.WriteAllText(Path.Combine(vault, "Grande.md"), "# Grande\n\n" + string.Concat(Enumerable.Range(0, 6000).Select(i => "Linha " + i + " com texto acentuado: ação.\n")));
            var before = UrbeApp.AssistantFolder;
            UrbeApp.AssistantFolder = Path.Combine(dir, "app", "assistant");
            var view = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "app", "sel.json")));
            var window = new Window { Width = 412, Height = 860, Content = view };
            window.Show();
            string Last(AssistantPanel panel) => string.Concat(panel.Conversation.Messages.Last().Content.Where(b => b.Type == "text").Select(b => b.Text)).Trim();
            async Task Run(AssistantPanel panel, string text)
            {
                panel.Send(text);
                for (int i = 0; i < 200 && panel.CurrentRun is null; i++) { await Task.Delay(5); Dispatcher.UIThread.RunJobs(); }
                Assert.NotNull(panel.CurrentRun);
                await panel.CurrentRun!;
                for (int i = 0; i < 30; i++) { await Task.Delay(5); Dispatcher.UIThread.RunJobs(); }
            }
            try
            {
                await view.OpenVaultAsync();
                await Settle(window, view);
                var model = new ScriptedModel();
                var panel = view.Assistant;
                panel.Open();
                panel.SelectAgent("geral");
                panel.Config.AgentPolicies["geral"] = "auto";
                panel.Providers = new AssistantProviders(model, (_, _) => Task.CompletedTask);
                panel.Config.Providers.Add(new AiProviderConfig("openrouter", "openrouter", ApiKey: "sk"));
                panel.Config.ProviderId = "openrouter"; panel.Config.Model = "org/m";
                panel.Conversation.ProviderId = "openrouter"; panel.Conversation.Model = "org/m";
                model.Chats.Enqueue(Sse(
                    """{"choices":[{"delta":{"tool_calls":[{"index":0,"id":"a","function":{"name":"edit_note","arguments":"{\"path\":\"Um\",\"old_te"}},{"index":1,"id":"b","function":{"name":"nao_existe","arguments":"{}"}},{"index":2,"id":"c","function":{"name":"read_note","arguments":"{\"path\":\"Grande\"}"}}]}}]}""",
                    """{"choices":[{"delta":{},"finish_reason":"tool_calls"}]}"""));
                var longAnswer = "Resumo.\n\n| n | texto |\n|---|---|\n" + string.Concat(Enumerable.Range(0, 1500).Select(i => "| " + i + " | célula " + i + " |\n")) + "\nFIM DA RESPOSTA";
                model.Chats.Enqueue(Sse("""{"choices":[{"delta":{"content":""" + System.Text.Json.JsonSerializer.Serialize(longAnswer) + """}}]}"""));
                model.Chats.Enqueue(Sse("""{"choices":[{"delta":{"content":"Revisado depois de ler tudo. FIM"}}]}"""));
                await Run(panel, "Teste");
                Assert.Equal("# Um\n\n- velho\n", File.ReadAllText(Path.Combine(vault, "Um.md")));
                Assert.Contains("Argumentos inválidos para edit_note", model.Sent[1]);   // the broken JSON is named, not run
                Assert.Contains("nao_existe", model.Sent[1]);
                Assert.Contains("Linha 0 com texto", model.Sent[1]);
                Assert.Equal("Revisado depois de ler tudo. FIM", Last(panel));
                // the huge note came in pages: the agent is told which lines it has not read, and its draft is kept whole
                Assert.Contains(panel.Conversation.Messages, m => m.Role == "user" && m.Content.Any(b => b.Text?.Contains("Grande.md: faltam as linhas") == true));
                var draft = Assert.Single(panel.Conversation.Messages, m => m.Meta?.Draft == true);
                Assert.Contains("| 1499 | célula 1499 |", string.Concat(draft.Content.Select(b => b.Text)));
                // the panel still answers afterwards
                model.Chats.Enqueue(Sse("""{"choices":[{"delta":{"content":"Ainda aqui."}}]}"""));
                await Run(panel, "Você está aí?");
                Assert.Equal("Ainda aqui.", Last(panel));
                return true;
            }
            finally
            {
                window.Close();
                UrbeApp.AssistantFolder = before;
                try { Directory.Delete(dir, true); } catch (IOException) { }
            }
        }, CancellationToken.None));

    /// <summary>CST-132: a registered key, used in a run that writes a note and saves the conversation, is nowhere in the vault.</summary>
    [Fact]
    public async Task Assistant_TheProviderKeyNeverReachesTheVault() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            const string key = "sk-or-v1-NUNCANOVAULT0123456789";
            var dir = Path.Combine(Path.GetTempPath(), "urbe-keyvault-" + Guid.NewGuid().ToString("N"));
            var vault = Path.Combine(dir, "vault");
            Directory.CreateDirectory(vault);
            File.WriteAllText(Path.Combine(vault, "Um.md"), "# Um\n\n- velho\n");
            var before = UrbeApp.AssistantFolder;
            UrbeApp.AssistantFolder = Path.Combine(dir, "app", "assistant");
            var view = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "app", "sel.json")));
            var window = new Window { Width = 412, Height = 860, Content = view };
            window.Show();
            try
            {
                await view.OpenVaultAsync();
                await Settle(window, view);
                var model = new ScriptedModel();
                var panel = view.Assistant;
                panel.Open();
                panel.SelectAgent("geral");
                panel.Config.AgentPolicies["geral"] = "auto";
                panel.Providers = new AssistantProviders(model, (_, _) => Task.CompletedTask);
                panel.Config.Providers.Add(new AiProviderConfig("openrouter", "openrouter", ApiKey: key));
                panel.Config.ProviderId = "openrouter"; panel.Config.Model = "org/m";
                panel.Conversation.ProviderId = "openrouter"; panel.Conversation.Model = "org/m";
                model.Chats.Enqueue(Sse(
                    """{"choices":[{"delta":{"tool_calls":[{"index":0,"id":"a","function":{"name":"edit_note","arguments":"{\"path\":\"Um\",\"old_text\":\"- velho\",\"new_text\":\"- novo\"}"}}]}}]}""",
                    """{"choices":[{"delta":{},"finish_reason":"tool_calls"}]}"""));
                model.Chats.Enqueue(Sse("""{"choices":[{"delta":{"content":"Feito."}}]}"""));
                panel.Send("Troque velho por novo em Um");
                for (int i = 0; i < 200 && panel.CurrentRun is null; i++) { await Task.Delay(5); Dispatcher.UIThread.RunJobs(); }
                await panel.CurrentRun!;
                await view.FlushPendingAsync();
                for (int i = 0; i < 30; i++) { await Task.Delay(5); Dispatcher.UIThread.RunJobs(); }
                Assert.Contains("- novo", File.ReadAllText(Path.Combine(vault, "Um.md")));
                var leaks = Directory.EnumerateFiles(vault, "*", SearchOption.AllDirectories)
                    .Where(f => File.ReadAllText(f).Contains(key, StringComparison.Ordinal)).ToList();
                Assert.Empty(leaks);
                return true;
            }
            finally
            {
                window.Close();
                UrbeApp.AssistantFolder = before;
                try { Directory.Delete(dir, true); } catch (IOException) { }
            }
        }, CancellationToken.None));
}
