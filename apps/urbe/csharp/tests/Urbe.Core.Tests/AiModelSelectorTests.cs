using System.Net;
using System.Text;
using Urbe.Core.Assistant;

namespace Urbe.Core.Tests;

/// <summary>CST-220: one typed catalog per model/provider and the selector by modality, consent and cost.</summary>
public sealed class AiModelSelectorTests
{
    private sealed class Device : ILocalTextRuntime
    {
        public static readonly LocalModel M = new("gemma4-e2b", "Gemma 4 E2B", "d", "m.gguf", "https://x.test/m", 10, new string('a', 64), 16384, 1000, true);
        public IReadOnlyList<LocalModel> Models => [M];
        public LocalModel Recommended => M;
        public bool IsInstalled(LocalModel model) => true;
        public Task InstallAsync(LocalModel model, IProgress<(long Done, long Total)>? progress, CancellationToken cancel) => Task.CompletedTask;
        public void Remove(LocalModel model) { }
        public Task<AiResult> ChatAsync(LocalModel model, AiRequest request, Action<AiStreamEvent> onEvent, CancellationToken cancel) => throw new NotSupportedException();
    }

    private static AiConfig Config()
    {
        var cfg = new AiConfig();
        cfg.Providers.Add(new AiProviderConfig("dev", "device", "IA no aparelho", null, "local://aparelho", "local"));
        cfg.Providers.Add(new AiProviderConfig("or", "openrouter", "OpenRouter", "sk-or-1"));
        cfg.Providers.Add(new AiProviderConfig("ant", "anthropic", "Anthropic", "sk-ant-1"));
        cfg.Providers.Add(new AiProviderConfig("ollama", "ollama", "Ollama"));
        cfg.ModelCache["or"] = new AiModelCache(1, [
            new AiModelInfo("acme/text-only", "Só texto", 32000, (.5, 1), true, ["text"], ["text"]),
            new AiModelInfo("acme/vision", "Visão barata", 128000, (1, 2), true, ["text", "image", "file"], ["text"], .1),
            new AiModelInfo("acme/vision-pricey", "Visão cara", 128000, (10, 20), true, ["text", "image"], ["text"]),
            new AiModelInfo("acme/painter", "Pintor", null, null, false, ["text"], ["image"])
        ]);
        cfg.ModelCache["ollama"] = new AiModelCache(1, [new AiModelInfo("llama3", "Llama 3", 8192, null, null)]);
        return cfg;
    }

    private static IReadOnlyList<AiModelCapabilities> Catalog(AiConfig cfg) => AiModelCatalog.Configured(cfg, new Device());

    [Fact]
    public void Catalog_TypesEveryConfiguredModelWithoutInventingModalities()
    {
        var all = Catalog(Config());
        var device = all.Single(m => m.ProviderId == "dev");
        // the on-device model only gets text from the app, whatever the weights could do
        Assert.Equal(AiModalities.Text, device.Input);
        Assert.False(device.Reads(AiModalities.Image));
        Assert.Equal(AiModelPlace.Device, device.Place);
        Assert.False(device.LeavesDevice);
        Assert.Equal(0, device.Price!.Input);
        Assert.Equal(16384, device.Context);

        var vision = all.Single(m => m.ModelId == "acme/vision");
        Assert.True(vision.Reads(AiModalities.Image | AiModalities.Pdf));
        Assert.True(vision.Ocr);
        Assert.Equal(.1, vision.Price!.CachedInput);
        Assert.Equal(AiModelPlace.Remote, vision.Place);
        Assert.True(vision.Streaming);

        var painter = all.Single(m => m.ModelId == "acme/painter");
        Assert.True(painter.Makes(AiModalities.Image));

        // the provider did not report modalities and no source documents them: unknown, never "no"
        var llama = all.Single(m => m.ModelId == "llama3");
        Assert.Null(llama.Input);
        Assert.Null(llama.Reads(AiModalities.Image));
        Assert.Equal(AiModelPlace.OwnServer, llama.Place);

        // Anthropic without a listed catalog: the preset's models, with the published rates
        var opus = all.Single(m => m.ModelId == "claude-opus-5-5");
        Assert.True(opus.Reads(AiModalities.Image | AiModalities.Pdf));
        Assert.Equal((4.0, 20.0, (double?).2), (opus.Price!.Input, opus.Price.Output, opus.Price.CachedInput));
        Assert.Equal(1_000_000, opus.Context);
        Assert.True(opus.Batch);

        // the presets' image defaults are image generators
        Assert.True(all.Single(m => m.ModelId == "bytedance-seed/seedream-4.5").Makes(AiModalities.Image));
        Assert.Contains(AiModelCatalog.Describe(vision), l => l.StartsWith("Lê: texto, imagem, PDF", StringComparison.Ordinal));
        Assert.Contains(AiModelCatalog.Describe(llama), l => l == "Lê: não informado pelo provedor");
    }

