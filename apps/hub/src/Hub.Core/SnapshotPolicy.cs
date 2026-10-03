namespace Hub.Core;

/// <param name="Show">O que a tela mostra; <c>null</c> quando não há nada (nem leitura atual nem estado anterior).</param>
/// <param name="ToCache">Conteúdo a gravar no cache, ou <c>null</c> quando o cache não deve mudar.</param>
public sealed record SnapshotChoice(HubSnapshot? Show, string? ToCache);

/// <summary>
/// Decide o que o app mostra e o que guarda, sem UI (para ser testado sem aparelho). Uma leitura que não conseguiu nem o
/// <c>ecosystem.json</c> não é "estado": ela nunca sobrescreve o último estado bom, e o último estado bom (marcado como tal)
/// vale mais para o proprietário do que uma tela vazia.
/// </summary>
public static class SnapshotPolicy
{
    public static SnapshotChoice Choose(HubSnapshot? fresh, string? cachedJson)
    {
        if (fresh is { Products.Count: > 0 })
        {
            // Uma falha parcial não apaga releases confiáveis; nunca cruza canais ou transforma vazio válido em fallback.
            if (SnapshotCache.Load(cachedJson) is { } cachedHistory)
            {
                var releases = fresh.ProductReleases.ToDictionary(x => x.Key, x => x.Value);
                foreach (var (id, failed) in fresh.ProductReleases)
                    if (failed.Availability == Availability.NotAvailable &&
                        fresh.ReleaseChannels?.GetValueOrDefault(id) is { Availability: Availability.Derived, Value: { } channel } &&
                        cachedHistory.ReleaseChannels?.GetValueOrDefault(id)?.Value == channel &&
                        cachedHistory.ProductReleases.GetValueOrDefault(id) is { Value: not null, Availability: not Availability.NotAvailable } saved)
                        releases[id] = saved.AsStale() with { Note = "Último histórico conhecido; consulta atual indisponível: " + failed.Note };
                fresh = fresh with { ProductReleases = releases };
            }
            return new SnapshotChoice(fresh, SnapshotCache.Serialize(fresh));
        }

        if (SnapshotCache.Load(cachedJson) is { } previous)
            return new SnapshotChoice(previous, null);

        // Sem estado anterior: mostra o que a leitura trouxe (as notas explicam o que falhou) em vez de esconder.
        return new SnapshotChoice(fresh, null);
    }
}
