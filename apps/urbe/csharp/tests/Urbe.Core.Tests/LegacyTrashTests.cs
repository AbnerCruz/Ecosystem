using Urbe.Core;

namespace Urbe.Core.Tests;

/// <summary>The trash of the vault (.urbe/trash.v2.json, trash.js): another version is kept and never written; v1 is read.</summary>
public sealed class LegacyTrashTests
{
    [Theory]
    [InlineData("""{"version":3,"items":[]}""")]
    [InlineData("""{"items":[]}""")]
    [InlineData("não é json")]
    public void ATrashOfAnotherVersionIsPreservedAndNeverWritten(string v2)
    {
        var trash = LegacyTrash.Read(v2, null, out var readOnly);
        Assert.True(readOnly);
        Assert.Empty(trash.Items);
    }

    [Fact]
    public void AVersion1TrashIsReadWhenThereIsNoVersion2()
    {
        var v1 = """{"items":[{"document":{"id":"doc_a","path":"A.md","content":"x"},"deletedAt":5,"originalPath":"A.md"},{"document":{"path":"sem id"}}]}""";
        var trash = LegacyTrash.Read(null, v1, out var readOnly);
        Assert.False(readOnly);
        Assert.Equal("{\n \"version\": 2,\n \"items\": [\n  {\n   \"document\": {\n    \"id\": \"doc_a\",\n    \"path\": \"A.md\",\n    \"content\": \"x\"\n   },\n   \"deletedAt\": 5,\n   \"originalPath\": \"A.md\"\n  }\n ]\n}", trash.Export());
    }
}
