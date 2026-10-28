using System.Diagnostics;
using Urbe.Core;

namespace Urbe.Core.Tests;

/// <summary>
/// The authorship seal contract (docs/csharp/AI-AUTHORSHIP-SEALS.md, CST-027–033): the Urbe, not the agent, places one
/// {ia:"modelo"} per block the Assistente creates or regenerates; corrections keep the block's author; seals never
/// accumulate and cannot be forged or removed through the text an agent or a person sends.
/// </summary>
public sealed class AiAuthorshipTests
{
    private static string S(string model) => "{ia:\"" + model + "\"}";

    // ---------------------------------------------------------------- what the agent reads

    [Theory]
    [InlineData("Texto {ia:\"m\"}", "Texto")]
    [InlineData("Linha {ia:\"a\"} {ia:\"a\"} {ia:\"b\"}", "Linha")]
    [InlineData("- [x] ** {ia:\"gpt-6-luna\"}Páginas (PR #700):** foram", "- [x] **Páginas (PR #700):** foram")]
    [InlineData("{ia:\"m\"} Começo", "Começo")]
    [InlineData("a {ia:\"m\"} b", "a b")]
    [InlineData("Sem selo", "Sem selo")]
    [InlineData("Código `{ia:\"m\"}` fica", "Código `{ia:\"m\"}` fica")]
    [InlineData("```\n{ia:\"m\"}\n```", "```\n{ia:\"m\"}\n```")]
    [InlineData("```\nx\n```\n{ia:\"m\"}\nDepois", "```\nx\n```\nDepois")]
    [InlineData("```\nx\n```\n{ia:\"m\"}", "```\nx\n```")]
    [InlineData("Duro  {ia:\"m\"}\nquebra", "Duro \nquebra")]
    public void StripShowsTheTextWithoutSeals(string raw, string plain) =>
        Assert.Equal(plain, AiAuthorship.Strip(raw));

    [Fact]
    public void StripKeepsHardLineBreakSpacesOfACanonicalSeal()
    {
        var sealedText = AiAuthorship.Attribute("", "Duro  \nquebra", "m");
        Assert.Equal("Duro " + S("m") + "  \nquebra " + S("m"), sealedText);
        Assert.Equal("Duro  \nquebra", AiAuthorship.Strip(sealedText));
    }

    // ---------------------------------------------------------------- one seal per generated block

    [Fact]
    public void EveryGeneratedBlockIsSealedNotOnlyTheLast()
    {
        const string written = "# Plano\n\nPrimeiro parágrafo.\n\n- item um\n- [ ] item dois\n\n> citação\n> continua\n\nÚltimo.\n";
        var result = AiAuthorship.Attribute(null, written, "gpt-x");
        Assert.Equal(
            "# Plano " + S("gpt-x") + "\n\nPrimeiro parágrafo. " + S("gpt-x") + "\n\n- item um " + S("gpt-x") + "\n- [ ] item dois " + S("gpt-x") +
            "\n\n> citação\n> continua " + S("gpt-x") + "\n\nÚltimo. " + S("gpt-x") + "\n", result);
    }

    [Fact]
    public void CodeTablesMathAndStructureKeepValidMarkdown()
    {
        const string written = "---\ntitulo: X\n---\n\n```cs\nvar a = 1;\n```\n\n| a | b |\n|---|---|\n| 1 | 2 |\n\n---\n\n:::nota Título\nDentro.\n:::\n\n$$\nx^2\n$$\n";
        var result = AiAuthorship.Attribute(null, written, "m");
        Assert.Equal(
            "---\ntitulo: X\n---\n\n```cs\nvar a = 1;\n```\n" + S("m") + "\n\n| a | b |\n|---|---|\n| 1 | 2 " + S("m") + " |\n\n---\n\n:::nota Título\nDentro. " + S("m") +
            "\n:::\n\n$$\nx^2\n$$\n" + S("m") + "\n", result);
        Assert.Equal(written, AiAuthorship.Strip(result));
        // the code stays code and the table keeps its rows (the 1.8.4 block splitter sees the same blocks)
        Assert.Equal(MarkdownEngine.SplitBlocks(written).Count(b => b.Kind is "code" or "table"),
            MarkdownEngine.SplitBlocks(result).Count(b => b.Kind is "code" or "table"));
    }

