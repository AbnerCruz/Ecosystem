using System.Net;

namespace Hub.Core;

/// <summary>Resultado de um GET: corpo, ou o motivo de não haver corpo. Nunca lança por falha de rede/HTTP (o Hub segue vivo).</summary>
internal readonly record struct HttpResult(string? Body, string? Note);

internal static class GitHubHttp
{
    /// <summary>
    /// GET somente leitura. Authorization só quando há token (fornecido em execução, nunca embutido).
    /// Cancelamento pedido por quem chamou é propagado; tempo esgotado do cliente vira "tempo esgotado".
    /// </summary>
    public static async Task<HttpResult> GetAsync(HttpClient http, string url, string? token, string accept, CancellationToken ct)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.UserAgent.ParseAdd("readonly-client");
            req.Headers.Accept.ParseAdd(accept);
            if (!string.IsNullOrWhiteSpace(token)) req.Headers.Authorization = new("Bearer", token);

            using var res = await http.SendAsync(req, ct).ConfigureAwait(false);
            if (res.StatusCode != HttpStatusCode.OK) return new(null, $"HTTP {(int)res.StatusCode}");
            return new(await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false), null);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (OperationCanceledException) { return new(null, "tempo esgotado"); }
        catch (HttpRequestException) { return new(null, "falha de rede"); }
    }
}
