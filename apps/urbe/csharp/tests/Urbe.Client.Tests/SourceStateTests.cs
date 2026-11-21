using Urbe.Client;
using Urbe.Core.Assistant;

namespace Urbe.Client.Tests;

/// <summary>CST-124: the sources panel tells a search hit, a page read in part and a page read whole apart.</summary>
public sealed class SourceStateTests
{
    private static AiWebSource Source(string status) => new("S1", "T", "https://e.org", "https://e.org", "e.org", null, "2026-11-17", status);

    [Fact]
    public void EachReadingHasItsOwnWords()
    {
        var states = new[] { "página acessada", "leitura parcial", "resultado da busca; conteúdo da página NÃO verificado" }
            .Select(s => AssistantPanel.SourceState(Source(s))).ToList();
        Assert.Equal(3, states.Select(s => s.Short).Distinct().Count());
        Assert.Equal("Leitura parcial", states[1].Short);
        Assert.Contains("só uma parte", states[1].Long);
        Assert.Contains("não foi confirmado", states[2].Long);
    }
}