    [Fact]
    public void UnchangedHumanAndAiBlocksKeepTheirAuthorship()
    {
        var before = "Meu texto.\n\nTexto antigo da IA. " + S("velho") + "\n";
        var result = AiAuthorship.Attribute(before, "Meu texto.\n\nTexto antigo da IA.\n\nNovo parágrafo gerado agora.\n", "novo");
        Assert.Equal("Meu texto.\n\nTexto antigo da IA. " + S("velho") + "\n\nNovo parágrafo gerado agora. " + S("novo") + "\n", result);
    }

    [Fact]
    public void RegeneratingAnAiBlockReplacesItsSealWithoutAccumulating()
    {
        var before = "Resumo antigo sobre o projeto de casas. " + S("gpt-6-luna") + "\n";
        var result = AiAuthorship.Attribute(before, "Uma síntese inteiramente diferente, com outras ideias e conclusões novas.\n", "gpt-7");
        Assert.Equal("Uma síntese inteiramente diferente, com outras ideias e conclusões novas. " + S("gpt-7") + "\n", result);
    }

    [Theory]
    [InlineData("Eu nao sabia que voce vinha amanha de manha.", "Eu não sabia que você vinha amanhã de manhã.")]
    [InlineData("A gente vai no mercado comprar pao e leite.", "A gente vai ao mercado comprar pão e leite.")]
    [InlineData("O projeto tem tres etapas principais", "O projeto tem três etapas principais.")]
    [InlineData("texto em minusculas sem formatacao", "**Texto** em minúsculas, sem formatação")]
    [InlineData("Ideia de aluguel de casas container num terreno central.", "Ideia de aluguel de casas container.")]
    public void CorrectingHumanTextDoesNotTransferAuthorshipToTheAi(string human, string corrected) =>
        Assert.Equal(corrected + "\n", AiAuthorship.Attribute(human + "\n", corrected + "\n", "gpt-x"));

    [Theory]
    [InlineData("- uma\n", "- primeira\n")]
    [InlineData("&&: Dê uma sugestão de nome\n", "Nome sugerido: Aurora\n")]
    public void ReplacingTheWordsOfAShortBlockIsGeneration(string human, string written) =>
        Assert.Equal(written.TrimEnd('\n') + " " + S("m") + "\n", AiAuthorship.Attribute(human, written, "m"));

    [Fact]
    public void CorrectingAiTextKeepsTheModelThatWroteIt()
    {
        var before = "A proposta seria alugar um terreno e instalar varias casas. " + S("autor") + "\n";
        Assert.Equal("A proposta seria alugar um terreno e instalar várias casas. " + S("autor") + "\n",
            AiAuthorship.Attribute(before, "A proposta seria alugar um terreno e instalar várias casas.\n", "corretor"));
    }

    [Fact]
    public void AddingNewSentencesToHumanTextIsGeneration()
    {
        var result = AiAuthorship.Attribute("Pontos a investigar.\n", "Pontos a investigar: custo dos containers, zoneamento e infraestrutura necessária.\n", "gpt-x");
        Assert.EndsWith(S("gpt-x") + "\n", result);
    }

    [Fact]
    public void MovingAndReorderingBlocksIsNotGeneration()
    {
        const string before = "Primeiro bloco humano.\n\nSegundo bloco humano.\n\nTerceiro bloco. {ia:\"m\"}\n";
        var result = AiAuthorship.Attribute(before, "Terceiro bloco.\n\nPrimeiro bloco humano.\n\nSegundo bloco humano.\n", "outro");
        Assert.Equal("Terceiro bloco. " + S("m") + "\n\nPrimeiro bloco humano.\n\nSegundo bloco humano.\n", result);
    }

    [Fact]
    public void JoiningHumanParagraphsIsNotGeneration() =>
        Assert.Equal("Casas container são baratas e ficam num terreno central.\n",
            AiAuthorship.Attribute("Casas container são baratas.\n\nFicam num terreno central.\n", "Casas container são baratas e ficam num terreno central.\n", "m"));

