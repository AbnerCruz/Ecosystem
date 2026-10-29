using Urbe.Client;
using Urbe.Core.Assistant;

namespace Urbe.Client.Tests;

/// <summary>
/// CST-075: the agents learn the Urbe from its Tutorial (urbe_guide), so the Tutorial has to cover the Urbe's own
/// syntax and tools. A new mark in the editor without a line here fails this test.
/// </summary>
public sealed class AppDocsGuideTests
{
    private static string All => string.Join("\n", AppDocs.Load().Select(a => a.Content));

    [Theory]
    [InlineData("%%")]                    // comment
    [InlineData("palavra{\"")]            // comment on a word
    [InlineData("#Tag:")]                 // paragraph tag
    [InlineData(":::tipo")]               // boxes
    [InlineData("{ia:\"modelo\"}")]       // authorship seal
    [InlineData("&&:")]                   // request in the note
    [InlineData("[[")]                    // links
    [InlineData("![[")]                   // embeds
    [InlineData("#^")]                    // embed of a block
    [InlineData("#L3-L8")]                // embed of lines
    [InlineData("$$")]                    // display math
    [InlineData(".grafo.json")]           // graphs
    [InlineData("```grafico")]            // function plots
    [InlineData("- [ ] 08:11 —")]         // quick notes are checkboxes
    [InlineData("Nova nota nesta pasta")]
    [InlineData("Personalização/AGENTS.md")]
    [InlineData("Prévia | Código")]       // CST-141: live preview of an HTML
    [InlineData("Abrir no navegador")]
    [InlineData("{{titulo}}")]            // template fields
    public void TheTutorialCoversTheUrbesOwnSyntax(string mark) => Assert.Contains(mark, All);

    [Fact]
    public async Task TheAgentsToolReadsTheAppsTutorial()
    {
        var tool = AssistantPanel.Tools.Single(t => t.Name == AssistantGuideTools.Name);
        Assert.Equal("read", tool.Access);
        var answer = (await tool.Run(new System.Text.Json.Nodes.JsonObject { ["query"] = "incorporar imagem" }, null!)).Content;
        Assert.Contains("Editor › Incorporar", answer);
    }
}
