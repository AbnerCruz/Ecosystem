using System.Net;
using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using Urbe.Core;
using Urbe.Core.Assistant;

namespace Urbe.Core.Tests;

public sealed class AssistantSpeechProviderTests
{
    private sealed class SpeechHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Get)
            {
                if (request.RequestUri!.AbsolutePath.EndsWith("/endpoints", StringComparison.Ordinal))
                    return new(HttpStatusCode.OK) { Content = new StringContent("""
                        {"data":{"endpoints":[{"pricing":{"prompt":"0.0000005","completion":"0.00001"}}]}}
                        """) };
                Assert.Equal("/api/v1/audio/speech/models", request.RequestUri!.AbsolutePath);
                return new(HttpStatusCode.OK)
                {
                    Content = new StringContent("""
                        {"data":[{"id":"google/gemini-test-tts","name":"Gemini voz","output_modalities":[{"type":"speech",
                          "supported_parameters":{"voice":{"values":["Kore","Aoede"]},"response_format":{"values":["pcm"]}}}]}]}
                        """)
                };
            }
            Assert.Equal("/api/v1/audio/speech", request.RequestUri!.AbsolutePath);
            Assert.Equal("test", request.Headers.Authorization?.Parameter);
            var body = JsonNode.Parse(await request.Content!.ReadAsStringAsync(cancellationToken))!;
            Assert.Equal("google/gemini-test-tts", (string?)body["model"]);
            Assert.Equal("Kore", (string?)body["voice"]);
            Assert.Equal("pcm", (string?)body["response_format"]);
            return new(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(new byte[4800]) { Headers = { ContentType = new MediaTypeHeaderValue("audio/pcm") } }
            };
        }
    }

    [Fact]
    public async Task OpenRouterListsActualVoicesAndNormalizesPcmForTheTimeline()
    {
        var cancel = TestContext.Current.CancellationToken;
        var provider = new AiProviderConfig("router", "openrouter", ApiKey: "test");
        var service = new AssistantProviders(new SpeechHandler(), (_, _) => Task.CompletedTask);
        var model = Assert.Single(await service.ListSpeechModelsAsync(provider, cancel));
        Assert.Equal(["Kore", "Aoede"], model.Voices);
        var price = await service.SpeechPriceAsync(provider, model.Id, cancel);
        Assert.Contains("US$ 0,5 por 1 milhão de tokens de entrada", price);
        Assert.Contains("US$ 10 por 1 milhão de tokens de saída", price);
        var choice = new AiSpeechChoice(provider.Id, model.Id, model.Voices[0], model.Format);
        Assert.Equal(choice, AiSpeechChoice.Parse(choice.Reference));
        Assert.Null(AiSpeechChoice.Parse("kokoro-dora"));
        using var wav = new MemoryStream(await service.GenerateSpeechAsync(provider, choice, "Olá.", cancel));
        Assert.True(VideoAudio.ReadWavInfo(wav) is { SampleRate: 24000, Frames: 2400 });
    }
}
