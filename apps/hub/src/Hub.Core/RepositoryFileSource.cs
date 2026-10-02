using System.Text.Json;

namespace Hub.Core;

/// <summary>
/// Leitura somente leitura de arquivos do repositório público: texto por <c>raw.githubusercontent.com</c> (não consome o limite
/// da API) e listagem de uma pasta pela API de conteúdos. Falhas viram <see cref="Availability.NotAvailable"/> com o motivo.
/// </summary>
public sealed class RepositoryFileSource
{
    readonly HttpClient _http;
    readonly RepositoryRef _repo;
    readonly string _branch;
    readonly string? _token;

    public RepositoryFileSource(HttpClient http, RepositoryRef repo, string branch = "main", string? token = null)
    {
        _http = http;
        _repo = repo;
        _branch = branch;
        _token = token;
    }

    static string Escape(string path) => string.Join('/', path.Split('/').Select(Uri.EscapeDataString));

    public async Task<Datum<string>> ReadTextAsync(string path, CancellationToken ct = default)
    {
        var source = $"https://raw.githubusercontent.com/{_repo.FullName}/{Escape(_branch)}/{Escape(path)}";
        var r = await GitHubHttp.GetAsync(_http, source, _token, "text/plain", ct).ConfigureAwait(false);
        return r.Body is null ? Datum<string>.Missing(source, r.Note) : Datum<string>.From(r.Body, source);
    }

    /// <summary>Nomes dos arquivos (não pastas) de um diretório do repositório.</summary>
    public async Task<Datum<IReadOnlyList<string>>> ListFilesAsync(string directory, CancellationToken ct = default)
    {
        var source = $"https://api.github.com/repos/{_repo.FullName}/contents/{Escape(directory)}?ref={Uri.EscapeDataString(_branch)}";
        var r = await GitHubHttp.GetAsync(_http, source, _token, "application/vnd.github+json", ct).ConfigureAwait(false);
        if (r.Body is null) return Datum<IReadOnlyList<string>>.Missing(source, r.Note);
        try
        {
            using var doc = JsonDocument.Parse(r.Body);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return Datum<IReadOnlyList<string>>.Missing(source, "resposta inesperada");
            var names = new List<string>();
            foreach (var e in doc.RootElement.EnumerateArray())
                if (e.ValueKind == JsonValueKind.Object &&
                    e.TryGetProperty("type", out var t) && t.ValueKind == JsonValueKind.String && t.GetString() == "file" &&
                    e.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String && n.GetString() is { Length: > 0 } name)
                    names.Add(name);
            return Datum<IReadOnlyList<string>>.From(names, source);
        }
        catch (JsonException) { return Datum<IReadOnlyList<string>>.Missing(source, "resposta inválida"); }
    }
}