    [Fact]
    public void Catalog_SkipsProvidersWithoutTheirKey()
    {
        var cfg = Config();
        cfg.Providers.Add(new AiProviderConfig("oa", "openai", "OpenAI"));
        Assert.DoesNotContain(Catalog(cfg), m => m.ProviderId == "oa");
    }

    [Fact]
    public void Selector_TheChosenCapableModelWins()
    {
        var cfg = Config();
        var s = AiModelSelector.Select(new AiTaskNeeds(AiModalities.Text | AiModalities.Image, InputTokens: 100_000, OutputTokens: 0),
            Catalog(cfg), new AiModelChoice("or", "acme/vision-pricey"));
        Assert.Equal(AiSelectionOutcome.Chosen, s.Outcome);
        Assert.Equal("acme/vision-pricey", s.Model!.ModelId);
        Assert.Equal(1, s.Cost.Usd);
        Assert.Contains("estimado", s.Cost.Text);
    }

    [Fact]
    public void Selector_AnIncapableChoiceIsReportedWithTheFallbackNotSwitched()
    {
        var s = AiModelSelector.Select(AiModelSelector.ForAttachments(["image/png"]), Catalog(Config()), new AiModelChoice("or", "acme/text-only"));
        Assert.Equal(AiSelectionOutcome.Fallback, s.Outcome);
        Assert.Equal("acme/text-only", s.Model!.ModelId); // still the person's model
        Assert.Equal("acme/vision", s.Fallback!.ModelId); // same provider, cheapest capable
        Assert.Equal(AiModalities.Image, s.Missing);
        Assert.Contains("não consegue ler imagem", s.Reason);
        Assert.Same(s.Fallback, s.Capable);
    }

    [Fact]
    public void Selector_FromTheDeviceTheFallbackSaysTheFilesLeaveIt()
    {
        var s = AiModelSelector.Select(AiModelSelector.ForAttachments(["application/pdf"]), Catalog(Config()), new AiModelChoice("dev", "gemma4-e2b"));
        Assert.Equal(AiSelectionOutcome.Fallback, s.Outcome);
        Assert.True(s.Fallback!.Reads(AiModalities.Pdf));
        Assert.Contains("sairá do aparelho", s.Reason);
    }

    [Fact]
    public void Selector_WithoutConsentNothingRemoteIsOffered()
    {
        var s = AiModelSelector.Select(AiModelSelector.ForAttachments(["image/jpeg"]), Catalog(Config()), new AiModelChoice("dev", "gemma4-e2b"), allowRemote: false);
        Assert.Equal(AiSelectionOutcome.NeedsConsent, s.Outcome);
        Assert.Contains("fora do aparelho", s.Reason);
        // plain text stays on the device, at no cost
        var text = AiModelSelector.Select(new AiTaskNeeds(Tools: true), Catalog(Config()), allowRemote: false);
        Assert.Equal(AiSelectionOutcome.Automatic, text.Outcome);
        Assert.Equal(AiModelPlace.Device, text.Model!.Place);
        Assert.Equal(0, text.Cost.Usd);
    }

    [Fact]
    public void Selector_NeverPretends()
    {
        var cfg = new AiConfig();
        cfg.Providers.Add(new AiProviderConfig("dev", "device", "IA no aparelho", null, "local://aparelho", "local"));
        var s = AiModelSelector.Select(AiModelSelector.ForAttachments(["image/png"]), AiModelCatalog.Configured(cfg, new Device()), new AiModelChoice("dev", "gemma4-e2b"));
        Assert.Equal(AiSelectionOutcome.Missing, s.Outcome);
        Assert.Null(s.Capable);
        Assert.Contains("Nenhum modelo configurado faz isso", s.Reason);

        // audio is not something the app sends to models, whatever they accept
        var audio = AiModelSelector.Select(AiModelSelector.ForAttachments(["audio/mpeg"]), Catalog(Config()));
        Assert.Equal(AiSelectionOutcome.Missing, audio.Outcome);
        Assert.Contains("áudio", audio.Reason);

        // unknown capability: the person's model is used and the doubt said
        var unknown = AiModelSelector.Select(AiModelSelector.ForAttachments(["image/png"]), Catalog(Config()), new AiModelChoice("ollama", "llama3"));
        Assert.Equal(AiSelectionOutcome.Unverified, unknown.Outcome);
        Assert.Contains("não informa", unknown.Reason);
        Assert.Contains("sem tarifa", unknown.Cost.Text);
    }

