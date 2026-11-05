using System.Globalization;
using System.Text.RegularExpressions;

namespace Urbe.Core.Assistant;

/// <summary>What goes into or comes out of a model. PDF is a document read whole (OCR included when the model sees it).</summary>
[Flags]
public enum AiModalities
{
    None = 0,
    Text = 1,
    Image = 2,
    Pdf = 4,
    Audio = 8,
    Video = 16
}

/// <summary>Where a model runs: in the app on this device, on a server the person keeps (Ollama, a LAN), or at a provider on the internet.</summary>
public enum AiModelPlace { Device, OwnServer, Remote }

/// <summary>Price per 1M tokens (US$). <see cref="CachedInput"/> is the cache-read rate when known.</summary>
public sealed record AiModelPrice(double Input, double Output, double? CachedInput, string Source);

/// <summary>
/// CST-220: the typed capabilities of one model of one configured provider, as the Urbe can use it. Null always means
/// "not known" (never "no"): a model is only said to read images, use tools or draw when a source says so.
/// </summary>
public sealed record AiModelCapabilities(
    string ProviderId, string ProviderName, string Preset, string ModelId, string Name,
    AiModalities? Input, AiModalities? Output, bool? Tools, long? Context, AiModelPrice? Price, string? ImageRate,
    bool? PromptCache, bool? Batch, bool Streaming, AiModelPlace Place, string? HostRequirement, string Source)
{
    /// <summary>True/false when known; null when no source says.</summary>
    public bool? Reads(AiModalities m) => Input is { } i ? (i & m) == m : null;
    public bool? Makes(AiModalities m) => Output is { } o ? (o & m) == m : null;
    /// <summary>Reading text in pictures and scans comes with seeing them.</summary>
    public bool? Ocr => Reads(AiModalities.Image);
    public bool Voice => Reads(AiModalities.Audio) == true || Makes(AiModalities.Audio) == true;
    public bool LeavesDevice => Place != AiModelPlace.Device;
    public string Label => Name + " · " + ProviderName;
}

/// <summary>
/// CST-220: the ONE typed catalog of models. It is built from what the providers report (<see cref="AiModelInfo"/>,
/// cached in <see cref="AiConfig.ModelCache"/>), the on-device models (<see cref="LocalAi"/>), the image defaults of the
/// presets (<see cref="AssistantProviders.ImageModel"/>, <see cref="AssistantImagePricing"/>) and a short curated table
/// of documented facts; reported data wins over the table. It never invents a modality: the on-device model reads text
/// only because that is all the app gives it, whatever the weights could do.
/// </summary>
public static class AiModelCatalog
{
    /// <summary>What the app can actually send to a provider in a chat request (wire: text, images and PDFs).</summary>
    public const AiModalities AppSends = AiModalities.Text | AiModalities.Image | AiModalities.Pdf;

    /// <summary>The image models of Gemini that the app can call (one list; the image picker shows it).</summary>
    public static IReadOnlyList<string> GeminiImageModels { get; } =
        ["gemini-nano-banana-2.1", "gemini-3.1-flash-lite-image", "gemini-3.1-flash-image", "gemini-3-pro-image", "gemini-2.5-flash-image"];

    private sealed record Known(AiModalities Input, AiModalities Output, bool Tools, long? Context, AiModelPrice? Price, string Source);

    private const string AnthropicPrice = "Tabela Anthropic (out/2026)";
    private const AiModalities Reader = AiModalities.Text | AiModalities.Image | AiModalities.Pdf;

    /// <summary>Documented facts of exact models (prices per 1M tokens).</summary>
    private static readonly Dictionary<string, Known> Exact = new(StringComparer.OrdinalIgnoreCase)
    {
        ["claude-opus-5-5"] = new(Reader, AiModalities.Text, true, 1_000_000, new(4, 20, .2, AnthropicPrice), AnthropicPrice),
        ["claude-sonnet-5"] = new(Reader, AiModalities.Text, true, 1_000_000, new(2, 10, null, AnthropicPrice), AnthropicPrice),
        ["claude-haiku-4-5-20251001"] = new(Reader, AiModalities.Text, true, 200_000, new(1, 5, null, AnthropicPrice), AnthropicPrice),
        ["claude-haiku-4-5"] = new(Reader, AiModalities.Text, true, 200_000, new(1, 5, null, AnthropicPrice), AnthropicPrice)
    };

