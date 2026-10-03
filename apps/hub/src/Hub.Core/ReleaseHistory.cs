using System.Globalization;
using System.Numerics;
using System.Text.RegularExpressions;

namespace Hub.Core;

/// <summary>Boundary local de leitura de releases, sem formato/capability compartilhado nem domínio específico de transporte.</summary>
public interface IReleaseProvider
{
    Task<Datum<IReadOnlyList<ReleaseInfo>>> ReadReleasesAsync(CancellationToken ct = default);
}

public enum ReleaseHistoryState { Loading, Loaded, Empty, Unavailable, Stale }
public sealed record ReleaseHistoryEntry(string ProductId, string Version, ReleaseInfo Release, string Source);
public sealed record ChangesSinceVersion(string? InstalledVersion, IReadOnlyList<ReleaseHistoryEntry> Releases,
    bool Comparable, bool BaseFound, string Note);
public sealed record ProductReleaseHistory(string ProductId, string ProductName, Datum<string> SourceVersion,
    Datum<string> InstalledVersion, ReleaseHistoryState State, IReadOnlyList<ReleaseHistoryEntry> Releases,
    ChangesSinceVersion Changes, string Source, string? Note, bool Refreshing);

public static class ReleaseHistoryBuilder
{
    public const int MaxReleases = 100;
    public static ProductReleaseHistory Build(HubSnapshot? snapshot, string productId,
        Datum<string>? installedVersion = null, bool loading = false)
    {
        var product = snapshot?.Products.FirstOrDefault(p => p.Id == productId);
        var data = snapshot?.ProductReleases.GetValueOrDefault(productId);
        var prefix = snapshot?.ReleaseChannels?.GetValueOrDefault(productId)?.Value?.TagPrefix;
        installedVersion ??= Datum<string>.Missing("sistema operacional", "versão instalada ainda não consultada");
        var entries = data is { Value: not null, Availability: not Availability.NotAvailable }
            ? data.Value.Where(r => r is not null).Take(MaxReleases)
                .Select(r => new ReleaseHistoryEntry(productId, Version(r.Tag, prefix), r, data.Source))
                .OrderByDescending(r => Date(r.Release.PublishedAt))
                .ThenByDescending(r => ComparableVersion.Parse(r.Version), ComparableVersion.Comparer)
                .ThenBy(r => r.Release.Tag, StringComparer.Ordinal).ToList() : [];
        var state = data is null || data.Availability == Availability.NotAvailable || data.Value is null
            ? loading ? ReleaseHistoryState.Loading : ReleaseHistoryState.Unavailable
            : data.Availability == Availability.Stale || snapshot!.Stale ? ReleaseHistoryState.Stale
            : entries.Count == 0 ? ReleaseHistoryState.Empty : ReleaseHistoryState.Loaded;
        return new(productId, product?.Name.Value ?? productId,
            product?.Version ?? Datum<string>.Missing("ecosystem.json", "Product indisponível"), installedVersion,
            state, entries, Since(entries, installedVersion), data?.Source ?? "canal declarado", data?.Note, loading);
    }

    public static string Version(string tag, string? prefix = null)
    {
        var value = prefix is { Length: > 0 } && tag.StartsWith(prefix, StringComparison.Ordinal) ? tag[prefix.Length..] : tag;
        return value.Length > 1 && value[0] is 'v' or 'V' && char.IsAsciiDigit(value[1]) ? value[1..] : value;
    }

    static DateTimeOffset Date(string? value) => DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture,
        DateTimeStyles.AssumeUniversal, out var d) ? d : DateTimeOffset.MinValue;

    public static ChangesSinceVersion Since(IReadOnlyList<ReleaseHistoryEntry> entries, Datum<string> installed)
    {
        if (installed.Availability == Availability.NotAvailable || installed.Value is null ||
            ComparableVersion.Parse(Version(installed.Value)) is not { } baseline)
            return new(installed.Value, [], false, false, "Versão instalada desconhecida ou não comparável; nenhuma atualização é inferida.");
        var parsed = entries.Select(e => (Entry: e, Version: ComparableVersion.Parse(e.Version))).ToList();
        var newer = parsed.Where(e => e.Version is not null && e.Version.CompareTo(baseline) > 0)
            .OrderBy(e => e.Version, ComparableVersion.Comparer).ThenBy(e => Date(e.Entry.Release.PublishedAt))
            .ThenBy(e => e.Entry.Release.Tag, StringComparer.Ordinal).Select(e => e.Entry).ToList();
        bool found = entries.Any(e => e.Version == Version(installed.Value));
        int omitted = parsed.Count(e => e.Version is null);
        return new(installed.Value, newer, true, found,
            "Agregação das releases consultadas, sem resumo ou inferência de PRs. " +
            (found ? "A versão de base consta no histórico. " : "A versão de base não consta; o intervalo pode estar incompleto. ") +
            (omitted > 0 ? $"{omitted} tag(s) não comparáveis ficam apenas no histórico. " : "") +
            (installed.Availability == Availability.Stale ? "Versão instalada informada é antiga. " : "") +
            "Limite de 100 releases; inclui pré-lançamentos publicados do canal, sem assegurar outras releases/canais ou disponibilidade de atualização.");
    }
}

/// <summary>Comparação SemVer local; tags desconhecidas continuam apresentáveis, jamais ganham versão inventada.</summary>
internal sealed class ComparableVersion(BigInteger major, BigInteger minor, BigInteger patch, string[] pre) : IComparable<ComparableVersion>
{
    static readonly Regex Pattern = new(@"^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(?:-([0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?(?:\+[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?$", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);
    public static IComparer<ComparableVersion?> Comparer { get; } = System.Collections.Generic.Comparer<ComparableVersion?>.Create((a, b) => a is null ? b is null ? 0 : -1 : b is null ? 1 : a.CompareTo(b));
    public static ComparableVersion? Parse(string value)
    {
        var m = Pattern.Match(value);
        if (!m.Success) return null;
        var parts = m.Groups[4].Success ? m.Groups[4].Value.Split('.') : [];
        if (parts.Any(p => p.All(char.IsAsciiDigit) && p.Length > 1 && p[0] == '0')) return null;
        return new(BigInteger.Parse(m.Groups[1].Value), BigInteger.Parse(m.Groups[2].Value), BigInteger.Parse(m.Groups[3].Value), parts);
    }
    public int CompareTo(ComparableVersion? other)
    {
        if (other is null) return 1;
        int n = major.CompareTo(other.Major); if (n != 0) return n;
        n = minor.CompareTo(other.Minor); if (n != 0) return n;
        n = patch.CompareTo(other.Patch); if (n != 0) return n;
        if (pre.Length == 0 || other.Pre.Length == 0) return pre.Length == other.Pre.Length ? 0 : pre.Length == 0 ? 1 : -1;
        for (int i = 0; i < Math.Min(pre.Length, other.Pre.Length); i++)
        {
            bool a = BigInteger.TryParse(pre[i], NumberStyles.None, CultureInfo.InvariantCulture, out var x);
            bool b = BigInteger.TryParse(other.Pre[i], NumberStyles.None, CultureInfo.InvariantCulture, out var y);
            n = a && b ? x.CompareTo(y) : a != b ? a ? -1 : 1 : StringComparer.Ordinal.Compare(pre[i], other.Pre[i]);
            if (n != 0) return n;
        }
        return pre.Length.CompareTo(other.Pre.Length);
    }
    BigInteger Major => major; BigInteger Minor => minor; BigInteger Patch => patch; string[] Pre => pre;
}
