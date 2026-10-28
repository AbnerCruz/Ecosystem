using System.Text.Json.Nodes;
using Urbe.Core;
using Urbe.Core.Assistant;

namespace Urbe.Core.Tests;

/// <summary>
/// The Assistente's writes through the authorship engine (CST-028–034): the agent reads and edits seal-free text and the
/// Urbe seals each block it created or regenerated — no tool can remove, alter, forge or stack a seal, so there is
/// nothing to loop on.
/// </summary>
public sealed class AssistantAuthorshipSealTests
{
    private static string S(string model) => "{ia:\"" + model + "\"}";

    private sealed class Host : IAssistantHost
    {
        public DocumentStore Documents { get; } = new();
        public KnowledgeIndex Knowledge { get; }
        public int Writes { get; private set; }
        public int ReadChars => 16000;
        public Host(string content)
        {
            Documents.Upsert(new() { Id = "note", Path = "Nota.md", Content = content });
            Knowledge = new(Documents);
        }
        public (string? Path, string? Selection) EditorContext() => ("Nota.md", "Trecho " + S("m"));
        public Task<UrbeDocument> SaveNoteAsync(UrbeDocument doc, string text)
        {
            Writes++;
            return Task.FromResult(Documents.Upsert(new() { Id = doc.Id, Path = doc.Path, Content = text }));
        }
        public Task<UrbeDocument> CreateNoteAsync(string path, string content)
        {
            Writes++;
            return Task.FromResult(Documents.Upsert(new() { Path = path, Content = content }));
        }
        public Task<UrbeDocument> RenameNoteAsync(UrbeDocument doc, string path) => throw new NotSupportedException();
        public Task TrashNoteAsync(UrbeDocument doc) => throw new NotSupportedException();
        public Task CreateFolderAsync(string path) => throw new NotSupportedException();
        public void OpenNote(string path) { }
        public void Remember(string fact) { }
        public void SetPlan(IReadOnlyList<AiPlanStep> steps) { }
    }

    private static AiResult Answer(params AiBlock[] blocks) => new(blocks, "", "end_turn", new(1, 1, null), "test");

    private static async Task<(AiAgentResult Result, List<AiChatMessage> Messages, List<AiApprovalRequest> Approvals)> Run(
        Host host, string policy, Func<AiApprovalRequest, AiApproval>? approve, params AiBlock[][] steps)
    {
        var messages = new List<AiChatMessage> { new() { Id = "u", Role = "user", Content = [AiChatBlock.TextBlock("Trabalhe na nota")] } };
        var approvals = new List<AiApprovalRequest>();
        int step = 0;
        var result = await AssistantAgent.RunAsync(new()
        {
            Host = host, Agent = AssistantAgent.AgentById("geral"), Policy = policy, Model = "provedor/gpt-x", Provider = new("p", "openai"),
            Messages = messages, OptimizeToolUse = true, MarkAiText = true,
            Approve = (r, _) => { approvals.Add(r); return Task.FromResult(approve?.Invoke(r) ?? AiApproval.Approve); },
            Stream = (_, _, _, _) => Task.FromResult(step < steps.Length ? Answer(steps[step++]) : Answer(AiBlock.TextBlock("Pronto.")))
        });
        return (result, messages, approvals);
    }

    private static string ToolResult(List<AiChatMessage> messages, string id) =>
        messages.SelectMany(m => m.Content).Single(b => b.Type == "tool_result" && b.ToolUseId == id).Content;

    [Fact]
    public async Task ReadingANoteShowsNoSeals()
    {
        var host = new Host("# Título " + S("a") + "\n\nTexto humano.\n\n- ** " + S("a") + "item:** fim\n");
        var (_, messages, _) = await Run(host, "auto", null,
            [AiBlock.ToolUse("r", "read_note", new JsonObject { ["path"] = "Nota.md" })],
            [AiBlock.ToolUse("g", "grep_notes", new JsonObject { ["pattern"] = "item" })],
            [AiBlock.ToolUse("c", "get_editor_context", new JsonObject())]);
        Assert.DoesNotContain("{ia:", ToolResult(messages, "r"));
        Assert.Contains("- **item:** fim", ToolResult(messages, "r"));
        Assert.DoesNotContain("{ia:", ToolResult(messages, "g"));
        Assert.DoesNotContain("{ia:", ToolResult(messages, "c"));
    }

