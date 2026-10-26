using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Lunet.Core;

namespace Lunet.Tests;

public sealed class GitHubGameBuildTests
{
    [Theory]
    [InlineData("io.lunet.studio")]
    [InlineData("jogo")]
    [InlineData("com.bad-name.game")]
    [InlineData("com.test.3game")]
    [InlineData("com.test.game;garbage")]
    public void RefusesInvalidPackageIds(string id) =>
        Assert.Throws<ArgumentException>(() => GitHubGameBuildClient.ValidatePackageId(id));

    [Fact]
    public void CloudWorkflowUsesProjectRootsWithoutPassingSecrets()
    {
        var workflow = GitHubGameBuildWorkflow.Source;
        Assert.Contains("workflow_dispatch:", workflow);
        Assert.Contains("run-name: Lunet ${{ inputs.build_id }}", workflow);
        Assert.Contains("GAME_PACKAGE: ${{ inputs.package_id }}", workflow);
        Assert.Contains("-p:GameSourceRoot=", workflow);
        Assert.Contains("upload-artifact@v4", workflow);
        Assert.DoesNotContain("secrets.PAT", workflow);
    }

    [Fact]
    public async Task BuildUploadsSnapshotDispatchesAndDownloadsArtifact()
    {
        var handler = new FakeGitHubHandler();
        using var http = new HttpClient(handler);
        var client = new GitHubGameBuildClient(http);
        using var project = new MemoryStream(Encoding.UTF8.GetBytes("fake zip"));
        var id = Guid.NewGuid();
        var result = await client.BuildAsync("test-token", "com.lunet.games.testing", "Meu Jogo", id, project, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal("apk-data", Encoding.UTF8.GetString(result.Apk));
        Assert.Equal("https://github.com/tester/lunet-game-builds/actions/runs/123", result.RunUrl);
        Assert.Contains("projects/" + id.ToString("N") + "/source.zip", handler.ProjectPath);
        Assert.Equal("com.lunet.games.testing", handler.PackageId);
        Assert.True(handler.DispatchOccurred);
        Assert.True(handler.UploadOccurred);
        Assert.True(handler.EveryRequestAuthenticated);
    }

    private sealed class FakeGitHubHandler : HttpMessageHandler
    {
        public string ProjectPath { get; private set; } = "";
        public string PackageId { get; private set; } = "";
        public bool DispatchOccurred { get; private set; }
        public bool UploadOccurred { get; private set; }
        public bool EveryRequestAuthenticated { get; private set; } = true;
        private string _buildId = "";

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            EveryRequestAuthenticated &= request.Headers.Authorization?.Scheme == "Bearer"
                && request.Headers.Authorization.Parameter == "test-token";
            var path = request.RequestUri!.AbsolutePath;
            var method = request.Method.Method;
            var response = new HttpResponseMessage(HttpStatusCode.OK);
            string body = "";
            if (path == "/user") body = """{"login":"tester"}""";
            else if (path == "/repos/tester/lunet-game-builds") body = """{"private":true}""";
            else if (path.EndsWith("/contents/.github/workflows/lunet-build.yml"))
                body = """{"sha":"existing"}""";
            else if (path.Contains("/contents/projects/") && method == "GET")
                response.StatusCode = HttpStatusCode.NotFound;
            else if (path.Contains("/contents/projects/") && method == "PUT")
            {
                ProjectPath = path;
                UploadOccurred = true;
                body = "{}";
            }
            else if (path.EndsWith("/actions/workflows/lunet-build.yml/dispatches"))
            {
                using var payload = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
                var input = payload.RootElement.GetProperty("inputs");
                _buildId = input.GetProperty("build_id").GetString()!;
                PackageId = input.GetProperty("package_id").GetString()!;
                DispatchOccurred = true;
                response.StatusCode = HttpStatusCode.NoContent;
            }
            else if (path.EndsWith("/actions/workflows/lunet-build.yml/runs"))
                body = JsonSerializer.Serialize(new { workflow_runs = new[] {
                    new { display_title = "Lunet " + _buildId, status = "completed", conclusion = "success",
                          id = 123, html_url = "https://github.com/tester/lunet-game-builds/actions/runs/123" }
                }});
            else if (path.EndsWith("/actions/runs/123/artifacts"))
                body = """{"artifacts":[{"name":"lunet-game-apk","id":777}]}""";
            else if (path.EndsWith("/actions/artifacts/777/zip"))
            {
                using var zipBytes = new MemoryStream();
                using (var zip = new ZipArchive(zipBytes, ZipArchiveMode.Create, true))
                using (var entry = zip.CreateEntry("game.apk").Open())
                {
                    byte[] apk = Encoding.UTF8.GetBytes("apk-data");
                    entry.Write(apk);
                }
                response.Content = new ByteArrayContent(zipBytes.ToArray());
                return response;
            }
            else throw new InvalidOperationException("Unexpected GitHub call " + method + " " + path);
            response.Content = new StringContent(body);
            return response;
        }
    }
}
