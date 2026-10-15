using System.Net;
using System.Text;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Urbe.Client;
using Urbe.Core.Assistant;

namespace Urbe.Client.Tests;

/// <summary>
/// The Assistente (UC-21) in the native app with a scripted model server: connecting a provider (the key only on the
/// device), choosing the model, a request whose edit asks for approval with the diff, the vault file written, the
/// answer, and Desfazer bringing the note back.
/// </summary>
public sealed partial class HeadlessUiTests
{
    private sealed class ScriptedModel : HttpMessageHandler
    {
        public Queue<string> Chats { get; } = new();
        public List<string> Sent { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Get)
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{"data":[{"id":"org/modelo-b","name":"Modelo B","context_length":64000,"supported_parameters":["tools"]}]}""", Encoding.UTF8, "application/json")
                };
            Sent.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            var body = Chats.Count > 0 ? Chats.Dequeue() : "data: [DONE]\n\n";
            var res = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8) };
            res.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/event-stream");
            return res;
        }
    }

    private static string Sse(params string[] chunks) => string.Concat(chunks.Select(c => "data: " + c + "\n\n")) + "data: [DONE]\n\n";

    [Fact]
    public async Task Assistant_EditsTheVaultWithApprovalAndUndoes() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var dir = Path.Combine(Path.GetTempPath(), "urbe-assistant-" + Guid.NewGuid().ToString("N"));
            var vault = Path.Combine(dir, "vault");
            Directory.CreateDirectory(vault);
            const string original = "# Comece\n\nVeja as tarefas.\n\n## Tarefas\n- uma\n- duas\n";
            File.WriteAllText(Path.Combine(vault, "Comece.md"), original);
            var storage = new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "app", "sel.json"));
            var before = UrbeApp.AssistantFolder;
            UrbeApp.AssistantFolder = Path.Combine(dir, "app", "assistant");
            var view = new MainView(storage);
            var window = new Window { Width = 1280, Height = 760, Content = view };
            window.Show();
            try
            {
                await view.OpenVaultAsync();
                await Settle(window, view);
                var model = new ScriptedModel();

                // the rail's Assistente opens the panel: "Conecte um modelo"
                ClickButtonWithText(view, "Assistente");
                var panel = view.Assistant;
                panel.Providers = new AssistantProviders(model, (_, _) => Task.CompletedTask);
                Assert.True(panel.IsVisible);
                Assert.Equal(440, panel.Width);   // wide: a panel beside the city
                Assert.Contains("Conecte um modelo", panel.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text));

                // OpenRouter: the key, then the models list and the model
                ClickButtonWithText(panel, "OpenRouter");
                Assert.Equal("Conectar OpenRouter", panel.ViewTitle);
                var key = panel.GetLogicalDescendants().OfType<TextBox>().Single(t => Avalonia.Automation.AutomationProperties.GetName(t) == "Chave da API");
                key.Text = " sk-or-teste ";
                ClickButtonWithText(panel, "Salvar e escolher modelo");
                for (int i = 0; i < 50 && panel.ViewTitle != "Modelo"; i++) { await Task.Delay(10); Dispatcher.UIThread.RunJobs(); }
                for (int i = 0; i < 50 && !panel.GetLogicalDescendants().OfType<TextBlock>().Any(t => t.Text == "Modelo B"); i++) { await Task.Delay(10); Dispatcher.UIThread.RunJobs(); }
                ClickButtonWithText(panel, "Modelo B");
                Assert.Null(panel.ViewTitle);
                Assert.Equal("org/modelo-b", panel.Conversation.Model);
                Assert.Equal("sk-or-teste", Assert.Single(panel.Config.Providers).ApiKey);

                // a request: the model edits Comece; the change waits for approval with its diff
                model.Chats.Enqueue(Sse(
                    """{"model":"org/modelo-b","choices":[{"delta":{"content":"Vou trocar."}}]}""",
                    """{"choices":[{"delta":{"tool_calls":[{"index":0,"id":"call_1","function":{"name":"edit_note","arguments":"{\"path\":\"Comece\",\"old_text\":\"- uma\",\"new_text\":\"- primeira\"}"}}]}}]}""",
                    """{"choices":[{"delta":{},"finish_reason":"tool_calls"}],"usage":{"prompt_tokens":100,"completion_tokens":20}}"""));
                model.Chats.Enqueue(Sse(
                    """{"choices":[{"delta":{"content":"Troquei a primeira tarefa em [[Comece]]."}}]}""",
                    """{"choices":[{"delta":{},"finish_reason":"stop"}],"usage":{"prompt_tokens":150,"completion_tokens":12}}"""));
                panel.Input.Text = "Troque a primeira tarefa";
                panel.Send(panel.Input.Text);
                for (int i = 0; i < 200 && panel.PendingApproval is null; i++) { await Task.Delay(10); Dispatcher.UIThread.RunJobs(); }
                var pending = Assert.IsType<AiApprovalRequest>(panel.PendingApproval);
                Assert.Equal(("edit_note", "Editar Comece", "Comece.md"), (pending.Call.Name, pending.Label, pending.Preview!.Path));
                for (int i = 0; i < 20; i++) { await Task.Delay(5); Dispatcher.UIThread.RunJobs(); }
                var texts = panel.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
                Assert.Contains("Aprovar alteração · Comece.md", texts);
                Assert.Contains("- - uma", texts);
                // UC-51: the text the Assistente writes carries its mark, {ia:"modelo"}
                Assert.Contains("+ - primeira {ia:\"modelo-b\"}", texts);
                Assert.Equal(original, File.ReadAllText(Path.Combine(vault, "Comece.md")));   // nothing written before the approval
                Snapshot(window, "native-assistant-approval.png");

                ClickButtonWithText(panel, "Aprovar");
                await panel.CurrentRun!;
                for (int i = 0; i < 20; i++) { await Task.Delay(5); Dispatcher.UIThread.RunJobs(); }
                var edited = original.Replace("- uma", "- primeira {ia:\"modelo-b\"}");
                Assert.Equal(edited, File.ReadAllText(Path.Combine(vault, "Comece.md")));
                Assert.Equal(edited, view.Documents!.Get("Comece.md")!.Content);
                var run = panel.Conversation.Runs.Last();
                Assert.Equal(("end", 1), (run.Stopped, run.Changes.Count));
                texts = panel.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
                Assert.Contains("1 nota alterada", texts);
                Assert.Equal(2, model.Sent.Count);
                Assert.Contains("\"tool_call_id\":\"call_1\"", model.Sent[1]);
                Assert.Equal(282L, panel.Conversation.Usage.Input + panel.Conversation.Usage.Output);
                Snapshot(window, "native-assistant-done.png");

                // Desfazer: the note as it was, on disk too
                await panel.UndoLastAsync(confirmed: true);
                Assert.Equal(original, File.ReadAllText(Path.Combine(vault, "Comece.md")));
                Assert.True(panel.Conversation.Runs.Last().Reverted);

                // appearance: set_appearance writes Personalização/tema.json (after the approval) and applies it; Desfazer takes it away
                model.Chats.Enqueue(Sse(
                    """{"choices":[{"delta":{"tool_calls":[{"index":0,"id":"call_2","function":{"name":"set_appearance","arguments":"{\"config\":{\"tema\":\"sepia\"}}"}}]}}]}""",
                    """{"choices":[{"delta":{},"finish_reason":"tool_calls"}]}"""));
                model.Chats.Enqueue(Sse("""{"choices":[{"delta":{"content":"Pronto, tema sépia."}}]}"""));
                panel.Send("Deixe o Urbe em sépia");
                for (int i = 0; i < 200 && panel.PendingApproval is null; i++) { await Task.Delay(10); Dispatcher.UIThread.RunJobs(); }
                Assert.Equal(("set_appearance", "Personalização/tema.json"), (panel.PendingApproval!.Call.Name, panel.PendingApproval.Preview!.Path));
                Assert.False(File.Exists(Path.Combine(vault, "Personalização", "tema.json")));
                panel.Answer(AiApproval.Approve);
                await panel.CurrentRun!;
                Assert.Contains("\"tema\": \"sepia\"", File.ReadAllText(Path.Combine(vault, "Personalização", "tema.json")));
                Assert.Equal("sepia", (string?)view.Customization.Config["tema"]);
                await panel.UndoLastAsync(confirmed: true);
                Assert.False(File.Exists(Path.Combine(vault, "Personalização", "tema.json")));   // it did not exist: back to the trash
                Assert.Equal("escuro", (string?)view.Customization.Config["tema"]);

                // the conversation and the key stay in the app's data, never in the vault
                Assert.NotEmpty(Directory.GetFiles(UrbeApp.AssistantFolder!, "conversa-*.json"));
                Assert.Contains("sk-or-teste", File.ReadAllText(Path.Combine(UrbeApp.AssistantFolder!, "config.json")));
                Assert.DoesNotContain(Directory.GetFiles(vault, "*", SearchOption.AllDirectories), f => File.ReadAllText(f).Contains("sk-or-teste"));

                // a new panel (next launch) finds the recent conversation of this vault again
                var again = new AssistantPanel(view, new FolderAssistantStorage(UrbeApp.AssistantFolder!));
                again.Open();
                Assert.Equal(panel.Conversation.Id, again.Conversation.Id);
                Assert.Equal("Troque a primeira tarefa", again.Conversation.Title);
                return true;
            }
            finally
            {
                UrbeApp.AssistantFolder = before;
                window.Close();
                try { Directory.Delete(dir, recursive: true); } catch (IOException) { }
            }
        }, CancellationToken.None));
}
