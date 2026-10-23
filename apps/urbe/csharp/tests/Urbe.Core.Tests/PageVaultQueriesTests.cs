using System.Text.Json.Nodes;
using Urbe.Core.Pages;

namespace Urbe.Core.Tests;

/// <summary>UC-62 slice 1: the "Notas" section lists notes by the vault's links and by a search, and a folder can leave its subfolders out.</summary>
public sealed class PageVaultQueriesTests
{
    private sealed class Notes(params PageNote[] all) : IPageNotes
    {
        public PageNote? Get(string path) => all.FirstOrDefault(n => n.Path == path);
        public IReadOnlyList<PageNote> List() => all;
    }

    private static readonly Notes Vault = new(
        new PageNote("Mundo/Lia.md", "Lia", "# Lia\n\nAmiga de [[Bran]] e de [[Mundo/Cora|Cora]].\n"),
        new PageNote("Mundo/Bran.md", "Bran", "# Bran\n\nConhece a [[Lia]].\n"),
        new PageNote("Mundo/Cora.md", "Cora", "# Cora\n\nUm dragão azul.\n"),
        new PageNote("Mundo/Lugares/Vale.md", "Vale", "# Vale\n\nOnde [[Lia]] nasceu.\n"),
        new PageNote("Diário.md", "Diário", "# Diário\n\nHoje vi um Dragão.\n"));

    private static JsonObject Page(JsonObject props) => new()
    {
        ["meta"] = new JsonObject { ["title"] = "Teste" },
        ["sections"] = new JsonArray(new JsonObject { ["id"] = "s_a", ["type"] = "notes", ["props"] = props })
    };

    private static List<string> Titles(string html) =>
        [.. System.Text.RegularExpressions.Regex.Matches(html, "<a class=\"card note-card\"[^>]*><h3>([^<]+)</h3>").Select(m => m.Groups[1].Value)];

    private static string Render(JsonObject props, bool urbe = true) => new PageEngine(2026) { UrbeMarks = urbe }.Render(Page(props), Vault);

    [Fact]
    public void TheNotesThatLinkToANoteAndTheNotesItLinksTo()
    {
        Assert.Equal(["Bran", "Vale"], Titles(Render(new JsonObject { ["source"] = "links", ["note"] = "Mundo/Lia.md" })));
        Assert.Equal(["Bran", "Cora"], Titles(Render(new JsonObject { ["source"] = "linked", ["note"] = "Lia" })));
        Assert.Contains("Nenhuma nota encontrada", Render(new JsonObject { ["source"] = "links", ["note"] = "Ninguém" }));
    }

    [Fact]
    public void ASearchFindsTitlesAndTextIgnoringCase()
    {
        Assert.Equal(["Cora", "Diário"], Titles(Render(new JsonObject { ["source"] = "search", ["query"] = "dragão" })));
        Assert.Contains("Nenhuma nota encontrada", Render(new JsonObject { ["source"] = "search", ["query"] = "  " }));
    }

    [Fact]
    public void AFolderWithOrWithoutItsSubfolders()
    {
        Assert.Equal(["Bran", "Cora", "Lia", "Vale"], Titles(Render(new JsonObject { ["source"] = "folder", ["folder"] = "Mundo" })));
        Assert.Equal(["Bran", "Cora", "Lia"], Titles(Render(new JsonObject { ["source"] = "folder", ["folder"] = "Mundo", ["subfolders"] = false })));
    }

    [Fact]
    public void TheStudioOffersTheNewSourcesOnlyWithUrbeMarks()
    {
        static JsonArray Fields(bool urbe) => new PageEngine(2026) { UrbeMarks = urbe }.Blocks.First(b => b.Type == "notes").Fields;
        var on = Fields(true);
        Assert.Contains(on, f => (string?)f!["key"] == "query");
        Assert.Contains("search", on.First(f => (string?)f!["key"] == "source")!["options"]!.AsArray().Select(o => (string?)o));
        // off (the 1.8.4 and its oracles): the section is the 1.8.4's, and an Urbe source falls back to its default
        var off = Fields(false);
        Assert.DoesNotContain(off, f => (string?)f!["key"] == "query");
        Assert.DoesNotContain("search", off.First(f => (string?)f!["key"] == "source")!["options"]!.AsArray().Select(o => (string?)o));
        Assert.Equal(["Bran", "Cora", "Lia", "Vale"], Titles(Render(new JsonObject { ["source"] = "search", ["query"] = "dragão", ["folder"] = "Mundo" }, urbe: false)));
    }
}
