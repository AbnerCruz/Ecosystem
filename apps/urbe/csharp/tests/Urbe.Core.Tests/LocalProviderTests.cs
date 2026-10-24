using Urbe.Core.Assistant;

namespace Urbe.Core.Tests;

/// <summary>The provider "IA no aparelho": requests go to the runtime on the device, never to the network.</summary>
[Collection("LocalAi")]
public sealed class LocalProviderTests
{
    private sealed class Fake(bool installed) : ILocalTextRuntime
    {
        private static readonly LocalModel M = new("m", "M", "d", "m.gguf", "https://x.test/m", 10, new string('a', 64), 4096, 1000, true);
        public IReadOnlyList<LocalModel> Models => [M];
        public LocalModel Recommended => M;
        public bool IsInstalled(LocalModel model) => installed;
        public Task InstallAsync(LocalModel model, IProgress<(long Done, long Total)>? progress, CancellationToken cancel) => Task.CompletedTask;
        public void Remove(LocalModel model) { }
        public string? Said;
        public Task<AiResult> ChatAsync(LocalModel model, AiRequest request, Action<AiStreamEvent> onEvent, CancellationToken cancel)
        {
            Said = "ok";
            return Task.FromResult(new AiResult([AiBlock.TextBlock("ok")], "", "end", new AiUsage(1, 1, null), model.Id));
        }
    }

    [Fact]
    public async Task Device_AnswersThroughTheRuntimeAndExplainsWhenNotDownloaded()
    {
        var before = Urbe.Core.Assistant.LocalAi.Text;
        try
        {
            var cfg = new AiProviderConfig("p", "device", "IA no aparelho", null, "local://aparelho", "local");
            var request = new AiRequest("m", [new AiMessage("user", [AiBlock.TextBlock("oi")])], null, null, 100);
            var ready = new Fake(true);
            Urbe.Core.Assistant.LocalAi.Text = ready;
            var result = await new AssistantProviders().StreamAsync(cfg, request, _ => { }, CancellationToken.None);
            Assert.Equal("ok", ready.Said);
            Assert.Equal("ok", result.Content[0].Text);
            Assert.Equal(["m"], (await new AssistantProviders().ListModelsAsync(cfg, CancellationToken.None)).Select(m => m.Id));

            Urbe.Core.Assistant.LocalAi.Text = new Fake(false);
            var ex = await Assert.ThrowsAsync<AiProviderException>(() => new AssistantProviders().StreamAsync(cfg, request, _ => { }, CancellationToken.None));
            Assert.Contains("ainda não foi baixada", ex.Message, StringComparison.Ordinal);
        }
        finally { Urbe.Core.Assistant.LocalAi.Text = before; }
    }
}
