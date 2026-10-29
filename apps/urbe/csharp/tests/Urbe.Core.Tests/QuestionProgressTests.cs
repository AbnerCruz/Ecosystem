using System.Text.Json.Nodes;
using Urbe.Core.Assistant;

namespace Urbe.Core.Tests;

/// <summary>
/// CST-115: the question card shows how many doubts are cleared, the understanding the agent declares, the doubts
/// still open, and what changed since the previous question (doubts that appeared or were cleared).
/// </summary>
public sealed class QuestionProgressTests
{
    private static JsonObject Ask(int resolved, int total, int? understanding = null, params string[] pending)
    {
        var o = new JsonObject { ["question"] = "?", ["resolved"] = resolved, ["total"] = total };
        if (understanding is not null) o["understanding"] = understanding;
        if (pending.Length > 0) o["pending"] = new JsonArray(pending.Select(p => (JsonNode)p).ToArray());
        return o;
    }

    [Fact]
    public void TheFirstQuestionShowsWhereThingsStand()
    {
        var p = QuestionProgress.From(Ask(1, 4, 40, "público", "prazo", "orçamento"), null);
        Assert.Equal("1 de 4 dúvidas esclarecidas · compreensão declarada 40%", p.Summary);
        Assert.Equal(["público", "prazo", "orçamento"], p.Pending);
        Assert.Null(p.Change);
    }

    [Fact]
    public void ALaterQuestionSaysWhatChanged()
    {
        var before = Ask(1, 4, 40, "público", "prazo", "orçamento");
        var p = QuestionProgress.From(Ask(3, 5, 70, "orçamento", "formato"), before);
        Assert.Equal("Desde a pergunta anterior: 2 esclarecidas · 1 nova (formato)", p.Change);
        var same = QuestionProgress.From(Ask(1, 4, 40, "público", "prazo", "orçamento"), before);
        Assert.Null(same.Change);
    }

    [Fact]
    public void OddNumbersAreKeptSane()
    {
        var p = QuestionProgress.From(new JsonObject { ["resolved"] = 9, ["total"] = 3, ["understanding"] = 180 }, null);
        Assert.Equal(3, p.Resolved);
        Assert.Equal(100, p.Understanding);
        Assert.Equal("3 de 3 dúvidas esclarecidas · compreensão declarada 100%", p.Summary);
        Assert.Equal("1 de 1 dúvida esclarecida", QuestionProgress.From(Ask(1, 1), null).Summary);
    }
}
