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
                // The native assistant reopens its saved work for one quality-review turn.
                model.Chats.Enqueue(Sse("""{"choices":[{"delta":{"content":"Revisei o conteúdo salvo e confirmei a alteração."}}]}"""));
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
                Assert.Equal(3, model.Sent.Count);
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
                model.Chats.Enqueue(Sse("""{"choices":[{"delta":{"content":"Revisei a personalização salva."}}]}"""));
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
                // Regression: preserve the full settings layout and the new memory and network tabs.
                panel.ShowSettings();
                Assert.Equal("Configurações", panel.ViewTitle);
                Assert.Contains("Provedores de modelo", panel.GetLogicalDescendants().OfType<TextBlock>().Select(x => x.Text));
                ClickNamed(panel, "Configurações: Agentes");
                Assert.Contains("Agentes personalizados", panel.GetLogicalDescendants().OfType<TextBlock>().Select(x => x.Text));
                ClickNamed(panel, "Configurações: Contexto");
                Assert.Contains("Gerenciar memórias", panel.GetLogicalDescendants().OfType<TextBlock>().Select(x => x.Text));
                ClickNamed(panel, "Configurações: Pesquisa");
                Assert.Contains("Privacidade e pesquisa", panel.GetLogicalDescendants().OfType<TextBlock>().Select(x => x.Text));
                ClickNamed(panel, "Configurações: Execução");
                Assert.Contains("Profundidade da investigação", panel.GetLogicalDescendants().OfType<TextBlock>().Select(x => x.Text));
                return true;
            }
            finally
            {
                UrbeApp.AssistantFolder = before;
                window.Close();
                try { Directory.Delete(dir, recursive: true); } catch (IOException) { }
            }
        }, CancellationToken.None));

    [Fact]
    public async Task Assistant_AnAgentTakesTheNameThePersonAgreesTo() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            // UC-81: "Jarvis, …" → the agent asks; with the yes it takes the name, on this device only
            var dir = Path.Combine(Path.GetTempPath(), "urbe-assistant-" + Guid.NewGuid().ToString("N"));
            var vault = Path.Combine(dir, "vault");
            Directory.CreateDirectory(vault);
            File.WriteAllText(Path.Combine(vault, "Comece.md"), "# Comece\n");
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
                ClickButtonWithText(view, "Assistente");
                var panel = view.Assistant;
                var model = new ScriptedModel();
                panel.Providers = new AssistantProviders(model, (_, _) => Task.CompletedTask);
                panel.Config.Providers.Add(new AiProviderConfig("openrouter", "openrouter", ApiKey: "sk-teste"));
                panel.Config.Model = "org/modelo-b";
                model.Chats.Enqueue(Sse(
                    """{"choices":[{"delta":{"tool_calls":[{"index":0,"id":"call_1","function":{"name":"set_my_name","arguments":"{\"name\":\"Jarvis\"}"}}]}}]}""",
                    """{"choices":[{"delta":{},"finish_reason":"tool_calls"}]}"""));
                model.Chats.Enqueue(Sse("""{"choices":[{"delta":{"content":"Combinado, sou o Jarvis."}}]}"""));
                panel.Send("Sim, pode se chamar Jarvis");
                for (int i = 0; i < 200 && panel.CurrentRun is null; i++) { await Task.Delay(5); Dispatcher.UIThread.RunJobs(); }
                if (panel.CurrentRun is { } run) await run;
                for (int i = 0; i < 20; i++) { await Task.Delay(5); Dispatcher.UIThread.RunJobs(); }
                Assert.Contains("## Seu nome", model.Sent[0]);
                Assert.Contains("Seu nome é Assistente.", model.Sent[0]);
                Assert.Contains("\"set_my_name\"", model.Sent[0]);
                Assert.Contains("uma apostila, um livro, um caderno", model.Sent[0]);   // UC-77: Páginas without being told
                Assert.Equal("Jarvis", panel.Config.AgentNames["geral"]);
                // the chip says the new name, and the next request introduces it
                Assert.Contains("Jarvis", panel.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text));
                Assert.Contains("Jarvis", File.ReadAllText(Path.Combine(UrbeApp.AssistantFolder!, "config.json")));
                Assert.DoesNotContain(Directory.GetFiles(vault, "*", SearchOption.AllDirectories), f => File.ReadAllText(f).Contains("Jarvis"));
                return true;
            }
            finally
            {
                UrbeApp.AssistantFolder = before;
                window.Close();
                try { Directory.Delete(dir, recursive: true); } catch (IOException) { }
            }
        }, CancellationToken.None));

    [Fact]
    public async Task Assistant_ALineOfTheNoteAsksTheAgentByItsName() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            // UC-80: "&Jarvis&: …" in a note; nothing is sent until "Pedir a Jarvis"
            var dir = Path.Combine(Path.GetTempPath(), "urbe-assistant-" + Guid.NewGuid().ToString("N"));
            var vault = Path.Combine(dir, "vault");
            Directory.CreateDirectory(vault);
            File.WriteAllText(Path.Combine(vault, "Herói.md"), "# Herói\n\nAlto, do norte.\n\n&Jarvis&: Dê uma sugestão de nome\n\n&Sexta&: e outro\n");
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
                ClickButtonWithText(view, "Assistente");
                var panel = view.Assistant;
                var model = new ScriptedModel();
                panel.Providers = new AssistantProviders(model, (_, _) => Task.CompletedTask);
                panel.Config.Providers.Add(new AiProviderConfig("openrouter", "openrouter", ApiKey: "sk-teste"));
                panel.Config.Model = "org/modelo-b";
                Assert.Null(panel.RenameAgent("escritor", "Jarvis"));
                panel.Close();

                view.ShowNotes();
                Assert.True(view.Explorer.TryOpenNote("Herói.md"));
                await Settle(window, view);
                Assert.Empty(model.Sent);
                var texts = view.Editor.GetLogicalDescendants().OfType<TextBlock>()
                    .Select(t => t.Inlines is { Count: > 0 } runs ? string.Concat(runs.OfType<Avalonia.Controls.Documents.Run>().Select(r => r.Text)) : t.Text).ToList();
                Assert.Contains("Jarvis", texts);
                Assert.Contains("Dê uma sugestão de nome", texts);
                Assert.DoesNotContain(texts, t => t?.Contains("&Jarvis&", StringComparison.Ordinal) == true);
                Snapshot(window, "native-agent-in-note.png");

                model.Chats.Enqueue(Sse("""{"choices":[{"delta":{"content":"Sugiro Aurora."}}]}"""));
                ClickNamed(view.Editor, "Pedir a Jarvis");
                for (int i = 0; i < 200 && panel.CurrentRun is null; i++) { await Task.Delay(5); Dispatcher.UIThread.RunJobs(); }
                if (panel.CurrentRun is { } run) await run;
                for (int i = 0; i < 20; i++) { await Task.Delay(5); Dispatcher.UIThread.RunJobs(); }
                Assert.True(panel.IsVisible);
                Assert.Equal(("Na nota Herói", "escritor"), (panel.Conversation.Title, panel.Conversation.AgentId));
                var sent = Assert.Single(model.Sent);
                Assert.Contains("Seu nome é Jarvis.", sent);
                Assert.Contains("Pedido feito dentro da nota Herói.md, nesta linha:\\n&Jarvis&: Dê uma sugestão de nome", sent);
                Assert.Contains(panel.GetLogicalDescendants().OfType<TextBlock>(), t => (t.Text ?? string.Concat(t.Inlines?.OfType<Avalonia.Controls.Documents.Run>().Select(r => r.Text) ?? [])).Contains("Sugiro Aurora.", StringComparison.Ordinal));
                Snapshot(window, "native-agent-in-note-answer.png");

                // the answer kept in the note, as a quote under the line that asked it, marked as the Assistente's
                ClickNamed(panel, "Pôr a resposta na nota");
                var kept = await ReadWhenWritten(Path.Combine(vault, "Herói.md"), t => t.Contains("> Sugiro", StringComparison.Ordinal));
                Assert.Matches(@"&Jarvis&: Dê uma sugestão de nome\n\n> Sugiro Aurora\. \{ia:""[^""]+""\}\n\n&Sexta&: e outro\n", kept);
                Assert.False(panel.GetLogicalDescendants().OfType<Button>().Single(b => Avalonia.Automation.AutomationProperties.GetName(b) == "Pôr a resposta na nota").IsEnabled);

                // a name no agent has: it says so, and nothing is sent
                panel.Close();
                ClickNamed(view.Editor, "Pedir a Sexta");
                for (int i = 0; i < 20 && !view.Dialogs.IsOpen; i++) { await Task.Delay(5); Dispatcher.UIThread.RunJobs(); }
                Assert.True(view.Dialogs.IsOpen);
                Assert.Contains(view.Dialogs.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text?.StartsWith("Nenhum agente se chama Sexta.", StringComparison.Ordinal) == true);
                Assert.Single(model.Sent);
                return true;
            }
            finally
            {
                UrbeApp.AssistantFolder = before;
                window.Close();
                try { Directory.Delete(dir, recursive: true); } catch (IOException) { }
            }
        }, CancellationToken.None));

    [Fact]
    public async Task Assistant_TheAgentsChangeIsASuggestionInTheNote() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            // UC-80 fatia 3: the change comes into the note itself, with Aceitar and Recusar; the panel stays closed
            var dir = Path.Combine(Path.GetTempPath(), "urbe-assistant-" + Guid.NewGuid().ToString("N"));
            var vault = Path.Combine(dir, "vault");
            Directory.CreateDirectory(vault);
            var file = Path.Combine(vault, "Herói.md");
            File.WriteAllText(file, "# Herói\n\nAlto, do norte.\n\n&&: Dê uma sugestão de nome\n\n&&: e um apelido\n");
            var storage = new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "app", "sel.json"));
            var before = UrbeApp.AssistantFolder;
            UrbeApp.AssistantFolder = Path.Combine(dir, "app", "assistant");
            var view = new MainView(storage);
            var window = new Window { Width = 1280, Height = 760, Content = view };
            window.Show();
            static string Edit(string id, string old, string now) =>
                Sse("{\"choices\":[{\"delta\":{\"tool_calls\":[{\"index\":0,\"id\":\"" + id + "\",\"function\":{\"name\":\"edit_note\",\"arguments\":" +
                    System.Text.Json.JsonSerializer.Serialize(System.Text.Json.JsonSerializer.Serialize(new { path = "Herói", old_text = old, new_text = now })) + "}}]}}]}",
                    """{"choices":[{"delta":{},"finish_reason":"tool_calls"}]}""");
            try
            {
                await view.OpenVaultAsync();
                await Settle(window, view);
                ClickButtonWithText(view, "Assistente");
                var panel = view.Assistant;
                var model = new ScriptedModel();
                panel.Providers = new AssistantProviders(model, (_, _) => Task.CompletedTask);
                panel.Config.Providers.Add(new AiProviderConfig("openrouter", "openrouter", ApiKey: "sk-teste"));
                panel.Config.Model = "org/modelo-b";
                panel.Close();
                view.ShowNotes();
                Assert.True(view.Explorer.TryOpenNote("Herói.md"));
                await Settle(window, view);

                model.Chats.Enqueue(Edit("call_1", "&&: Dê uma sugestão de nome", "Nome sugerido: Aurora"));
                model.Chats.Enqueue(Sse("""{"choices":[{"delta":{"content":"Pronto."}}]}"""));
                ClickNamed(view.Editor, "Pedir ao Assistente");
                for (int i = 0; i < 300 && view.Editor.Suggestion is null; i++) { await Task.Delay(5); Dispatcher.UIThread.RunJobs(); }
                var sg = Assert.IsType<VisualSurface.Suggestion>(view.Editor.Suggestion);
                Assert.Equal("Assistente", sg.Who);
                Assert.StartsWith("Nome sugerido: Aurora", sg.Change.Replacement);
                Assert.False(panel.IsVisible);
                Assert.Contains("Sugestão de Assistente", view.Editor.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text));
                Assert.Contains("&&: Dê uma sugestão de nome", File.ReadAllText(file));   // nothing written before the answer
                Snapshot(window, "native-agent-suggestion.png");

                ClickNamed(view.Editor, "Aceitar");
                await panel.CurrentRun!;
                for (int i = 0; i < 20; i++) { await Task.Delay(5); Dispatcher.UIThread.RunJobs(); }
                Assert.Null(view.Editor.Suggestion);
                Assert.False(panel.IsVisible);
                var text = File.ReadAllText(file);
                Assert.Contains("Nome sugerido: Aurora {ia:\"modelo-b\"}", text);
                Assert.DoesNotContain("Dê uma sugestão", text);
                Assert.Equal(text, view.Editor.Current!.Content);

                // fatia 4: the notice's Desfazer takes the whole change back, in the vault and in the editor
                var undo = view.GetLogicalDescendants().OfType<Button>().First(b => b.FindLogicalAncestorOfType<Urbe.Client.Ui.UrbeToast>() is not null &&
                    (b.Content as string == "Desfazer" || b.GetLogicalDescendants().OfType<TextBlock>().Any(t => t.Text == "Desfazer")));
                undo.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                await panel.CurrentUndo!;
                var original = "# Herói\n\nAlto, do norte.\n\n&&: Dê uma sugestão de nome\n\n&&: e um apelido\n";
                Assert.Equal(original, File.ReadAllText(file));
                Assert.Equal(original, view.Editor.Current!.Content);
                text = original;

                // Recusar: the note stays as it is, and the agent is told
                model.Chats.Enqueue(Edit("call_2", "&&: e um apelido", "Apelido: Faísca"));
                model.Chats.Enqueue(Sse("""{"choices":[{"delta":{"content":"Tudo bem."}}]}"""));
                ClickNamed(view.Editor, "Pedir ao Assistente");
                for (int i = 0; i < 300 && view.Editor.Suggestion is null; i++) { await Task.Delay(5); Dispatcher.UIThread.RunJobs(); }
                ClickNamed(view.Editor, "Recusar");
                await panel.CurrentRun!;
                Assert.Equal(text, File.ReadAllText(file));
                Assert.Contains("recusou a sugestão na nota", model.Sent.Last());
                return true;
            }
            finally
            {
                UrbeApp.AssistantFolder = before;
                window.Close();
                try { Directory.Delete(dir, recursive: true); } catch (IOException) { }
            }
        }, CancellationToken.None));

    [Fact]
    public async Task Assistant_ALongConversationStaysLight() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            // observation of 22/10/2026: after 11 requests full of steps the Assistente froze the app
            var dir = Path.Combine(Path.GetTempPath(), "urbe-assistant-" + Guid.NewGuid().ToString("N"));
            var vault = Path.Combine(dir, "vault");
            Directory.CreateDirectory(vault);
            File.WriteAllText(Path.Combine(vault, "Comece.md"), "# Comece\n\n- uma\n");
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
                ClickButtonWithText(view, "Assistente");
                var panel = view.Assistant;
                var model = new ScriptedModel();
                panel.Providers = new AssistantProviders(model, (_, _) => Task.CompletedTask);
                panel.Config.Providers.Add(new AiProviderConfig("openrouter", "openrouter", ApiKey: "sk-teste"));
                panel.Config.Model = "org/modelo-b";
                // eleven earlier requests, ten steps each
                var msgs = panel.Conversation.Messages;
                for (int r = 0; r < 11; r++)
                {
                    msgs.Add(new AiChatMessage { Id = "u" + r, Role = "user", Content = [AiChatBlock.TextBlock("pedido " + r)] });
                    for (int k = 0; k < 10; k++)
                    {
                        var id = "c" + r + "_" + k;
                        msgs.Add(new AiChatMessage { Id = "a" + id, Role = "assistant", Content = [new AiChatBlock { Type = "tool_use", Id = id, Name = "read_note", Input = new System.Text.Json.Nodes.JsonObject { ["path"] = "Comece.md" } }] });
                        msgs.Add(new AiChatMessage { Id = "r" + id, Role = "user", Meta = new AiChatMeta { ToolResults = true }, Content = [new AiChatBlock { Type = "tool_result", ToolUseId = id, Content = new string('x', 5000) }] });
                    }
                    msgs.Add(new AiChatMessage { Id = "f" + r, Role = "assistant", Content = [AiChatBlock.TextBlock("Resposta " + r + ".")] });
                }
                panel.RenderAll();
                // Regression for #734: the latest request must group its operations into ONE timeline.
                Assert.Equal(11 * 3, panel.Log.Children.Count);
                var folded = panel.Log.Children.OfType<Expander>().ToList();
                Assert.Equal(10, folded.Count);
                Assert.All(folded, x => Assert.Null(x.Content));   // built only when opened
                Assert.Contains("10 passos", panel.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text));
                folded[0].IsExpanded = true;
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(10, ((Panel)folded[0].Content!).Children.Count);

                // Latest request: collapsed summary and nested inspection with intact results.
                var activity = Assert.IsType<StackPanel>(panel.Log.Children[31]);
                var activityBorder = Assert.IsType<Border>(activity.Children[0]);
                var activityFold = Assert.IsType<Expander>(activityBorder.Child);
                Assert.Null(activityFold.Content);
                Assert.Contains(panel.GetLogicalDescendants().OfType<TextBlock>(),
                    t => t.Text?.StartsWith("10 operações", StringComparison.Ordinal) == true);
                activityFold.IsExpanded = true;
                Dispatcher.UIThread.RunJobs();
                var timeline = Assert.IsType<StackPanel>(activityFold.Content);
                Assert.Equal(10, timeline.Children.Count);
                var firstStage = Assert.IsType<Expander>(Assert.IsType<Border>(timeline.Children[0]).Child);
                firstStage.IsExpanded = true;
                Dispatcher.UIThread.RunJobs();
                Assert.Contains("Resultado devolvido ao agente",
                    panel.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text));

                // a new request: while it streams and waits for approval, the earlier ones are not rebuilt
                model.Chats.Enqueue(Sse(
                    """{"model":"org/modelo-b","choices":[{"delta":{"content":"Vou "}}]}""",
                    """{"choices":[{"delta":{"content":"trocar "}}]}""",
                    """{"choices":[{"delta":{"content":"agora."}}]}""",
                    """{"choices":[{"delta":{"tool_calls":[{"index":0,"id":"call_1","function":{"name":"edit_note","arguments":"{\"path\":\"Comece\",\"old_text\":\"- uma\",\"new_text\":\"- primeira\"}"}}]}}]}""",
                    """{"choices":[{"delta":{},"finish_reason":"tool_calls"}]}"""));
                model.Chats.Enqueue(Sse("""{"choices":[{"delta":{"content":"Pronto."}}]}"""));
                panel.Send("Troque a primeira tarefa");
                var earlier = panel.Log.Children.Take(30).ToList();
                for (int i = 0; i < 200 && panel.PendingApproval is null; i++) { await Task.Delay(10); Dispatcher.UIThread.RunJobs(); }
                Assert.NotNull(panel.PendingApproval);
                for (int i = 0; i < 20; i++) { await Task.Delay(5); Dispatcher.UIThread.RunJobs(); }
                for (int i = 0; i < 30; i++) Assert.Same(earlier[i], panel.Log.Children[i]);
                Assert.Contains("Aprovar alteração · Comece.md", panel.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text));
                panel.Answer(AiApproval.Approve);
                await panel.CurrentRun!;
                return true;
            }
            finally
            {
                UrbeApp.AssistantFolder = before;
                window.Close();
                try { Directory.Delete(dir, recursive: true); } catch (IOException) { }
            }
        }, CancellationToken.None));
    [Fact]
    public void ImageToolIsVisibleRegardlessOfChatProvider()
    {
        var claude = new AiProviderConfig("claude", "anthropic", ApiKey: "sk-ant");
        var router = new AiProviderConfig("router", "openrouter", ApiKey: "sk-or");
        Assert.Contains(AssistantPanel.ToolsFor(claude), tool => tool.Name == "generate_image");
        Assert.Contains(AssistantPanel.ToolsFor(router), tool => tool.Name == "generate_image");
        Assert.Equal("write", AssistantImageTools.Tool.Access);
    }

}
