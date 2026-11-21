using System.Net;
using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.LogicalTree;
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
            if (request.Method == HttpMethod.Get)
            {
                Assert.Equal("/v1/models", request.RequestUri!.AbsolutePath);
                return new(HttpStatusCode.OK) { Content = new StringContent("""{"data":[{"id":"tts-1"}]}""") };
            }
            Assert.Equal("/v1/audio/speech", request.RequestUri!.AbsolutePath);
            var body = JsonNode.Parse(await request.Content!.ReadAsStringAsync(cancellationToken))!;
            Assert.Equal("tts-1", (string?)body["model"]);
            Assert.Equal("wav", (string?)body["response_format"]);
            Requests.Add(((string)body["input"]!, (string)body["voice"]!));
            return new(HttpStatusCode.OK) { Content = new ByteArrayContent(LongAudio.Wav(new short[800])) };
        }
    }

    private sealed class TestVoicePlayback : IPreviewAudio
    {
        public double Position => 0;
        public bool Ended => false;
        public bool Failed => false;
        public bool Disposed { get; private set; }
        public void Dispose() => Disposed = true;
    }

    [Fact]
    public async Task VoicePickerShowsPriceAndSynthesizesOnlyOnExplicitPreview() =>
        Assert.True(await Session.Dispatch(async () =>
        {
            var view = new MainView();
            var provider = ConfigureSpeechProvider(view);
            var previous = PlatformAudio.StartPreview;
            var player = new TestVoicePlayback();
            PlatformAudio.StartPreview = (path, offset, _) =>
            {
                Assert.Equal(0, offset);
                Assert.True(File.Exists(path));
                return Task.FromResult<IPreviewAudio>(player);
            };
            try
            {
                var pick = view.Assistant.ChooseProviderVoiceAsync();
                async Task Click(string label)
                {
                    for (int i = 0; i < 100 &&
                        !view.Dialogs.GetLogicalDescendants().OfType<TextBlock>().Any(t => t.Text == label); i++)
                        await Task.Delay(10);
                    ClickButtonWithText(view.Dialogs, label);
                }
                await Click("openai");
                await Click("tts-1");
                await Click("nova");
                Assert.Empty(provider.Requests);
                Assert.Contains(view.Dialogs.GetLogicalDescendants().OfType<TextBlock>(),
                    t => t.Text?.Contains("US$ 15 por 1 milhão de caracteres", StringComparison.Ordinal) == true);
                await Click("Ouvir prévia");
                await Click("Ok");
                Assert.True(player.Disposed);
                await Click("Usar esta voz");
                Assert.Equal("nova", AiSpeechChoice.Parse((await pick)!.Id)!.Voice);
                Assert.Single(provider.Requests);
                return true;
            }
            finally { PlatformAudio.StartPreview = previous; }
        }, TestContext.Current.CancellationToken));

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
