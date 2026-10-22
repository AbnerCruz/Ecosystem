using System.Text.Json.Nodes;
using Urbe.Core.Assistant;

namespace Urbe.Core.Tests;

/// <summary>
/// Observation of 22/10/2026: eleven requests in one conversation took 5,216k tokens and the app slowed down, because every
/// step sent again every earlier result and every page written. Earlier requests now go settled; the current one, whole.
/// </summary>
public sealed class AssistantContextTests
{
    private static AiChatMessage User(string text) => new() { Id = "u" + text.Length, Role = "user", Content = [AiChatBlock.TextBlock(text)] };

    private static AiChatMessage Call(string id, string name, JsonObject input) =>
        new() { Id = "a" + id, Role = "assistant", Content = [new AiChatBlock { Type = "tool_use", Id = id, Name = name, Input = input }] };

    private static AiChatMessage Result(string id, string content) =>
        new() { Id = "r" + id, Role = "user", Meta = new AiChatMeta { ToolResults = true }, Content = [new AiChatBlock { Type = "tool_result", ToolUseId = id, Content = content }] };

    private static AiChatMessage Answer(string text) => new() { Id = "t" + text.Length, Role = "assistant", Content = [AiChatBlock.TextBlock(text)] };

    private static readonly string Big = new('x', 50_000);

    private static List<AiChatMessage> TwoRequests() =>
    [
        User("crie a apostila"),
        Call("c1", "read_note", new JsonObject { ["path"] = "Matriz.md" }),
        Result("c1", Big),
        Call("c2", "write_page", new JsonObject { ["path"] = "Apostila.page.json", ["spec"] = new JsonObject { ["blocks"] = new JsonArray(Big) } }),
        Result("c2", "ok"),
        Answer("Pronto."),
        User("continue o módulo 1"),
        Call("c3", "read_note", new JsonObject { ["path"] = "Plano.md" }),
        Result("c3", Big)
    ];

    [Fact]
    public void EarlierRequestsGoSettledAndTheCurrentOneWhole()
    {
        var conv = TwoRequests();
        var sent = AssistantAgent.SettlePast(conv);
        Assert.Equal(conv.Count, sent.Count);
        // the earlier read and the page written: a short head and where to see the current version
        Assert.True(sent[2].Content[0].Content.Length < 600);
        Assert.Contains("pedido anterior", sent[2].Content[0].Content);
        var spec = sent[3].Content[0].Input!["spec"]!.GetValue<string>();
        Assert.True(spec.Length < 200);
        Assert.Contains("caracteres já gravados", spec);
        Assert.Equal("Apostila.page.json", (string)sent[3].Content[0].Input!["path"]!);
        // the call and its result stay paired; the short ones are the same blocks
        Assert.Equal("c2", sent[3].Content[0].Id);
        Assert.Same(conv[4], sent[4]);
        // the current request, whole
        Assert.Same(conv[8], sent[8]);
        Assert.Equal(Big, sent[8].Content[0].Content);
        // the conversation itself is not changed
        Assert.Equal(Big, conv[2].Content[0].Content);
        Assert.IsType<JsonObject>(conv[3].Content[0].Input!["spec"]);
    }

    [Fact]
    public void AReminderFromTheLoopIsNotANewRequest()
    {
        var conv = TwoRequests();
        conv.Add(new AiChatMessage { Id = "n", Role = "user", Meta = new AiChatMeta { ToolResults = true }, Content = [AiChatBlock.TextBlock("[Contexto do sistema] Leia o restante")] });
        var sent = AssistantAgent.SettlePast(conv);
        Assert.Same(conv[8], sent[8]);
        Assert.True(sent[2].Content[0].Content.Length < 600);
    }

    [Fact]
    public void TheFirstRequestGoesWhole()
    {
        var conv = TwoRequests().Take(6).ToList();
        var sent = AssistantAgent.SettlePast(conv);
        for (int i = 0; i < conv.Count; i++) Assert.Same(conv[i], sent[i]);
    }

    [Fact]
    public void ASecondRequestSendsFarLess()
    {
        var conv = TwoRequests();
        static int Size(IEnumerable<AiMessage> ms) => ms.Sum(m => m.Content.Sum(b => b.Text.Length + b.Content.Length + (b.Input?.ToJsonString().Length ?? 0)));
        var before = Size(AssistantAgent.Prepare(conv, 10_000_000));
        var after = Size(AssistantAgent.Prepare(AssistantAgent.SettlePast(conv), 10_000_000));
        Assert.True(after < before / 2, $"{after} de {before}");
        Assert.True(after > Big.Length, "a leitura do pedido atual vai inteira");
    }
}
