using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json.Nodes;

namespace Urbe.Core.Tests;

public sealed class PublicationPackageTests
{
    [Fact]
    public void ThePackageHasThePublicationAndAManifestThatChecksIt()
    {
        var project = PublicationProject.Create("Livro", "d1");
        var zipBytes = PublicationPackage.Build(project, "<html>oi</html>", new Dictionary<string, PublicationPackage.Source>
        {
            ["d1"] = new("Livro.md", "abc", "Escrito pela pessoa.\n\nResumo da IA. {ia:\"modelo-b\"}\n")
        }, DateTimeOffset.UnixEpoch);
        using var zip = new ZipArchive(new MemoryStream(zipBytes));
        var index = new MemoryStream();
        zip.GetEntry("index.html")!.Open().CopyTo(index);
        var manifest = JsonNode.Parse(zip.GetEntry("manifest.json")!.Open())!;
        Assert.Equal(project.PublicationId, (string?)manifest["publicationId"]);
        var file = manifest["files"]![0]!;
        Assert.Equal(index.Length, (long)file["bytes"]!);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(index.ToArray())).ToLowerInvariant(), (string?)file["sha256"]);
        var source = manifest["sources"]!["d1"]!;
        Assert.Equal(("Livro.md", "abc", 2, 1), ((string?)source["path"], (string?)source["version"], (int)source["blocks"]!, (int)source["blocksByAi"]!));
        Assert.Equal("modelo-b", (string?)source["aiModels"]![0]);
    }

    [Fact]
    public void SizesReadLikeAPerson() => Assert.Equal(("1 KB", "840 KB", "2,3 MB"),
        (PublicationPackage.Size(10), PublicationPackage.Size(860_000), PublicationPackage.Size(2_411_725)));

    [Fact]
    public void EmbeddedAudioAndVideoGoToMediaAndPlayFromThere()
    {
        var html = "<meta http-equiv=\"Content-Security-Policy\" content=\"default-src 'none'; img-src data:\">" +
            MarkdownEngine.Render("![[aula.mp3]]\n\n![[Pasta/clipe.mp4]]\n\n![[sumiu.wav]]\n\n![[aula.mp3]]", (_, _) => "");
        var refs = PublicationPackage.MediaReferences(html);
        Assert.Equal(["aula.mp3", "Pasta/clipe.mp4", "sumiu.wav"], refs);
        var zipBytes = PublicationPackage.Build(PublicationProject.Create("P"), html, new Dictionary<string, PublicationPackage.Source>(), DateTimeOffset.UnixEpoch,
            new Dictionary<string, byte[]?> { ["aula.mp3"] = [1, 2], ["Pasta/clipe.mp4"] = [3], ["sumiu.wav"] = null });
        using var zip = new ZipArchive(new MemoryStream(zipBytes));
        Assert.Equal(["index.html", "media/1-aula.mp3", "media/2-clipe.mp4", "manifest.json"], zip.Entries.Select(e => e.FullName));
        var index = new StreamReader(zip.GetEntry("index.html")!.Open()).ReadToEnd();
        Assert.Contains("<audio controls preload=\"metadata\" src=\"media/1-aula.mp3\"", index);
        Assert.Contains("<video controls preload=\"metadata\" src=\"media/2-clipe.mp4\"", index);
        Assert.Contains("[mídia não encontrada: sumiu.wav]", index);
        Assert.Contains("media-src 'self' file:", index);
        var manifest = JsonNode.Parse(zip.GetEntry("manifest.json")!.Open())!;
        Assert.Equal("audio/mpeg", (string?)manifest["files"]![1]!["mime"]);
    }
}
