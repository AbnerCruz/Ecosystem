using System.Text;
using System.Text.Json.Nodes;
using Urbe.Core;

namespace Urbe.Core.Tests;

/// <summary>The reconciliation at the opening (identity.js reconcile) against csharp/tools/legacy-identity-oracle.mjs: the same
/// fingerprints, ids, renames and ambiguous groups (the 1.8.4 also moved the city map; the city left the app, ADD-0028).</summary>
public sealed class LegacyIdentityOracleTests
{
    private static JsonArray Cases()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "csharp", "tests", "fixtures", "identity", "reconcile-run-a.json"))) dir = dir.Parent;
        Assert.NotNull(dir);
        return JsonNode.Parse(File.ReadAllBytes(Path.Combine(dir.FullName, "csharp", "tests", "fixtures", "identity", "reconcile-run-a.json")))!["cases"]!.AsArray();
    }

    [Fact]
    public void EachCaseIsTheOriginals()
    {
        foreach (var c in Cases().OfType<JsonObject>())
        {
            var name = (string)c["name"]!;
            var meta = c["metaBefore"] as JsonObject;
            var notes = meta?["notas"] as JsonObject;
            var items = c["items"]!.AsArray().Select(i =>
            {
                var path = (string)i![0]!;
                return new LegacyIdentity.Item(path, (string)i[1]!, notes?[path]?["id"]?.GetValue<string>());
            }).ToList();
            Assert.Equal(c["fingerprints"]!.AsArray().Select(f => (string)f!), items.Select(i => VaultIdentity.Fingerprint(i.Content)));
            var identity = c["identity"] is JsonObject s ? VaultIdentity.Parse(Encoding.UTF8.GetBytes(s.ToJsonString()), out _) : null;
            var r = LegacyIdentity.Reconcile(items, identity);
            Assert.True(c["ids"]!.AsArray().Select(x => (string?)x).SequenceEqual(items.Select(i => i.Id)), name + " ids");
            Assert.True(c["renames"]!.AsArray().Select(x => ((string)x!["id"]!, (string)x["from"]!, (string)x["to"]!)).SequenceEqual(r.Renames.Select(x => (x.Id, x.From, x.To))), name + " renames");
            Assert.True(c["ambiguous"]!.AsArray().Select(x => string.Join("|", x!["to"]!.AsArray().Select(p => (string)p!)) + "<" + string.Join("|", x["from"]!.AsArray().Select(p => (string)p!)))
                .SequenceEqual(r.Ambiguous.Select(x => string.Join("|", x.To) + "<" + string.Join("|", x.From))), name + " ambiguous");
        }
    }
}
