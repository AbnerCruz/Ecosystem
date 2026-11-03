using Urbe.Core;

namespace Urbe.Core.Tests;

public sealed class ReaderUriTests
{
    [Theory]
    [InlineData("file:///private/vault.md")]
    [InlineData("content://private/1")]
    [InlineData("javascript:alert(1)")]
    [InlineData("https://user:secret@example.org/")]
    public void BrowserNeverAcceptsLocalProtocolsOrEmbeddedCredentials(string value) => Assert.Null(ReaderUris.Web(value));
    [Fact]
    public void YouTubeReferencesKeepOfficialIdentityAndTimestamp()
    {
        Assert.Equal(("abcdefghijk", 120), ReaderUris.YouTube("https://www.youtube.com/watch?v=abcdefghijk&t=120s"));
        Assert.Equal(("abcdefghijk", 0), ReaderUris.YouTube("https://youtu.be/abcdefghijk"));
        Assert.Null(ReaderUris.YouTube("https://youtube.com.evil.invalid/watch?v=abcdefghijk"));
        Assert.Null(ReaderUris.YouTube("https://www.youtube.com/watch?v=<script>"));
    }
}
