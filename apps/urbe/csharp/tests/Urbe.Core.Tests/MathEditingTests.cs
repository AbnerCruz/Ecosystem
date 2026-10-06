using System.Text.Json;
using System.Security.Cryptography;
using Urbe.Core;

namespace Urbe.Core.Tests;

public sealed class MathEditingTests
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    private static JsonDocument Oracle()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Urbe.Portable.slnx")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        var json = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(dir.FullName, "tests", "fixtures", "math-editing.json")));
        var source = File.ReadAllBytes(Path.Combine(dir.FullName, "..", "src", "math", "core.js"));
        Assert.Equal(json.RootElement.GetProperty("sha256").GetString(),
            Convert.ToHexStringLower(SHA256.HashData(source)));
        return json;
    }

    [Fact]
    public void ScannerAndEveryCursorPositionMatchFrozenJavaScript()
    {
        using var oracle = Oracle();
        foreach (var item in oracle.RootElement.GetProperty("scan").EnumerateArray())
        {
            var markdown = item.GetProperty("markdown").GetString();
            var expected = item.GetProperty("items").Deserialize<MathFormula[]>(Options)!;
            Assert.Equal(expected, MathEditing.Scan(markdown));
            var cursors = item.GetProperty("cursors").Deserialize<int[]>(Options)!;
            for (var i = 0; i < cursors.Length; i++)
                Assert.Equal(cursors[i] < 0 ? null : expected[cursors[i]], MathEditing.FindAt(markdown, i - 1));
            foreach (var formula in expected)
                Assert.Equal(markdown![formula.Start..formula.End], MathEditing.RawOf(formula.Open, formula.Tex, formula.Close));
        }
    }

    [Fact]
    public void FullCatalogMatchesFrozenJavaScript()
    {
        using var oracle = Oracle();
        Assert.Equal(oracle.RootElement.GetProperty("commands").Deserialize<MathCommand[]>(Options), MathEditing.Commands);
        Assert.Equal(oracle.RootElement.GetProperty("templates").Deserialize<MathSymbol[]>(Options), MathEditing.Templates);
        var groups = oracle.RootElement.GetProperty("symbols").Deserialize<MathSymbolGroup[]>(Options)!;
        Assert.Equal(groups.Length, MathEditing.Symbols.Count);
        for (var i = 0; i < groups.Length; i++)
        {
            Assert.Equal(groups[i].Name, MathEditing.Symbols[i].Name);
            Assert.Equal(groups[i].Items, MathEditing.Symbols[i].Items);
        }
    }

    [Fact]
    public void CompletionOrderingAndLimitsMatchFrozenJavaScript()
    {
        using var oracle = Oracle();
        foreach (var item in oracle.RootElement.GetProperty("complete").EnumerateArray())
            Assert.Equal(item.GetProperty("items").Deserialize<MathCommand[]>(Options),
                MathEditing.Complete(item.GetProperty("prefix").GetString(), item.GetProperty("limit").GetInt32()));
    }

    [Fact]
    public void SnippetsPreserveTextAndUtf16Caret()
    {
        using var oracle = Oracle();
        foreach (var item in oracle.RootElement.GetProperty("snippets").EnumerateArray())
            Assert.Equal(item.GetProperty("expected").Deserialize<MathSnippet>(Options),
                MathEditing.Snippet(item.GetProperty("template").GetString()));
    }

    [Fact]
    public void CatalogAndScanCollectionsCannotBeMutatedByConsumers()
    {
        Assert.Throws<NotSupportedException>(() => ((IList<MathCommand>)MathEditing.Commands).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<MathSymbol>)MathEditing.Symbols[0].Items).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<MathFormula>)MathEditing.Scan("$x$")).Clear());
        Assert.Empty(MathEditing.Scan(null));
        Assert.Null(MathEditing.FindAt(null, 0));
        Assert.Empty(MathEditing.Complete(null));
        Assert.Equal(new MathSnippet("null", 4), MathEditing.Snippet(null));
        Assert.Equal("$$", MathEditing.RawOf(null, null, null));
    }
}
