using System.Text.Json;
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
            var store = new ProjectStore(root);
            var project = store.Create("SecondHost");
            var host = LunetCapabilityHost.CreateDefault();

            using var session = host.OpenForProject(project);
            Assert.Equal("lunet-user", session.Identity);
            Assert.Equal(
                new[] { "ecosystem", "product", "project" },
                session.Context.Path.Select(step => step.Level));
            Assert.Equal("ecosystem", session.Context.Path[0].Id);
            Assert.Equal("lunet2d", session.Context.Path[1].Id);
            Assert.Equal(project.Manifest.GameId, session.Context.Path[2].Id);

            var capability = Assert.Single(session.Discover());
            Assert.Equal(LunetTextInspectionCapability.CapabilityId, capability.Capability);
            Assert.Equal(new Version(1, 0, 0), capability.Version);
            Assert.Empty(capability.RequiredPermissions);

            var response = await session.InvokeAsync(
                capability.Capability,
                new Version(1, 0, 0),
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
    public async Task DefaultHostRejectsInvalidInputWithoutReadingProjectFiles()
    {
        var root = Path.Combine(Path.GetTempPath(), "lunet-p5-5-" + Guid.NewGuid().ToString("N"));
        try
        {
            var project = new ProjectStore(root).Create("NoFsGrant");
            project.WriteText("secret.txt", "não deve ser lido pela Tool");

            using var session = LunetCapabilityHost.CreateDefault().OpenForProject(project);
            var response = await session.InvokeAsync(
                LunetTextInspectionCapability.CapabilityId,
                new Version(1, 0, 0),
                JsonSerializer.SerializeToElement(new { text = 12 }),
                TestContext.Current.CancellationToken);

            Assert.Equal("INVALID_INPUT", response.CapabilityError);
            Assert.Null(response.Output);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
