using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace Lunet.Git;

/// <summary>Como falar com um servidor Git: descobrir referências e trocar pacotes (protocolo "smart" sem estado).</summary>
public interface IGitTransport
{
    /// <summary>Anúncio de referências de <paramref name="service"/> (<c>git-upload-pack</c> ou <c>git-receive-pack</c>).</summary>
    Task<byte[]> AdvertiseAsync(string service, CancellationToken cancellation);

    /// <summary>Envia a requisição do serviço e devolve a resposta inteira.</summary>
    Task<byte[]> RpcAsync(string service, byte[] request, CancellationToken cancellation);
}

/// <summary>Transporte HTTPS do Git (GitHub e outros). A autenticação usa um token de acesso pessoal.</summary>
public sealed class HttpGitTransport : IGitTransport
{
    private readonly HttpClient _client;
    private readonly string _url;

    /// <param name="url">Endereço do repositório, por exemplo https://github.com/usuario/projeto.git</param>
    /// <param name="username">Usuário (no GitHub pode ser qualquer texto não vazio quando se usa token).</param>
    /// <param name="token">Token de acesso pessoal ou senha; nulo para repositórios públicos (só leitura).</param>
    /// <param name="client">Cliente HTTP (para testes).</param>
    public HttpGitTransport(string url, string? username = null, string? token = null, HttpClient? client = null)
    {
        if (!url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) && !url.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            throw new GitException("Só endereços http(s) são suportados. Use, por exemplo, https://github.com/usuario/projeto.git");
        _url = url.TrimEnd('/');
        _client = client ?? new HttpClient(new HttpClientHandler { AllowAutoRedirect = true }) { Timeout = TimeSpan.FromMinutes(10) };
        _client.DefaultRequestHeaders.UserAgent.ParseAdd("git/2.43.0 (Lunet)");
        if (!string.IsNullOrEmpty(token))
        {
            var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{(string.IsNullOrEmpty(username) ? "x-access-token" : username)}:{token}"));
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        }
    }

    public async Task<byte[]> AdvertiseAsync(string service, CancellationToken cancellation)
    {
        using var response = await SendAsync(new HttpRequestMessage(HttpMethod.Get, $"{_url}/info/refs?service={service}"), cancellation).ConfigureAwait(false);
        return await response.Content.ReadAsByteArrayAsync(cancellation).ConfigureAwait(false);
    }

    public async Task<byte[]> RpcAsync(string service, byte[] request, CancellationToken cancellation)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, $"{_url}/{service}") { Content = new ByteArrayContent(request) };
        message.Content.Headers.ContentType = new MediaTypeHeaderValue($"application/x-{service}-request");
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue($"application/x-{service}-result"));
        using var response = await SendAsync(message, cancellation).ConfigureAwait(false);
        return await response.Content.ReadAsByteArrayAsync(cancellation).ConfigureAwait(false);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage message, CancellationToken cancellation)
    {
        HttpResponseMessage response;
        try
        {
            response = await _client.SendAsync(message, cancellation).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            throw new GitException("Não foi possível falar com o servidor Git (sem internet ou endereço errado): " + ex.Message, ex);
        }
        catch (TaskCanceledException ex) when (!cancellation.IsCancellationRequested)
        {
            throw new GitException("O servidor Git demorou demais para responder.", ex);
        }
        if (response.IsSuccessStatusCode) return response;
        var status = response.StatusCode;
        response.Dispose();
        throw new GitException(status switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => "Acesso negado. Confira o token de acesso (no GitHub ele precisa da permissão \"repo\").",
            HttpStatusCode.NotFound => "Repositório não encontrado. Confira o endereço e se o token tem acesso a ele.",
            _ => $"O servidor Git respondeu com erro {(int)status}.",
        });
    }
}
