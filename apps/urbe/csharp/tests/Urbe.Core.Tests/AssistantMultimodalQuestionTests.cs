using System.Text.Json.Nodes;
using Urbe.Core;
using Urbe.Core.Assistant;

namespace Urbe.Core.Tests;

/// <summary>UC-63: user-selected attachments and single-question clarification; no network/real credentials.</summary>
public sealed class AssistantMultimodalQuestionTests
{
    [Fact]
    public void ImageAndPdfAttachmentsRoundTripAndUseProviderSpecificWireFormats()
    {
        var png = Convert.ToBase64String(new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a });
        var pdf = Convert.ToBase64String("%PDF-1.7"u8.ToArray());
        var message = new AiChatMessage
        {
            Role = "user",
            Content =
            [
                AiChatBlock.TextBlock("Analise estas referências"),
                new AiChatBlock { Type = "attachment", FileName = "referência.png", MimeType = "image/png", Content = png },
                new AiChatBlock { Type = "attachment", FileName = "aula.pdf", MimeType = "application/pdf", Content = pdf }
            ]
        };
        var restored = AiChatMessage.FromJson(message.ToJson());
        Assert.Equal(message.ToJson().ToJsonString(), restored.ToJson().ToJsonString());
        var blocks = restored.Content.Select(b => b.Type == "attachment"
            ? AiBlock.Attachment(b.MimeType, b.FileName, b.Content) : AiBlock.TextBlock(b.Text ?? "")).ToList();
        var req = new AiRequest("vision-model", [new AiMessage("user", blocks)]);
        var openai = AssistantProviders.OpenAiWire(req, AssistantProviders.Resolve(new AiProviderConfig("p", "openai")));
        var content = openai["messages"]?[0]?["content"] as JsonArray;
        Assert.NotNull(content);
        Assert.Equal("image_url", (string?)content[1]?["type"]);
        Assert.Equal("data:image/png;base64," + png, (string?)content[1]?["image_url"]?["url"]);
        Assert.Equal("file", (string?)content[2]?["type"]);
        Assert.Equal("aula.pdf", (string?)content[2]?["file"]?["filename"]);
        Assert.Equal("data:application/pdf;base64," + pdf, (string?)content[2]?["file"]?["file_data"]);
        var anthropic = AssistantProviders.AnthropicWire(req);
        var a = anthropic["messages"]?[0]?["content"] as JsonArray;
        Assert.NotNull(a);
        Assert.Equal("image", (string?)a[1]?["type"]);
        Assert.Equal("document", (string?)a[2]?["type"]);
        Assert.Equal("base64", (string?)a[2]?["source"]?["type"]);
    }

    [Fact]
    public void OldAttachmentIsNotResentToTheModelAfterUserMovesOn()
    {
        var old = new AiChatMessage { Role = "user", Content =
        [
            AiChatBlock.TextBlock("Veja a imagem."),
            new AiChatBlock { Type = "attachment", FileName = "mapa.png", MimeType = "image/png", Content = new string('a', 10000) }
        ] };
        var now = new AiChatMessage { Role = "user", Content = [AiChatBlock.TextBlock("Obrigado, próximo assunto.")] };
        var settled = AssistantAgent.SettlePast([old, now]);
        Assert.DoesNotContain(settled[0].Content, b => b.Type == "attachment");
        Assert.Contains(settled[0].Content, b => b.Text?.Contains("mapa.png") == true);
        Assert.Contains(old.Content, b => b.Type == "attachment"); // stored original unchanged
    }

    private sealed class Host : IAssistantHost
    {
        public DocumentStore Documents { get; } = new();
        public KnowledgeIndex Knowledge { get; }
        public Host() => Knowledge = new KnowledgeIndex(Documents);
        public int ReadChars => 4000;
        public (string? Path, string? Selection) EditorContext() => (null, null);
        public Task<UrbeDocument> CreateNoteAsync(string path, string content) => throw new NotSupportedException();
        public Task<UrbeDocument> SaveNoteAsync(UrbeDocument doc, string content) => throw new NotSupportedException();
        public Task<UrbeDocument> RenameNoteAsync(UrbeDocument doc, string path) => throw new NotSupportedException();
        public Task TrashNoteAsync(UrbeDocument doc) => throw new NotSupportedException();
        public Task CreateFolderAsync(string path) => throw new NotSupportedException();
        public void OpenNote(string path) { }
        public void Remember(string fact) { }
        public IReadOnlyList<AiPlanStep> LastPlan { get; private set; } = [];
        public void SetPlan(IReadOnlyList<AiPlanStep> steps) => LastPlan = steps;
    }

    [Fact]
    public async Task AskUserToolYieldsAndWaitsWithoutRepeatingQuestionsOrWritingFiles()
    {
        var tool = AssistantQuestionTools.Ask;
        Assert.Equal("ui", tool.Access);
        var input = new JsonObject
        {
            ["question"] = "Qual estilo de citação devo usar?",
            ["resolved"] = 2,
            ["total"] = 3,
            ["reason"] = "A escolha afeta a formatação."
        };
        var host = new Host();
        var streamed = 0;
        var messages = new List<AiChatMessage>
        {
            new() { Role = "user", Content = [AiChatBlock.TextBlock("Prepare minha monografia.")] }
        };
        var result = await AssistantAgent.RunAsync(new AiAgentRun
        {
            Agent = AssistantAgent.AgentById("geral"), Host = host, Policy = "ask",
            Provider = new AiProviderConfig("p", "openai"), Model = "model-with-tools",
            Messages = messages, Tools = [tool], MaxSteps = 5,
            Stream = (_, req, _, _) =>
            {
                streamed++;
                if (streamed > 1) throw new InvalidOperationException("O agente deveria ter aguardado a resposta do usuário");
                return Task.FromResult(new AiResult([AiBlock.ToolUse("call_1", "ask_user", input)], "", "tool_use",
                    new AiUsage(15, 8, null), "model-with-tools"));
            }
        });
        Assert.Equal(1, streamed);
        Assert.Equal("needs_input", result.Stopped);
        Assert.Null(result.Error);
        Assert.Equal("ask_user", messages.SelectMany(m => m.Content).Single(b => b.Type == "tool_use").Name);
        Assert.Contains(messages.SelectMany(m => m.Content), b => b.Type == "tool_result" && !b.IsError);
        Assert.Empty(result.Changes);
    }
    [Fact]
    public async Task PlanningAcceptsDetailedPlansBeyondTwentySteps()
    {
        var host = new Host();
        var steps = new JsonArray(Enumerable.Range(1, 27)
            .Select(i => (JsonNode)new JsonObject { ["step"] = "Revisar detalhe " + i, ["status"] = i == 1 ? "in_progress" : "pending" })
            .ToArray());
        var tool = AssistantAdaptivePlanTools.Update;
        var input = new JsonObject { ["steps"] = steps };
        Assert.Empty(AssistantTools.Validate(tool, input));
        await tool.Run(input, host);
        Assert.Equal(27, host.LastPlan.Count);
        Assert.Equal("Revisar detalhe 27", host.LastPlan[^1].Step);
    }

    [Fact]
    public async Task QuestionsHaveNoArtificialHundredItemProgressMaximum()
    {
        var host = new Host();
        var input = new JsonObject { ["question"] = "Qual atributo é determinante?", ["resolved"] = 135, ["total"] = 180 };
        Assert.Empty(AssistantTools.Validate(AssistantQuestionTools.Ask, input));
        var result = await AssistantQuestionTools.Ask.Run(input, host);
        Assert.Contains("135/180", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LongAgentRunDoesNotStopAtThirtyModelTurns()
    {
        var host = new Host();
        var messages = new List<AiChatMessage> { new() { Role = "user", Content = [AiChatBlock.TextBlock("Investigue em profundidade.")] } };
        int requested = 0;
        var result = await AssistantAgent.RunAsync(new AiAgentRun
        {
            Agent = AssistantAgent.AgentById("geral"), Host = host, Policy = "readonly",
            Provider = new AiProviderConfig("p", "openai"), Model = "m",
            Messages = messages, Tools = [AssistantAdaptivePlanTools.Update],
            Stream = (_, _, _, _) =>
            {
                requested++;
                var usage = new AiUsage(1, 1, null);
                if (requested > 36)
                    return Task.FromResult(new AiResult([AiBlock.TextBlock("Investigação concluída.")], "", "end_turn", usage, "m"));
                var input = new JsonObject { ["steps"] = new JsonArray(new JsonObject { ["step"] = "Inspecionar " + requested, ["status"] = "in_progress" }) };
                return Task.FromResult(new AiResult([AiBlock.ToolUse("c" + requested, "update_plan", input)], "", "tool_use", usage, "m"));
            }
        });
        Assert.Equal("end", result.Stopped);
        Assert.Equal(37, requested);
        Assert.Equal(37, result.Steps);
    }

    [Fact]
    public void QualityGuidanceCoversCreativeArtifactsAndRelatedNotes()
    {
        var guidance = AssistantAgent.UrbeGuidance;
        Assert.Contains("cada tipo de artefato", guidance, StringComparison.Ordinal);
        Assert.Contains("sem aplicar formulários, modelos ou seções predefinidos", guidance, StringComparison.Ordinal);
        Assert.DoesNotContain("ficha de personagem", guidance, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Ideias paralelas", guidance, StringComparison.Ordinal);
        Assert.Contains("nota existente", guidance, StringComparison.Ordinal);
        Assert.Contains("cânone", guidance, StringComparison.Ordinal);
        Assert.Contains("reabra", guidance, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WrittenArtifactTriggersReadbackAndQualityReviewBeforeCompletion()
    {
        var host = new Host();
        int reads = 0;
        var schema = (JsonObject)JsonNode.Parse("""{"type":"object","properties":{},"additionalProperties":false}""")!;
        var write = new AiTool
        {
            Name = "write_artifact", Access = "write", Description = "Test file writer",
            Parameters = schema, Label = _ => "Write",
            Run = (_, _) => Task.FromResult(new AiToolOutput("Saved",
                Changes: [new AiChange("character", null, new AiNoteSnap("Projetos/Documento.md", "# Documento"))]))
        };
        var read = new AiTool
        {
            Name = "audit_artifact", Access = "read", Description = "Test file readback",
            Parameters = schema, Label = _ => "Read",
            Run = (_, _) => { reads++; return Task.FromResult(new AiToolOutput("# Alma — verificado")); }
        };
        var turns = 0;
        var messages = new List<AiChatMessage>
        {
            new() { Role = "user", Content = [AiChatBlock.TextBlock("Crie e revise um documento detalhado.")] }
        };
        var result = await AssistantAgent.RunAsync(new AiAgentRun
        {
            Host = host, Policy = "auto", Agent = AssistantAgent.AgentById("geral"),
            Provider = new AiProviderConfig("p", "openai"), Model = "m",
            Messages = messages, Tools = [write, read], ReviewWrittenArtifacts = true,
            Stream = (_, _, _, _) =>
            {
                turns++;
                var result = turns switch
                {
                    1 => new AiResult([AiBlock.ToolUse("w", "write_artifact", new JsonObject())], "", "tool_use", new AiUsage(1, 1, null), "m"),
                    2 => new AiResult([AiBlock.TextBlock("Pronto.")], "", "end_turn", new AiUsage(1, 1, null), "m"),
                    3 => new AiResult([AiBlock.ToolUse("r", "audit_artifact", new JsonObject())], "", "tool_use", new AiUsage(1, 1, null), "m"),
                    _ => new AiResult([AiBlock.TextBlock("Revisado.")], "", "end_turn", new AiUsage(1, 1, null), "m")
                };
                return Task.FromResult(result);
            }
        });
        Assert.Equal("end", result.Stopped);
        Assert.Equal(4, turns);
        Assert.Equal(1, reads);
        Assert.Contains(messages.SelectMany(m => m.Content), block =>
            block.Text?.Contains("Revisão obrigatória do trabalho", StringComparison.Ordinal) == true);
        Assert.Equal("Revisado.", messages.Last(m => m.Role == "assistant").Content.First(b => b.Type == "text").Text);
    }

    [Fact]
    public async Task FormRequestDefersLaterWritesInTheSameModelResponse()
    {
        int writes = 0;
        var form = new AiTool { Name = "request_form", Access = "ui", Description = "form", Parameters = new(), Label = _ => "Form",
            Run = (_, _) => Task.FromResult(new AiToolOutput("Formulário salvo")) };
        var write = new AiTool { Name = "write_artifact", Access = "write", Description = "write", Parameters = new(), Label = _ => "Write",
            Run = (_, _) => { writes++; return Task.FromResult(new AiToolOutput("Gravado")); } };
        var messages = new List<AiChatMessage> { new() { Role = "user", Content = [AiChatBlock.TextBlock("Preencha antes de criar")] } };
        var result = await AssistantAgent.RunAsync(new AiAgentRun
        {
            Agent = AssistantAgent.AgentById("geral") with { Tools = "all" }, Host = new Host(), Policy = "auto",
            Provider = new AiProviderConfig("p", "openai"), Model = "test", Messages = messages, Tools = [form, write],
            Stream = (_, _, _, _) => Task.FromResult(new AiResult([AiBlock.ToolUse("form", "request_form", new JsonObject()), AiBlock.ToolUse("write", "write_artifact", new JsonObject())], "", "tool_use", new AiUsage(1, 1, null), "test"))
        });
        Assert.Equal(0, writes);
        Assert.Equal("needs_input", result.Stopped);
        var outputs = messages.SelectMany(m => m.Content).Where(b => b.Type == "tool_result").ToList();
        Assert.Equal(2, outputs.Count);
        Assert.Equal("deferred", outputs.Single(b => b.ToolUseId == "write").Status);
    }

}
