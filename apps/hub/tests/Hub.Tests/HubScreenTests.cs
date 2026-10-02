using Hub.Core;

namespace Hub.Tests;

public class HubScreenTests
{
    static Datum<string> D(string v) => Datum<string>.From(v, "s");
    static Datum<string> Missing => Datum<string>.Missing("s");

    static ProductSummary Product(string id, Datum<string>? name = null, Datum<string>? version = null, Datum<string>? status = null) =>
        new(id, name ?? D(id.ToUpperInvariant()), D("product"), status ?? D("active"), version ?? D("1.0.0"), Missing, Missing);

    static TimelineEntry E(string id, string? detail = null) => new(EntryKind.RoadmapItem, id, $"título {id}", "ROADMAP.md", detail);

    static HubSnapshot Snap(IReadOnlyList<ProductSummary>? products = null, PastNowNext? timeline = null,
        IReadOnlyDictionary<string, Datum<IReadOnlyList<ReleaseInfo>>>? releases = null, bool stale = false) =>
        new(products ?? [], timeline ?? new PastNowNext([], [], []), releases ?? new Dictionary<string, Datum<IReadOnlyList<ReleaseInfo>>>(), stale);

    static ScreenSection Section(HubScreen s, string title) => s.Sections.Single(x => x.Title == title);

    [Fact]
    public void NoSnapshotMeansAnHonestBannerAndNoSections()
    {
        var s = HubScreenBuilder.Build(null);
        Assert.Equal(HubScreenBuilder.NoDataBanner, s.Banner);
        Assert.Empty(s.Sections);
    }

    [Fact]
    public void FreshSnapshotHasNoBannerAndTheFixedSectionOrder()
    {
        var s = HubScreenBuilder.Build(Snap([Product("alpha")]));
        Assert.Null(s.Banner);
        Assert.Equal(["Products", "Passado", "Agora", "Próximo"], s.Sections.Select(x => x.Title));
    }

    [Fact]
    public void StaleSnapshotSaysItIsNotACurrentReading()
    {
        var s = HubScreenBuilder.Build(Snap([Product("alpha")], stale: true));
        Assert.Equal(HubScreenBuilder.StaleBanner, s.Banner);
    }

    [Fact]
    public void ProductLineShowsNameVersionAndStatus()
    {
        var line = Section(HubScreenBuilder.Build(Snap([Product("alpha", version: D("2.0.1"))])), "Products").Lines.Single();
        Assert.Equal("ALPHA · 2.0.1 · active", line.Text);
    }

    [Fact]
    public void MissingDataIsSaidNotHidden()
    {
        var p = Product("alpha", name: Missing, version: Missing, status: Missing);
        var line = Section(HubScreenBuilder.Build(Snap([p])), "Products").Lines.Single();
        Assert.Equal("alpha · versão indisponível · estado indisponível", line.Text);   // sem nome, cai no id; nada é inventado
    }

    [Fact]
    public void ValuesFromTheCacheAreMarkedPerField()
    {
        var p = Product("alpha").AsStale();
        var line = Section(HubScreenBuilder.Build(Snap([p], stale: true)), "Products").Lines.Single();
        Assert.Equal("ALPHA (último estado) · 1.0.0 (último estado) · active (último estado)", line.Text);
    }

    [Fact]
    public void NoProductsIsSaid()
    {
        Assert.Contains("Nenhum Product", Section(HubScreenBuilder.Build(Snap()), "Products").Lines.Single().Text);
    }

    [Fact]
    public void LatestReleaseOfEachProductIsShown()
    {
        var releases = new Dictionary<string, Datum<IReadOnlyList<ReleaseInfo>>>
        {
            ["alpha"] = Datum<IReadOnlyList<ReleaseInfo>>.From([new("v2.0.0-beta", null, true, null, "u", 1), new("v1.0.0", null, false, null, "u", 1)], "s"),
            ["beta"] = Datum<IReadOnlyList<ReleaseInfo>>.From([], "s"),
            ["gamma"] = Datum<IReadOnlyList<ReleaseInfo>>.Missing("s", "HTTP 403"),
        };
        var lines = Section(HubScreenBuilder.Build(Snap([Product("alpha"), Product("beta"), Product("gamma"), Product("delta")], releases: releases)), "Products").Lines;
        Assert.Equal("última release: v2.0.0-beta (pré-lançamento)", lines[0].Detail);
        Assert.Equal("nenhuma release publicada", lines[1].Detail);
        Assert.Equal("releases indisponíveis (HTTP 403)", lines[2].Detail);
        Assert.Null(lines[3].Detail);                                                    // sem repositório declarado: nada a dizer
    }

    [Fact]
    public void TimelineBlocksShowIdTitleAndDetail()
    {
        var s = HubScreenBuilder.Build(Snap(timeline: new PastNowNext([E("P1-1")], [E("P1-2", "aguardando validação/revisão")], [E("P1-3", "pronta")])));
        Assert.Equal("P1-1 — título P1-1", Section(s, "Passado").Lines.Single().Text);
        Assert.Equal("aguardando validação/revisão", Section(s, "Agora").Lines.Single().Detail);
        Assert.Equal("pronta", Section(s, "Próximo").Lines.Single().Detail);
    }

    [Fact]
    public void EmptyBlocksSaySo()
    {
        var s = HubScreenBuilder.Build(Snap());
        Assert.All(new[] { "Passado", "Agora", "Próximo" }, t => Assert.Equal("Nada por aqui.", Section(s, t).Lines.Single().Text));
    }

    [Fact]
    public void LongBlocksAreCappedAndTheRestIsCounted()
    {
        var many = Enumerable.Range(1, HubScreenBuilder.MaxLinesPerSection + 4).Select(i => E($"P9-{i}")).ToList();
        var lines = Section(HubScreenBuilder.Build(Snap(timeline: new PastNowNext(many, [], []))), "Passado").Lines;
        Assert.Equal(HubScreenBuilder.MaxLinesPerSection + 1, lines.Count);
        Assert.Equal("… e mais 4", lines[^1].Text);
    }

    [Fact]
    public void NotesBecomeAWarningsSectionOnlyWhenThereAreNotes()
    {
        Assert.DoesNotContain(HubScreenBuilder.Build(Snap()).Sections, x => x.Title == "Avisos");
        var s = HubScreenBuilder.Build(Snap(timeline: new PastNowNext([], [], [], ["ROADMAP.md indisponível — HTTP 403 [u]"])));
        Assert.Equal("ROADMAP.md indisponível — HTTP 403 [u]", Section(s, "Avisos").Lines.Single().Text);
    }

    [Fact]
    public void EndToEndWithTheCacheOfARealLoad()
    {
        var live = Snap([Product("alpha")], new PastNowNext([E("P1-1")], [], []));
        var screen = HubScreenBuilder.Build(SnapshotCache.Load(SnapshotCache.Serialize(live)));
        Assert.Equal(HubScreenBuilder.StaleBanner, screen.Banner);
        Assert.Contains("(último estado)", Section(screen, "Products").Lines.Single().Text);
        Assert.Equal("P1-1 — título P1-1", Section(screen, "Passado").Lines.Single().Text);
    }
}
