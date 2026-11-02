using Lunet.Ai;
using Lunet.Core;

namespace Lunet.Tests;

public sealed class LunetAiCapabilitiesTests
{
    [Fact]
    public void Search_ReturnsGroundedLineNumbersAndRespectsResultCap()
    {
        var root = Path.Combine(Path.GetTempPath(), "lunet-ai-search-" + Guid.NewGuid().ToString("N"));
        try
        {
            var project = new ProjectStore(root).Create("SearchGame");
            var files = new LunetAiProjectFiles(project);
            files.Write("Content/Data/quests.txt", "dragon one\nno match\nDragon two\n", "");
            var hits = files.Search("dragon", 1);
            Assert.Single(hits);
            Assert.Equal("Content/Data/quests.txt", hits[0].Path);
            Assert.Equal(1, hits[0].Line);
            Assert.Equal(1, hits[0].Column);
            Assert.Empty(files.Search("missing", 5));
            Assert.Throws<ArgumentOutOfRangeException>(() => files.Search("dragon", 0));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void ReadExcerpt_ReturnsFullFileHash_WithoutTruncationAmbiguity()
    {
        var root = Path.Combine(Path.GetTempPath(), "lunet-ai-excerpt-" + Guid.NewGuid().ToString("N"));
        try
        {
            var project = new ProjectStore(root).Create("ExcerptGame");
            var files = new LunetAiProjectFiles(project);
            files.Write("Content/Data/story.txt", "alpha\nbeta\ngamma\ndelta", "");
            var excerpt = files.ReadExcerpt("Content/Data/story.txt", 2, 2);
            Assert.Equal("beta\ngamma", excerpt.Content);
            Assert.Equal(4, excerpt.TotalLines);
            Assert.Equal(2, excerpt.StartLine);
            Assert.Equal(3, excerpt.EndLine);
            Assert.Equal(files.Read("Content/Data/story.txt").Sha256, excerpt.Sha256);
            Assert.Throws<ArgumentOutOfRangeException>(() => files.ReadExcerpt("Content/Data/story.txt", 0, 3));
            Assert.Throws<ArgumentOutOfRangeException>(() => files.ReadExcerpt("Content/Data/story.txt", 1, 101));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void Replace_RejectsStaleHashAndAmbiguousText()
    {
        var root = Path.Combine(Path.GetTempPath(), "lunet-ai-replace-" + Guid.NewGuid().ToString("N"));
        try
        {
            var project = new ProjectStore(root).Create("ReplaceGame");
            var files = new LunetAiProjectFiles(project);
            files.Write("Content/Data/config.txt", "old\nkeep\nold\n", "");
            var initial = files.Read("Content/Data/config.txt");
            Assert.Throws<InvalidOperationException>(() => files.Replace("Content/Data/config.txt", "old", "new", initial.Sha256));
            Assert.Throws<InvalidOperationException>(() => files.Replace("Content/Data/config.txt", "keep", "saved", "stale"));
            var changed = files.Replace("Content/Data/config.txt", "keep", "saved", initial.Sha256);
            Assert.Equal("old\nsaved\nold\n", files.Read("Content/Data/config.txt").Content);
            Assert.Equal(changed.Sha256, files.Read("Content/Data/config.txt").Sha256);
            Assert.Throws<InvalidOperationException>(() => files.Replace("Content/Data/config.txt", "saved", "oops", initial.Sha256));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
    }

    [Theory]
    [InlineData("http://example.org/chat/completions")]
    [InlineData("https://user:secret@example.org/chat/completions")]
    [InlineData("https://example.org/chat/completions?token=secret")]
    [InlineData("https://example.org/chat/completions#fragment")]
    [InlineData("https://example.org/other")]
    public void Endpoint_RejectsInsecureOrAmbiguousConfigurations(string endpoint)
    {
        Assert.Throws<ArgumentException>(() => LunetAiEndpoint.Validate(endpoint));
    }

    [Fact]
    public void Endpoint_AcceptsHttpsChatCompletions()
    {
        var uri = LunetAiEndpoint.Validate("https://openrouter.ai/api/v1/chat/completions");
        Assert.Equal("https", uri.Scheme);
        Assert.Equal("openrouter.ai", uri.Host);
    }
}
