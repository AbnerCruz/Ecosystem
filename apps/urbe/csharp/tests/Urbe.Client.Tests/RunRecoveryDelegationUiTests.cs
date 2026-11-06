using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Urbe.Client;
using Urbe.Core.Assistant;

namespace Urbe.Client.Tests;

/// <summary>
/// CST-216: work the app was ended in the middle of comes back as interrupted work: resumed from its journal without
/// repeating what was done, or only reopened as a conversation. CST-217: a scoped delegation to a configured agent
/// writes only inside its scope, shows its steps nested in the asking card, and its artifact is read from the vault.
/// </summary>
public sealed partial class HeadlessUiTests
{
    private static string ToolCalls(params (string Id, string Name, JsonObject Args)[] calls) =>
        Sse(new JsonObject
        {
            ["choices"] = new JsonArray(new JsonObject
            {
                ["delta"] = new JsonObject
                {
                    ["tool_calls"] = new JsonArray(calls.Select((c, i) => (JsonNode)new JsonObject
                    {
                        ["index"] = i, ["id"] = c.Id, ["function"] = new JsonObject { ["name"] = c.Name, ["arguments"] = c.Args.ToJsonString() }
                    }).ToArray())
                }
            })
        }.ToJsonString(), """{"choices":[{"delta":{},"finish_reason":"tool_calls"}]}""");

    private static string Says(string text) =>
        Sse(new JsonObject { ["choices"] = new JsonArray(new JsonObject { ["delta"] = new JsonObject { ["content"] = text } }) }.ToJsonString(),
            """{"choices":[{"delta":{},"finish_reason":"stop"}]}""");

    private static (AiConversation Conv, AssistantRunJournal Journal) InterruptedWork(string folder, string vault)
    {
        var storage = new FolderAssistantStorage(folder);
        var request = new AiChatMessage { Id = "u_x", Role = "user", Content = [AiChatBlock.TextBlock("Acrescente duas linhas em Notas")] };
        var conv = new AiConversation { Id = "c_x", AgentId = "two", Vault = vault, Title = "Duas linhas", Created = 1, Messages = [request] };
        new AssistantStore(storage).SaveConversation(conv);
        var journal = AssistantRunJournal.Begin(storage, new AiRunStart("r_x", conv.Id, vault, "two", "test-model", "Acrescente duas linhas em Notas", 1, request));
        journal.Request(1);
        var step = new AiChatMessage { Id = "m_x", Role = "assistant", Meta = new AiChatMeta { Model = "test-model" },
            Content = [new AiChatBlock { Type = "tool_use", Id = "t1", Name = "append_to_note", Input = new JsonObject { ["path"] = "Notas.md", ["content"] = "Primeira." } }] };
        journal.Response(1, step, new AiUsage(10, 5, 0.01));
        journal.Intent("t1", "append_to_note", AssistantRunJournal.InputHash(step.Content[0].Input), "Notas.md", AssistantRunJournal.Sha("# Notas\n"), null, null);
        journal.Result("t1", "append_to_note", "write", AssistantRunJournal.InputHash(step.Content[0].Input), "Texto acrescentado em Notas.md.", false, "done",
            [new AiChange("doc", new AiNoteSnap("Notas.md", "# Notas\n"), new AiNoteSnap("Notas.md", "# Notas\n\nPrimeira.\n"))], null);
        journal.Request(2);   // the app was ended while the model answered
        return (conv, journal);
    }

