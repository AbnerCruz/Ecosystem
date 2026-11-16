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
        var zipBytes = PublicationPackage.Build(project, "<html>oi</html>", new Dictionary<string, string> { ["d1"] = "abc" }, DateTimeOffset.UnixEpoch);
        using var zip = new ZipArchive(new MemoryStream(zipBytes));
        var index = new MemoryStream();
        zip.GetEntry("index.html")!.Open().CopyTo(index);
        var manifest = JsonNode.Parse(zip.GetEntry("manifest.json")!.Open())!;
        Assert.Equal(project.PublicationId, (string?)manifest["publicationId"]);
        var file = manifest["files"]![0]!;
        Assert.Equal(index.Length, (long)file["bytes"]!);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(index.ToArray())).ToLowerInvariant(), (string?)file["sha256"]);
        Assert.Equal("abc", (string?)manifest["sources"]!["d1"]);
    }

    [Fact]
    public void SizesReadLikeAPerson() => Assert.Equal(("1 KB", "840 KB", "2,3 MB"),
        (PublicationPackage.Size(10), PublicationPackage.Size(860_000), PublicationPackage.Size(2_411_725)));
}
