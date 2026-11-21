using System.IO.Compression;
using Urbe.Client;
using Urbe.Core;

namespace Urbe.Client.Tests;

/// <summary>
/// CST-192: the hard inputs of a publication through every exporter at once — Unicode (accents, emoji, right-to-left),
/// a formula, a table, many chapters, a missing image, a note shared by two publications and a change made outside
/// between two exports. Nothing throws, nothing is silently lost, and the change shows in the next export.
/// </summary>
public sealed class MultiformatRegressionTests
{
    private const string Hard = "# Unicode e companhia\n\nAção ñ ü — 😀 العربية עברית 中文.\n\n$$E = mc^2$$\n\n| A | B |\n|---|---|\n| 1 | 2 |\n\n- item\n";

    private static async Task<IReadOnlyDictionary<string, string>> AllFormats(PublicationProject project, PublicationSourceCatalog catalog)
    {
        var cancel = TestContext.Current.CancellationToken;
        string Zip(byte[] bytes, Func<string, bool> part)
        {
            using var zip = new ZipArchive(new MemoryStream(bytes));
            return string.Concat(zip.Entries.Where(e => part(e.FullName)).Select(e => new StreamReader(e.Open()).ReadToEnd()));
        }
        return new Dictionary<string, string>
        {
            ["html"] = PublicationHtml.Export(project, catalog).Html,
            ["epub"] = Zip(await PublicationEpub.ExportAsync(project, catalog, cancellationToken: cancel), n => n.EndsWith(".xhtml", StringComparison.Ordinal)),
            ["docx"] = Zip((await PublicationDocx.ExportAsync(project, catalog, cancellationToken: cancel)).Bytes, n => n == "word/document.xml"),
            ["odt"] = Zip((await PublicationOdt.ExportAsync(project, catalog, cancellationToken: cancel)).Bytes, n => n == "content.xml"),
            ["pptx"] = Zip((await PublicationPptx.ExportAsync(project, catalog, cancellationToken: cancel)).Bytes, n => n.StartsWith("ppt/slides/slide", StringComparison.Ordinal)),
            ["pdf-bytes"] = (await PublicationPdf.ExportAsync(project, catalog, cancellationToken: cancel)).Bytes.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)
        };
    }

    [Fact]
    public async Task HardInputsSurviveEveryFormatAndAnOutsideChangeReachesTheNextExport()
    {
        var store = new DocumentStore();
        var hard = store.Upsert(new DocumentInput { Path = "Difícil.md", Content = Hard });
        var many = Enumerable.Range(1, 60).Select(i => store.Upsert(new DocumentInput { Path = "Cap " + i + ".md", Content = "# Capítulo " + i + "\n\nTexto " + i + "." })).ToList();
        using var catalog = new PublicationSourceCatalog(store);
        var book = many.Aggregate(PublicationProject.Create("Livro grande", hard.Id), (p, d) => p.AddSource(d.Id));
        var other = PublicationProject.Create("Outra com a mesma nota", hard.Id);   // a source shared by two publications

        var first = await AllFormats(book, catalog);
        foreach (var (format, text) in first.Where(kv => kv.Key != "pdf-bytes"))
        {
            Assert.Contains("Ação ñ ü", text);
            Assert.Contains("😀", text);
            Assert.Contains("العربية", text);
            Assert.Contains("Capítulo 60", text);
        }
        Assert.True(int.Parse(first["pdf-bytes"], System.Globalization.CultureInfo.InvariantCulture) > 1000);
        Assert.Contains("Ação ñ ü", (await AllFormats(other, catalog))["docx"]);

        // changed outside (another editor, a sync): the next export of both publications has the new text
        store.Upsert(new DocumentInput { Path = "Difícil.md", Content = Hard.Replace("Ação", "Revisão") });
        var second = await AllFormats(book, catalog);
        Assert.Contains("Revisão ñ ü", second["html"]);
        Assert.DoesNotContain("Ação ñ ü", second["odt"]);
        Assert.Contains("Revisão ñ ü", (await AllFormats(other, catalog))["pptx"]);
    }

    [Fact]
    public async Task AMissingImageNeverBreaksAnExportAndIsSaid()
    {
        var store = new DocumentStore();
        var note = store.Upsert(new DocumentInput { Path = "Foto.md", Content = "# Foto\n\n![[sumiu.png]]\n\nTexto depois.\n" });
        using var catalog = new PublicationSourceCatalog(store);
        var project = PublicationProject.Create("Com imagem ausente", note.Id);
        var html = PublicationAssets.Embed(PublicationHtml.Export(project, catalog).Html, _ => null);
        Assert.Contains("[imagem não encontrada: sumiu.png]", html);
        Assert.Contains("Texto depois.", html);
        // the office formats stop with the reason, naming the image, instead of writing a file without it
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => PublicationDocx.ExportAsync(project, catalog, _ => Task.FromResult<byte[]?>(null), cancellationToken: TestContext.Current.CancellationToken));
        Assert.Contains("sumiu.png", error.Message);
    }
}
