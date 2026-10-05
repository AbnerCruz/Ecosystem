using System.Text.Json;
using Lunet.Core;
using Lunet.Core.Capabilities;

namespace Lunet.Tests;

public class LunetCapabilityHostTests
{
    [Fact]
    public async Task DefaultHostUsesRealProjectGameIdAndSharedTextInspect()
    {
        var root = Path.Combine(Path.GetTempPath(), "lunet-p5-5-" + Guid.NewGuid().ToString("N"));
        try
        {
            var project = new ProjectStore(root).Create("SecondHost");
            using var session = LunetCapabilityHost.CreateDefault().OpenForProject(project);

            Assert.Equal("lunet-local-user", session.Identity);
            Assert.Equal(new[] { "ecosystem", "product", "project" },
                session.Context.Path.Select(step => step.Level));
            Assert.Equal(project.Manifest.GameId, session.Context.Path[2].Id);

            var capability = Assert.Single(session.Discover());
            Assert.Equal("text.inspect", capability.Capability);
            Assert.Equal(new Version(1, 0, 0), capability.Version);
            Assert.Empty(capability.RequiredPermissions);

            var response = await session.InvokeAsync(
                capability.Capability,
                capability.Version,
                JsonSerializer.SerializeToElement(new { text = "Olá mundo\n🙂" }),
                TestContext.Current.CancellationToken);

            Assert.True(response.Succeeded);
            Assert.Equal(11, response.Output!.Value.GetProperty("characters").GetInt32());
            Assert.Equal(3, response.Output.Value.GetProperty("words").GetInt32());
            Assert.Equal(2, response.Output.Value.GetProperty("lines").GetInt32());
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ToolReceivesOnlyProvidedText()
    {
        var root = Path.Combine(Path.GetTempPath(), "lunet-p5-5-" + Guid.NewGuid().ToString("N"));
        try
        {
            var project = new ProjectStore(root).Create("LocalOnly");
            project.WriteText("secret.txt", "não deve ser lido pela Tool");

            using var session = LunetCapabilityHost.CreateDefault().OpenForProject(project);
            var response = await session.InvokeAsync(
                "text.inspect",
                new Version(1, 0, 0),
                JsonSerializer.SerializeToElement(new { text = "somente este texto" }),
                TestContext.Current.CancellationToken);

            Assert.True(response.Succeeded);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ConnectionsIsAProjectionOfTheSameRegistryContextAndGrants()
    {
        var scope = new LunetHostContext([new("ecosystem", "ecosystem"), new("product", "lunet2d"), new("project", "one")]);
        var capability = new LunetHostCapability(
            "test.connection", "test-provider", new Version(1, 0, 0), scope, ["ui.display"], "stateless",
            _ => true, _ => true, (_, _) => Task.FromResult(JsonSerializer.SerializeToElement(new { ok = true })));
        var host = new LunetCapabilityHost([capability], ["ui.display"]);

        using var noGrant = host.Open("test-user", scope, []);
        var blocked = Assert.Single(noGrant.Connections());
        Assert.False(blocked.Available);
        Assert.True(blocked.ContextMatches);
        Assert.Equal("grant-required", blocked.Status);
        Assert.Equal(["ui.display"], blocked.RequiredPermissions);
        Assert.Equal(["ui.display"], blocked.MissingPermissions);
        Assert.Empty(noGrant.Discover());

        using var granted = host.Open("test-user", scope, ["ui.display"]);
        Assert.True(Assert.Single(granted.Connections()).Available);
        Assert.Equal(["ui.display"], granted.Grants);
        Assert.Single(granted.Discover());
        Assert.True(granted.Revoke(["ui.display"]));
        Assert.Equal("grant-required", Assert.Single(granted.Connections()).Status);
        Assert.Empty(granted.Discover());

        var other = new LunetHostContext([new("ecosystem", "ecosystem"), new("product", "lunet2d"), new("project", "two")]);
        using var wrongContext = host.Open("test-user", other, ["ui.display"]);
        var mismatch = Assert.Single(wrongContext.Connections());
        Assert.False(mismatch.Available);
        Assert.False(mismatch.ContextMatches);
        Assert.Equal("context-mismatch", mismatch.Status);
        Assert.Empty(mismatch.MissingPermissions);
        Assert.Empty(wrongContext.Discover());
    }

    [Fact]
    public async Task InvalidAndOversizedInputAreRejectedByAdapter()
    {
        var root = Path.Combine(Path.GetTempPath(), "lunet-p5-5-" + Guid.NewGuid().ToString("N"));
        try
        {
            var project = new ProjectStore(root).Create("Limits");
            using var session = LunetCapabilityHost.CreateDefault().OpenForProject(project);

            var wrongType = await session.InvokeAsync("text.inspect", new Version(1, 0, 0),
                JsonSerializer.SerializeToElement(new { text = 12 }), TestContext.Current.CancellationToken);
            Assert.Equal("INVALID_INPUT", wrongType.CapabilityError);

            var tooLarge = await session.InvokeAsync("text.inspect", new Version(1, 0, 0),
                JsonSerializer.SerializeToElement(new { text = new string('x', 100_001) }),
                TestContext.Current.CancellationToken);
            Assert.Equal("INVALID_INPUT", tooLarge.CapabilityError);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
