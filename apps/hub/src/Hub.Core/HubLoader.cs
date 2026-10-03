using System.Text.Json;

namespace Hub.Core;

/// <param name="Repository">Repositório do Ecosystem a ler. Vem da configuração do app: é o ponto de partida, a única coisa que o Hub não pode descobrir sozinho.</param>
/// <param name="Token">Opcional, fornecido em execução (nunca embutido). Sem ele, a API pública limita ~60 requisições/hora.</param>
public sealed record HubOptions(RepositoryRef Repository, string Branch = "main", string? Token = null);

/// <summary>Tudo o que o Hub mostra, numa leitura. <see cref="Stale"/> = veio do cache (último estado conhecido), não da leitura atual.</summary>
public sealed record HubSnapshot(
    IReadOnlyList<ProductSummary> Products,
    PastNowNext Timeline,
    IReadOnlyDictionary<string, Datum<IReadOnlyList<ReleaseInfo>>> ProductReleases,
    bool Stale,
    IReadOnlyDictionary<string, Datum<ReleaseChannel>>? ReleaseChannels = null);

/// <summary>
/// Carrega o estado do Ecosystem a partir do repositório e do GitHub — só leitura, nada é gravado de volta, nada é inventado.
/// Cada arquivo ou chamada que falha vira nota/NotAvailable sem derrubar o resto. Orquestra os leitores de P3-4, P3-5 e P3-6.
/// </summary>
public sealed class HubLoader
{
    const string HandoffsDir = "docs/governance/handoffs";
    const int MaxParallelFiles = 8;

    readonly HttpClient _http;
    readonly HubOptions _options;

    public HubLoader(HttpClient http, HubOptions options)
    {
        _http = http;
        _options = options;
    }

    public async Task<HubSnapshot> LoadAsync(CancellationToken ct = default)
    {
        var o = _options;
        var files = new RepositoryFileSource(_http, o.Repository, o.Branch, o.Token);
        var github = new GitHubReader(_http, o.Repository, o.Token);

        var ecoT = files.ReadTextAsync("ecosystem.json", ct);
        var profileT = files.ReadTextAsync(DistributionReader.ProfilePath, ct);
        var roadmapT = files.ReadTextAsync("ROADMAP.md", ct);
        var decisionsT = files.ReadTextAsync("docs/governance/decisions.json", ct);
        var handoffListT = files.ListFilesAsync(HandoffsDir, ct);
        var ghT = github.ReadAllAsync(o.Branch, ct);
        await Task.WhenAll(ecoT, profileT, roadmapT, decisionsT, handoffListT, ghT).ConfigureAwait(false);

        var notes = new List<string>();
        void Need<T>(Datum<T> d, string what)
        {
            if (d.Availability != Availability.Derived)
                notes.Add($"{what} indisponível{(d.Note is null ? "" : $" — {d.Note}")} [{d.Source}]");
        }

        // Products: a versão tem autoridade em arquivos que precisam ser lidos antes.
        var eco = ecoT.Result;
        Need(eco, "ecosystem.json");
        var products = new List<ProductSummary>();
        if (eco.Value is { } ecoJson)
        {
            var versions = await ReadAll(files, EcosystemReader.VersionFiles(ecoJson), ct).ConfigureAwait(false);
            products.AddRange(EcosystemReader.ReadProducts(ecoJson, f => versions.GetValueOrDefault(f)));
        }

        // Handoffs: lista + cada arquivo, com concorrência limitada.
        var handoffs = new List<(string, string)>();
        if (handoffListT.Result.Value is { } names)
        {
            var paths = names.Where(n => n.EndsWith(".json", StringComparison.OrdinalIgnoreCase)).Select(n => $"{HandoffsDir}/{n}").ToList();
            var texts = await ReadAll(files, paths, ct).ConfigureAwait(false);
            handoffs.AddRange(paths.Where(texts.ContainsKey).Select(p => (p, texts[p]!)));
            var missing = paths.Count - handoffs.Count;
            if (missing > 0) notes.Add($"{missing} handoff(s) indisponível(is) — o Now pode estar incompleto");
        }
        else Need(handoffListT.Result, "lista de handoffs");

        Need(roadmapT.Result, "ROADMAP.md");
        Need(decisionsT.Result, "decisions.json");
        Need(profileT.Result, "perfil de distribuição");

        var timeline = TimelineBuilder.Build(roadmapT.Result.Value ?? "", decisionsT.Result.Value ?? "", handoffs);
        timeline = TimelineGitHub.AddGitHub(timeline, ghT.Result, o.Branch);
        timeline = timeline with { Notes = notes.Concat(timeline.Notes).ToList() };

        // Canal operacional declarado pelo perfil. SOURCE e DISTRIBUTION não são intercambiáveis.
        var releases = new Dictionary<string, Datum<IReadOnlyList<ReleaseInfo>>>();
        var channels = products.ToDictionary(p => p.Id, p => DistributionReader.ReleaseChannelFor(p, profileT.Result, o.Repository));
        var releaseTasks = products
            .Select(async p =>
            {
                var channel = channels[p.Id];
                if (channel.Value is not { } c)
                    return (p.Id, Data: Datum<IReadOnlyList<ReleaseInfo>>.Missing(channel.Source, channel.Note));
                var data = c.Repository.FullName.Equals(o.Repository.FullName, StringComparison.OrdinalIgnoreCase)
                    ? ghT.Result.Releases : await new GitHubReader(_http, c.Repository, o.Token).ReadReleasesAsync(ct).ConfigureAwait(false);
                return (p.Id, Data: GitHubReader.ForChannel(data, c));
            })
            .ToList();
        foreach (var (id, data) in await Task.WhenAll(releaseTasks).ConfigureAwait(false)) releases[id] = data;

        return new HubSnapshot(products, timeline, releases, Stale: false, channels);
    }

