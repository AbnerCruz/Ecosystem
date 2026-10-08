using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using AgentRuntime.Providers.ChatCompletions;

namespace AgentRuntime.Tests;

public class ChatCompletionsProviderTests
{
    private sealed class Handler(string json, HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        public string? Body { get; private set; }
        public string? Authorization { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            Authorization = request.Headers.Authorization?.Parameter;
            return new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        }
    }
    private static ModelRequest Request(params Message[] messages) => new(
        new ModelProfile("chat-completions", "model-id", new ModelCapabilities(true, false, true, false, false, 8_000),
            new Money(100, "USD")),
        "sistema", messages,
        [new ToolSpec("files.read", "Lê arquivo", """{"type":"object","properties":{"path":{"type":"string"}}}""")]);

    [Fact]
    public async Task Provider_traduz_tool_calls_versoes_e_custo_informado_sem_vazar_token_no_corpo()
    {
        const string json = """
        {"choices":[{"message":{"role":"assistant","content":null,"tool_calls":[{"id":"call-1","type":"function","function":{"name":"files.read","arguments":"{\"path\":\"README.md\"}"}}]},"finish_reason":"tool_calls"}],"usage":{"prompt_tokens":10,"completion_tokens":20,"cost":0.012}}
        """;
        var handler = new Handler(json);
        using var http = new HttpClient(handler);
        var provider = new ChatCompletionsProvider(http,
            new Uri("https://provider.example.test/v1/chat/completions"), "super-secret-token", 0, 0);
        var response = await provider.CompleteAsync(Request(Message.User("Leia")), CancellationToken.None);
        Assert.Equal(StopReason.ToolUse, response.Stop);
        var call = Assert.IsType<ToolUseBlock>(Assert.Single(response.Content));
        Assert.Equal("call-1", call.Id);
        Assert.Equal("README.md", call.Input.GetProperty("path").GetString());
        Assert.Equal(2, response.Usage.Cost.Minor);
        Assert.False(provider.LastCostEstimated);
        Assert.Equal("super-secret-token", handler.Authorization);
        Assert.DoesNotContain("super-secret-token", handler.Body!);
        using var sent = JsonDocument.Parse(handler.Body!);
        Assert.Equal("sistema", sent.RootElement.GetProperty("messages")[0].GetProperty("content").GetString());
        Assert.Equal("files.read", sent.RootElement.GetProperty("tools")[0].GetProperty("function").GetProperty("name").GetString());
    }

    [Fact]
    public async Task Conversa_usa_tool_role_no_retorno_e_nao_finge_contexto_sem_escopo()
    {
        const string json = """{"choices":[{"message":{"content":"Feito"},"finish_reason":"stop"}],"usage":{"prompt_tokens":100,"completion_tokens":50}}""";
        var handler = new Handler(json);
        using var http = new HttpClient(handler);
        var provider = new ChatCompletionsProvider(http, new Uri("https://provider.example.test/v1/chat/completions"), null, 1m, 2m);
        var toolUse = new ToolUseBlock("call-2", "files.read", JsonDocument.Parse("""{"path":"README.md"}""").RootElement.Clone());
        var response = await provider.CompleteAsync(Request(Message.User("Leia"),
            new Message(MessageRole.Assistant, [toolUse]),
            new Message(MessageRole.User, [new ToolResultBlock("call-2", "conteúdo", false)])), CancellationToken.None);
        Assert.Equal("Feito", Assert.IsType<TextBlock>(Assert.Single(response.Content)).Text);
        Assert.True(provider.LastCostEstimated);
        Assert.Equal(1, response.Usage.Cost.Minor);
        using var sent = JsonDocument.Parse(handler.Body!);
        var messages = sent.RootElement.GetProperty("messages");
        Assert.Equal("assistant", messages[2].GetProperty("role").GetString());
        Assert.Equal("tool", messages[3].GetProperty("role").GetString());
        Assert.Equal("call-2", messages[3].GetProperty("tool_call_id").GetString());
    }

    [Fact]
    public async Task Resposta_sem_custo_e_sem_tarifas_falha_fechada()
    {
        const string json = """{"choices":[{"message":{"content":"ok"},"finish_reason":"stop"}],"usage":{"prompt_tokens":100,"completion_tokens":20}}""";
        using var http = new HttpClient(new Handler(json));
        var provider = new ChatCompletionsProvider(http, new Uri("https://provider.example.test/v1/chat/completions"), null, 0, 0);
        var ex = await Assert.ThrowsAsync<ProviderException>(async () => await provider.CompleteAsync(Request(Message.User("a")), CancellationToken.None));
        Assert.Equal("cost-unknown", ex.Kind);
    }

    [Fact]
    public async Task HTTP_de_erro_nao_copia_corpo_com_segredos()
    {
        using var http = new HttpClient(new Handler("token-na-resposta", HttpStatusCode.TooManyRequests));
        var provider = new ChatCompletionsProvider(http, new Uri("https://provider.example.test/v1/chat/completions"), "secret-value", 1, 1);
        var ex = await Assert.ThrowsAsync<ProviderException>(async () => await provider.CompleteAsync(Request(Message.User("a")), CancellationToken.None));
        Assert.Equal("http-429", ex.Kind);
        Assert.True(ex.IsTransient);
        Assert.DoesNotContain("token-na-resposta", ex.Message);
    }

    [Fact]
    public void Endpoint_inseguro_recusado_com_excecao_para_loopback()
    {
        using var http = new HttpClient();
        Assert.Throws<ArgumentException>(() => new ChatCompletionsProvider(http, new Uri("http://example.com/v1/chat/completions"), null, 1, 1));
        Assert.NotNull(new ChatCompletionsProvider(http, new Uri("http://localhost:11434/v1/chat/completions"), null, 1, 1));
        Assert.Throws<ArgumentException>(() => new ChatCompletionsProvider(http, new Uri("https://evil:token@example.com/v1/chat/completions"), null, 1, 1));
    }

    [Fact]
    public async Task Arguments_invalidos_e_resposta_sem_mensagem_sao_recusados()
    {
        const string json = """
        {"choices":[{"message":{"tool_calls":[{"id":"x","function":{"name":"files.read","arguments":"not-json"}}]}}],"usage":{"cost":0}}
        """;
        using var http = new HttpClient(new Handler(json));
        var provider = new ChatCompletionsProvider(http, new Uri("https://provider.example.test/v1/chat/completions"), null, 1, 1);
        var ex = await Assert.ThrowsAsync<ProviderException>(async () => await provider.CompleteAsync(Request(Message.User("a")), CancellationToken.None));
        Assert.Equal("bad-response", ex.Kind);
    }

    [Fact]
    public async Task Cli_help_e_exigencia_de_configuracao_fechada()
    {
        Assert.Equal(0, await EcosystemAiCli.RunAsync(["--help"]));
        Assert.Equal(2, await EcosystemAiCli.RunAsync(["--project", "/nao/existe", "--goal", "x"]));
    }
}
