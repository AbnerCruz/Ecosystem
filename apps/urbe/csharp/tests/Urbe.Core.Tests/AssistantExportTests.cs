using System.Text.Json.Nodes;
using Urbe.Core.Assistant;

namespace Urbe.Core.Tests;

public sealed class AssistantExportTests
{
    [Fact]
    public void FullConversationIncludesPersistedCallsResultsContextRunsAndMetrics()
    {
        var chat = new AiConversation
        {
            Id = "chat-1", Title = "Diagnóstico", AgentId = "revisor",
            Model = "modelo-x", Usage = new AiUsage(123, 45, 0.02),
            WebSources = [new AiWebSource("s1", "Fonte", "https://example.org", "https://example.org", "example.org", null, "2026-10-26", "ok")]
        };
        chat.Messages.Add(new AiChatMessage
        {
            Id = "m1", Role = "assistant",
            Meta = new AiChatMeta { Reasoning = "Raciocínio registrado", Usage = new AiUsage(30, 10, 0.01) },
            Content =
            [
                new AiChatBlock { Type = "text", Text = "Contexto privado", Hidden = true },
                new AiChatBlock { Type = "tool_use", Id = "tool-1", Name = "read_note", Input = JsonNode.Parse("""{"path":"Teste.md","start_line":2}""") },
                new AiChatBlock { Type = "tool_result", ToolUseId = "tool-1", Content = "Falha ao ler", IsError = true, Status = "error" }
            ]
        });
        chat.Runs.Add(new AiRunRecord
        {
            Id = "run-1", Stopped = "no_progress",
            Efficiency = new AiExecutionMetrics { ReusedReads = 2, NoOpWrites = 3, ContextCharsSaved = 1000 }
        });

        var json = JsonNode.Parse(AssistantExport.Conversation(chat, true, "2.0.0-beta.31", 123456))!.AsObject();
        Assert.Equal("urbe.assistant.export/1", (string?)json["schema"]);
        Assert.Equal("complete", (string?)json["mode"]);
        var saved = json["conversation"]!.AsObject();
        Assert.Equal("Teste.md", (string?)saved["messages"]![0]!["content"]![1]!["input"]!["path"]);
        Assert.True((bool)saved["messages"]![0]!["content"]![0]!["_hidden"]!);
        Assert.Equal("Falha ao ler", (string?)saved["messages"]![0]!["content"]![2]!["content"]);
        Assert.Equal("Raciocínio registrado", (string?)saved["messages"]![0]!["meta"]!["reasoning"]);
        Assert.Equal(2, (int)saved["runs"]![0]!["efficiency"]!["reusedReads"]!);
        Assert.Equal(1000L, (long)saved["runs"]![0]!["efficiency"]!["contextCharsSaved"]!);
        Assert.Equal(123L, (long)saved["usage"]!["input"]!);
        Assert.Single(saved["webSources"]!.AsArray());
        Assert.Equal("chat-1", (string?)saved["id"]);
    }

    [Fact]
    public void LiteConversationIsReadableAndDoesNotExposeHiddenOrRawArguments()
    {
        var chat = new AiConversation
        {
            Id = "c1", Title = "Leitura",
            Messages =
            [
                new AiChatMessage
                {
                    Id = "m1", Role = "assistant",
                    Content =
                    [
                        new AiChatBlock { Type = "text", Text = "texto secreto do contexto", Hidden = true },
                        AiChatBlock.TextBlock("Resposta pública"),
                        new AiChatBlock { Type = "tool_use", Id = "t1", Name = "read_note", Input = JsonNode.Parse("""{"private":"argumentos sigilosos"}""") },
                        new AiChatBlock { Type = "tool_result", ToolUseId = "t1", Content = "resultado" }
                    ]
                }
            ]
        };
        var md = AssistantExport.Conversation(chat, false);
        Assert.Contains("Resposta pública", md);
        Assert.Contains("Ferramenta: read_note", md);
        Assert.Contains("Resultado da ferramenta t1", md);
        Assert.DoesNotContain("texto secreto do contexto", md);
        Assert.DoesNotContain("argumentos sigilosos", md);
    }

    [Fact]
    public void AgentMemoryIncludesAllStatesAndNeverCopiesProviderOrOtherAgentMemories()
    {
        var cfg = new AiConfig();
        cfg.Providers.Add(new AiProviderConfig("p", "openrouter", ApiKey: "sk-never-export"));
        cfg.MemoryRecords.AddRange(
        [
            new AiMemoryRecord { Id = "own", AgentId = "agent-a", Vault = "Vault", Scope = "agent", Kind = "decision", Text = "Um registro", State = "archived", Confidence = 42 },
            new AiMemoryRecord { Id = "other", AgentId = "agent-b", Vault = "Vault", Scope = "agent", Text = "outro-agente" },
            new AiMemoryRecord { Id = "global", Scope = "global", Text = "global-private" },
            new AiMemoryRecord { Id = "vault", Vault = "Vault", Scope = "vault", Text = "vault-private" }
        ]);
        var json = AssistantExport.Memory(cfg, "agent-a", true);
        var root = JsonNode.Parse(json)!.AsObject();
        var memory = Assert.Single(root["memories"]!.AsArray());
        Assert.Equal("own", (string?)memory!["id"]);
        Assert.Equal("archived", (string?)memory["state"]);
        Assert.Equal(42, (int)memory["confidence"]!);
        Assert.DoesNotContain("sk-never-export", json);
        Assert.DoesNotContain("outro-agente", json);
        Assert.DoesNotContain("vault-private", json);
        Assert.DoesNotContain("global-private", json);
        var lite = AssistantExport.Memory(cfg, "agent-a", false);
        Assert.Contains("Um registro", lite);
        Assert.DoesNotContain("outro-agente", lite);
    }
}
