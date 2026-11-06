using Urbe.Core;

namespace Urbe.Tests.Fixtures;

/// <summary>An EPUB made by the Urbe's own exporter, with figures (one described, two without alt).</summary>
public static class BookFixture
{

    public static async Task<byte[]> WithFigures()
    {
        var store = new DocumentStore();
        var one = store.Upsert(new DocumentInput { Path = "Um.md", Content = "# Ciclo\n\nTexto.\n\n![Diagrama do ciclo da água](ciclo.png)\n\n![](ciclo.png)\n\n![](sem.png)" });
        var two = store.Upsert(new DocumentInput { Path = "Dois.md", Content = "# Sem figuras\n\nSó texto." });
        using var catalog = new PublicationSourceCatalog(store);
        return await PublicationEpub.ExportAsync(PublicationProject.Create("Livro", one.Id).AddSource(two.Id), catalog,
            path => Task.FromResult<byte[]?>(PptxFixture.Png(32, 24, 30, 120, 200)), TestContext.Current.CancellationToken);
    }
}
