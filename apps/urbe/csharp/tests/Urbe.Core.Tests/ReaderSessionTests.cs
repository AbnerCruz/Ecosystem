using Urbe.Core;

namespace Urbe.Core.Tests;

public sealed class ReaderSessionTests
{
    private static readonly string Vault = new('a', 64), Hash = new('b', 64);
    [Fact]
    public void SessionsAreRevisionBoundAndSurviveSerializationAndMoves()
    {
        var catalog = ReaderSessionCatalog.Read(null);
        catalog.Record(new(Vault, "Books/a.epub", Hash, "epub", new(4, 120, .4), new(24, 1.8, 1.5), 10));
        var reopened = ReaderSessionCatalog.Read(catalog.Serialize());
        Assert.Equal(4, reopened.Restore(Vault, "Books/a.epub", Hash)!.Location.Part);
        Assert.Null(reopened.Restore(Vault, "Books/a.epub", new string('c', 64)));
        Assert.Null(reopened.Restore(new string('d', 64), "Books/a.epub", Hash));
        reopened.Move(Vault, "Books", "Library", true);
        Assert.Null(reopened.Restore(Vault, "Books/a.epub", Hash));
        Assert.Equal(24, reopened.Restore(Vault, "Library/a.epub", Hash)!.Preferences.FontSize);
    }
    [Theory]
    [InlineData("{\"version\":99,\"entries\":[]}")]
    [InlineData("not-json")]
    public void FutureAndCorruptMetadataArePreservedReadOnly(string data)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(data);
        var catalog = ReaderSessionCatalog.Read(bytes);
        Assert.True(catalog.ReadOnly);
        Assert.False(catalog.Record(new(Vault, "a.pdf", Hash, "pdf", new(1), new(), 0)));
        Assert.Throws<InvalidOperationException>(() => catalog.Serialize());
    }
    [Fact]
    public void InvalidPathsAndNonFinitePreferencesAreRejected()
    {
        var catalog = ReaderSessionCatalog.Read(null);
        Assert.Throws<InvalidDataException>(() => catalog.Record(new(Vault, "../escape", Hash, "pdf", new(1), new(), 0)));
        Assert.Throws<InvalidDataException>(() => catalog.Record(new(Vault, "a.pdf", Hash, "pdf", new(1), new(double.NaN), 0)));
    }
}
