using System.Text.Json;
using Urbe.Core;

namespace Urbe.Core.Tests;

/// <summary>"Localizar e substituir" against the unmodified 1.8.4-beta src/editor/workspace.js run in Chromium
/// (tools/legacy-find-oracle.mjs → fixtures/editor-find.json).</summary>
public sealed class LegacyEditorFindOracleTests
{
    [Fact]
    public void FindAndReplaceAreTheOriginals()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "csharp", "tests", "fixtures", "editor-find.json"))) dir = dir.Parent;
        Assert.NotNull(dir);
        using var json = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(dir!.FullName, "csharp", "tests", "fixtures", "editor-find.json")));
        var cases = json.RootElement.GetProperty("cases").EnumerateArray().ToList();
        Assert.True(cases.Count >= 10);
        foreach (var c in cases)
        {
            var text = c.GetProperty("text").GetString()!;
            var query = c.GetProperty("query").GetString()!;
            var repl = c.GetProperty("replacement").GetString()!;
            var hits = LegacyEditorFind.Find(text, query);
            Assert.Equal(c.GetProperty("hits").EnumerateArray().Select(h => (h.GetProperty("index").GetInt32(), h.GetProperty("length").GetInt32(), h.GetProperty("line").GetInt32())),
                hits.Select(h => (h.Index, h.Length, h.Line)));
            // null: replace() refused an empty query, the text stays
            Assert.Equal(c.GetProperty("afterOne").GetString() ?? text, LegacyEditorFind.Replace(text, query, repl, all: false));
            Assert.Equal(c.GetProperty("afterAll").GetString() ?? text, LegacyEditorFind.Replace(text, query, repl, all: true));
        }
    }
}
