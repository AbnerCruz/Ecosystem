using System.IO.Compression;
using System.Text.RegularExpressions;
using Urbe.Core;

namespace Urbe.Core.Tests;

/// <summary>What every output keeps and leaves out: nested boxes (CST-026) and AI authorship (CST-035).</summary>
public sealed class NestedContainersExportTests
{
    private const string Note = "# Personagens\n\n:::ficha Frodo\nUm hobbit.\n\n:::atributos Valores\nForça: 3 %%anotação oculta%%\n:::\n\nDepois da caixa interna.\n:::\n\nFim.\n";

    private static Task<byte[]?> NoAsset(string _) => Task.FromResult<byte[]?>(null);

    private static string ZipText(byte[] zip)
    {
        using var archive = new ZipArchive(new MemoryStream(zip));
        return string.Concat(archive.Entries.Where(e => e.FullName.EndsWith(".xml", StringComparison.Ordinal) || e.FullName.EndsWith(".xhtml", StringComparison.Ordinal))
            .Select(e => { using var r = new StreamReader(e.Open()); return Regex.Replace(r.ReadToEnd(), "<[^>]+>", " "); }));
    }

    [Fact]
    public async Task EveryOutputKeepsTheNestedBoxAndLeavesTheCommentOut()
    {
        var store = new DocumentStore();
        var note = store.Upsert(new DocumentInput { Path = "Personagens.md", Content = Note });
        using var catalog = new PublicationSourceCatalog(store);
        var project = PublicationProject.Create("Fichas", note.Id);
        var cancel = TestContext.Current.CancellationToken;
        var outputs = new Dictionary<string, string>
        {
            ["html"] = PublicationHtml.Export(project, catalog).Html,
            ["epub"] = ZipText(await PublicationEpub.ExportAsync(project, catalog, NoAsset, cancel)),
            ["docx"] = ZipText((await PublicationDocx.ExportAsync(project, catalog, NoAsset, cancel)).Bytes),
            ["odt"] = ZipText((await PublicationOdt.ExportAsync(project, catalog, NoAsset, cancel)).Bytes),
            ["pptx"] = ZipText((await PublicationPptx.ExportAsync(project, catalog, NoAsset, cancel)).Bytes),
        };
        foreach (var (format, text) in outputs)
        {
            foreach (var kept in new[] { "Frodo", "Um hobbit.", "Valores", "Força: 3", "Depois da caixa interna.", "Fim." })
                Assert.True(text.Contains(kept, StringComparison.Ordinal), format + " perdeu “" + kept + "”");
            Assert.False(text.Contains("anotação oculta", StringComparison.Ordinal), format + " mostrou o comentário");
        }
    }

    /// <summary>CST-035: an AI-written block shows its model discreetly ("IA: modelo") in every output, never the raw seal.</summary>
    [Fact]
    public async Task EveryOutputShowsAuthorshipDiscreetlyWithoutTheRawSeal()
    {
        var store = new DocumentStore();
        var note = store.Upsert(new DocumentInput { Path = "Resumo.md", Content = "# Resumo\n\nEscrito pela pessoa.\n\nParágrafo da IA. {ia:\"modelo-x\"}\n" });
        using var catalog = new PublicationSourceCatalog(store);
        var project = PublicationProject.Create("Resumo", note.Id);
        var cancel = TestContext.Current.CancellationToken;
        var outputs = new Dictionary<string, string>
        {
            ["html"] = PublicationHtml.Export(project, catalog).Html,
            ["slides"] = PublicationHtml.Export(project with { Design = project.Design with { Layout = "slides" } }, catalog).Html,
            ["epub"] = ZipText(await PublicationEpub.ExportAsync(project, catalog, NoAsset, cancel)),
            ["docx"] = ZipText((await PublicationDocx.ExportAsync(project, catalog, NoAsset, cancel)).Bytes),
            ["odt"] = ZipText((await PublicationOdt.ExportAsync(project, catalog, NoAsset, cancel)).Bytes),
            ["pptx"] = ZipText((await PublicationPptx.ExportAsync(project, catalog, NoAsset, cancel)).Bytes),
        };
        foreach (var (format, text) in outputs)
        {
            Assert.True(text.Contains("IA: modelo-x", StringComparison.Ordinal), format + " não mostra a autoria");
            Assert.False(text.Contains("{ia:", StringComparison.Ordinal), format + " mostra o selo cru");
            Assert.True(text.Contains("Escrito pela pessoa.", StringComparison.Ordinal), format);
        }
        Assert.Equal("Escrito pela pessoa.\n\nParágrafo da IA. {ia:\"modelo-x\"}\n", store.Get(note.Id)!.Content.Split("# Resumo\n\n")[1]);   // the note keeps its seal
    }
}
