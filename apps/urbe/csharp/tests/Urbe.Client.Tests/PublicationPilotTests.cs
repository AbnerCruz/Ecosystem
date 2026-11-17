using System.IO.Compression;
using System.Text.Json.Nodes;
using Urbe.Client;
using Urbe.Core;

namespace Urbe.Client.Tests;

/// <summary>
/// CST-187: one real project, the same notes, every product: article (HTML), paginated PDF, slides, a motion-graphics
/// timeline with captions, and the distributable package — with a source changed between productions, a second
/// publication on the same note, the file read back from disk (offline), and the package inspected.
/// </summary>
public sealed class PublicationPilotTests
{
    [Fact]
    public async Task OneProjectEveryProductTheSameLiveNotes()
    {
        var cancel = TestContext.Current.CancellationToken;
        var store = new DocumentStore();
        var intro = store.Upsert(new DocumentInput { Path = "Pesquisa/Introdução.md", Content = "# Introdução\n\nA água cobre 71% da Terra.\n" });
        var data = store.Upsert(new DocumentInput { Path = "Pesquisa/Dados.md", Content = "# Dados\n\n| Fonte | % |\n|---|---|\n| Oceanos | 97 |\n" });
        using var catalog = new PublicationSourceCatalog(store);
        var project = PublicationProject.Create("Água", intro.Id).AddSource(data.Id);

        // (a) article and (b) paginated PDF
        var article = PublicationHtml.Export(project, catalog).Html;
        Assert.Contains("71% da Terra", article);
        var pdf = (await PublicationPdf.ExportAsync(project, catalog, cancellationToken: cancel)).Bytes;
        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(pdf, 0, 4));

        // the note changes between productions: (c) slides already show the new text
        store.Upsert(new DocumentInput { Path = "Pesquisa/Introdução.md", Content = "# Introdução\n\nA água cobre cerca de 71% da Terra.\n" });
        var slides = PublicationHtml.Export(project with { Design = project.Design with { Layout = "slides" } }, catalog).Html;
        Assert.Contains("cerca de 71%", slides);

        // (d) motion graphics: one scene per note, with captions from the live text
        var video = VideoComposition.Synced(project with { Design = project.Design with { Layout = "video" } });
        var timeline = VideoTimeline.Build(video, catalog);
        Assert.True(timeline.IsComplete);
        Assert.Equal(2, timeline.Scenes.Count);
        Assert.Contains("cerca de 71%", timeline.Srt());

        // a second publication on the same note sees the same change
        Assert.Contains("cerca de 71%", PublicationHtml.Export(PublicationProject.Create("Resumo", intro.Id), catalog).Html);

        // the project file read back from disk (no network, no copy of the text inside)
        var json = PublicationProjectCodec.Serialize(project);
        Assert.DoesNotContain("71%", json);
        var reopened = PublicationProjectCodec.Parse(json);
        Assert.Contains("cerca de 71%", PublicationHtml.Export(reopened, catalog).Html);

        // the distributable package, opened and checked against its manifest
        var record = PublicationExportRecord.Of(project, "zip", id => store.Get(id)?.Content, DateTimeOffset.UnixEpoch);
        var sources = record.Sources.ToDictionary(kv => kv.Key, kv => new PublicationPackage.Source(store.Get(kv.Key)!.Path, kv.Value, store.Get(kv.Key)!.Content));
        using var zip = new ZipArchive(new MemoryStream(PublicationPackage.Build(project, slides, sources, DateTimeOffset.UnixEpoch)));
        var manifest = JsonNode.Parse(zip.GetEntry("manifest.json")!.Open())!;
        var index = new MemoryStream();
        zip.GetEntry("index.html")!.Open().CopyTo(index);
        Assert.Equal(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(index.ToArray())).ToLowerInvariant(), (string?)manifest["files"]![0]!["sha256"]);
        Assert.Equal("Pesquisa/Dados.md", (string?)manifest["sources"]![data.Id]!["path"]);

        // the change since the package is said when the publication is reopened
        store.Upsert(new DocumentInput { Path = "Pesquisa/Dados.md", Content = "# Dados\n\n| Fonte | % |\n|---|---|\n| Oceanos | 96,5 |\n" });
        Assert.Equal([data.Id], record.Since(project, id => store.Get(id)?.Content).Changed.Select(c => c.Id));
    }
}
