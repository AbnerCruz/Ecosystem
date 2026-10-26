using Urbe.Core.Assistant;
using System.Text.Json.Nodes;
namespace Urbe.Core.Tests;
/// <summary>Red regressions: characterize actual 26/10 native C# behavior before repairs.</summary>
public sealed class MemoryQualityRegressionTests
{
    private static AiChatMessage User(string s) => new() { Role = "user", Content = [AiChatBlock.TextBlock(s)] };
    [Fact]
    public void ExactLexicalMatching_RioMustNotMatchInventario()
    {
        var cfg = new AiConfig();
        cfg.MemoryRecords.Add(new AiMemoryRecord { Id="unrelated", Scope="vault", Vault="V", Text="Inventário da loja", Kind="fact" });
        Assert.Empty(AssistantMemoryEngine.Search(cfg, "V", "a", "rio"));
    }
    [Fact]
    public void LongRelevantMemoryMustLeaveRecoverableReference()
    {
        var cfg = new AiConfig();
        cfg.MemoryRecords.Add(new AiMemoryRecord { Id="long-doc", Scope="vault", Vault="V", Kind="decision",
            Text="O formato deve ser compatível. " + new string('x', 5000), SourceReference="Projeto/Decisões.md" });
        var selected = AssistantMemoryEngine.ForPrompt(cfg, "V", "a", "formato compatível", 360);
        Assert.Contains(selected, t => t.Contains("long-doc") && t.Contains("Projeto/Decisões.md"));
    }
    [Fact]
    public void RefinementMustExposeEvidencePastFirst400Characters()
    {
        var original = new string('x', 1000) + "\nRESTRIÇÃO: não remover acessibilidade ao refinar o formulário\n" + new string('y', 1000);
        var c = new List<AiChatMessage>
        {
            User("Crie um formulário"),
            new() { Role="assistant", Content=[new AiChatBlock { Type="tool_use", Id="call-1", Name="read_note", Input=new JsonObject { ["path"]="Regras.md" } }] },
            new() { Role="user", Meta=new AiChatMeta { ToolResults=true }, Content=[new AiChatBlock { Type="tool_result", ToolUseId="call-1", Content=original }] },
            User("Refine o formulário sem remover acessibilidade")
        };
        var compacted=AssistantAgent.SettlePast(c);
        Assert.Contains("não remover acessibilidade",compacted[2].Content[0].Content);
        Assert.Equal(original,c[2].Content[0].Content);
    }
}
