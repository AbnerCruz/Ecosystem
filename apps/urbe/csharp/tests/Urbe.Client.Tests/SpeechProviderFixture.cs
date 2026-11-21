using System.Net;
using System.Text.Json.Nodes;
using Urbe.Client;
using Urbe.Core;
using Urbe.Core.Assistant;

namespace Urbe.Client.Tests;

public sealed partial class HeadlessUiTests
{
    private sealed class SpeechProvider : HttpMessageHandler
    {
        public List<(string Text, string Voice)> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal("/v1/audio/speech", request.RequestUri!.AbsolutePath);
            var body = JsonNode.Parse(await request.Content!.ReadAsStringAsync(cancellationToken))!;
            Assert.Equal("tts-1", (string?)body["model"]);
            Assert.Equal("wav", (string?)body["response_format"]);
            Requests.Add(((string)body["input"]!, (string)body["voice"]!));
            return new(HttpStatusCode.OK) { Content = new ByteArrayContent(LongAudio.Wav(new short[800])) };
        }
    }

    private static SpeechProvider ConfigureSpeechProvider(MainView view)
    {
        var panel = view.Assistant;
        _ = panel.SpeechFor();
        var handler = new SpeechProvider();
        panel.Providers = new AssistantProviders(handler, (_, _) => Task.CompletedTask);
        panel.Config.Providers.Add(new AiProviderConfig("speech-provider", "openai", ApiKey: "test"));
        panel.Config.AgentMedia["geral"] = new(Voice: new AiSpeechChoice("speech-provider", "tts-1", "nova").Reference);
        return handler;
    }
}
