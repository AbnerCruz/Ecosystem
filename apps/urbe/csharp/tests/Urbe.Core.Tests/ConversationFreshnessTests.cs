using Urbe.Core.Assistant;

namespace Urbe.Core.Tests;

/// <summary>CST-145: when a new chat with a summary is more practical than going on, and what it starts with.</summary>
public sealed class ConversationFreshnessTests
{
    private const long Hour = 3_600_000;
    private const long Now = 1_800_000_000_000;

    private static AiChatMessage Msg(string role, string text, string? context = null) => new()
    {
        Id = AssistantAgent.NewId("m"), Role = role, Content = [AiChatBlock.TextBlock(text)],
        Meta = context is null ? null : new AiChatMeta { Context = context }
    };

    private static AiConversation Chat(int turns, string question = "Como organizo o orçamento da viagem a Lisboa?",
        string answer = "Separe o orçamento da viagem em transporte, hospedagem e passeios.", long updated = Now)
    {
        var c = new AiConversation { Id = "c-velha", Title = "Orçamento da viagem", Updated = updated };
        for (int i = 0; i < turns; i++) { c.Messages.Add(Msg("user", question)); c.Messages.Add(Msg("assistant", answer)); }
        return c;
    }

    [Fact]
    public void AConversationNearTheWindowIsLong()
    {
        var room = ConversationFreshness.HistoryRoom(32000);
        Assert.Equal((32000 - 8000) * 3 / 2, room);   // a quarter of a small window is the answer's reserve
        var c = Chat(3, answer: new string('x', (int)(room * 0.75)));
        var hint = ConversationFreshness.Evaluate(c, 32000, Now);
        Assert.NotNull(hint);
        Assert.Equal(ConversationHintReason.Long, hint.Reason);
        Assert.Equal("Esta conversa está longa: começar uma nova com um resumo?", hint.Text);
        Assert.InRange(hint.Fill, 0.7, 1);
        // the same conversation for a model with a big window is still the right place
        Assert.Null(ConversationFreshness.Evaluate(c, 1_000_000, Now));
    }

    [Fact]
    public void TheRoomNeverPassesTheAgentsWorkingHistory()
    {
        Assert.Equal(AssistantAgent.WorkingChars, ConversationFreshness.HistoryRoom(2_000_000));
        Assert.Equal(ConversationFreshness.HistoryRoom(128000), ConversationFreshness.HistoryRoom(null));
        Assert.Equal(ConversationFreshness.HistoryRoom(128000), ConversationFreshness.HistoryRoom(0));
    }

    [Fact]
    public void ToolResultsAndAttachmentsCountAsTheRequestCarriesThem()
    {
        var c = Chat(3);
        long before = ConversationFreshness.HistoryChars(c);
        c.Messages.Add(new AiChatMessage { Id = "t", Role = "user", Meta = new AiChatMeta { ToolResults = true },
            Content = [new AiChatBlock { Type = "tool_result", ToolUseId = "u1", Content = new string('y', 40000) }] });
        Assert.True(ConversationFreshness.HistoryChars(c) >= before + 40000);
        Assert.Equal(ConversationHintReason.Long, ConversationFreshness.Evaluate(c, 32000, Now)!.Reason);
        // a cached size is used as given
        Assert.Null(ConversationFreshness.Evaluate(c, 32000, Now, historyChars: 10));
    }

    [Fact]
    public void AShortConversationIsNeverWorthLeaving()
    {
        var c = Chat(2, answer: new string('x', 200000));   // four messages, however big
        Assert.Null(ConversationFreshness.Evaluate(c, 32000, Now));
        Assert.Null(ConversationFreshness.Evaluate(Chat(2, updated: Now - 48 * Hour), 128000, Now, "Receita de bolo de cenoura com cobertura de chocolate"));
    }

    [Fact]
    public void ANewSubjectAfterALongPauseSuggestsANewChat()
    {
        var c = Chat(4, updated: Now - 7 * Hour);
        var hint = ConversationFreshness.Evaluate(c, 128000, Now, "Receita de bolo de cenoura com cobertura de chocolate");
        Assert.NotNull(hint);
        Assert.Equal(ConversationHintReason.NewTopicAfterPause, hint.Reason);
        Assert.Equal(ConversationFreshness.NewTopicText, hint.Text);
    }

    [Fact]
    public void TheSameSubjectOrAShortPauseOrAShortRequestKeepsTheConversation()
    {
        var old = Chat(4, updated: Now - 7 * Hour);
        // the same subject, with other forms of the words (orçamentos, viagens) and accents left out
        Assert.Null(ConversationFreshness.Evaluate(old, 128000, Now, "E os orcamentos das viagens de trem?"));
        // a new subject, but the person was here an hour ago
        Assert.Null(ConversationFreshness.Evaluate(Chat(4, updated: Now - Hour), 128000, Now, "Receita de bolo de cenoura com cobertura de chocolate"));
        // "ok, e agora?" says nothing about a subject
        Assert.Null(ConversationFreshness.Evaluate(old, 128000, Now, "ok, e agora?"));
        Assert.Null(ConversationFreshness.Evaluate(old, 128000, Now, "Quero que você faça isso para mim agora, por favor"));
        Assert.Null(ConversationFreshness.Evaluate(old, 128000, Now));
    }

    [Fact]
    public void AConsultationBetweenAgentsGetsNoHint()
    {
        var c = Chat(3, answer: new string('x', 60000));
        c.OriginConversationId = "c-origem";
        Assert.Null(ConversationFreshness.Evaluate(c, 32000, Now));
    }

    [Fact]
    public void TheSeedSummarisesTheOldConversationAndKeepsWhatWasTyped()
    {
        var c = new AiConversation { Id = "c-123", Title = "Orçamento da viagem", Updated = Now };
        c.Messages.Add(Msg("user", "Como organizo o orçamento\nda viagem?", "Viagens/Lisboa.md · seleção"));
        c.Messages.Add(Msg("assistant", "Separe em transporte e hospedagem."));
        c.Messages.Add(new AiChatMessage { Id = "h", Role = "user", Content = [new AiChatBlock { Type = "text", Text = "contexto interno", Hidden = true }] });
        c.Messages.Add(Msg("user", "Some os valores da planilha", "Viagens/Lisboa.md"));
        c.Messages.Add(Msg("assistant", "Total: 4 200 euros. " + new string('a', 400)));
        c.Runs.Add(new AiRunRecord { Changes = [new AiChange("x", null, new AiNoteSnap("Viagens/Custos.md", "…"))] });

        var seed = ConversationFreshness.Seed(c, "  e o seguro?  ");
        Assert.StartsWith("Continuando a conversa “Orçamento da viagem” de ", seed);
        Assert.Contains("(id: c-123; os detalhes estão nela).", seed);
        Assert.Contains("- Começou com: Como organizo o orçamento da viagem?\n", seed);
        Assert.Contains("- Último pedido: Some os valores da planilha\n", seed);
        Assert.Contains("- Última resposta: Total: 4 200 euros.", seed);
        Assert.Contains("…\n", seed);                                          // a long answer is cut
        Assert.Contains("- Notas: [[Viagens/Custos]], [[Viagens/Lisboa]]\n", seed);
        Assert.DoesNotContain("contexto interno", seed);                      // hidden text stays out
        Assert.EndsWith("\n\ne o seguro?", seed);
        // the old conversation is not changed
        Assert.Equal(5, c.Messages.Count);
        Assert.StartsWith("Continuando a conversa “sem título” (id: c-0;", ConversationFreshness.Seed(new AiConversation { Id = "c-0" }));
    }
}
