using System.Text.Json;

namespace Hub.Core;

/// <summary>
/// Leitura SOMENTE LEITURA da API pública do GitHub (CI, releases, branches, PRs, Issues). GitHub aqui é registro de
/// desenvolvimento, não IPC (NN-015). Nenhum token é embutido: se houver, quem usa o Hub o fornece em tempo de execução
/// (a API pública sem token tem limite baixo e responde 403 ao excedê-lo — isso vira <see cref="Availability.NotAvailable"/>).
/// Falha de rede, tempo esgotado, status HTTP de erro ou resposta inválida nunca lançam: viram dado não disponível com o motivo.
/// </summary>
public sealed class GitHubReader
{
    const string Api = "https://api.github.com";
    readonly HttpClient _http;
    readonly RepositoryRef _repo;
    readonly string? _token;

    public GitHubReader(HttpClient http, RepositoryRef repo, string? token = null)
    {
        _http = http;
        _repo = repo;
        _token = string.IsNullOrWhiteSpace(token) ? null : token;
    }

    public Task<Datum<IReadOnlyList<PullRequestInfo>>> ReadPullRequestsAsync(CancellationToken ct = default) =>
        GetList($"/repos/{_repo.FullName}/pulls?state=open&per_page=100", null, e =>
            Int(e, "number") is { } n && Str(e, "title") is { } t
                ? new PullRequestInfo(n, t, Bool(e, "draft"), Nested(e, "head", "ref") ?? "", Str(e, "html_url") ?? "")
                : null, ct);

    public Task<Datum<IReadOnlyList<BranchInfo>>> ReadBranchesAsync(CancellationToken ct = default) =>
        GetList($"/repos/{_repo.FullName}/branches?per_page=100", null, e => Str(e, "name") is { } n ? new BranchInfo(n) : null, ct);

    public Task<Datum<IReadOnlyList<ReleaseInfo>>> ReadReleasesAsync(CancellationToken ct = default) =>
        GetList($"/repos/{_repo.FullName}/releases?per_page=30", null, e =>
            Str(e, "tag_name") is { } tag
                ? new ReleaseInfo(tag, Str(e, "name"), Bool(e, "prerelease"), Str(e, "published_at"), Str(e, "html_url") ?? "",
                    e.TryGetProperty("assets", out var a) && a.ValueKind == JsonValueKind.Array ? a.GetArrayLength() : 0)
                : null, ct);

    /// <summary>Issues abertas (a API devolve PRs na mesma lista; elas são descartadas aqui).</summary>
    public Task<Datum<IReadOnlyList<IssueInfo>>> ReadIssuesAsync(CancellationToken ct = default) =>
        GetList($"/repos/{_repo.FullName}/issues?state=open&per_page=100", null, e =>
        {
            if (e.TryGetProperty("pull_request", out _)) return null;
            if (Int(e, "number") is not { } n || Str(e, "title") is not { } t) return null;
            var labels = new List<string>();
            if (e.TryGetProperty("labels", out var ls) && ls.ValueKind == JsonValueKind.Array)
                foreach (var l in ls.EnumerateArray())
                    if (l.ValueKind == JsonValueKind.Object && Str(l, "name") is { } ln) labels.Add(ln);
            var state = labels.FirstOrDefault(x => x.StartsWith("state:", StringComparison.Ordinal))?["state:".Length..];
            return new IssueInfo(n, t, Str(e, "html_url") ?? "", labels, state);
        }, ct);

    /// <summary>Execuções recentes de workflows na branch dada (a API devolve da mais nova para a mais antiga).</summary>
    public Task<Datum<IReadOnlyList<WorkflowRunInfo>>> ReadCiRunsAsync(string branch, CancellationToken ct = default) =>
        GetList($"/repos/{_repo.FullName}/actions/runs?branch={Uri.EscapeDataString(branch)}&per_page=30", "workflow_runs", e =>
            Str(e, "name") is { } n && Str(e, "status") is { } s
                ? new WorkflowRunInfo(n, s, Str(e, "conclusion"), Str(e, "head_branch") ?? branch, Str(e, "head_sha") ?? "", Str(e, "html_url") ?? "")
                : null, ct);

    /// <summary>Lê tudo em paralelo; cada parte falha sozinha.</summary>
    public async Task<GitHubSnapshot> ReadAllAsync(string defaultBranch, CancellationToken ct = default)
    {
        var pr = ReadPullRequestsAsync(ct);
        var br = ReadBranchesAsync(ct);
        var rel = ReadReleasesAsync(ct);
        var iss = ReadIssuesAsync(ct);
        var ci = ReadCiRunsAsync(defaultBranch, ct);
        await Task.WhenAll(pr, br, rel, iss, ci);
        return new GitHubSnapshot(_repo, pr.Result, br.Result, rel.Result, iss.Result, ci.Result);
    }

    async Task<Datum<IReadOnlyList<T>>> GetList<T>(string path, string? arrayProperty, Func<JsonElement, T?> map, CancellationToken ct) where T : class
    {
        var source = Api + path;
        var r = await GitHubHttp.GetAsync(_http, source, _token, "application/vnd.github+json", ct).ConfigureAwait(false);
        if (r.Body is null) return Datum<IReadOnlyList<T>>.Missing(source, r.Note);

        try
        {
            using var doc = JsonDocument.Parse(r.Body);
            var root = doc.RootElement;
            if (arrayProperty is not null)
            {
                if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty(arrayProperty, out root))
                    return Datum<IReadOnlyList<T>>.Missing(source, "resposta inesperada");
            }
            if (root.ValueKind != JsonValueKind.Array) return Datum<IReadOnlyList<T>>.Missing(source, "resposta inesperada");

            var list = new List<T>();
            foreach (var e in root.EnumerateArray())
                if (e.ValueKind == JsonValueKind.Object && map(e) is { } item) list.Add(item);
            return Datum<IReadOnlyList<T>>.From(list, source);
        }
        catch (JsonException) { return Datum<IReadOnlyList<T>>.Missing(source, "resposta inválida"); }
    }

    static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    static int? Int(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var n) ? n : null;

    static bool Bool(JsonElement e, string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.True;

    static string? Nested(JsonElement e, string outer, string inner) =>
        e.TryGetProperty(outer, out var o) && o.ValueKind == JsonValueKind.Object ? Str(o, inner) : null;
}
