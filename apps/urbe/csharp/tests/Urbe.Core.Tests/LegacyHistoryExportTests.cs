using System.Text.Json.Nodes;
using Urbe.Core;

namespace Urbe.Core.Tests;

public sealed class LegacyHistoryExportTests
{
    // CST-129: every save wrote the history by cloning and serializing all of it again; the text must stay the same
    private static string Reference(LegacyHistory history, IEnumerable<string> ids)
    {
        var documents = new JsonObject();
        foreach (var id in ids)
            documents[id] = new JsonArray(history.List(id).Reverse().Select(r =>
            {
                var o = new JsonObject { ["timestamp"] = r.Timestamp, ["path"] = r.Path, ["content"] = r.Content };
                if (r.Removed) o["removed"] = true;
                return (JsonNode)o;
            }).ToArray());
        return LegacyJson.Stringify(new JsonObject { ["version"] = 2, ["documents"] = documents });
    }

    [Fact]
    public void TheExportIsTheSameTextAfterManyChanges()
    {
        var history = new LegacyHistory();
        var ids = new List<string>();
        for (int d = 0; d < 30; d++)
        {
            var id = "doc-" + d + (d % 7 == 0 ? "\"aspas\"" : "");
            ids.Add(id);
            var text = "";
            for (int v = 0; v < 45; v++)
            {
                var next = text + $"Linha {v} com \"aspas\", acentuação, \u2028 separador, \t tab e emoji 🌙\n";
                history.Capture(id, $"Pasta/Nota {d}.md", text, next, 1_000_000L + v * 10_000L);
                text = next;
                if (v % 9 == 0) Assert.Equal(Reference(history, ids), history.Export());
            }
        }
        history.CaptureRemoved(new UrbeDocument("doc-3", "Pasta/Nota 3.md", "Nota 3", "fim", new Dictionary<string, string>(), [], [], null, null, 1), 9_999_999);
        Assert.True(history.Forget("doc-5"));
        ids.Remove("doc-5");
        Assert.Equal(Reference(history, ids), history.Export());
        var again = LegacyHistory.Read(history.Export(), null, out var readOnly);
        Assert.False(readOnly);
        Assert.Equal(history.Export(), again.Export());
    }

    [Fact]
    public void AnEmptyHistoryExportsAsBefore() =>
        Assert.Equal("{\"version\":2,\"documents\":{}}", new LegacyHistory().Export());
}
