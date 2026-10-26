using System.Text;
using Urbe.Core;

namespace Urbe.Core.Tests;

public sealed class VaultCompatibilityTests
{
    private static VaultFile File(string path, string content) => new(path, Encoding.UTF8.GetBytes(content));

    [Fact]
    public void OnlyIncompatibleFilesAreBlockedWithTheInstalledVersion()
    {
        var files = new[]
        {
            File(".urbe/compatibility.json", """
                {"schemaVersion":1,"files":[
                  {"path":"Novo.md","minWriterVersion":"2.0.0-beta.50","features":["markdown.v1"]},
                  {"path":"Antigo.md","minWriterVersion":"2.0.0-beta.20","features":["markdown.v1"]}]}
                """),
            File("Novo.md", "# Novo"),
            File("Antigo.md", "# Antigo"),
            File("Livre.md", "# Livre")
        };
        var snapshot = VaultReader.Read(files, "2.0.0-beta.31");
        Assert.False(snapshot.IsReadOnly);
        Assert.Contains("Novo.md", snapshot.FutureFiles);
        Assert.DoesNotContain("Antigo.md", snapshot.FutureFiles);
        Assert.DoesNotContain("Livre.md", snapshot.FutureFiles);
        Assert.Null(VaultWriter.PlanOne(files, snapshot, "Novo.md", "# mudança proibida"));
        Assert.NotNull(VaultWriter.PlanOne(files, snapshot, "Antigo.md", "# mudança permitida"));
        Assert.NotNull(VaultWriter.PlanOne(files, snapshot, "Livre.md", "# livre"));
    }

    [Fact]
    public void UnknownCapabilityRemainsProtectedEvenWithNewerAppNumber()
    {
        var files = new[]
        {
            File(".urbe/compatibility.json", """{"schemaVersion":1,"files":[{"path":"Misterio.md","minWriterVersion":"2.0.0-beta.1","features":["future.magic.v1"]}]}"""),
            File("Misterio.md", "# Conteúdo")
        };
        var snapshot = VaultReader.Read(files, "9.0.0");
        Assert.False(snapshot.IsReadOnly);
        Assert.Contains("Misterio.md", snapshot.FutureFiles);
        Assert.Null(VaultWriter.PlanOne(files, snapshot, "Misterio.md", "# estrago"));
    }

    [Theory]
    [InlineData("""{"schemaVersion":2,"files":[]}""")]
    [InlineData("""{"schemaVersion":1,"files":[{"path":"../A.md","minWriterVersion":"2.0.0","features":[]}]}""")]
    [InlineData("{malformed}")]
    public void CorruptOrUnknownManifestMakesVaultReadOnlyWithoutMutatingIt(string metadata)
    {
        var original = File(".urbe/compatibility.json", metadata);
        var files = new[] { original, File("A.md", "Original") };
        var snapshot = VaultReader.Read(files, "2.0.0-beta.31");
        Assert.True(snapshot.IsReadOnly);
        Assert.Equal(metadata, Encoding.UTF8.GetString(original.Bytes.Span));
        Assert.Null(VaultWriter.PlanOne(files, snapshot, "A.md", "Mudança"));
    }

    [Fact]
    public void OrdinaryVaultWithoutCompatibilityManifestKeepsItsCurrentBehavior()
    {
        var files = new[] { File("Base.md", "# texto") };
        var snapshot = VaultReader.Read(files, "2.0.0-beta.31");
        Assert.False(snapshot.IsReadOnly);
        Assert.Empty(snapshot.FutureFiles);
        Assert.NotNull(VaultWriter.PlanOne(files, snapshot, "Base.md", "# editado"));
    }
}
