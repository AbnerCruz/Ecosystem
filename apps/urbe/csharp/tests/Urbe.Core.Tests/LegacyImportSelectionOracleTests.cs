using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Urbe.Core;

namespace Urbe.Core.Tests;

/// <summary>"Importar" selection rules (path validation, duplicates and source conflicts, UTF-8,
/// kind and counts) against the unmodified 1.8.4 import.js inspect() run by Node
/// (tools/import-oracle.mjs).</summary>
public sealed class LegacyImportSelectionOracleTests
{
    private sealed record Item(string Rel, string? Text = null, byte[]? Bytes = null);

    private static readonly Item[][] Selections =
    [
        [new("a.md", "x"), new("B.md", "y"), new("foto.png", null, [137, 80, 78, 71])],
        [new("a.md", "x"), new("A.MD", "y"), new("foto.png", null, [1, 2])],
        [new("a.md", "x"), new("A.md", "x")],
        [new("a.md", "x"), new("a.MD", "y"), new("A.Md", "z"), new("A.md", "y")],
        [new("../x.md", "x")], [new("/abs.md", "x")], [new("a\\b.md", "x")], [new("c:/x.md", "x")], [new("a//b.md", "x")],
        [new("./a.md", "x")], [new("a/./b.md", "x")], [new("a/b.md", "x")], [new("ação/Ideias 🙂.md", "# oi")],
        [new("bad.md", null, [0xff, 0xfe])], [new("bin.png", null, [0xff, 0xfe])],
        [new(".urbe/import.v1.json", "{}")], [new(".URBE/IMPORT.V1.JSON", "{}")],
        [new(".urbe/vault.json", "{\"formatVersion\":2}"), new("n.md", "x")],
        // vault.json with formatVersion 1: the original says current-vault, the portable pipeline keeps HistoricalVault
        // (ImportPipelineTests) - a known, documented kind-label divergence that changes no file written.
        [new(".urbe/vault.json", "{\"formatVersion\":99}"), new("n.md", "x")],
        [new(".urbe/mapa.json", "{\"v\":4}"), new("n.md", "x")],
        [new(".urbe/mapa.json", "{\"v\":5}"), new("n.md", "x")],
        [new(".urbe/other.json", "{}"), new("n.md", "x")],
        [new("x.page.json", "{\"version\":1}")], [new("x.page.json", "{\"version\":2}")], [new("x.page.json", "nope")],
        [new("falso.zip", "isto não é zip")],
        [new("a.md", "x"), new("falso.zip", "isto não é zip")]
    ];

    private static ImportSelection ToSelection(Item[] items, bool directory) =>
        new(items.Select(i => new ImportSource(i.Rel, _ => ValueTask.FromResult<Stream>(new MemoryStream(i.Bytes ?? Encoding.UTF8.GetBytes(i.Text!))))).ToList(), directory);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SelectionsMatchTheOriginalInspect(bool directory)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "csharp", "Urbe.Portable.slnx"))) root = root.Parent;
        Assert.NotNull(root);
        var payload = JsonSerializer.Serialize(Selections.Select(s => s.Select(i => i.Bytes is null
            ? (object)new { rel = i.Rel, text = i.Text } : new { rel = i.Rel, b64 = Convert.ToBase64String(i.Bytes) })));
        var start = new ProcessStartInfo("node") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        start.ArgumentList.Add(Path.Combine(root!.FullName, "csharp", "tools", "import-oracle.mjs"));
        start.ArgumentList.Add(Path.Combine(root.FullName, "src"));
        start.ArgumentList.Add(payload);
        start.ArgumentList.Add(directory ? "true" : "false");
        using var process = Process.Start(start)!;
        var token = TestContext.Current.CancellationToken;
        var stdout = process.StandardOutput.ReadToEndAsync(token);
        var stderr = process.StandardError.ReadToEndAsync(token);
        await process.WaitForExitAsync(token);
        Assert.True(process.ExitCode == 0, "Oráculo JS falhou: " + await stderr);
        using var oracle = JsonDocument.Parse(await stdout);

        int index = 0, accepted = 0;
        foreach (var expected in oracle.RootElement.EnumerateArray())
        {
            var label = $"#{index} {string.Join(",", Selections[index].Select(i => i.Rel))}";
            bool ok = expected.GetProperty("ok").GetBoolean();
            ImportInspection? actual = null;
            try { actual = await ImportPipeline.InspectAsync(ToSelection(Selections[index], directory), null, TestContext.Current.CancellationToken); }
            catch (InvalidDataException) { }
            Assert.True(ok == (actual is not null), label + (ok ? " deveria ser aceita" : " deveria ser recusada: " + expected.GetProperty("error").GetString()));
            if (actual is not null)
            {
                accepted++;
                Assert.Equal(expected.GetProperty("documents").GetInt32(), actual.DocumentCount);
                Assert.Equal(expected.GetProperty("assets").GetInt32(), actual.AssetCount);
                Assert.Equal(expected.GetProperty("kind").GetString(), actual.Kind switch
                {
                    ImportKind.Documents => "documents", ImportKind.HistoricalVault => "historical-vault",
                    ImportKind.CurrentVault => "current-vault", ImportKind.Export => "export", _ => "backup"
                });
                Assert.Equal(expected.GetProperty("files").EnumerateArray().Select(e => e.GetString()).Order(StringComparer.Ordinal).ToArray(),
                    actual.Files.Keys.Order(StringComparer.Ordinal).ToArray());
                var js = expected.GetProperty("conflicts").EnumerateArray()
                    .Select(c => (c.GetProperty("path").GetString()!, c.GetProperty("candidates").EnumerateArray().Select(e => e.GetString()!).ToArray())).ToArray();
                var cs = (actual.SourceConflicts ?? []).Select(c => (c.Path, c.Candidates.Select(x => x.Path).ToArray())).ToArray();
                Assert.Equal(js.Select(c => c.Item1).ToArray(), cs.Select(c => c.Path).ToArray());
                Assert.Equal(js.Select(c => c.Item2.Length).ToArray(), cs.Select(c => c.Item2.Length).ToArray());
            }
            index++;
        }
        Assert.Equal(Selections.Length, index);
        Assert.True(accepted >= 8, "aceitas: " + accepted);
    }
}
