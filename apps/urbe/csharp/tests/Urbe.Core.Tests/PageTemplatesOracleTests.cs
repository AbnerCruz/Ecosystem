using System.Text.Json.Nodes;
using Urbe.Core;
using Urbe.Core.Pages;

namespace Urbe.Core.Tests;

/// <summary>The built-in page templates (templates.js) against csharp/tools/pages-templates-oracle.mjs: same list and, for every
/// title/folder/note combination, the same normalized page (ids aside, random in the original).</summary>
public sealed class PageTemplatesOracleTests
{
    private static JsonNode Fixture() =>
        JsonNode.Parse(File.ReadAllBytes(Path.Combine(Root().FullName, "csharp", "tests", "fixtures", "pages", "templates-run-a.json")))!;

    private static JsonNode? Strip(JsonNode? n) => n switch
    {
        JsonObject o => new JsonObject(o.Where(p => p.Key != "id").Select(p => KeyValuePair.Create(p.Key, Strip(p.Value)))),
        JsonArray a => new JsonArray(a.Select(Strip).ToArray()),
        _ => n?.DeepClone()
    };

    [Fact]
    public void TheListIsTheOriginals()
    {
        var list = Fixture()["list"]!.AsArray();
        Assert.Equal(list.Select(t => ((string)t!["id"]!, (string)t["name"]!, (string)t["icon"]!, (string)t["description"]!, (string?)t["needs"])),
            PageTemplates.List().Select(t => (t.Id, t.Name, t.Icon, t.Description, t.Needs)));
    }

    [Fact]
    public void EachBuildIsTheOriginals()
    {
        var engine = new PageEngine(2025);
        foreach (var r in Fixture()["runs"]!.AsArray())
        {
            var input = r!["input"]!;
            (string, string)? note = input["note"] is JsonObject n ? ((string)n["title"]!, (string)n["path"]!) : null;
            var spec = PageTemplates.Build(engine, (string)r["id"]!, (string?)input["title"], (string?)input["folder"], note, new DateTime(2025, 10, 9, 12, 0, 0));
            Assert.True(LegacyCustomize.Stringify(r["spec"]) == LegacyCustomize.Stringify(Strip(spec)),
                (string)r["id"]! + " " + input.ToJsonString() + "\nexpected: " + LegacyCustomize.Stringify(r["spec"])[..400] + "\nactual:   " + LegacyCustomize.Stringify(Strip(spec))[..400]);
        }
    }

    private static DirectoryInfo Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "csharp", "Urbe.Portable.slnx"))) dir = dir.Parent;
        return dir ?? throw new InvalidOperationException("apps/urbe not found");
    }
}
