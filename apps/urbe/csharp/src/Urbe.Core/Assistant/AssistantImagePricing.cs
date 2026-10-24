using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Urbe.Core.Assistant;

/// <summary>Quoted catalogue rate, not the final billed cost of a generation.</summary>
public sealed record AiImageQuote(string Rate, string Explanation, string SourceUrl);

/// <summary>Image billing units are not interchangeable: images, megapixels and tokens.</summary>
public static class AssistantImagePricing
{
    private static string Usd(double price) => "US$ " + price.ToString(price < .1 ? "0.####" : "0.###", CultureInfo.GetCultureInfo("pt-BR"));
    private const string GooglePrice = "https://ai.google.dev/gemini-api/docs/pricing";
    private const string OpenAiPrice = "https://openai.com/index/image-generation-api/";

    /// <summary>Official published reference prices, only where the chosen model and requested output match.</summary>
    public static AiImageQuote? Published(string preset, string model) => preset switch
    {
        "gemini" when model == "gemini-nano-banana-2.1" =>
            new(Usd(.0336) + "/imagem · 1K", "Referência Google (out/2026). Tokens de entrada e outras modalidades são cobrados à parte.", GooglePrice),
        "gemini" when model == "gemini-3.1-flash-lite-image" =>
            new(Usd(.0336) + "/imagem · 1K", "Referência Google (out/2026). Tokens de entrada e outras modalidades são cobrados à parte.", GooglePrice),
        "gemini" when model == "gemini-3.1-flash-image" =>
            new(Usd(.067) + "/imagem · 1K", "Referência Google (out/2026). Tokens de entrada e outras modalidades são cobrados à parte.", GooglePrice),
        "gemini" when model == "gemini-3-pro-image" =>
            new(Usd(.134) + "/imagem · 1K/2K", "Referência Google (out/2026). Tokens de entrada e outras modalidades são cobrados à parte.", GooglePrice),
        "openai" when model == "gpt-image-1" =>
            new("≈ US$ 0,02–0,19/imagem", "Referência OpenAI: 1K quadrada; varia por qualidade (baixa/média/alta). Texto de entrada adicional.", OpenAiPrice),
        _ => null
    };

    private static double? Number(JsonNode? node)
    {
        if (node is not JsonValue v) return null;
        if (v.TryGetValue<double>(out var d) && double.IsFinite(d) && d >= 0) return d;
        return v.TryGetValue<string>(out var s) && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out d)
            && double.IsFinite(d) && d >= 0 ? d : null;
    }

    /// <summary>OpenRouter's image /endpoints pricing is an array with billable, unit and cost_usd.</summary>
    public static AiImageQuote? FromOpenRouterEndpoints(JsonNode? data, string model)
    {
        var endpoints = data?["endpoints"] as JsonArray ?? [];
        var prices = endpoints.OfType<JsonObject>()
            .SelectMany(e => (e["pricing"] as JsonArray ?? []).OfType<JsonObject>())
            .Select(p => (Billable: (string?)p["billable"] ?? "", Unit: (string?)p["unit"] ?? "", Cost: Number(p["cost_usd"])))
            .Where(p => p.Cost is not null && p.Billable.StartsWith("output", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var url = "https://openrouter.ai/models/" + model;

        // Prefer direct per-image output costs. A prompt or reference-image fee is not a generated-image price.
        foreach (var unit in new[] { "image", "megapixel", "token" })
        {
            var matched = prices.Where(p => p.Unit.Equals(unit, StringComparison.OrdinalIgnoreCase)).ToList();
            if (matched.Count == 0) continue;
            var amount = matched.Min(x => x.Cost!.Value);
            if (unit == "token") amount *= 1e6;
            var basis = unit switch { "image" => "/imagem", "megapixel" => "/megapixel de saída", _ => "/1M tokens de saída" };
            return new AiImageQuote("A partir de " + Usd(amount) + basis,
                "Tabela OpenRouter consultada via API. Cobrança final pode incluir entrada, resolução, qualidade e rota; consulte a atividade do provedor.", url);
        }
        return null;
    }

    public static bool ValidOpenRouterSlug(string model) => Regex.IsMatch(model, @"^[a-zA-Z0-9_.-]+/[a-zA-Z0-9_.:-]+$");
}
