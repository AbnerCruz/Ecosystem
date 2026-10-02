namespace Hub.Core;

/// <summary>Uma linha de tela já em texto. A camada de UI só desenha; a decisão do que mostrar (e do que avisar) é testada aqui.</summary>
public sealed record ScreenLine(string Text, string? Detail = null);

public sealed record ScreenSection(string Title, IReadOnlyList<ScreenLine> Lines);

/// <param name="Banner">Aviso de topo quando o que se vê não é uma leitura atual (cache) ou não há nada para mostrar; <c>null</c> quando tudo é atual.</param>
public sealed record HubScreen(string? Banner, IReadOnlyList<ScreenSection> Sections);

/// <summary>Transforma o <see cref="HubSnapshot"/> no que a tela mostra. Puro e sem UI: só texto, sem inventar dado.</summary>
public static class HubScreenBuilder
{
    /// <summary>Quantas entradas cada bloco do Past/Now/Next mostra; o resto vira "… e mais N".</summary>
    public const int MaxLinesPerSection = 15;

    public const string NoDataBanner = "Sem dados: não foi possível ler o Ecosystem e não há estado anterior guardado.";
    public const string StaleBanner = "Mostrando o último estado conhecido: a leitura atual não foi feita.";

    /// <param name="snapshot">O que foi lido (ou carregado do cache); <c>null</c> se nada existe.</param>
    public static HubScreen Build(HubSnapshot? snapshot)
    {
        if (snapshot is null) return new HubScreen(NoDataBanner, []);

        var sections = new List<ScreenSection> { Products(snapshot) };
        sections.Add(Block("Passado", snapshot.Timeline.Past));
        sections.Add(Block("Agora", snapshot.Timeline.Now));
        sections.Add(Block("Próximo", snapshot.Timeline.Next));
        if (snapshot.Timeline.Notes.Count > 0)
            sections.Add(new ScreenSection("Avisos", snapshot.Timeline.Notes.Select(n => new ScreenLine(n)).ToList()));

        return new HubScreen(snapshot.Stale ? StaleBanner : null, sections);
    }

    static ScreenSection Products(HubSnapshot s)
    {
        if (s.Products.Count == 0)
            return new ScreenSection("Products", [new ScreenLine("Nenhum Product lido de ecosystem.json.")]);

        var lines = s.Products.Select(p =>
        {
            var name = Show(p.Name, p.Id);
            var version = Show(p.Version, "versão indisponível");
            var status = Show(p.Status, "estado indisponível");
            var detail = Release(s, p.Id);
            return new ScreenLine($"{name} · {version} · {status}", detail);
        }).ToList();
        return new ScreenSection("Products", lines);
    }

    /// <summary>Valor do dado; marca o que veio do cache e explicita o que não existe, sem esconder.</summary>
    static string Show(Datum<string> d, string whenMissing) => d.Availability switch
    {
        Availability.Derived => d.Value!,
        Availability.Stale => $"{d.Value} (último estado)",
        _ => whenMissing,
    };

    static string? Release(HubSnapshot s, string productId)
    {
        if (!s.ProductReleases.TryGetValue(productId, out var rel)) return null;
        if (rel.Availability == Availability.NotAvailable) return $"releases indisponíveis{(rel.Note is null ? "" : $" ({rel.Note})")}";
        var latest = rel.Value?.FirstOrDefault();
        return latest is null ? "nenhuma release publicada" : $"última release: {latest.Tag}{(latest.Prerelease ? " (pré-lançamento)" : "")}";
    }

    static ScreenSection Block(string title, IReadOnlyList<TimelineEntry> entries)
    {
        if (entries.Count == 0) return new ScreenSection(title, [new ScreenLine("Nada por aqui.")]);

        var lines = entries.Take(MaxLinesPerSection).Select(e => new ScreenLine($"{e.Id} — {e.Title}", e.Detail)).ToList();
        if (entries.Count > MaxLinesPerSection) lines.Add(new ScreenLine($"… e mais {entries.Count - MaxLinesPerSection}"));
        return new ScreenSection(title, lines);
    }
}