    /// <summary>Families whose input modalities are documented by their makers (the provider may still report otherwise).</summary>
    private static (AiModalities Input, string Source)? Family(string id)
    {
        var bare = id.Contains('/') ? id[(id.LastIndexOf('/') + 1)..] : id;
        if (bare.StartsWith("claude-", StringComparison.OrdinalIgnoreCase)) return (Reader, "família Claude (documentação Anthropic)");
        if (bare.StartsWith("gemini-", StringComparison.OrdinalIgnoreCase) && !IsImageModelId(bare))
            return (Reader | AiModalities.Audio | AiModalities.Video, "família Gemini (documentação Google)");
        if (Regex.IsMatch(bare, @"^(gpt-5|gpt-4o|gpt-4\.1)", RegexOptions.IgnoreCase)) return (Reader, "família GPT (documentação OpenAI)");
        return null;
    }

    /// <summary>True for ids that name image generators (the image picker filters the same way).</summary>
    public static bool IsImageModelId(string id) =>
        id.Contains("gpt-image", StringComparison.OrdinalIgnoreCase) || Regex.IsMatch(id, @"(^|[-/])image($|[-.])", RegexOptions.IgnoreCase) ||
        id.Contains("imagen", StringComparison.OrdinalIgnoreCase) || id.Contains("flux", StringComparison.OrdinalIgnoreCase) ||
        id.Contains("seedream", StringComparison.OrdinalIgnoreCase) || id.Contains("nano-banana", StringComparison.OrdinalIgnoreCase);

    private static AiModalities? FromReported(IReadOnlyList<string>? list)
    {
        if (list is null) return null;
        var m = AiModalities.None;
        foreach (var x in list)
            m |= x switch
            {
                "text" => AiModalities.Text,
                "image" => AiModalities.Image,
                "file" or "pdf" => AiModalities.Pdf,
                "audio" => AiModalities.Audio,
                "video" => AiModalities.Video,
                _ => AiModalities.None
            };
        return m;
    }

    /// <summary>Where the provider runs, read from its preset and address.</summary>
    public static AiModelPlace PlaceOf(AiProviderConfig provider)
    {
        var p = AssistantProviders.Resolve(provider);
        if (p.Kind == "local") return AiModelPlace.Device;
        if (!Uri.TryCreate(p.BaseUrl, UriKind.Absolute, out var uri)) return AiModelPlace.Remote;
        var host = uri.Host.ToLowerInvariant();
        return host is "localhost" or "::1" or "[::1]" || host.StartsWith("127.", StringComparison.Ordinal) ||
               host.StartsWith("10.", StringComparison.Ordinal) || host.StartsWith("192.168.", StringComparison.Ordinal) ||
               Regex.IsMatch(host, @"^172\.(1[6-9]|2\d|3[01])\.") || host.EndsWith(".local", StringComparison.Ordinal)
            ? AiModelPlace.OwnServer : AiModelPlace.Remote;
    }

