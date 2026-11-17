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

    /// <summary>CST-236: share-link times, playlists and the official player's address.</summary>
    [Fact]
    public void YouTubeShareTimesPlaylistsAndTheEmbeddedPlayer()
    {
        Assert.Equal(150, ReaderUris.YouTube("https://youtu.be/abcdefghijk?t=2m30s")!.Value.Second);
        Assert.Equal(3723, ReaderUris.YouTube("https://www.youtube.com/watch?v=abcdefghijk&t=1h2m3s")!.Value.Second);
        Assert.Equal(0, ReaderUris.Seconds("bobagem"));
        Assert.Equal("PLx_1-y", ReaderUris.YouTubePlaylist("https://www.youtube.com/playlist?list=PLx_1-y"));
        Assert.Null(ReaderUris.YouTubePlaylist("https://www.youtube.com/playlist?list=<x>"));
        Assert.Equal("https://www.youtube.com/embed/videoseries?list=PLx_1-y", ReaderUris.YouTubeEmbed("https://www.youtube.com/playlist?list=PLx_1-y")!.AbsoluteUri);
        Assert.Equal("https://www.youtube.com/embed/abcdefghijk?start=90&list=PLx_1-y", ReaderUris.YouTubeEmbed("https://www.youtube.com/watch?v=abcdefghijk&list=PLx_1-y&t=1m30s")!.AbsoluteUri);
        Assert.Null(ReaderUris.YouTubeEmbed("https://example.org/watch?v=abcdefghijk"));
        // the playlist player is not a video called "videoseries", and both players get the app's Referer
        Assert.Null(ReaderUris.YouTube("https://www.youtube.com/embed/videoseries?list=PLx_1-y"));
        Assert.True(ReaderUris.IsYouTubeEmbed("https://www.youtube.com/embed/videoseries?list=PLx_1-y"));
        Assert.True(ReaderUris.IsYouTubeEmbed("https://www.youtube.com/embed/abcdefghijk?start=0"));
        Assert.False(ReaderUris.IsYouTubeEmbed("https://www.youtube.com/watch?v=abcdefghijk"));
    }
}
