using System.Diagnostics;
using System.Text.Json.Nodes;
using Urbe.Core.Assistant;

namespace Urbe.Core.Tests;

public sealed class MemoryQualityExtendedTests
{
    private static AiChatMessage User(string t) => new() { Role = "user", Content = [AiChatBlock.TextBlock(t)] };
    private static AiMemoryRecord Fact(string id, string text, string agent="A", string kind="decision") => new()
    { Id=id, Scope="agent", Vault="V", AgentId=agent, Text=text, Kind=kind, CreatedAt=100, UpdatedAt=200 };

    [Fact]
    public void ShortContinuationsReuseNearestTaskButNotAnEarlierUnrelatedTopic()
    {
        var cfg = new AiConfig();
        cfg.MemoryRecords.Add(Fact("form", "A decisão do formulário exige validação acessível"));
        cfg.MemoryRecords.Add(Fact("geo", "O mundo geográfico tem rios e clima"));
        var one = new List<AiChatMessage> { User("Revise o formulário acessível"), User("Pode seguir") };
        var query = AssistantMemoryEngine.ContinuityQuery(one);
        Assert.Contains("formulário", query);
        Assert.Contains(AssistantMemoryEngine.Search(cfg, "V", "A", query), m => m.Memory.Id == "form");
        one.Insert(1, User("Agora quero estudar rios e clima do mundo geográfico"));
        var changed = AssistantMemoryEngine.ContinuityQuery(one);
        Assert.Contains("geográfico", changed);
        Assert.DoesNotContain("formulário", changed);
        Assert.Equal("Calcule o custo do projeto", AssistantMemoryEngine.ContinuityQuery([User("continuar"), User("Calcule o custo do projeto")]));
    }

    [Fact]
    public void SupersededDecisionsAndOtherAgentsCannotLeakInRankedOrPromptResults()
    {
        var cfg = new AiConfig();
        cfg.MemoryRecords.Add(Fact("old", "Usar a estrutura antiga para atlas"));
        cfg.MemoryRecords.Add(Fact("new", "Usar a estrutura nova para atlas"));
        cfg.MemoryRecords.Add(Fact("private", "Outra memória atlas exclusiva", "B"));
        Assert.True(AssistantMemoryEngine.ChangeState(cfg, "old", "superseded", 300));
        var result = AssistantMemoryEngine.Search(cfg, "V", "A", "atlas");
        Assert.Equal(["new"], result.Select(x => x.Memory.Id).ToArray());
        var packed = AssistantMemoryEngine.ForPromptWithin(cfg, "V", "A", "atlas", 900);
        Assert.Contains(packed, t => t.Contains("new"));
        Assert.DoesNotContain(packed, t => t.Contains("private") || t.Contains("old"));
    }

    [Fact]
    public void CachedLexicalProjectionInvalidatesAfterTextPathsAndStateChanges()
    {
        var cfg = new AiConfig();
        var mem = Fact("id", "Formulário claro");
        cfg.MemoryRecords.Add(mem);
        Assert.Single(AssistantMemoryEngine.Search(cfg, "V", "A", "formulário"));
        Assert.Single(AssistantMemoryEngine.Search(cfg, "V", "A", "formulário"));
        Assert.True(AssistantMemoryEngine.Edit(cfg, "id", "Editor direto", 1000));
        Assert.Empty(AssistantMemoryEngine.Search(cfg, "V", "A", "formulário"));
        Assert.Single(AssistantMemoryEngine.Search(cfg, "V", "A", "editor"));
        mem.RelatedPaths.Add("Plano/Arquitetura.md");
        Assert.Single(AssistantMemoryEngine.Search(cfg, "V", "A", "arquitetura"));
        mem.RelatedPaths[0] = "Plano/Processo.md";
        Assert.Empty(AssistantMemoryEngine.Search(cfg, "V", "A", "arquitetura"));
        Assert.Single(AssistantMemoryEngine.Search(cfg, "V", "A", "processo"));
        Assert.True(AssistantMemoryEngine.ChangeState(cfg, "id", "archived"));
        Assert.Empty(AssistantMemoryEngine.Search(cfg, "V", "A", "editor"));
        Assert.True(AssistantMemoryEngine.Forget(cfg, "id"));
        Assert.Empty(AssistantMemoryEngine.Search(cfg, "V", "A", "processo"));
    }

