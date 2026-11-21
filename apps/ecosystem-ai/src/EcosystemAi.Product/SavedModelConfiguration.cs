using System.Net.Http.Headers;
using System.Text.Json;

namespace EcosystemAi.Product;

// Product-local preferences, independent of the runtime, project catalog and ledger.
// Credentials are owned by the platform vault, never by these portable records.
public sealed record SavedModel(string Id, string Name, decimal InputUsdPerMillion,
    decimal OutputUsdPerMillion, long RunBudgetCents = 20, long MaxCallCents = 5, bool UseHistory = false);
public sealed record SavedProvider(string Id, string Name, string BaseUrl, IReadOnlyList<SavedModel> Models);
public sealed record SavedModelConfiguration(int Version, IReadOnlyList<SavedProvider> Providers,
    string? ProviderId = null, string? ModelId = null)
{
    public static SavedModelConfiguration Empty => new(1, []);
    public void Validate()
    {
        if (Version != 1 || Providers is null || Providers.Count > 30)
            throw new InvalidDataException("Configuração de IA inválida.");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var p in Providers)
        {
            if (p is null || !Guid.TryParseExact(p.Id, "N", out _) || !ids.Add(p.Id)
                || string.IsNullOrWhiteSpace(p.Name) || p.Name.Length > 80 || p.Name.Any(char.IsControl)
                || p.Models is null || p.Models.Count > 200)
                throw new InvalidDataException("Provedor inválido.");
            _ = BaseUri(p.BaseUrl);
            if (p.Models.Any(m => m is null) || p.Models.Select(m => m.Id).Distinct(StringComparer.Ordinal).Count() != p.Models.Count)
                throw new InvalidDataException("Modelo duplicado no provedor.");
            foreach (var m in p.Models)
            {
                if (m is null || string.IsNullOrWhiteSpace(m.Name) || m.Name.Length > 100 || m.Name.Any(char.IsControl))
                    throw new InvalidDataException("Nome do modelo inválido.");
                Settings(p, m, "validation-key").Validate();
            }
        }
        if (ProviderId is null && ModelId is null) return;
        var provider = Providers.SingleOrDefault(p => p.Id == ProviderId);
        if (provider is null || !provider.Models.Any(m => m.Id == ModelId))
            throw new InvalidDataException("O modelo selecionado não pertence ao provedor.");
    }
    // No cleartext endpoint outside the device. The Android Host additionally
    // whitelists these two hosts in its Network Security Configuration.
    public static bool IsLocalHttp(Uri uri) =>
        uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback
        && (uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || uri.Host == "127.0.0.1");

    public static Uri BaseUri(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && !IsLocalHttp(uri))
            || uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0)
            throw new ArgumentException("Use HTTPS ou HTTP local (localhost/127.0.0.1), sem credenciais, parâmetros ou fragmento.");
        return new Uri(uri.AbsoluteUri.TrimEnd('/') + "/");
    }
    public static ProductModelSettings Settings(SavedProvider provider, SavedModel model, string? key) => new()
    {
        Endpoint = new Uri(BaseUri(provider.BaseUrl), "chat/completions").AbsoluteUri,
        Model = model.Id, ApiKey = key, InputUsdPerMillion = model.InputUsdPerMillion,
        OutputUsdPerMillion = model.OutputUsdPerMillion, RunBudgetCents = model.RunBudgetCents,
        MaxCallCents = model.MaxCallCents, UseHistory = model.UseHistory
    };
    public ProductModelSettings? Selected(Func<string, string?> credential)
    {
        Validate();
        var p = Providers.SingleOrDefault(p => p.Id == ProviderId);
        var m = p?.Models.SingleOrDefault(m => m.Id == ModelId);
        if (p is null || m is null) return null;
        var result = Settings(p, m, credential(p.Id));
        result.Validate();
        return result;
    }
    public SavedModelConfiguration RemoveProvider(string id) => this with
    {
        Providers = Providers.Where(p => p.Id != id).ToArray(),
        ProviderId = ProviderId == id ? null : ProviderId,
        ModelId = ProviderId == id ? null : ModelId
    };
}

public static class ProviderModelDiscovery
{
    // Explicit GET only; no completion/test inference and no redirects carrying credentials.
    public static async Task<IReadOnlyList<string>> ListAsync(HttpClient http, SavedProvider provider,
        string? key, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get,
            new Uri(SavedModelConfiguration.BaseUri(provider.BaseUrl), "models"));
        if (!string.IsNullOrWhiteSpace(key)) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode) throw new IOException("Não foi possível listar os modelos. Confira o provedor e a chave, ou cadastre o ID manualmente.");
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var bytes = new MemoryStream();
        var buffer = new byte[8192];
        int count;
        while ((count = await stream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            if (bytes.Length + count > 2 * 1024 * 1024) throw new IOException("Lista de modelos acima do limite.");
            bytes.Write(buffer, 0, count);
        }
        using var doc = JsonDocument.Parse(bytes.ToArray());
        if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
            throw new IOException("O provedor não retornou uma lista compatível. Cadastre o ID manualmente.");
        return data.EnumerateArray().Take(5000)
            .Where(m => m.ValueKind == JsonValueKind.Object && m.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String)
            .Select(m => m.GetProperty("id").GetString()!)
            .Where(id => !string.IsNullOrWhiteSpace(id) && id.Length <= 256 && !id.Any(char.IsControl))
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
    }
}
