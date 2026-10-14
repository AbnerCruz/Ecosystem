using Urbe.Core;

namespace Urbe.Core.Tests;

/// <summary>DEC-0045 (UC-41): .urbe/mundo.json, apart from mapa.json (which the 1.8.4 rebuilds with only its own fields).</summary>
public sealed class WorldPrefsTests
{
    [Fact]
    public void DefaultsAreWallsAndSoftOutlines()
    {
        var p = WorldPrefs.Parse(null, out var newer);
        Assert.False(newer);
        Assert.True(p.Walls);
        Assert.Equal(WorldPrefs.Soft, p.Outlines);
    }

    [Fact]
    public void TextRoundTrips()
    {
        var p = new WorldPrefs(false, WorldPrefs.Hidden);
        Assert.Equal("{\n  \"versao\": 1,\n  \"muralhas\": false,\n  \"contornos\": \"oculto\"\n}\n", p.Text().Replace("\r\n", "\n"));
        Assert.Equal(p, WorldPrefs.Parse(p.Text(), out _));
    }

    [Theory]
    [InlineData("{ quebrado")]
    [InlineData("[]")]
    [InlineData("{\"versao\":1,\"contornos\":\"neon\",\"muralhas\":\"sim\"}")]
    public void BadContentFallsBackToDefaults(string text) => Assert.Equal(WorldPrefs.Default, WorldPrefs.Parse(text, out _));

    [Fact]
    public void ANewerFileIsRecognised()
    {
        Assert.Equal(WorldPrefs.Default, WorldPrefs.Parse("{\"versao\":2,\"muralhas\":false}", out var newer));
        Assert.True(newer);
    }
}
