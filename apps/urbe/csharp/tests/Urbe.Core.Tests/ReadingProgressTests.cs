using Urbe.Core;

namespace Urbe.Core.Tests;

/// <summary>CST-241: progress by source, reader defaults and the additive growth of the private reading state.</summary>
public sealed class ReadingProgressTests
{
    private static readonly string Vault = new('a', 64), Other = new('e', 64), Hash = new('b', 64);
    private static ReaderSession Session(string path, string format, ReaderLocation at, long updated) => new(Vault, path, Hash, format, at, new(), updated);

    [Fact]
    public void FilesWrittenBeforeTotalsAndDefaultsStillReadAndStayVersionOne()
    {
        var old = "{\"version\":1,\"entries\":[{\"vaultId\":\"" + Vault + "\",\"path\":\"a.pdf\",\"revision\":\"" + Hash +
            "\",\"format\":\"pdf\",\"location\":{\"part\":12,\"offset\":0,\"scroll\":0},\"preferences\":{\"fontSize\":18,\"lineHeight\":1.6,\"zoom\":1.5,\"theme\":\"system\",\"margin\":5},\"updated\":10,\"deleted\":false}]}";
        var catalog = ReaderSessionCatalog.Read(System.Text.Encoding.UTF8.GetBytes(old));
        Assert.False(catalog.ReadOnly);
        var session = catalog.Restore(Vault, "a.pdf", Hash)!;
        Assert.Null(session.Location.Total);
        Assert.Equal(new ReaderProgressInfo("página 12", null), ReaderProgress.Describe(session));
        catalog.RecordDefault(new(ReaderKinds.Text, new(21, 1.8, Theme: "sepia"), 20));
        var json = System.Text.Json.JsonDocument.Parse(catalog.Serialize()).RootElement;
        Assert.Equal(1, json.GetProperty("version").GetInt32());
        // a reader of the earlier schema ignores the new members and keeps the positions
        var older = System.Text.Json.JsonSerializer.Deserialize<OldModel>(catalog.Serialize(), new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        Assert.Equal(12, older.Entries.Single().Location.Part);
    }
    private sealed record OldLocation(int Part, int Offset, double Scroll);
    private sealed record OldEntry(string Path, OldLocation Location);
    private sealed record OldModel(int Version, List<OldEntry> Entries);

    [Fact]
    public void ReaderDefaultsAreKeptPerKindMergedByTimeAndNeverMixed()
    {
        var catalog = ReaderSessionCatalog.Read(null);
        Assert.Equal(new ReaderPreferences(), catalog.PreferencesFor(VaultFileKinds.Epub));
        Assert.True(catalog.RecordDefault(new(ReaderKinds.Book, new(24, 2, 3, "sepia", 10), 10)));
        Assert.True(catalog.RecordDefault(new(ReaderKinds.Pdf, new(Zoom: 1.75), 11)));
        Assert.False(catalog.RecordDefault(new(ReaderKinds.Pdf, new(Zoom: 1.75), 12)));
        var book = catalog.PreferencesFor(VaultFileKinds.Epub);
        Assert.Equal((24d, 2d, "sepia", 10d, 1d), (book.FontSize, book.LineHeight, book.Theme, book.Margin, book.Zoom));
        var pdf = catalog.PreferencesFor(VaultFileKinds.Pdf);
        Assert.Equal((18d, 1.75), (pdf.FontSize, pdf.Zoom));
        Assert.Equal(new ReaderPreferences(), catalog.PreferencesFor(VaultFileKinds.Other));

        var reopened = ReaderSessionCatalog.Read(catalog.Serialize());
        Assert.Equal(24, reopened.DefaultFor(ReaderKinds.Book)!.FontSize);
        var newer = ReaderSessionCatalog.Read(null);
        newer.RecordDefault(new(ReaderKinds.Book, new(30), 50));
        newer.RecordDefault(new(ReaderKinds.Pdf, new(Zoom: 3), 1));
        reopened.Merge(newer);
        Assert.Equal(30, reopened.DefaultFor(ReaderKinds.Book)!.FontSize);
        Assert.Equal(1.75, reopened.DefaultFor(ReaderKinds.Pdf)!.Zoom);
    }

    [Fact]
    public void InvalidTotalsAndReaderKindsAreRejected()
    {
        var catalog = ReaderSessionCatalog.Read(null);
        Assert.Throws<InvalidDataException>(() => catalog.Record(Session("a.pdf", "pdf", new(1, Total: 0), 1)));
        Assert.Throws<InvalidDataException>(() => catalog.RecordDefault(new("Editor!", new(), 1)));
        Assert.Throws<InvalidDataException>(() => catalog.RecordDefault(new(ReaderKinds.Text, new(double.PositiveInfinity), 1)));
    }

    [Fact]
    public void RecentListsResumableSourcesOfTheVaultMostRecentFirst()
    {
        var catalog = ReaderSessionCatalog.Read(null);
        catalog.Record(Session("Livros/b.epub", "epub", new(3, Total: 12), 30));
        catalog.Record(Session("a.pdf", "pdf", new(5, Total: 9), 50));
        catalog.Record(Session("foto.png", "image", new(1), 90));
        catalog.Record(Session("notas.txt", "other", new(1), 80));
        catalog.Record(Session("antigo.doc", "document", new(1), 70));
        catalog.Record(Session("aula.mp4", "video", new(61), 40));
        catalog.Record(Session("apagado.pdf", "pdf", new(1), 60) with { Deleted = true });
        catalog.Record(Session("outro.pdf", "pdf", new(1), 99) with { VaultId = Other });
        Assert.Equal(["a.pdf", "aula.mp4", "Livros/b.epub"], catalog.Recent(Vault, 10).Select(s => s.Path));
        Assert.Single(catalog.Recent(Vault, 1));
        Assert.Null(catalog.Latest(Vault, "apagado.pdf"));
        Assert.Equal(Hash, catalog.Latest(Vault, "a.pdf")!.Revision);
    }

    [Fact]
    public void ProgressUsesEachFormatsOwnSemantics()
    {
        Assert.Equal("página 12 de 240 · 5%", ReaderProgress.Describe(Session("a.pdf", "pdf", new(12, Total: 240), 1)).Position);
        Assert.Equal(1, ReaderProgress.Describe(Session("a.pdf", "pdf", new(240, Total: 240), 1)).Fraction);
        // EPUB: spine position plus how far into the chapter, never a page count
        var epub = ReaderProgress.Describe(Session("b.epub", "epub", new(3, 900, .5, 12), 1));
        Assert.Equal("capítulo 3 de 12 · 21% dos capítulos", epub.Position);
        Assert.Equal(2.5 / 12, epub.Fraction!.Value, 6);
        Assert.Equal("seção 2 de 4 · 25% das seções", ReaderProgress.Describe(Session("c.docx", "document", new(2, Total: 4), 1)).Position);
        Assert.Equal("slide 3 de 20 · 15%", ReaderProgress.Describe(Session("d.pptx", "slides", new(3, Total: 20), 1)).Position);
        Assert.Equal(new ReaderProgressInfo("em 1:05", null), ReaderProgress.Describe(Session("e.mp4", "video", new(66), 1)));
        Assert.Equal("em 1:00:00", ReaderProgress.Describe(Session("e.mp3", "audio", new(3601), 1)).Position);
        Assert.Equal("capítulo 7", ReaderProgress.Describe(Session("b.epub", "epub", new(7), 1)).Position);
    }

    [Fact]
    public void LastReadIsAShortRelativeTime()
    {
        const long now = 1_800_000_000_000;
        Assert.Equal("agora há pouco", ReaderProgress.LastRead(now - 20_000, now));
        Assert.Equal("há 5 min", ReaderProgress.LastRead(now - 5 * 60_000, now));
        Assert.Equal("há 3 h", ReaderProgress.LastRead(now - 3 * 3_600_000, now));
        Assert.Equal("ontem", ReaderProgress.LastRead(now - 30 * 3_600_000, now));
        Assert.Equal("há 4 dias", ReaderProgress.LastRead(now - 4 * 86_400_000L, now));
    }
}
