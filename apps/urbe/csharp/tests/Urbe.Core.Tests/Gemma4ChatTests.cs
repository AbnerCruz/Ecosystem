using System.Text.Json;
using System.Text.Json.Nodes;
using Urbe.Core.Assistant;

namespace Urbe.Core.Tests;

/// <summary>
/// The prompt of the on-device model is the one Google's own chat template writes (tests/fixtures/gemma4-chat, made by
/// make.py from chat_template.jinja), and the model's notation for tool calls is read back.
/// </summary>
public sealed class Gemma4ChatTests
{
    private static string Fixtures
    {
        get
        {
            var d = new DirectoryInfo(AppContext.BaseDirectory);
            while (d is not null && !File.Exists(Path.Combine(d.FullName, "Urbe.Portable.slnx"))) d = d.Parent;
            return Path.Combine(d!.FullName, "tests", "fixtures", "gemma4-chat", "cases.json");
        }
    }

    private static AiRequest Request(JsonNode r)
    {
        var tools = (r["tools"] as JsonArray)?.Select(t => new AiToolSchema((string)t!["name"]!, (string)t["description"]!, (JsonObject)t["parameters"]!.DeepClone())).ToList();
        var messages = ((JsonArray)r["messages"]!).Select(m => new AiMessage((string)m!["role"]!, ((JsonArray)m["content"]!).Select(b => (string)b!["type"]! switch
        {
            "text" => AiBlock.TextBlock((string)b["text"]!),
            "tool_use" => AiBlock.ToolUse((string)b["id"]!, (string)b["name"]!, b["input"]!.DeepClone()),
            _ => AiBlock.ToolResult((string)b["tool_use_id"]!, (string)b["content"]!, false)
        }).ToList())).ToList();
        return new AiRequest("local", messages, (string?)r["system"], tools);
    }

    public static IEnumerable<object[]> Cases() =>
        ((JsonObject)JsonNode.Parse(File.ReadAllText(Fixtures))!).Select(p => new object[] { p.Key });

    [Theory]
    [MemberData(nameof(Cases))]
    public void ThePromptIsTheTemplatesOwn(string name)
    {
        var all = (JsonObject)JsonNode.Parse(File.ReadAllText(Fixtures))!;
        var c = all[name]!;
        Assert.Equal((string)c["expected"]!, Gemma4Chat.Render(Request(c["request"]!)));
    }

    [Fact]
    public void AToolCallIsReadWithItsArguments()
    {
        var (text, reasoning, calls) = Gemma4Chat.Parse("<|channel>thought\nvou ler<channel|>Vou olhar.<|tool_call>call:buscar{consulta:<|\"|>plano de ação<|\"|>,exata:true,limite:3,pastas:[<|\"|>x<|\"|>,<|\"|>y<|\"|>],opcoes:{aviso:null,salvar:false}}<tool_call|><|tool_response>");
        Assert.Equal("Vou olhar.", text);
        Assert.Equal("vou ler", reasoning);
        var call = Assert.Single(calls);
        Assert.Equal("buscar", call.Name);
        var args = (JsonObject)call.Input!;
        Assert.Equal("plano de ação", (string)args["consulta"]!);
        Assert.True((bool)args["exata"]!);
        Assert.Equal(3, (int)args["limite"]!);
        Assert.Equal(["x", "y"], ((JsonArray)args["pastas"]!).Select(v => (string)v!));
        Assert.Null(args["opcoes"]!["aviso"]);
        Assert.False((bool)args["opcoes"]!["salvar"]!);
    }

    [Fact]
    public void SeveralCallsAndAPlainAnswer()
    {
        var (_, _, calls) = Gemma4Chat.Parse("<|tool_call>call:a{x:<|\"|>1<|\"|>}<tool_call|><|tool_call>call:b{}<tool_call|>");
        Assert.Equal(["a", "b"], calls.Select(c => c.Name));
        Assert.NotEqual(calls[0].Id, calls[1].Id);
        var (text, reasoning, none) = Gemma4Chat.Parse("Olá! Tudo bem?<turn|>");
        Assert.Equal("Olá! Tudo bem?", text);
        Assert.Equal("", reasoning);
        Assert.Empty(none);
    }

    [Fact]
    public void AStringWithBracesAndQuotesSurvives()
    {
        var (_, _, calls) = Gemma4Chat.Parse("<|tool_call>call:editar{trocas:[{de:<|\"|>a {b} \"c\"<|\"|>,para:<|\"|>d<|\"|>}]}<tool_call|>");
        Assert.Equal("a {b} \"c\"", (string)((JsonArray)((JsonObject)calls[0].Input!)["trocas"]!)[0]!["de"]!);
    }
}

/// <summary>The answer as it streams: text and reasoning apart, marks and tool calls never shown, even cut in two.</summary>
public sealed class Gemma4StreamerTests
{
    private static (string Text, string Reasoning) Run(params string[] pieces)
    {
        var s = new Gemma4Chat.Streamer();
        var text = new System.Text.StringBuilder();
        var reasoning = new System.Text.StringBuilder();
        void Take(IEnumerable<(string Kind, string Text)> events) { foreach (var (k, t) in events) (k == "text" ? text : reasoning).Append(t); }
        foreach (var p in pieces) Take(s.Push(p));
        Take(s.Flush());
        return (text.ToString(), reasoning.ToString());
    }

    [Fact]
    public void PlainTextPassesThrough() => Assert.Equal(("Olá, tudo bem? 2 < 3", ""), Run("Olá, ", "tudo bem? ", "2 < 3"));

    [Fact]
    public void MarksCutInPiecesNeverShow()
    {
        var (text, reasoning) = Run("<|chan", "nel>thou", "ght\npenso um pouco<chan", "nel|>Res", "posta<tur", "n|>");
        Assert.Equal("Resposta", text);
        Assert.Equal("penso um pouco", reasoning);
    }

    [Fact]
    public void AToolCallIsHeldBackFromTheText()
    {
        var (text, _) = Run("Vou ver.<|tool_c", "all>call:ler{caminho:<|\"|>a<|\"|>}<tool_call|>", "<|tool_response>");
        Assert.Equal("Vou ver.", text);
    }
}
