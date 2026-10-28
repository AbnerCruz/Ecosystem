using System.Text.Json.Nodes;
using Urbe.Core.Assistant;

namespace Urbe.Core.Tests;

/// <summary>CST-075: the agents consult the Urbe's own documentation instead of guessing from other editors.</summary>
public sealed class AssistantGuideToolsTests
{
    private static readonly IReadOnlyList<(string Path, string Text)> Articles =
    [
        ("Tutorial/Comece aqui.md", "# Comece aqui\n\nBem-vindo.\n\n## A tela\n\nA Área de trabalho e o editor.\n"),
        ("Tutorial/Escrever/Editor.md", "# Editor\n\nO editor salva sozinho.\n\n## Caixas\n\nUm bloco entre `:::tipo Título` e `:::` vira uma caixa. Caixas podem ficar umas dentro das outras.\n\n## Incorporar\n\n`![[Nota]]` mostra a nota no lugar; `![[foto.png]]` mostra a imagem.\n\n```\n## isto é código, não seção\n```\n"),
        ("Tutorial/Ferramentas/Páginas.md", "# Páginas\n\nPáginas e livros.\n\n## Exportar\n\nHTML, PDF e EPUB.\n"),
    ];

    [Fact]
    public void WithoutArgumentsItListsTheArticlesAndTheirSections()
    {
        var index = AssistantGuideTools.Answer(Articles, null, null);
        Assert.Contains("- Editor: Caixas · Incorporar", index);
        Assert.Contains("- Páginas: Exportar", index);
        Assert.DoesNotContain("isto é código", index);
    }

    [Fact]
    public void AQueryBringsTheRelevantSections()
    {
        var answer = AssistantGuideTools.Answer(Articles, "como incorporar uma imagem na nota", null);
        Assert.StartsWith("Guia do Urbe — seções", answer);
        Assert.Contains("## Editor › Incorporar", answer);
        Assert.Contains("![[foto.png]]", answer);
        var boxes = AssistantGuideTools.Answer(Articles, "caixa dentro de caixa", null);
        Assert.Contains("## Editor › Caixas", boxes.Split("━━━━━━━━")[0]);
    }

    [Fact]
    public void AnArticleIsReadWholeAndAnUnknownOneSaysWhatExists()
    {
        Assert.Contains("vira uma caixa", AssistantGuideTools.Answer(Articles, null, "editor"));
        var missing = AssistantGuideTools.Answer(Articles, null, "Grafos");
        Assert.Contains("Não há artigo", missing);
        Assert.Contains("Editor", missing);
        Assert.Contains("diga isso ao usuário", AssistantGuideTools.Answer(Articles, "xilogravura", null));
    }

    [Fact]
    public async Task TheToolIsReadOnlyAndUsesTheAppsArticles()
    {
        var tool = AssistantGuideTools.Create(() => Articles);
        Assert.Equal("read", tool.Access);
        var output = await tool.Run(new JsonObject { ["query"] = "exportar pdf" }, null!);
        Assert.Contains("Páginas › Exportar", output.Content);
    }
}