    [Fact]
    public async Task RewritingANoteSealsEveryNewBlockAndKeepsTheOthers()
    {
        var host = new Host("Diário humano.\n\nResumo antigo. " + S("velho") + "\n");
        var (result, _, approvals) = await Run(host, "ask", null,
            [AiBlock.ToolUse("w", "write_note", new JsonObject
            {
                ["path"] = "Nota.md",
                ["content"] = "Diário humano.\n\nResumo antigo.\n\n## Próximos passos\n\n- Levantar custos dos containers\n- Consultar o zoneamento da prefeitura\n"
            })]);
        var expected = "Diário humano.\n\nResumo antigo. " + S("velho") + "\n\n## Próximos passos " + S("gpt-x") +
                       "\n\n- Levantar custos dos containers " + S("gpt-x") + "\n- Consultar o zoneamento da prefeitura " + S("gpt-x") + "\n";
        Assert.Equal(expected, host.Documents.Get("Nota.md")!.Content);
        // the person approves exactly the bytes that are written, seals included
        Assert.Equal(expected, Assert.Single(approvals).Preview!.After);
        Assert.Equal(1, host.Writes);
        Assert.Equal("end", result.Stopped);
    }

    [Fact]
    public async Task EditMatchesTheTextTheAgentReadEvenWithLegacySealsInTheMiddle()
    {
        // the dictionary-template loop: old_text copied from what the agent read did not match the stored bytes
        var host = new Host("- [x] ** " + S("gpt-6-luna") + "Páginas (PR #700):** introduzidas.\n- [ ] Outro item\n");
        var (_, messages, _) = await Run(host, "auto", null,
            [AiBlock.ToolUse("e", "edit_note", new JsonObject
            {
                ["path"] = "Nota.md", ["old_text"] = "**Páginas (PR #700):** introduzidas.", ["new_text"] = "**Páginas (PR #700):** introduzidas e revisadas na beta 36 com seleção por pasta."
            })]);
        Assert.Contains("Editada", ToolResult(messages, "e"));
        Assert.Equal("- [x] **Páginas (PR #700):** introduzidas e revisadas na beta 36 com seleção por pasta. " + S("gpt-x") + "\n- [ ] Outro item\n",
            host.Documents.Get("Nota.md")!.Content);
    }

    [Fact]
    public async Task SpellingCorrectionOfHumanTextAddsNoSeal()
    {
        var host = new Host("Eu nao sabia que voce vinha amanha.\n");
        await Run(host, "auto", null,
            [AiBlock.ToolUse("e", "edit_note", new JsonObject
            {
                ["path"] = "Nota.md", ["old_text"] = "Eu nao sabia que voce vinha amanha.", ["new_text"] = "Eu não sabia que você vinha amanhã."
            })]);
        Assert.Equal("Eu não sabia que você vinha amanhã.\n", host.Documents.Get("Nota.md")!.Content);
    }

    [Fact]
    public async Task AttemptsToRemoveOrForgeSealsChangeNothingAndDoNotLoop()
    {
        var before = "Texto " + S("modelo") + "\n";
        var host = new Host(before);
        var (result, messages, _) = await Run(host, "auto", null,
            [AiBlock.ToolUse("e1", "edit_note", new JsonObject { ["path"] = "Nota.md", ["old_text"] = " {ia:\"modelo\"}", ["new_text"] = "" })],
            [AiBlock.ToolUse("w1", "write_note", new JsonObject { ["path"] = "Nota.md", ["content"] = "Texto\n" })],
            [AiBlock.ToolUse("w2", "write_note", new JsonObject { ["path"] = "Nota.md", ["content"] = "Texto {ia:\"outro\"}\n" })]);
        Assert.Equal(before, host.Documents.Get("Nota.md")!.Content);
        Assert.Equal(0, host.Writes);
        Assert.Contains("gerenciados pelo Urbe", ToolResult(messages, "e1"));
        Assert.All(new[] { "e1", "w1", "w2" }, id =>
            Assert.False(messages.SelectMany(m => m.Content).Single(b => b.ToolUseId == id && b.Type == "tool_result").IsError));
        Assert.NotEqual("error", result.Stopped);
    }

