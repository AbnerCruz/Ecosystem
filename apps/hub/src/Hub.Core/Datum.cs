namespace Hub.Core;

/// <summary>Disponibilidade de um dado lido pelo Hub. Nunca é inventada: sem fonte, o dado é <see cref="NotAvailable"/>.</summary>
public enum Availability
{
    /// <summary>Lido de uma fonte canônica na leitura atual.</summary>
    Derived,
    /// <summary>Último estado conhecido, lido antes e exibido sem nova leitura (offline).</summary>
    Stale,
    /// <summary>Sem fonte disponível.</summary>
    NotAvailable,
}

/// <summary>Um valor lido pelo Hub com a sua origem. O Hub só consome fontes; não é autoridade de nenhum dado (NN-001, NN-021).</summary>
public sealed record Datum<T>(T? Value, Availability Availability, string Source, string? Note = null)
{
    public static Datum<T> From(T value, string source) => new(value, Availability.Derived, source);

    /// <summary>Sem fonte. <paramref name="note"/> diz por quê (ex.: "HTTP 403"), para a UI explicar em vez de esconder.</summary>
    public static Datum<T> Missing(string source, string? note = null) => new(default, Availability.NotAvailable, source, note);

    /// <summary>Marca como último estado conhecido; um dado que já não está disponível continua não disponível.</summary>
    public Datum<T> AsStale() => Availability == Availability.Derived ? this with { Availability = Availability.Stale } : this;
}
