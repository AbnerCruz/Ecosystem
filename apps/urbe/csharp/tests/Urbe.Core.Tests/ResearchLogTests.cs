using System.Text.Json;

namespace Urbe.Core.Tests;

/// <summary>CST-188: every research entry is auditable: sources, both sides with their nature, a decision and proof that exists.</summary>
public sealed class ResearchLogTests
{
    private static readonly string Root = FindRoot();
    private static readonly string[] Natures = ["relato", "documentação", "inferência", "ausência"];

    private static string FindRoot()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "Urbe.Portable.slnx"))) return d.FullName;
        throw new DirectoryNotFoundException("Execute os testes dentro do checkout do Numa.");
    }

    [Fact]
    public void EveryEntryCanBeAudited()
    {
        using var log = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root, "..", "docs", "csharp", "RESEARCH-LOG.json")));
        var ids = new HashSet<string>();
        foreach (var entry in log.RootElement.GetProperty("entradas").EnumerateArray())
        {
            var id = entry.GetProperty("id").GetString()!;
            Assert.True(ids.Add(id), id + " repetido");
            foreach (var field in new[] { "tema", "data", "hipotese", "licenca", "custo", "maturidade", "decisao", "riscoAndroid" })
                Assert.False(string.IsNullOrWhiteSpace(entry.GetProperty(field).GetString()), id + ": " + field);
            Assert.True(DateOnly.TryParseExact(entry.GetProperty("data").GetString(), "yyyy-MM-dd", out _), id + ": data");
            Assert.All(entry.GetProperty("cst").EnumerateArray(), c => Assert.Matches(@"^CST-\d{3}$", c.GetString()!));
            Assert.NotEmpty(entry.GetProperty("alternativas").EnumerateArray());
            foreach (var side in new[] { "android", "windows", "offline" }) Assert.False(string.IsNullOrWhiteSpace(entry.GetProperty("suporte").GetProperty(side).GetString()), id + ": suporte " + side);
            var sources = entry.GetProperty("fontes").EnumerateArray().ToList();
            Assert.NotEmpty(sources);
            Assert.All(sources, s => Assert.StartsWith("https://", s.GetProperty("url").GetString()));
            // the case against is required as much as the case for: an opinion is never passed off as consensus
            foreach (var side in new[] { "favoravel", "desfavoravel" })
            {
                var items = entry.GetProperty(side).EnumerateArray().ToList();
                Assert.True(items.Count > 0, id + ": " + side + " vazio (use natureza \"ausência\" se nada foi encontrado)");
                Assert.All(items, i => Assert.Contains(i.GetProperty("natureza").GetString(), Natures));
            }
            var proofs = entry.GetProperty("prova").EnumerateArray().Select(p => p.GetString()!).ToList();
            Assert.NotEmpty(proofs);
            Assert.All(proofs, p => Assert.True(File.Exists(Path.Combine(Root, p)), id + ": prova " + p + " não existe"));
        }
        Assert.NotEmpty(ids);
    }
}