    /// <summary>The capabilities of <paramref name="modelId"/> at <paramref name="provider"/>, from what is reported and known.</summary>
    public static AiModelCapabilities Describe(AiProviderConfig provider, string modelId, AiModelInfo? reported = null, LocalModel? local = null)
    {
        var p = AssistantProviders.Resolve(provider);
        var place = PlaceOf(provider);
        var name = reported?.Name is { Length: > 0 } n ? n : local?.Name ?? modelId;
        string? host = place switch
        {
            AiModelPlace.Device => null,
            AiModelPlace.OwnServer => "o servidor em " + (Uri.TryCreate(p.BaseUrl, UriKind.Absolute, out var u) ? u.Authority : p.BaseUrl) + " ligado",
            _ => "internet e a conta em " + p.Name
        };
        bool streaming = p.Kind is "openai" or "anthropic" or "local";
        // provider-level facts: prompt caching and batch APIs the provider offers (the Urbe does not send batches)
        bool? cache = p.Preset switch { "anthropic" or "openai" or "gemini" => true, "device" => false, _ => null };
        bool? batch = p.Preset switch { "anthropic" or "openai" or "gemini" => true, "device" => false, _ => null };

        if (place == AiModelPlace.Device)
            return new(provider.Id, p.Name, p.Preset, modelId, name, AiModalities.Text, AiModalities.Text,
                local?.Tools ?? reported?.Tools, local?.Context ?? reported?.Context, new AiModelPrice(0, 0, 0, "no aparelho, sem cobrança"), null,
                false, false, streaming, place, null, "IA no aparelho (o app só lhe entrega texto)");

        // image generators: the app sends them a text prompt and gets a picture back
        bool image = IsImageModelId(modelId) || AssistantProviders.ImageModel(p.Preset) is { Length: > 0 } d && d == modelId ||
                     FromReported(reported?.OutputModalities) is { } outs && outs.HasFlag(AiModalities.Image) && !outs.HasFlag(AiModalities.Text);
        if (image)
            return new(provider.Id, p.Name, p.Preset, modelId, name, AiModalities.Text, AiModalities.Image, false, reported?.Context, null,
                AssistantImagePricing.Published(p.Preset, modelId)?.Rate, null, null, false, place, host,
                AssistantImagePricing.Published(p.Preset, modelId) is not null ? "tabela publicada do provedor" : "modelo de imagem do provedor");

        Exact.TryGetValue(modelId, out var known);
        var family = Family(modelId);
        var input = FromReported(reported?.InputModalities) ?? known?.Input ?? family?.Input;
        var output = FromReported(reported?.OutputModalities) ?? known?.Output ?? (input is not null ? AiModalities.Text : null);
        var price = reported?.Price is { } rp ? new AiModelPrice(rp.Input, rp.Output, reported.CachedInputPrice, "catálogo do provedor")
            : known?.Price;
        var source = reported?.InputModalities is not null ? "informado pelo provedor" : known?.Source ?? family?.Source ?? "o provedor não informa as modalidades";
        return new(provider.Id, p.Name, p.Preset, modelId, name, input, output, reported?.Tools ?? known?.Tools,
            known?.Context ?? reported?.Context, price, null,
            price?.CachedInput is not null ? true : cache, batch, streaming, place, host, source);
    }

    private static bool HasCredentials(AiProviderConfig p) =>
        !AssistantProviders.Presets.TryGetValue(p.Preset, out var pre) || !pre.NeedsKey || !string.IsNullOrWhiteSpace(p.ApiKey);

    /// <summary>
    /// Every model the app can use now: each usable provider's listed models (the cached catalog), the on-device models that
    /// are installed, the image defaults, and the models chosen in the settings even when the list was never loaded.
    /// </summary>
    public static IReadOnlyList<AiModelCapabilities> Configured(AiConfig cfg, ILocalTextRuntime? local = null)
    {
        local ??= LocalAi.Text;
        var all = new List<AiModelCapabilities>();
        var seen = new HashSet<(string, string)>();
        void Add(AiProviderConfig p, string id, AiModelInfo? info, LocalModel? lm = null)
        {
            if (id.Length == 0 || !seen.Add((p.Id, id))) return;
            all.Add(Describe(p, id, info, lm));
        }
        foreach (var p in cfg.Providers.Where(HasCredentials))
        {
            var r = AssistantProviders.Resolve(p);
            if (r.Kind == "local")
            {
                foreach (var m in local?.Models.Where(local.IsInstalled) ?? []) Add(p, m.Id, null, m);
                continue;
            }
            var list = cfg.ModelCache.TryGetValue(p.Id, out var cache) ? cache.List : [];
            foreach (var m in list) Add(p, m.Id, m);
            if (list.Count == 0 && AssistantProviders.Presets.TryGetValue(p.Preset, out var pre) && pre.Models is { } models)
                foreach (var m in models) Add(p, m, null);
            if (AssistantProviders.ImageModel(p.Preset) is { Length: > 0 } image) Add(p, image, null);
        }
        IEnumerable<AiModelChoice> chosen = cfg.AgentModels.Values.Concat(cfg.AgentImages.Values)
            .Append(new AiModelChoice(cfg.ProviderId ?? cfg.Providers.FirstOrDefault()?.Id ?? "", cfg.Model));
        foreach (var c in chosen)
            if (cfg.Providers.FirstOrDefault(p => p.Id == c.ProviderId) is { } p && HasCredentials(p) && AssistantProviders.Resolve(p).Kind != "local")
                Add(p, c.Model, cfg.ModelCache.TryGetValue(p.Id, out var cache) ? cache.List.FirstOrDefault(m => m.Id == c.Model) : null);
        return all;
    }

