using Urbe.Core.Assistant;

namespace Urbe.Core.Tests;

/// <summary>Owner's report (Alice, beta.54): a specific preference is not a contradiction of the general one.</summary>
public sealed class MemoryRelationsTests
{
    private static AiConfig With(params string[] facts)
    {
        var cfg = new AiConfig();
        foreach (var f in facts) AssistantMemoryEngine.Remember(cfg, f, "v", kind: "preference");
        return cfg;
    }

    private static string[] Pairs(AiConfig cfg) => AssistantMemoryEngine.PotentialConflicts(cfg, "v", "geral")
        .Select(p => string.Join(" ⇄ ", new[] { p.Left.Text, p.Right.Text }.Order())).ToArray();

    [Fact]
    public void AGeneralAndItsOppositeAreAContradiction() =>
        Assert.Equal(["gosta de strogonoff ⇄ não gosta de strogonoff"], Pairs(With("gosta de strogonoff", "não gosta de strogonoff")));

    [Fact]
    public void AMoreSpecificPreferenceRefinesTheGeneralOne()
    {
        var pairs = Pairs(With("gosta de strogonoff", "gosta de strogonoff de frango, mas não de porco"));
        Assert.Empty(pairs);
    }

    [Fact]
    public void TheOwnersThreeMemoriesShowOnlyTheRealConflict()
    {
        var pairs = Pairs(With("gosta de strogonoff", "não gosta de strogonoff", "gosta de strogonoff de frango, mas não de porco"));
        Assert.Contains("gosta de strogonoff ⇄ não gosta de strogonoff", pairs);
        Assert.DoesNotContain(pairs, p => p.Contains("frango") && p.StartsWith("gosta de strogonoff ⇄", StringComparison.Ordinal) && !p.Contains("não gosta de strogonoff ⇄"));
    }
}