    [Fact]
    public async Task Recovery_ResumedWorkGoesOnWithoutRepeatingWhatWasDone() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var vault = Path.Combine(Path.GetTempPath(), "urbe-resume-vault-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(vault);
        File.WriteAllText(Path.Combine(vault, "Notas.md"), "# Notas\n");
        var (view, window, panel, model, folder, before) = await TwoAgentsAsync("write", vault);
        panel.Config.AgentPolicies["two"] = "auto";
        try
        {
            InterruptedWork(folder, panel.Conversation.Vault);
            panel.Close();
            panel.Open();
            await Settle(window, view);
            var card = await WaitFor(window, view, () => panel.Log.GetLogicalDescendants().OfType<Border>()
                .FirstOrDefault(b => Avalonia.Automation.AutomationProperties.GetName(b) == "Trabalho interrompido"));
            Assert.Contains(card.GetLogicalDescendants().OfType<TextBlock>(), t => (t.Text ?? "").Contains("1 ações feitas e conferidas"));
            Assert.Contains(card.GetLogicalDescendants().OfType<TextBlock>(), t => (t.Text ?? "").Contains("pode ter sido cobrada"));
            Snapshot(window, "native-run-interrupted.png");
            // resumed: the model asks for the same append again (not repeated), then the second line, then answers
            model.Chats.Enqueue(ToolCalls(("n1", "append_to_note", new JsonObject { ["path"] = "Notas.md", ["content"] = "Primeira." }),
                ("n2", "append_to_note", new JsonObject { ["path"] = "Notas.md", ["content"] = "Segunda." })));
            model.Chats.Enqueue(Says("Pronto."));
            model.Chats.Enqueue(Says("Pronto: acrescentei a segunda linha."));
            ClickButtonWithText(card, "Retomar trabalho interrompido");
            var run = panel.CurrentRun!;
            await run;
            await Settle(window, view);
            var text = await ReadWhenWritten(Path.Combine(vault, "Notas.md"), t => t.Contains("Segunda."));
            Assert.DoesNotContain("Primeira.", text);   // the journal says it was done: never written twice
            Assert.Equal("c_x", panel.Conversation.Id);
            Assert.Equal("r_x", panel.Conversation.Runs[^1].Id);
            Assert.Contains(panel.Conversation.Messages.SelectMany(m => m.Content), b => b.ToolUseId == "n1" && b.Status == "reused");
            var storage = new FolderAssistantStorage(folder);
            Assert.True(AssistantRunJournal.Open(storage, "r_x")!.Closed);
            Assert.Empty(AssistantRunJournal.Interrupted(storage, panel.Conversation.Vault));
            return true;
        }
        finally { window.Close(); UrbeApp.AssistantFolder = before; try { Directory.Delete(folder, true); Directory.Delete(vault, true); } catch (IOException) { } }
    }, CancellationToken.None));

    [Fact]
    public async Task Recovery_ReopeningOnlyShowsTheConversationAndRunsNothing() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var vault = Path.Combine(Path.GetTempPath(), "urbe-resume-vault-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(vault);
        File.WriteAllText(Path.Combine(vault, "Notas.md"), "# Notas\n");
        var (view, window, panel, model, folder, before) = await TwoAgentsAsync("write", vault);
        try
        {
            InterruptedWork(folder, panel.Conversation.Vault);
            panel.Close();
            panel.Open();
            var card = await WaitFor(window, view, () => panel.Log.GetLogicalDescendants().OfType<Border>()
                .FirstOrDefault(b => Avalonia.Automation.AutomationProperties.GetName(b) == "Trabalho interrompido"));
            int sent = model.Sent.Count;
            ClickButtonWithText(card, "Abrir conversa");
            await Settle(window, view);
            Assert.Equal("c_x", panel.Conversation.Id);
            Assert.False(panel.Running);
            Assert.Equal(sent, model.Sent.Count);
            Assert.Contains(panel.Conversation.Messages.SelectMany(m => m.Content), b => b.ToolUseId == "t1" && b.Content.StartsWith("Texto acrescentado", StringComparison.Ordinal));
            Assert.Equal("aborted", panel.Conversation.Runs[^1].Stopped);
            Assert.Equal("# Notas\n", File.ReadAllText(Path.Combine(vault, "Notas.md")));
            Assert.Empty(AssistantRunJournal.Interrupted(new FolderAssistantStorage(folder), panel.Conversation.Vault));
            Assert.DoesNotContain(panel.Log.GetLogicalDescendants().OfType<Border>(), b => Avalonia.Automation.AutomationProperties.GetName(b) == "Trabalho interrompido");
            return true;
        }
        finally { window.Close(); UrbeApp.AssistantFolder = before; try { Directory.Delete(folder, true); Directory.Delete(vault, true); } catch (IOException) { } }
    }, CancellationToken.None));

    [Fact]
    public async Task Delegation_TheSpecialistWritesOnlyInsideItsScopeAndItsStepsShowNested() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var vault = Path.Combine(Path.GetTempPath(), "urbe-delegate-vault-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(vault);
        File.WriteAllText(Path.Combine(vault, "Comece.md"), "# Comece\n");
        var (view, window, panel, model, folder, before) = await TwoAgentsAsync("write", vault);
        panel.Config.AgentPolicies["two"] = "auto";
        panel.Config.AgentPolicies["one"] = "auto";
        try
        {
            model.Chats.Enqueue(ToolCalls(("d1", "delegate_to_agent", new JsonObject { ["agent_id"] = "two", ["task"] = "Escreva um resumo", ["expected"] = "note", ["artifact_path"] = "Resumos/Resumo.md", ["sources"] = new JsonArray("Comece.md") })));
            model.Chats.Enqueue(ToolCalls(("w1", "create_note", new JsonObject { ["path"] = "Fora.md", ["content"] = "Fora do escopo." })));
            model.Chats.Enqueue(ToolCalls(("w2", "create_note", new JsonObject { ["path"] = "Resumos/Resumo.md", ["content"] = "# Resumo\n\nTexto do resumo." })));
            model.Chats.Enqueue(Says("ARTEFATO: Resumos/Resumo.md; JUSTIFICATIVA: li Comece.md."));
            model.Chats.Enqueue(Says("ARTEFATO: Resumos/Resumo.md; JUSTIFICATIVA: li Comece.md."));
            model.Chats.Enqueue(Says("A revisora entregou Resumos/Resumo.md."));
            panel.Send("Delegue o resumo à revisora.");
            var run = panel.CurrentRun!;
            await WaitFor(window, view, () => view.Dialogs.IsOpen ? view.Dialogs : null);
            Assert.Contains(view.Dialogs.GetLogicalDescendants().OfType<TextBlock>(), t => (t.Text ?? "").Contains("Pode gravar em: Resumos/Resumo.md"));
            ClickButtonWithText(view.Dialogs, "Delegar");
            await run;
            await Settle(window, view);
            Assert.False(File.Exists(Path.Combine(vault, "Fora.md")));
            Assert.Contains("Texto do resumo.", await ReadWhenWritten(Path.Combine(vault, "Resumos", "Resumo.md")));
            var result = panel.Conversation.Messages.SelectMany(m => m.Content).First(b => b.Type == "tool_result" && b.ToolUseId == "d1");
            Assert.False(result.IsError);
            Assert.Contains("ARTEFATO (lido agora do vault): Resumos/Resumo.md", result.Content);
            Assert.Contains("Texto do resumo.", result.Content);
            var steps = panel.NestedSteps("d1");
            Assert.True(steps.Count >= 2);
            Assert.EndsWith("falhou", steps[0]);
            var store = new AssistantStore(new FolderAssistantStorage(folder));
            var child = Assert.Single(store.ConversationsFor("two", panel.Conversation.Vault));
            Assert.Equal("Resumos/Resumo.md", child.Brief!.ArtifactPath);
            Assert.Contains(child.Messages.SelectMany(m => m.Content), b => b.ToolUseId == "w1" && b.IsError && b.Content.Contains("Fora do escopo"));
            Assert.DoesNotContain(child.Messages.SelectMany(m => m.Content), b => b.Text?.Contains("Delegue o resumo") == true);   // never the asking conversation
            return true;
        }
        finally { window.Close(); UrbeApp.AssistantFolder = before; try { Directory.Delete(folder, true); Directory.Delete(vault, true); } catch (IOException) { } }
    }, CancellationToken.None));
}