    [Fact]
    public async Task CreatedNoteIsSealedPerBlockWithTheModelShortName()
    {
        var host = new Host("");
        await Run(host, "auto", null,
            [AiBlock.ToolUse("c", "create_note", new JsonObject { ["path"] = "Nova.md", ["content"] = "# Nova\n\nPrimeiro.\n\nSegundo.\n" })]);
        Assert.Equal("# Nova " + S("gpt-x") + "\n\nPrimeiro. " + S("gpt-x") + "\n\nSegundo. " + S("gpt-x") + "\n", host.Documents.Get("Nova.md")!.Content);
    }

    [Fact]
    public async Task FilesThatAreNotNotesAreNeverSealed()
    {
        var host = new Host("");
        const string json = "{\n  \"cor\": \"#3fb86b\"\n}\n";
        await Run(host, "auto", null,
            [AiBlock.ToolUse("c", "create_note", new JsonObject { ["path"] = "Personalização/tema.json", ["content"] = json })]);
        Assert.Equal(json, host.Documents.Get("Personalização/tema.json")!.Content);
    }

    [Fact]
    public async Task AppendSealsOnlyTheAppendedBlocks()
    {
        var host = new Host("## Notas\n\nMinha ideia.\n");
        await Run(host, "auto", null,
            [AiBlock.ToolUse("a", "append_to_note", new JsonObject { ["path"] = "Nota.md", ["content"] = "Complemento gerado agora.\n\nOutro complemento.", ["heading"] = "Notas" })]);
        Assert.Equal("## Notas\n\nMinha ideia.\nComplemento gerado agora. " + S("gpt-x") + "\n\nOutro complemento. " + S("gpt-x") + "\n",
            host.Documents.Get("Nota.md")!.Content);
    }

    [Fact]
    public async Task ThePersonsEditOfTheProposalKeepsAuthorshipHonest()
    {
        var host = new Host("Humano.\n");
        await Run(host, "ask", r =>
            {
                // the person fixes a word in the AI paragraph and adds a paragraph of their own; seals typed by hand do nothing
                var proposal = AiAuthorship.Strip(r.Preview!.After!);
                return AiApproval.ApproveEdited(proposal.Replace("gerada", "escrita") + "\nMeu acréscimo. {ia:\"falso\"}\n");
            },
            [AiBlock.ToolUse("w", "write_note", new JsonObject { ["path"] = "Nota.md", ["content"] = "Humano.\n\nUma frase nova gerada pela IA sobre o tema.\n" })]);
        Assert.Equal("Humano.\n\nUma frase nova escrita pela IA sobre o tema. " + S("gpt-x") + "\n\nMeu acréscimo.\n", host.Documents.Get("Nota.md")!.Content);
    }

    [Fact]
    public async Task ANoteChangedDuringTheApprovalIsNotOverwritten()
    {
        var host = new Host("Original.\n");
        var (_, messages, _) = await Run(host, "ask", r =>
            {
                host.Documents.Upsert(new() { Id = "note", Path = "Nota.md", Content = "Original editado pela pessoa.\n" });
                return AiApproval.Approve;
            },
            [AiBlock.ToolUse("w", "write_note", new JsonObject { ["path"] = "Nota.md", ["content"] = "Texto completamente novo da IA.\n" })]);
        Assert.Equal("Original editado pela pessoa.\n", host.Documents.Get("Nota.md")!.Content);
        Assert.Contains("mudou", ToolResult(messages, "w"));
    }

    [Fact]
    public async Task ThePromptSaysTheUrbeManagesTheSeals()
    {
        var host = new Host("x");
        string? system = null;
        await AssistantAgent.RunAsync(new()
        {
            Host = host, Agent = AssistantAgent.AgentById("geral"), Policy = "auto", Model = "m", Provider = new("p", "openai"),
            Messages = [new() { Id = "u", Role = "user", Content = [AiChatBlock.TextBlock("oi")] }], Tools = [], MarkAiText = true,
            Stream = (_, request, _, _) => { system = request.System; return Task.FromResult(Answer(AiBlock.TextBlock("oi"))); }
        });
        Assert.Contains("aplicados e mantidos pelo próprio Urbe", system);
        Assert.Contains("não aparecem no texto", system);
    }
}
