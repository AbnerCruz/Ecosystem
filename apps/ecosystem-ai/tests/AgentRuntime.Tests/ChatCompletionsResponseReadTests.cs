using System.Net;
using System.Text;
using AgentRuntime.Providers.ChatCompletions;

namespace AgentRuntime.Tests;

public class ChatCompletionsResponseReadTests
{
    private const int Limit = 2_000_000;
    private const string Json = """{"choices":[{"message":{"content":"ok"},"finish_reason":"stop"}],"usage":{"cost":0}}""";
    private sealed class ResponseStream(byte[] bytes, Exception? failure = null, Action? beforeRead = null) : MemoryStream(bytes)
    {
        public override bool CanSeek => false;
        public int BytesRead { get; private set; }
        public bool Disposed { get; private set; }
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            beforeRead?.Invoke();
            cancellationToken.ThrowIfCancellationRequested();
            if (failure is not null) throw failure;
            var read = base.Read(buffer.Span);
            BytesRead += read;
            return ValueTask.FromResult(read);
        }
        protected override void Dispose(bool disposing) { Disposed = true; base.Dispose(disposing); }
    }
    private sealed class Handler(HttpContent content) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        }
    }
    private static ModelRequest Request() => new(
        new ModelProfile("chat-completions", "model", new ModelCapabilities(true, false, true, false, false, 8000), new Money(100, "USD")),
        "system", [Message.User("hello")], []);
    private static ChatCompletionsProvider Provider(HttpClient client) => new(client,
        new Uri("https://provider.example.test/chat/completions"), null, 1, 1);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Oversized_body_stops_at_one_byte_over_limit_even_with_missing_or_false_length(bool falseLength)
    {
        var stream = new ResponseStream(new byte[Limit * 2]);
        using var content = new StreamContent(stream);
        if (falseLength) content.Headers.ContentLength = 10;
        var handler = new Handler(content);
        using var client = new HttpClient(handler);
        var error = await Assert.ThrowsAsync<ProviderException>(async () => await Provider(client).CompleteAsync(Request(), CancellationToken.None));
        Assert.Equal("response-too-large", error.Kind);
        Assert.False(error.IsTransient);
        Assert.Equal(Limit + 1, stream.BytesRead);
        Assert.True(stream.Disposed);
        Assert.Equal(1, handler.Calls);
    }
    [Fact]
    public async Task Oversized_declared_length_is_rejected_before_body_read()
    {
        var stream = new ResponseStream(Encoding.UTF8.GetBytes(Json));
        using var content = new StreamContent(stream);
        content.Headers.ContentLength = Limit + 1;
        using var client = new HttpClient(new Handler(content));
        var error = await Assert.ThrowsAsync<ProviderException>(async () => await Provider(client).CompleteAsync(Request(), CancellationToken.None));
        Assert.Equal("response-too-large", error.Kind);
        Assert.Equal(0, stream.BytesRead);
        Assert.True(stream.Disposed);
    }
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Read_failures_are_redacted_and_never_retryable(int kind)
    {
        Exception failure = kind switch { 0 => new IOException("private-body-token"), 1 => new HttpRequestException("private-body-token"), _ => new OperationCanceledException("private-body-token") };
        var stream = new ResponseStream([], failure);
        using var content = new StreamContent(stream);
        var handler = new Handler(content);
        using var client = new HttpClient(handler);
        var error = await Assert.ThrowsAsync<ProviderException>(async () => await Provider(client).CompleteAsync(Request(), CancellationToken.None));
        Assert.Equal("read-response", error.Kind);
        Assert.False(error.IsTransient);
        Assert.DoesNotContain("private-body-token", error.ToString());
        Assert.True(stream.Disposed);
        Assert.Equal(1, handler.Calls);
    }
    [Fact]
    public async Task Caller_cancellation_during_read_is_preserved_and_stream_closed()
    {
        using var cancellation = new CancellationTokenSource();
        var stream = new ResponseStream([], beforeRead: cancellation.Cancel);
        using var content = new StreamContent(stream);
        using var client = new HttpClient(new Handler(content));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await Provider(client).CompleteAsync(Request(), cancellation.Token));
        Assert.True(stream.Disposed);
    }
    [Fact]
    public async Task Valid_json_exactly_at_byte_limit_is_accepted()
    {
        var stream = new ResponseStream(Encoding.UTF8.GetBytes(Json.PadRight(Limit)));
        using var content = new StreamContent(stream);
        using var client = new HttpClient(new Handler(content));
        var response = await Provider(client).CompleteAsync(Request(), CancellationToken.None);
        Assert.Equal("ok", Assert.IsType<TextBlock>(Assert.Single(response.Content)).Text);
        Assert.Equal(Limit, stream.BytesRead);
        Assert.True(stream.Disposed);
    }
    [Fact]
    public async Task Limit_counts_utf8_bytes_instead_of_characters()
    {
        var json = Json.Replace("ok", new string('á', Limit / 2));
        Assert.True(json.Length < Limit);
        var stream = new ResponseStream(Encoding.UTF8.GetBytes(json));
        using var content = new StreamContent(stream);
        using var client = new HttpClient(new Handler(content));
        var error = await Assert.ThrowsAsync<ProviderException>(async () => await Provider(client).CompleteAsync(Request(), CancellationToken.None));
        Assert.Equal("response-too-large", error.Kind);
        Assert.Equal(Limit + 1, stream.BytesRead);
    }
}
