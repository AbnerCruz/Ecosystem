using System.Text.Json.Nodes;
using Urbe.Core;
using Urbe.Core.Pages;

namespace Urbe.Core.Tests;

/// <summary>"Converter em layout livre" (free.js fromBlock) against csharp/tools/pages-convert-oracle.mjs, ids aside.</summary>
public sealed class PageConvertOracleTests
{
    private static JsonNode Fixture()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "csharp", "Urbe.Portable.slnx"))) dir = dir.Parent;
        return JsonNode.Parse(File.ReadAllBytes(Path.Combine(dir!.FullName, "csharp", "tests", "fixtures", "pages", "convert-run-a.json")))!;
    }

    private static JsonNode? Strip(JsonNode? n) => n switch
    {
        JsonObject o => new JsonObject(o.Where(p => p.Key != "id").Select(p => KeyValuePair.Create(p.Key, Strip(p.Value)))),
        JsonArray a => new JsonArray(a.Select(Strip).ToArray()),
        _ => n?.DeepClone()
    };

    [Fact]
    public void EachBlockConvertsAsTheOriginal()
    {
        var fx = Fixture();
        var engine = new PageEngine(2025);
        var notes = (JsonObject)fx["notes"]!;
        PageNote? Note(string? p) => p is not null && notes[p] is JsonObject n ? new PageNote((string)n["path"]!, (string)n["title"]!, (string)n["content"]!) : null;
        Assert.Equal(LegacyCustomize.Stringify(fx["paragraphs"]), LegacyCustomize.Stringify(new JsonArray(PageEngine.FreeParagraphs((string)fx["md"]!).Select(x => Strip(x)).ToArray())));
        foreach (var r in fx["runs"]!.AsArray())
        {
            var type = (string)r!["type"]!;
            var sec = engine.NewSection(type, r["props"] is JsonObject p ? (JsonObject)p.DeepClone() : null);
            Assert.Equal(LegacyCustomize.Stringify(r["section"]), LegacyCustomize.Stringify(Strip(sec)));
            var free = PageEngine.FreeFromBlock(sec, Note);
            Assert.True(LegacyCustomize.Stringify(r["free"]) == LegacyCustomize.Stringify(Strip(free)),
                type + "\nexpected: " + LegacyCustomize.Stringify(r["free"]) + "\nactual:   " + LegacyCustomize.Stringify(Strip(free)));
        }
    }
}
