using System.Text;
using Urbe.Client;

namespace Urbe.Client.Tests;

/// <summary>Atlas Map V1: contract and on-disk concurrency tests (not a substitute for Android WebView DEVICE tests).</summary>
public sealed class AtlasMapHostTests
{
    private static string Html(string id = "atlas-1", int revision = 0, string title = "Mundo") =>
        "<!DOCTYPE html><html><head><title>Atlas Map</title></head><body>" +
        "<script type=\"application/json\" id=\"atlas-map-data\">" +
        System.Text.Json.JsonSerializer.Serialize(new {
            format = "atlas-map", version = 1, mapId = id, title, revision,
            project = new { seed = "abc" }, bounds = new[] { 0d, 0d, 1d, 1d },
            annotations = new { type = "FeatureCollection", features = Array.Empty<object>() }
        }) +
        "</script><script>window.AtlasMap = {};</script></body></html>";

    [Fact]
    public void Detection_IsRestrictedToRealAtlasMapDocuments()
    {
        Assert.True(AtlasMapSession.TryParse(Html(), out var mapId, out var revision));
        Assert.Equal("atlas-1", mapId);
        Assert.Equal(0, revision);
        Assert.False(AtlasMapSession.TryParse("<html><p>plain</p></html>", out _, out _));
        Assert.False(AtlasMapSession.TryParse(Html().Replace("atlas-map", "not-atlas"), out _, out _));
        Assert.False(AtlasMapSession.TryParse(Html().Replace("\"version\":1", "\"version\":2"), out _, out _));
        Assert.False(AtlasMapSession.TryParse(Html().Replace("\"mapId\":\"atlas-1\"", "\"mapId\":\"\""), out _, out _));
    }

    [Theory]
    [InlineData("Cidades/Porto.md", "Cidades/Porto.md")]
    [InlineData("Cidades/Porto.md#História", "Cidades/Porto.md")]
    [InlineData("../segredo.md", null)]
    [InlineData("Cidades/../segredo.md", null)]
    [InlineData("/root.md", null)]
    [InlineData("https://host/x.md", null)]
    [InlineData("a%2fb.md", null)]
    [InlineData("Cidades\\fora.md", null)]
    [InlineData(".urbe/notas.json", null)]
    [InlineData("arquivo.html", null)]
    [InlineData("a//b.md", null)]
    [InlineData("a.md?query=1", null)]
    public void NotePaths_AreVaultRelativeAndStrict(string input, string? expected)
    {
        Assert.Equal(expected is not null, AtlasMapSession.TryNotePath(input, out var actual));
        if (expected is not null) Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task Save_RevisionsIdentityAndExternalConflicts()
    {
        var dir = Path.Combine(Path.GetTempPath(), "urbe-atlas-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var vaultDir = Path.Combine(dir, "vault");
        Directory.CreateDirectory(vaultDir);
        var mapPath = Path.Combine(vaultDir, "Mundo.html");
        await File.WriteAllTextAsync(mapPath, Html());
        var vault = new FolderVaultStorage(_ => Task.FromResult<string?>(vaultDir), Path.Combine(dir, "selection.json"));
        try
        {
            await vault.PickAsync();
            var bytes = Encoding.UTF8.GetBytes(Html());
            var request = new ContentRequest(vault, "Mundo.html", "Mundo.html", Urbe.Core.VaultFileKinds.Html, bytes);
            var session = AtlasMapSession.TryCreate(request);
            Assert.NotNull(session);
            var wrongId = await session.SaveAsync("other-map", 1, Html(revision: 1));
            Assert.False(wrongId.Ok);
            var mismatch = await session.SaveAsync("atlas-1", 1, Html(revision: 2));
            Assert.False(mismatch.Ok);
            Assert.True((await session.SaveAsync("atlas-1", 1, Html(revision: 1))).Ok);
            Assert.Equal(Html(revision: 1), await File.ReadAllTextAsync(mapPath));
            Assert.True((await session.SaveAsync("atlas-1", 2, Html(revision: 2, title: "Nova cidade"))).Ok);
            Assert.Equal(Html(revision: 2, title: "Nova cidade"), await File.ReadAllTextAsync(mapPath));
            Assert.False((await session.SaveAsync("atlas-1", 2, Html(revision: 2))).Ok);
            // Another file writer changes the same file while this WebView still holds revision 2.
            await vault.SaveExistingTextFileAsync("Mundo.html", Html(revision: 3, title: "Outro editor"));
            Assert.False((await session.SaveAsync("atlas-1", 3, Html(revision: 3))).Ok);
            Assert.Equal(Html(revision: 3, title: "Outro editor"), await File.ReadAllTextAsync(mapPath));
        }
        finally { try { Directory.Delete(dir, recursive: true); } catch (IOException) { } }
    }

    [Fact]
    public async Task Notes_UseExplicitHostPermissionAndDoNotCreateMissingFiles()
    {
        var dir = Path.Combine(Path.GetTempPath(), "urbe-atlas-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var vaultDir = Path.Combine(dir, "vault");
            Directory.CreateDirectory(vaultDir);
            File.WriteAllText(Path.Combine(vaultDir, "Mundo.html"), Html());
            var vault = new FolderVaultStorage(_ => Task.FromResult<string?>(vaultDir), Path.Combine(dir, "selection.json"));
            await vault.PickAsync();
            var opened = new List<string>();
            var session = AtlasMapSession.TryCreate(new ContentRequest(vault, "Mundo.html", "Mundo.html", Urbe.Core.VaultFileKinds.Html, Encoding.UTF8.GetBytes(Html())) {
                AtlasOpenNote = path => { opened.Add(path); return Task.FromResult(path == "Existe.md"); }
            });
            Assert.NotNull(session);
            Assert.False(await session.OpenNoteAsync("fake-id", "Existe.md"));
            Assert.False(await session.OpenNoteAsync("atlas-1", "../fora.md"));
            Assert.False(await session.OpenNoteAsync("atlas-1", "Ausente.md"));
            Assert.True(await session.OpenNoteAsync("atlas-1", "Existe.md#Seção"));
            Assert.Equal(new[] { "Ausente.md", "Existe.md" }, opened);
            Assert.False(File.Exists(Path.Combine(dir, "Ausente.md")));
        }
        finally { try { Directory.Delete(dir, recursive: true); } catch (IOException) { } }
    }
}