    static async Task<Dictionary<string, string?>> ReadAll(RepositoryFileSource files, IEnumerable<string> paths, CancellationToken ct)
    {
        var result = new Dictionary<string, string?>();
        using var gate = new SemaphoreSlim(MaxParallelFiles);
        var tasks = paths.Distinct().Select(async path =>
        {
            await gate.WaitAsync(ct).ConfigureAwait(false);
            try { return (path, Data: await files.ReadTextAsync(path, ct).ConfigureAwait(false)); }
            finally { gate.Release(); }
        }).ToList();
        foreach (var (path, data) in await Task.WhenAll(tasks).ConfigureAwait(false))
            if (data.Value is not null) result[path] = data.Value;
        return result;
    }
}

/// <summary>Cache do último <see cref="HubSnapshot"/> para funcionar offline. Projeção, não autoridade: tudo que volta dele sai com <c>Stale = true</c>.</summary>
public static class SnapshotCache
{
    public static string Serialize(HubSnapshot snapshot) => JsonSerializer.Serialize(snapshot);

    /// <summary>Conteúdo ausente ou inválido vira <c>null</c> (sem estado anterior), nunca exceção.</summary>
    public static HubSnapshot? Load(string? cached)
    {
        if (string.IsNullOrWhiteSpace(cached)) return null;
        try
        {
            var s = JsonSerializer.Deserialize<HubSnapshot>(cached);
            return s is null || s.Products is null || s.Timeline is null || s.ProductReleases is null ? null : s with
            {
                Products = s.Products.Select(p => p.AsStale()).ToList(), Stale = true,
                ProductReleases = s.ProductReleases.ToDictionary(p => p.Key, p => p.Value.AsStale()),
                ReleaseChannels = s.ReleaseChannels?.ToDictionary(p => p.Key, p => p.Value.AsStale()),
            };
        }
        catch (JsonException) { return null; }
    }
}
