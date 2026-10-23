using System.Text.Json.Nodes;
using Urbe.Core.Pages;

namespace Urbe.Core.Tests;

/// <summary>UC-62 slice 3: a template of the structure only keeps the sections, their look and their lists, without the texts.</summary>
public sealed class PageStructureTemplateTests
{
    [Fact]
    public void TheTextsGoTheStructureStays()
    {
        var engine = new PageEngine(2026) { UrbeMarks = true };
        var spec = engine.Normalize(new JsonObject
        {
            ["meta"] = new JsonObject { ["title"] = "Portfólio" },
            ["sections"] = new JsonArray(
                new JsonObject { ["type"] = "hero", ["props"] = new JsonObject { ["title"] = "Olá, sou a Lia", ["layout"] = "split" }, ["style"] = new JsonObject { ["background"] = "accent" } },
                new JsonObject { ["type"] = "text", ["props"] = new JsonObject { ["markdown"] = "Meu **texto**." } },
                new JsonObject { ["type"] = "features", ["props"] = new JsonObject { ["items"] = new JsonArray(
                    new JsonObject { ["icon"] = "★", ["title"] = "Rápido", ["text"] = "Muito." },
                    new JsonObject { ["icon"] = "☂", ["title"] = "Seguro", ["text"] = "Sempre." }) } },
                new JsonObject { ["type"] = "notes", ["props"] = new JsonObject { ["source"] = "search", ["query"] = "dragão" } })
        }).Spec;
        var bare = PageStudio.StructureOnly(spec, engine);
        var sections = bare["sections"]!.AsArray();
        Assert.Equal(4, sections.Count);
        var html = engine.Render(bare);
        Assert.DoesNotContain("Olá, sou a Lia", html);
        Assert.DoesNotContain("Meu <strong>texto</strong>", html);
        Assert.DoesNotContain("Rápido", html);
        // the look, the list's length and the non-text choices stay
        Assert.Equal("split", (string?)sections[0]!["props"]!["layout"]);
        Assert.Equal(spec["sections"]![0]!["style"]!.ToJsonString(), sections[0]!["style"]!.ToJsonString());
        Assert.Equal(2, sections[2]!["props"]!["items"]!.AsArray().Count);
        Assert.Equal("★", (string?)sections[2]!["props"]!["items"]![0]!["icon"]);
        Assert.Equal("search", (string?)sections[3]!["props"]!["source"]);
        // the page itself is untouched
        Assert.Equal("Olá, sou a Lia", (string?)spec["sections"]![0]!["props"]!["title"]);
    }
}