    [Fact]
    public void Selector_ContextAndToolsCount()
    {
        var s = AiModelSelector.Select(new AiTaskNeeds(InputTokens: 100_000), Catalog(Config()), new AiModelChoice("or", "acme/text-only"));
        Assert.Equal(AiSelectionOutcome.Fallback, s.Outcome);
        Assert.Contains("contexto", s.Reason);
        var painter = AiModelSelector.Select(new AiTaskNeeds(Tools: true), Catalog(Config()), new AiModelChoice("or", "acme/painter"));
        Assert.Contains("ferramentas", painter.Reason);
    }

    [Fact]
    public void Selector_ImagesPickAnImageModelWithItsRate()
    {
        var cfg = Config();
        cfg.Providers.Add(new AiProviderConfig("gem", "gemini", "Google Gemini", "AIza1"));
        var s = AiModelSelector.Select(new AiTaskNeeds(AiModalities.Text, AiModalities.Image), Catalog(cfg), new AiModelChoice("gem", "gemini-nano-banana-2.1"));
        Assert.Equal(AiSelectionOutcome.Chosen, s.Outcome);
        Assert.Contains("/imagem", s.Cost.Text);
        var chat = AiModelSelector.Select(new AiTaskNeeds(AiModalities.Text, AiModalities.Image), Catalog(cfg), new AiModelChoice("or", "acme/text-only"));
        Assert.Equal(AiSelectionOutcome.Fallback, chat.Outcome);
        Assert.True(chat.Fallback!.Makes(AiModalities.Image));
    }

    [Fact]
    public void Video_SaysWhichRouteAndNeverInventsOne()
    {
        var none = AiModelSelector.Video(Catalog(Config()));
        Assert.False(none.Available);
        Assert.Contains("renderizador de vídeo programático", none.Text);
        Assert.Contains("nenhum modelo configurado gera vídeo", none.Text);
        var programmatic = AiModelSelector.Video(Catalog(Config()), programmaticRenderer: true);
        Assert.True(programmatic is { Available: true, Programmatic: true });
        Assert.Contains("renderizado no aparelho", programmatic.Text);

        var cfg = Config();
        cfg.ModelCache["or"] = new AiModelCache(1, [new AiModelInfo("acme/film", "Filme", null, null, false, ["text"], ["video"])]);
        var generative = AiModelSelector.Video(Catalog(cfg));
        Assert.True(generative is { Available: true, Programmatic: false });
        Assert.Contains("sai do aparelho", generative.Text);
        Assert.False(AiModelSelector.Video(Catalog(cfg), allowRemote: false).Available);
    }

    [Fact]
    public void Store_KeepsReportedModalitiesAndOldCachesByteForByte()
    {
        {
            var store = new AssistantStore(new MemoryAssistantStorage());
            var cfg = new AiConfig();
            cfg.ModelCache["or"] = new AiModelCache(5, [new AiModelInfo("a/b", "B", 8000, (1, 2), true, ["text", "image"], ["text"], .25),
                new AiModelInfo("c/d", "D", null, null, null)]);
            store.SetConfig(cfg);
            var back = store.GetConfig().ModelCache["or"].List;
            Assert.Equal(["text", "image"], back[0].InputModalities);
            Assert.Equal(.25, back[0].CachedInputPrice);
            Assert.Null(back[1].InputModalities);
            Assert.DoesNotContain("inputModalities", AssistantStore.ConfigToJson(new AiConfig { ModelCache = { ["x"] = new AiModelCache(1, [new AiModelInfo("c/d", "D", null, null, null)]) } }).ToJsonString());
        }
    }

    private sealed class Handler(string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") });
    }

    [Fact]
    public async Task OpenRouter_ModalitiesAndCacheRateAreRead()
    {
        var json = """{"data":[{"id":"x/y","name":"Y","context_length":1000,"pricing":{"prompt":"0.000001","completion":"0.000002","input_cache_read":"0.0000001"},"architecture":{"input_modalities":["text","image","file"],"output_modalities":["text"]},"supported_parameters":["tools"]}]}""";
        var list = await new AssistantProviders(new Handler(json)).ListModelsAsync(new AiProviderConfig("or", "openrouter", "OpenRouter", "k"), TestContext.Current.CancellationToken);
        var m = Assert.Single(list);
        Assert.Equal(["text", "image", "file"], m.InputModalities);
        Assert.Equal(.1, m.CachedInputPrice!.Value, 6);
        var caps = AiModelCatalog.Describe(new AiProviderConfig("or", "openrouter", "OpenRouter", "k"), m.Id, m);
        Assert.True(caps.Reads(AiModalities.Pdf));
        Assert.Equal("informado pelo provedor", caps.Source);
    }
}