    [Fact]
    public void DeletingABlockTakesItsSealAlong() =>
        Assert.Equal("Fica.\n", AiAuthorship.Attribute("Fica.\n\nSai daqui. " + S("m") + "\n", "Fica.\n", "m"));

    // ---------------------------------------------------------------- the seal is the app's, not the text's

    [Fact]
    public void SealsSentByTheAgentAreIgnoredAndCannotBeForged()
    {
        Assert.Equal("Texto " + S("real") + "\n", AiAuthorship.Attribute(null, "Texto {ia:\"forjado\"} {ia:\"outro\"}\n", "real"));
        // copying the seals it saw in the note does not stack new ones
        var before = "Item " + S("real") + "\n";
        Assert.Equal(before, AiAuthorship.Attribute(before, "Item {ia:\"real\"} {ia:\"real\"}\n", "real"));
    }

    [Fact]
    public void RewritingTheWholeNoteWithoutTheSealsPreservesThem()
    {
        // the beta.32 loop: write_note with the seals left out was refused; now the seals are the app's to keep
        var before = "# Diário\n\nEntrada humana.\n\nResposta da IA. " + S("gpt-6-luna") + "\n";
        Assert.Equal(before, AiAuthorship.Attribute(before, AiAuthorship.Strip(before), "gpt-6-luna"));
    }

    [Fact]
    public void LegacyAccumulatedAndMisplacedSealsAreRepairedOnlyWhereTheAiWrites()
    {
        var before = "- [x] ** " + S("gpt-6-luna") + "Páginas — seleção (PR #700):** introduzidas.\n" +
                     "- [ ] **Geração de imagens:** melhorar. " + S("gpt-6-luna") + " " + S("gpt-6-luna") + " " + S("gpt-6-luna") + "\n" +
                     "- [ ] Intocado " + S("a") + " " + S("a") + "\n";
        var plain = AiAuthorship.Strip(before);
        Assert.Equal("- [x] **Páginas — seleção (PR #700):** introduzidas.\n- [ ] **Geração de imagens:** melhorar.\n- [ ] Intocado\n", plain);
        var edited = plain.Replace("introduzidas.", "introduzidas na beta 18.").Replace("melhorar.", "melhorar e expandir.");
        var result = AiAuthorship.Attribute(before, edited, "gpt-6-luna");
        Assert.Equal("- [x] **Páginas — seleção (PR #700):** introduzidas na beta 18. " + S("gpt-6-luna") + "\n" +
                     "- [ ] **Geração de imagens:** melhorar e expandir. " + S("gpt-6-luna") + "\n" +
                     "- [ ] Intocado " + S("a") + " " + S("a") + "\n", result);
    }

    [Fact]
    public void CanonicalDisplayRepairsLegacySealsWithoutTouchingContent()
    {
        var legacy = "- [x] ** " + S("g") + "Páginas:** y\n- b " + S("a") + " " + S("a") + "\n```\nx\n``` " + S("c") + "\nfim\n";
        var canonical = AiAuthorship.Canonical(legacy);
        Assert.Equal("- [x] **Páginas:** y " + S("g") + "\n- b " + S("a") + "\n```\nx\n```\n" + S("c") + "\nfim\n", canonical);
        Assert.Equal(canonical, AiAuthorship.Canonical(canonical));
        Assert.Equal(AiAuthorship.Strip(legacy), AiAuthorship.Strip(canonical));
        Assert.Equal("Sem selos\n", AiAuthorship.Canonical("Sem selos\n"));
    }

    [Fact]
    public void AttributionIsIdempotentAndRoundTrips()
    {
        const string before = "Humano.\n\nIA antiga. {ia:\"a\"}\n\n```\nx\n```\n{ia:\"a\"}\n";
        const string proposed = "Humano corrigido.\n\nIA antiga.\n\n```\nx\ny\n```\n\nNovo bloco com bastante conteúdo inventado.\n";
        var once = AiAuthorship.Attribute(before, proposed, "b");
        Assert.Equal(once, AiAuthorship.Attribute(before, once, "b"));
        Assert.Equal(proposed, AiAuthorship.Strip(once));
    }

