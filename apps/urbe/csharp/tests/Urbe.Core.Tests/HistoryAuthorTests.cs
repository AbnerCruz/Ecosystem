namespace Urbe.Core.Tests;

public sealed class HistoryAuthorTests
{
    [Fact]
    public void AnAgentsChangeIsItsOwnVersionNamedAfterIt()
    {
        var history = new LegacyHistory();
        Assert.True(history.Capture("d", "N.md", "v1", "v2", 1_000));
        // a second later (inside the typing window) an agent rewrites it: still a version of its own, with its name
        Assert.True(history.Capture("d", "N.md", "v2", "v3", 2_000, by: "Alice"));
        var back = LegacyHistory.Read(history.Export(), null, out var readOnly);
        Assert.False(readOnly);
        var list = back.List("d");
        Assert.Equal(["v2", "v1"], list.Select(r => r.Content));
        Assert.Equal("Alice", list[0].By);
        Assert.Null(list[1].By);
    }
}
