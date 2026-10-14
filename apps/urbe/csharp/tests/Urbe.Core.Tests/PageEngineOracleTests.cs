using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Urbe.Core;
using Urbe.Core.Pages;

namespace Urbe.Core.Tests;

/// <summary>
/// The page engine of the 1.8.4 (src/pages/engine.js + free.js), recorded by csharp/tools/pages-engine-oracle.mjs: every
/// built-in template (and variants), Markdown and notes, every block, a book, an invalid page and JSON text. The
/// normalized page, its errors and warnings, the compact form and the HTML must be the original's, byte for byte.
/// </summary>
public sealed class PageEngineOracleTests
{
    private sealed class Notes(IReadOnlyList<PageNote> all) : IPageNotes
    {
        public PageNote? Get(string path) => all.FirstOrDefault(n => n.Path == path);
        public IReadOnlyList<PageNote> List() => all;
    }

    private static JsonDocument Fixture() =>
        JsonDocument.Parse(File.ReadAllBytes(Path.Combine(Root().FullName, "csharp", "tests", "fixtures", "pages", "engine-run-a.json")));

    public static TheoryData<string> Cases()
    {
        using var json = Fixture();
        var data = new TheoryData<string>();
        foreach (var c in json.RootElement.GetProperty("cases").EnumerateArray()) data.Add(c.GetProperty("name").GetString()!);
        return data;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void ThePageIsTheOriginals(string name)
    {
        using var json = Fixture();
        var root = json.RootElement;
        var styles = root.GetProperty("styles").EnumerateArray().Select(s => s.GetString()!).ToList();
        var notes = new Notes(root.GetProperty("notes").EnumerateArray().Select(n => new PageNote(
            n.GetProperty("path").GetString()!, n.GetProperty("title").GetString()!, n.GetProperty("content").GetString()!,
            n.GetProperty("modified").GetString(), n.GetProperty("tags").EnumerateArray().Select(t => t.GetString()!).ToList())).ToList());
        var c = root.GetProperty("cases").EnumerateArray().Single(x => x.GetProperty("name").GetString() == name);
        var engine = new PageEngine(2025);   // the oracle's Date is 2025-10-09

        var input = c.GetProperty("input");
        var n = input.ValueKind == JsonValueKind.String ? engine.Normalize(input.GetString()!) : engine.Normalize(JsonNode.Parse(input.GetRawText()));
        static string Issues(IEnumerable<PageIssue> l) => string.Join("\n", l.Select(i => i.Path + ": " + i.Message));
        static string Expected(JsonElement e) => string.Join("\n", e.EnumerateArray().Select(i => i.GetProperty("path").GetString() + ": " + i.GetProperty("message").GetString()));
        if (name == "not-json")
        {
            // the message after "JSON inválido: " is the parser's own
            Assert.StartsWith("(json): JSON inválido: ", Issues(n.Errors));
        }
        else Assert.Equal(Expected(c.GetProperty("errors")), Issues(n.Errors));
        Assert.Equal(Expected(c.GetProperty("warnings")), Issues(n.Warnings));

        // ids made up for sections without a valid one are random in both; the oracle's are used from here on
        var expectedSpec = JsonNode.Parse(c.GetProperty("spec").GetRawText())!;
        var made = expectedSpec["sections"]!.AsArray().Zip(n.Spec["sections"]!.AsArray());
        foreach (var (e, a) in made)
            if (Regex.IsMatch((string)e!["id"]!, "^s_") && Regex.IsMatch((string)a!["id"]!, "^s_")) a["id"] = (string)e["id"]!;
        Assert.Equal(LegacyCustomize.Stringify(expectedSpec), LegacyCustomize.Stringify(n.Spec));

        Assert.Equal(LegacyCustomize.Stringify(JsonNode.Parse(c.GetProperty("compact").GetRawText())), LegacyCustomize.Stringify(engine.Compact(expectedSpec)));

        var html = Regex.Replace(c.GetProperty("html").GetString()!, "@@style:([0-9]+)@@", m => styles[int.Parse(m.Groups[1].Value)]);
        var actual = engine.Render(expectedSpec, notes, c.GetProperty("preview").GetBoolean());
        if (actual != html)
        {
            int i = 0;
            while (i < Math.Min(actual.Length, html.Length) && actual[i] == html[i]) i++;
            Assert.Fail($"{name}: differs at {i}\nexpected: …{html.Substring(i, Math.Min(400, html.Length - i))}\nactual:   …{actual.Substring(i, Math.Min(400, actual.Length - i))}");
        }
    }

    private static DirectoryInfo Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "csharp", "Urbe.Portable.slnx"))) dir = dir.Parent;
        return dir ?? throw new InvalidOperationException("apps/urbe not found");
    }
}
