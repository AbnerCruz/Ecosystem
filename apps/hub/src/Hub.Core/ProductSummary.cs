namespace Hub.Core;

/// <summary>Resumo de um Product lido de <c>ecosystem.json</c>. Cada campo carrega a sua origem; sem fonte, <see cref="Availability.NotAvailable"/>.</summary>
public sealed record ProductSummary(
    string Id,
    Datum<string> Name,
    Datum<string> Type,
    Datum<string> Status,
    Datum<string> Version,
    Datum<string> Repository,
    Datum<string> PublicUrl)
{
    /// <summary>Marca todos os campos como último estado conhecido (leitura vinda do cache, sem nova leitura da fonte).</summary>
    public ProductSummary AsStale() => this with
    {
        Name = Name.AsStale(), Type = Type.AsStale(), Status = Status.AsStale(),
        Version = Version.AsStale(), Repository = Repository.AsStale(), PublicUrl = PublicUrl.AsStale(),
    };
}
