using System.Text.Json;

namespace Hub.Core;

/// <summary>
/// Lê <c>ecosystem.json</c> e devolve os Products declarados. Somente leitura: não escreve nada e não guarda estado próprio (NN-001, NN-021).
/// Os Products são descobertos nos dados — o código nunca nomeia outro Product (NN-002).
/// </summary>
public static class EcosystemReader
{
    /// <summary>
    /// Lê os componentes <c>type == "product"</c>, exceto o próprio Hub (<paramref name="selfId"/>).
    /// <paramref name="readFile"/> devolve o texto de um arquivo relativo à raiz do repositório, ou <c>null</c> se não existir/estiver inacessível
    /// (usado só para a versão cuja autoridade é um arquivo).
    /// </summary>
    public static IReadOnlyList<ProductSummary> ReadProducts(string ecosystemJson, Func<string, string?> readFile, string source = "ecosystem.json", string selfId = "hub")
    {
        JsonDocument doc;
        try { doc = JsonDocument.Parse(ecosystemJson); }
        catch (JsonException) { return []; }

        using (doc)
        {
            if (doc.RootElement.ValueKind != JsonValueKind.Object ||
                !doc.RootElement.TryGetProperty("components", out var comps) || comps.ValueKind != JsonValueKind.Object)
                return [];

            var result = new List<ProductSummary>();
            foreach (var c in comps.EnumerateObject())
            {
                if (c.Name == selfId || c.Value.ValueKind != JsonValueKind.Object) continue;
                if (Str(c.Value, "type") != "product") continue;
                var at = $"{source}#components.{c.Name}";
                result.Add(new ProductSummary(
                    c.Name,
                    Field(c.Value, "name", at),
                    Field(c.Value, "type", at),
                    Field(c.Value, "status", at),
                    ReadVersion(c.Value, readFile, at),
                    Nested(c.Value, "source", "repository", at),
                    Field(c.Value, "publicUrl", at)));
            }
            return result;
        }
    }

    static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    static Datum<string> Field(JsonElement e, string name, string at) =>
        Str(e, name) is { Length: > 0 } s ? Datum<string>.From(s, at + "." + name) : Datum<string>.Missing(at + "." + name);

    static Datum<string> Nested(JsonElement e, string outer, string inner, string at) =>
        e.TryGetProperty(outer, out var o) && o.ValueKind == JsonValueKind.Object ? Field(o, inner, at + "." + outer) : Datum<string>.Missing($"{at}.{outer}.{inner}");

    /// <summary>A versão tem como autoridade o arquivo declarado em <c>version.file</c> (NN-001); o Hub só o lê.</summary>
    static Datum<string> ReadVersion(JsonElement product, Func<string, string?> readFile, string at)
    {
        if (!product.TryGetProperty("version", out var v) || v.ValueKind != JsonValueKind.Object)
            return Datum<string>.Missing(at + ".version");
        if (Str(v, "authority") != "version-file" || Str(v, "file") is not { Length: > 0 } file)
            return Datum<string>.Missing(at + ".version");

        string? text;
        try { text = readFile(file); }
        catch (IOException) { text = null; }
        catch (UnauthorizedAccessException) { text = null; }
        if (string.IsNullOrWhiteSpace(text)) return Datum<string>.Missing(file);

        if (file.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                using var d = JsonDocument.Parse(text);
                return d.RootElement.ValueKind == JsonValueKind.Object && Str(d.RootElement, "version") is { Length: > 0 } ver
                    ? Datum<string>.From(ver, file) : Datum<string>.Missing(file);
            }
            catch (JsonException) { return Datum<string>.Missing(file); }
        }
        return Datum<string>.From(text.Trim(), file);
    }
}