    [Fact]
    public void WindowsLineEndingsArePreserved()
    {
        var result = AiAuthorship.Attribute("Humano.\r\n", "Humano.\r\n\r\nGerado pela IA agora mesmo.\r\n", "m");
        Assert.Equal("Humano.\r\n\r\nGerado pela IA agora mesmo. " + S("m") + "\r\n", result);
    }

    // ---------------------------------------------------------------- what a person can do in the editor

    [Fact]
    public void PersonEditingAnAiBlockInPlaceKeepsItsSeal()
    {
        var before = "Texto do agente. " + S("m") + "\n";
        Assert.Equal("Outra coisa escrita pela pessoa. " + S("m") + "\n", AiAuthorship.Attribute(before, "Outra coisa escrita pela pessoa.\n", null));
    }

    [Fact]
    public void PersonSplittingAnAiLineKeepsTheSealOnBothParts()
    {
        var before = "Primeira metade do texto e segunda metade do texto. " + S("m");
        Assert.Equal("Primeira metade do texto " + S("m") + "\ne segunda metade do texto. " + S("m"),
            AiAuthorship.Attribute(before, "Primeira metade do texto\ne segunda metade do texto.", null));
    }

    [Fact]
    public void PersonWritingANewBlockIsNotSealedAndCannotForgeOne()
    {
        var before = "IA. " + S("m") + "\n";
        Assert.Equal("IA. " + S("m") + "\n\nMeu bloco.\n", AiAuthorship.Attribute(before, "IA.\n\nMeu bloco. {ia:\"m\"}\n", null));
    }

    [Fact]
    public void PersonDeletingAnAiBlockRemovesItsSeal() =>
        Assert.Equal("Fica.\n", AiAuthorship.Attribute("Fica.\n" + "Sai. " + S("m") + "\n", "Fica.\n", null));

    // ---------------------------------------------------------------- what the block view reports

    [Fact]
    public void BlocksReportTheAuthorOfEachBlock()
    {
        var blocks = AiAuthorship.Blocks("Humano.\n\n- a " + S("x") + "\n- b\n\n```\nc\n```\n" + S("y") + "\n");
        Assert.Equal([("Humano.", (string?)null), ("- a", "x"), ("- b", null), ("```\nc\n```", "y")], blocks.Select(b => (b.Text, b.Model)).ToArray());
    }

    [Fact]
    public void OrphanSealLinesGoButTheSealOfACodeBlockStays()
    {
        Assert.Equal("Intro\n\nFim\n", AiAuthorship.RemoveOrphans("Intro\n  " + S("m") + " \nFim\n"));
        Assert.Equal("```\nx\n```\n" + S("m") + "\n", AiAuthorship.RemoveOrphans("```\nx\n```\n" + S("m") + "\n"));
        Assert.Equal("\n", AiAuthorship.RemoveOrphans(S("m") + "\n"));
        Assert.Equal("Conteúdo " + S("m") + "\n", AiAuthorship.RemoveOrphans("Conteúdo " + S("m") + "\n"));
    }

    // ---------------------------------------------------------------- cost

    [Fact]
    public void AttributingALargeNoteStaysFast()
    {
        var lines = Enumerable.Range(0, 6000).Select(i => i % 3 == 0 ? "Linha " + i + " escrita pela IA. " + S("m") : "Linha humana número " + i + ".");
        var before = string.Join("\n\n", lines) + "\n";
        var plain = AiAuthorship.Strip(before);
        var proposed = plain.Replace("Linha humana número 3001.", "Linha humana número 3001, agora com uma frase nova gerada pela IA.");
        AiAuthorship.Attribute(before, proposed, "m"); // warm-up
        var clock = Stopwatch.StartNew();
        var result = AiAuthorship.Attribute(before, proposed, "m");
        clock.Stop();
        Assert.Contains("gerada pela IA. " + S("m"), result);
        Assert.Equal(before.Length + proposed.Length - plain.Length + S("m").Length + 1, result.Length);
        Assert.True(clock.ElapsedMilliseconds < 400, "Attribute took " + clock.ElapsedMilliseconds + " ms");
    }
}