    /// <summary>One model's capabilities from the catalog, or described from scratch when it is not listed.</summary>
    public static AiModelCapabilities? Find(AiConfig cfg, AiModelChoice choice, ILocalTextRuntime? local = null)
    {
        local ??= LocalAi.Text;
        var p = cfg.Providers.FirstOrDefault(x => x.Id == choice.ProviderId);
        if (p is null || choice.Model.Length == 0) return null;
        if (AssistantProviders.Resolve(p).Kind == "local")
            return Describe(p, choice.Model, null, local?.Models.FirstOrDefault(m => m.Id == choice.Model) ?? local?.Recommended);
        return Describe(p, choice.Model, cfg.ModelCache.TryGetValue(p.Id, out var cache) ? cache.List.FirstOrDefault(m => m.Id == choice.Model) : null);
    }

    // ---------------------------------------------------------------- reading it

    private static readonly CultureInfo Pt = CultureInfo.GetCultureInfo("pt-BR");

    public static string ModalityNames(AiModalities m)
    {
        var names = new List<string>();
        if (m.HasFlag(AiModalities.Text)) names.Add("texto");
        if (m.HasFlag(AiModalities.Image)) names.Add("imagem");
        if (m.HasFlag(AiModalities.Pdf)) names.Add("PDF");
        if (m.HasFlag(AiModalities.Audio)) names.Add("áudio");
        if (m.HasFlag(AiModalities.Video)) names.Add("vídeo");
        return names.Count == 0 ? "nada" : string.Join(", ", names);
    }

    /// <summary>Same form as the chat-price lines of the panel (small rates are never rounded to zero).</summary>
    public static string Usd(double v) => "US$ " + v.ToString("0.######", CultureInfo.InvariantCulture);

    public static string PriceText(AiModelCapabilities m) =>
        m.Place == AiModelPlace.Device ? "Sem cobrança: roda no aparelho"
        : m.ImageRate is { } rate ? rate
        : m.Price is { } p ? Usd(p.Input) + " entrada · " + Usd(p.Output) + " saída" + (p.CachedInput is { } c ? " · " + Usd(c) + " entrada em cache" : "") + " / 1M tokens"
        : m.Place == AiModelPlace.OwnServer ? "Servidor seu: sem tarifa informada"
        : "Preço não informado · não significa gratuito";

    /// <summary>The readable lines of a model's capabilities (settings view).</summary>
    public static IReadOnlyList<string> Describe(AiModelCapabilities m)
    {
        string Yes(bool? b, string what) => b switch { true => what + ": sim", false => what + ": não", _ => what + ": não informado" };
        var lines = new List<string>
        {
            m.Input is { } i ? "Lê: " + ModalityNames(i & AppSends) + (i & ~AppSends) switch
            {
                AiModalities.None => "",
                var extra => " (o modelo também aceita " + ModalityNames(extra) + ", que o Urbe ainda não envia)"
            } : "Lê: não informado pelo provedor",
            m.Output is { } o ? "Produz: " + ModalityNames(o) : "Produz: não informado",
            Yes(m.Tools, "Ferramentas") + " · " + Yes(m.Ocr, "OCR (texto em imagens)"),
            "Contexto: " + (m.Context is { } c ? c >= 1_000_000 ? (c / 1e6).ToString("0.#", Pt) + "M tokens" : Math.Round(c / 1000.0) + "k tokens" : "não informado"),
            "Preço: " + PriceText(m),
            Yes(m.PromptCache, "Cache de prompt") + " · " + (m.Batch == true ? "lote: o provedor oferece (o Urbe não usa)" : Yes(m.Batch, "Lote")) +
                " · streaming: " + (m.Streaming ? "sim" : "não"),
            m.Place switch
            {
                AiModelPlace.Device => "Onde roda: neste aparelho; nada sai dele",
                AiModelPlace.OwnServer => "Onde roda: no seu servidor; precisa de " + m.HostRequirement,
                _ => "Onde roda: no provedor; o que for enviado sai do aparelho e precisa de " + m.HostRequirement
            },
            "Fonte: " + m.Source
        };
        return lines;
    }
}
