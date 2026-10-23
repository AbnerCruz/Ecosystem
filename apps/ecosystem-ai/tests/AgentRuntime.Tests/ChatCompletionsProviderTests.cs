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

    private sealed class SequenceHandler(params string[] replies) : HttpMessageHandler
    {
        private readonly Queue<string> _replies = new(replies);
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Calls++;
            if (_replies.Count == 0) throw new InvalidOperationException("Resposta HTTP inesperada.");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_replies.Dequeue(), Encoding.UTF8, "application/json")
            });
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
        var json = """
        {"choices":[{"message":{"role":"assistant","content":null,"tool_calls":[{"id":"call-1","type":"function","function":{"name":"WIRE_NAME","arguments":"{\"path\":\"README.md\"}"}}]},"finish_reason":"tool_calls"}],"usage":{"prompt_tokens":10,"completion_tokens":20,"cost":0.012}}
        """.Replace("WIRE_NAME", ChatCompletionsProvider.WireName("files.read"), StringComparison.Ordinal);
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
        Assert.Equal(ChatCompletionsProvider.WireName("files.read"), sent.RootElement.GetProperty("tools")[0].GetProperty("function").GetProperty("name").GetString());
        Assert.Matches("^[a-zA-Z0-9_-]{1,64}$", ChatCompletionsProvider.WireName("files.read"));
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
        Assert.Equal(ChatCompletionsProvider.WireName("files.read"), messages[2].GetProperty("tool_calls")[0].GetProperty("function").GetProperty("name").GetString());
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
    public async Task Truncamento_e_filtro_falham_sem_declarar_conclusao()
    {
        foreach (var reason in new[] { "length", "content_filter" })
        {
            var json = """{"choices":[{"message":{"content":"parcial"},"finish_reason":"REASON"}],"usage":{"cost":0.01}}""".Replace("REASON", reason);
            using var http = new HttpClient(new Handler(json));
            var provider = new ChatCompletionsProvider(http, new Uri("https://provider.example.test/v1/chat/completions"), null, 1, 1);
            var ex = await Assert.ThrowsAsync<ProviderException>(async () => await provider.CompleteAsync(Request(Message.User("texto")), CancellationToken.None));
            Assert.Equal(reason == "length" ? "truncated" : "filtered", ex.Kind);
            Assert.False(ex.IsTransient);
        }
    }

    [Theory]
    [InlineData("{\"cost\":0.01}", false)]
    [InlineData("{\"cost\":0.01,\"prompt_tokens\":10}", false)]
    [InlineData("{\"cost\":0.01,\"prompt_tokens\":0,\"completion_tokens\":0}", true)]
    [InlineData("{\"cost\":0.01,\"prompt_tokens\":10,\"completion_tokens\":2}", true)]
    public async Task Custo_informado_nao_inventa_medicao_de_tokens(string usage, bool known)
    {
        var json = "{\"choices\":[{\"message\":{\"content\":\"ok\"},\"finish_reason\":\"stop\"}],\"usage\":" + usage + "}";
        using var client = new HttpClient(new Handler(json));
        var result = await new ChatCompletionsProvider(client, new Uri("https://provider.example.test/chat/completions"), null, 1, 1)
            .CompleteAsync(Request(Message.User("x")), CancellationToken.None);
        Assert.Equal(known, result.Usage.TokensKnown);
    }

    [Theory]
    [InlineData("{\"cost\":0.01,\"prompt_tokens\":9223372036854775808,\"completion_tokens\":1}")]
    [InlineData("{\"cost\":0.01,\"prompt_tokens\":1.5,\"completion_tokens\":1}")]
    [InlineData("{\"cost\":0.01,\"prompt_tokens\":\"secret-token\",\"completion_tokens\":1}")]
    [InlineData("{\"prompt_tokens\":10}")]
    public async Task Tokens_malformados_ou_tarifa_com_contagem_parcial_falham_fechados(string usage)
    {
        var json = "{\"choices\":[{\"message\":{\"content\":\"ok\"},\"finish_reason\":\"stop\"}],\"usage\":" + usage + "}";
        using var client = new HttpClient(new Handler(json));
        var error = await Assert.ThrowsAsync<ProviderException>(async () => await new ChatCompletionsProvider(client,
            new Uri("https://provider.example.test/chat/completions"), null, 1, 1).CompleteAsync(Request(Message.User("x")), CancellationToken.None));
        Assert.False(error.IsTransient);
        Assert.DoesNotContain("secret-token", error.Message);
    }


    [Theory]
    [InlineData("""{"cost":"0.10","prompt_tokens":10,"completion_tokens":10}""")]
    [InlineData("""{"cost":true,"prompt_tokens":10,"completion_tokens":10}""")]
    [InlineData("""{"cost":1e100,"prompt_tokens":10,"completion_tokens":10}""")]
    public async Task Custo_explicito_invalido_nao_se_transforma_em_estimativa(string usage)
    {
        var json = "{\"choices\":[{\"message\":{\"content\":\"ok\"},\"finish_reason\":\"stop\"}],\"usage\":" + usage + "}";
        using var client = new HttpClient(new Handler(json));
        var provider = new ChatCompletionsProvider(client,
            new Uri("https://provider.example.test/chat/completions"), null, 1, 1);
        var error = await Assert.ThrowsAsync<ProviderException>(async () =>
            await provider.CompleteAsync(Request(Message.User("texto")), CancellationToken.None));
        Assert.Equal("bad-response", error.Kind);
        Assert.False(error.IsTransient);
        Assert.False(provider.LastCostEstimated);
    }

    [Fact]
    public async Task Custo_null_continua_podendo_ser_estimado_com_tokens_e_tarifas()
    {
        const string json = """{"choices":[{"message":{"content":"ok"},"finish_reason":"stop"}],"usage":{"cost":null,"prompt_tokens":10,"completion_tokens":20}}""";
        using var client = new HttpClient(new Handler(json));
        var provider = new ChatCompletionsProvider(client,
            new Uri("https://provider.example.test/chat/completions"), null, 1, 1);
        var response = await provider.CompleteAsync(Request(Message.User("texto")), CancellationToken.None);
        Assert.True(provider.LastCostEstimated);
        Assert.Equal(1, response.Usage.Cost.Minor);
    }

    [Theory]
    [InlineData("files.read")]
    [InlineData("invented_tool")]
    public async Task Provider_nao_pode_invocar_tool_nao_anunciada(string unadvertised)
    {
        var json = """
        {"choices":[{"message":{"tool_calls":[{"id":"call-x","function":{"name":"WIRE_NAME","arguments":"{}"}}]},"finish_reason":"tool_calls"}],"usage":{"cost":0}}
        """.Replace("WIRE_NAME", unadvertised, StringComparison.Ordinal);
        using var client = new HttpClient(new Handler(json));
        var provider = new ChatCompletionsProvider(client,
            new Uri("https://provider.example.test/chat/completions"), null, 1, 1);
        var error = await Assert.ThrowsAsync<ProviderException>(async () =>
            await provider.CompleteAsync(Request(Message.User("texto")), CancellationToken.None));
        Assert.Equal("bad-response", error.Kind);
        Assert.False(error.IsTransient);
    }

    [Fact]
    public async Task Finish_tool_calls_sem_chamada_nao_simula_conclusao()
    {
        const string json = """{"choices":[{"message":{"content":"parcial"},"finish_reason":"tool_calls"}],"usage":{"cost":0}}""";
        using var client = new HttpClient(new Handler(json));
        var provider = new ChatCompletionsProvider(client,
            new Uri("https://provider.example.test/chat/completions"), null, 1, 1);
        var error = await Assert.ThrowsAsync<ProviderException>(async () =>
            await provider.CompleteAsync(Request(Message.User("texto")), CancellationToken.None));
        Assert.Equal("bad-response", error.Kind);
        Assert.False(error.IsTransient);
    }

    [Fact]
    public async Task Tool_calls_de_tipo_invalido_nao_sao_ignoradas()
    {
        const string json = """{"choices":[{"message":{"content":"texto","tool_calls":"not-an-array"},"finish_reason":"stop"}],"usage":{"cost":0}}""";
        using var client = new HttpClient(new Handler(json));
        var provider = new ChatCompletionsProvider(client,
            new Uri("https://provider.example.test/chat/completions"), null, 1, 1);
        var error = await Assert.ThrowsAsync<ProviderException>(async () =>
            await provider.CompleteAsync(Request(Message.User("texto")), CancellationToken.None));
        Assert.Equal("bad-response", error.Kind);
    }


    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Estimativa_em_qualquer_resposta_mantem_recibo_da_execucao_estimado(bool primeiraEstimativa)
    {
        const string estimated = """{"choices":[{"message":{"content":"parcial"},"finish_reason":"stop"}],"usage":{"prompt_tokens":10,"completion_tokens":20}}""";
        const string reported = """{"choices":[{"message":{"content":"fim"},"finish_reason":"stop"}],"usage":{"cost":0.01}}""";
        var handler = new SequenceHandler(primeiraEstimativa ? estimated : reported,
            primeiraEstimativa ? reported : estimated);
        using var client = new HttpClient(handler);
        var provider = new ChatCompletionsProvider(client,
            new Uri("https://provider.example.test/chat/completions"), null, 1, 1);

        await provider.CompleteAsync(Request(Message.User("primeira")), CancellationToken.None);
        Assert.Equal(primeiraEstimativa, provider.LastCostEstimated);
        await provider.CompleteAsync(Request(Message.User("segunda")), CancellationToken.None);

        Assert.True(provider.LastCostEstimated);
        Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public async Task Resposta_parcial_recusada_nao_contamina_estado_da_estimativa()
    {
        const string truncated = """{"choices":[{"message":{"content":"incompleto"},"finish_reason":"length"}],"usage":{"prompt_tokens":10,"completion_tokens":20}}""";
        const string reported = """{"choices":[{"message":{"content":"completo"},"finish_reason":"stop"}],"usage":{"cost":0.01}}""";
        var handler = new SequenceHandler(truncated, reported);
        using var client = new HttpClient(handler);
        var provider = new ChatCompletionsProvider(client,
            new Uri("https://provider.example.test/chat/completions"), null, 1, 1);

        var error = await Assert.ThrowsAsync<ProviderException>(async () =>
            await provider.CompleteAsync(Request(Message.User("primeira")), CancellationToken.None));
        Assert.Equal("truncated", error.Kind);
        Assert.False(provider.LastCostEstimated);
        await provider.CompleteAsync(Request(Message.User("segunda")), CancellationToken.None);
        Assert.False(provider.LastCostEstimated);
        Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public async Task IDs_duplicados_de_tool_calls_sao_recusados_antes_de_executar_qualquer_tool()
    {
        var wire = ChatCompletionsProvider.WireName("files.read");
        var json = """
        {"choices":[{"message":{"tool_calls":[
          {"id":"same-id","function":{"name":"WIRE_NAME","arguments":"{\"path\":\"a.md\"}"}},
          {"id":"same-id","function":{"name":"WIRE_NAME","arguments":"{\"path\":\"b.md\"}"}}
        ]},"finish_reason":"tool_calls"}],"usage":{"cost":0.01}}
        """.Replace("WIRE_NAME", wire, StringComparison.Ordinal);
        using var client = new HttpClient(new Handler(json));
        var provider = new ChatCompletionsProvider(client,
            new Uri("https://provider.example.test/chat/completions"), null, 1, 1);
        var error = await Assert.ThrowsAsync<ProviderException>(async () =>
            await provider.CompleteAsync(Request(Message.User("leia")), CancellationToken.None));
        Assert.Equal("bad-response", error.Kind);
        Assert.False(error.IsTransient);
        Assert.DoesNotContain("a.md", error.Message);
    }

    [Fact]
    public async Task Tool_calls_com_ids_distintos_preservam_correlacao()
    {
        var wire = ChatCompletionsProvider.WireName("files.read");
        var json = """
        {"choices":[{"message":{"tool_calls":[
          {"id":"call-a","function":{"name":"WIRE_NAME","arguments":"{}"}},
          {"id":"call-b","function":{"name":"WIRE_NAME","arguments":"{}"}}
        ]},"finish_reason":"tool_calls"}],"usage":{"cost":0.01}}
        """.Replace("WIRE_NAME", wire, StringComparison.Ordinal);
        using var client = new HttpClient(new Handler(json));
        var provider = new ChatCompletionsProvider(client,
            new Uri("https://provider.example.test/chat/completions"), null, 1, 1);
        var response = await provider.CompleteAsync(Request(Message.User("leia")), CancellationToken.None);
        Assert.Equal(StopReason.ToolUse, response.Stop);
        Assert.Equal(["call-a", "call-b"], response.Content.OfType<ToolUseBlock>().Select(x => x.Id));
    }

    [Fact]
    public async Task Cli_help_e_exigencia_de_configuracao_fechada()
    {
        Assert.Equal(0, await EcosystemAiCli.RunAsync(["--help"]));
        Assert.Equal(2, await EcosystemAiCli.RunAsync(["--project", "/nao/existe", "--goal", "x"]));
    }
}