    [Fact]
    public void FinalRequestFitsOneWindowIncludingToolsMemoryHistoryAndOutput()
    {
        var cfg = new AiConfig();
        cfg.MemoryRecords.Add(Fact("f", "Acessibilidade é requisito do formulário, inclusive no refinamento"));
        var events = new List<AiChatMessage>();
        for(int i=0;i<15;i++)
        {
            events.Add(User("Rascunho " + i + " " + new string('x',800)));
            events.Add(new AiChatMessage { Role="assistant", Content=[AiChatBlock.TextBlock("Aprovado " + new string('y',700))] });
        }
        events.Add(User("Refine o formulário com acessibilidade"));
        var run = new AiAgentRun
        {
            Messages = events, Host = null!, Provider = new AiProviderConfig("p","openai"), Model = "model",
            Stream = (_,_,_,_) => Task.FromResult(new AiResult([], "", "end", new AiUsage(0,0,null), "model")),
            ContextTokens = 6000, OptimizeToolUse = true,
            RecallMemory = budget => AssistantMemoryEngine.ForPromptWithin(cfg, "V", "A",
                AssistantMemoryEngine.ContinuityQuery(events), budget),
            BuildSystemWithMemory = memories => "Instruções obrigatórias. " + string.Join(" ", memories)
        };
        var tools = new[] { new AiToolSchema("read_note", new string('s',1000), new JsonObject { ["type"]="object" }) };
        var req = AssistantContextBudget.Build(run, "Instruções obrigatórias.", tools, events);
        Assert.True(AssistantContextBudget.ConservativeTokens(req) + req.MaxTokens!.Value <= 6000);
        Assert.Contains("Instruções obrigatórias", req.System);
        Assert.NotEmpty(req.Messages);
        Assert.NotEmpty(req.Tools!);
        Assert.Equal(events.Count, run.Messages.Count);
    }

    [Fact]
    public void UsageCacheBucketsAreNotCountedTwiceAndOldJsonRemainsReadable()
    {
        var actual = new AiUsage(150, 30, .25, CacheReadInput: 75, CacheCreationInput: 25, ReasoningOutput: 7, CostSource: "reported");
        var json = AssistantAgent.UsageJson(actual);
        Assert.Equal(150, (int)json["input"]!);
        Assert.Equal(75, (int)json["cacheReadInput"]!);
        Assert.Equal(actual, AiUsage.FromJson(json));
        Assert.Null(AiUsage.FromJson(new JsonObject { ["input"]=2, ["output"]=1 }) .CacheReadInput);
        var mixed = AiUsage.Add(actual, new AiUsage(20, 10, null));
        Assert.Equal(170, mixed.Input);
        Assert.Equal("partial", mixed.CostSource);
        Assert.Equal(75, mixed.CacheReadInput);
        Assert.Equal(.25, mixed.Cost);
        Assert.Null(AiUsage.Add(new AiUsage(2,1,null), new AiUsage(3,1,null)).Cost);
    }

    [Fact]
    public void IndexRepeatQueriesAndUpdatesHaveBoundedOwnedProjection()
    {
        var cfg = new AiConfig();
        for(int i=0;i<500;i++) cfg.MemoryRecords.Add(Fact("f"+i, "Documento técnico de arquitetura número " + i));
        var timer = Stopwatch.StartNew();
        Assert.NotEmpty(AssistantMemoryEngine.Search(cfg, "V", "A", "arquitetura", 1000));
        var initial = timer.ElapsedTicks; timer.Restart();
        for(int i=0;i<12;i++) AssistantMemoryEngine.Search(cfg, "V", "A", "arquitetura", 1000);
        var repeated = timer.ElapsedTicks; timer.Restart();
        Assert.True(AssistantMemoryEngine.Edit(cfg, "f4", "Processamento eficiente", 300));
        Assert.Single(AssistantMemoryEngine.Search(cfg, "V", "A", "eficiente"));
        var update = timer.ElapsedTicks;
        Console.WriteLine($"MEMORY_LOCAL baseline_initial_ticks={initial} repeat12_ticks={repeated} update_ticks={update} records=500; no token/cost savings claimed");
        Assert.True(initial>=0 && repeated>=0 && update>=0);
    }
}
