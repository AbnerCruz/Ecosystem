using System.Text.Json.Nodes;
using Urbe.Core;
using Urbe.Core.Assistant;

namespace Urbe.Core.Tests;

/// <summary>CST-223 / CST-226: citable evidence from the vault and its re-verification.</summary>
public sealed class AssistantEvidenceToolsTests
{
    private sealed class Host : IAssistantHost
    {
        public DocumentStore Documents { get; } = new();
        public KnowledgeIndex Knowledge { get; }
        public Host(params (string Path, string Content)[] notes)
        {
            foreach (var (path, content) in notes) Documents.Upsert(new() { Path = path, Content = content });
            Knowledge = new(Documents);
        }
        public void Write(string path, string content) => Documents.Upsert(new() { Id = Documents.Get(path)?.Id, Path = path, Content = content });
        public int ReadChars => 16000;
        public (string? Path, string? Selection) EditorContext() => (null, null);
        public Task<UrbeDocument> SaveNoteAsync(UrbeDocument doc, string text) => throw new NotSupportedException();
        public Task<UrbeDocument> CreateNoteAsync(string path, string content) => throw new NotSupportedException();
        public Task<UrbeDocument> RenameNoteAsync(UrbeDocument doc, string path) => throw new NotSupportedException();
        public Task TrashNoteAsync(UrbeDocument doc) => throw new NotSupportedException();
        public Task CreateFolderAsync(string path) => throw new NotSupportedException();
        public void OpenNote(string path) { }
        public void Remember(string fact) { }
        public void SetPlan(IReadOnlyList<AiPlanStep> steps) { }
    }

    private const string Aurora = "# Aurora\n\nProjeto de reforma da biblioteca.\n\n## Cronograma\n\n### Prazo\n\nA entrega final do projeto Aurora ficou para 15 de março.\nO prazo pode mudar se a obra atrasar.\n\n## Equipe\n\nAna coordena; Bruno cuida das compras.\n";

    private static async Task<string> Run(AiTool tool, IAssistantHost host, JsonObject input) => (await tool.Run(input, host)).Content;

    [Fact]
    public void ToolsAreReadOnlyWithSchemasAndLabels()
    {
        foreach (var tool in AssistantEvidenceTools.All)
        {
            Assert.Equal("read", tool.Access);
            Assert.Equal("object", (string?)tool.Parameters["type"]);
            Assert.False(string.IsNullOrWhiteSpace(tool.Label(new JsonObject { ["query"] = "x", ["path"] = "A.md" })));
        }
        Assert.Equal(["find_evidence", "cite_check"], AssistantEvidenceTools.All.Select(t => t.Name));
    }

    [Fact]
    public void RanksTheParagraphThatHoldsTheTermsFirstWithSectionLinesAndRevision()
    {
        var host = new Host(("Projetos/Aurora.md", Aurora), ("Diário.md", "Hoje choveu. Pensei no projeto.\n"), ("Receitas.md", "Bolo de cenoura com prazo de validade curto.\n"));
        var (passages, terms, absent) = AssistantEvidenceTools.Rank(host, "Qual é o prazo de entrega do projeto Aurora?");
        Assert.Equal(["prazo", "entrega", "projeto", "aurora"], terms);
        Assert.Empty(absent);
        var top = passages[0];
        Assert.Equal("Projetos/Aurora.md", top.Path);
        Assert.Equal(["Aurora", "Cronograma", "Prazo"], top.Section);
        Assert.Equal((9, 10), (top.From, top.To));
        Assert.Equal("A entrega final do projeto Aurora ficou para 15 de março.\nO prazo pode mudar se a obra atrasar.", top.Text);
        Assert.Equal(AssistantEvidenceTools.RevisionOf(Aurora), top.Revision);
        Assert.Equal(4, top.Matched.Count);
        Assert.True(passages.Skip(1).All(p => p.Score <= top.Score));
        // deterministic: the same question, the same answer
        static string Key(AssistantEvidenceTools.Passage p) => p.Path + ":" + p.From + "-" + p.To + ":" + p.Score.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(passages.Select(Key), AssistantEvidenceTools.Rank(host, "Qual é o prazo de entrega do projeto Aurora?").Passages.Select(Key));
    }

    [Fact]
    public void AccentsCaseAndPortugueseInflectionsMatch()
    {
        var host = new Host(("Notas/Reunião.md", "As REUNIÕES de planejamento acontecem às terças.\n\nOutro assunto qualquer.\n"));
        var (passages, _, _) = AssistantEvidenceTools.Rank(host, "reuniao planejar");
        var p = Assert.Single(passages);
        Assert.Equal(1, p.From);
        Assert.Equal(["reuniao", "planejar"], p.Matched);
    }

    [Fact]
    public async Task CommentsMarksAndPrivateAreasAreNeverEvidence()
    {
        var host = new Host(
            ("Ideias.md", "Texto visível.\n\n%%segredo guardado\nem duas linhas%%\nDepois do comentário vem o tesouro.\n"),
            ("Personalização/AGENTS.md", "Segredo nas instruções.\n"),
            (".urbe/notas.md", "segredo de sistema\n"));
        var output = await Run(AssistantEvidenceTools.Find, host, new JsonObject { ["query"] = "segredo" });
        Assert.StartsWith("Nenhum trecho no vault contém", output);
        Assert.Contains("sem supor", output);
        // the comment's lines still count, so the line numbers are read_note's
        var (passages, _, _) = AssistantEvidenceTools.Rank(host, "tesouro");
        var p = Assert.Single(passages);
        Assert.Equal(("Ideias.md", 5, 5), (p.Path, p.From, p.To));
        Assert.DoesNotContain("segredo", p.Text);
    }

    [Fact]
    public async Task NoMatchAndStopWordsOnlyAreSaidPlainly()
    {
        var host = new Host(("Projetos/Aurora.md", Aurora));
        var none = await Run(AssistantEvidenceTools.Find, host, new JsonObject { ["query"] = "orçamento da piscina" });
        Assert.Contains("Não há evidência nas notas", none);
        Assert.DoesNotContain("[1]", none);
        await Assert.ThrowsAsync<AiToolException>(() => Run(AssistantEvidenceTools.Find, host, new JsonObject { ["query"] = "o que é isso?" }));
    }

    [Fact]
    public async Task OutputCitesAndFlagsPartialEvidenceAndRelatedNotes()
    {
        var host = new Host(
            ("Projetos/Aurora.md", Aurora + "\nVer [[Orçamento]].\n"),
            ("Orçamento.md", "O orçamento do projeto Aurora é de 200 mil.\n"),
            ("Solta.md", "Nada a ver.\n"));
        var output = await Run(AssistantEvidenceTools.Find, host, new JsonObject { ["query"] = "prazo Aurora helicóptero" });
        Assert.Contains("[1] Projetos/Aurora.md › Aurora › Cronograma › Prazo · linhas 9–10 · revisão ", output);
        Assert.Contains("Sem nenhuma ocorrência no vault: helicoptero", output);
        Assert.Contains("Evidência parcial", output);
        Assert.Contains("ligadas: → Orçamento.md", output);
        Assert.DoesNotContain("Solta.md", output);
    }

    [Fact]
    public void LongParagraphsAreSplitByLinesAndLongLinesAreCut()
    {
        var lines = Enumerable.Range(1, 40).Select(n => "Linha " + n + " fala de assuntos gerais da casa e do jardim sem nada especial.").ToList();
        lines[30] = "Linha 31 menciona o girassol amarelo plantado no canto.";
        var host = new Host(("Casa.md", string.Join("\n", lines) + "\n\n" + new string('x', 900) + " girassol " + new string('y', 900) + "\n"));
        var (passages, _, _) = AssistantEvidenceTools.Rank(host, "girassol");
        Assert.Equal(2, passages.Count);
        Assert.All(passages, p => Assert.True(p.Text.Length <= AssistantEvidenceTools.PassageChars));
        var inLines = passages.Single(p => !p.Cut);
        Assert.InRange(31, inLines.From, inLines.To);
        Assert.Contains("girassol amarelo", inLines.Text);
        var cut = passages.Single(p => p.Cut);
        Assert.Equal(42, cut.From);
        Assert.Contains("girassol", cut.Text);
    }

    [Fact]
    public void CiteCheckTellsIntactPresentMovedChangedAndGone()
    {
        var host = new Host(("Projetos/Aurora.md", Aurora), ("Outra.md", "Nada.\n"));
        var p = AssistantEvidenceTools.Rank(host, "prazo entrega Aurora").Passages[0];
        var r = AssistantEvidenceTools.Verify(host, p.Path, p.Text, p.Revision, p.From);
        Assert.Equal((AssistantEvidenceTools.CiteState.Intact, 9, 10), (r.State, r.From, r.To));

        // another part of the note changed: the passage is the same, at the same lines
        host.Write("Projetos/Aurora.md", Aurora.Replace("Bruno", "Carla"));
        r = AssistantEvidenceTools.Verify(host, p.Path, p.Text, p.Revision, p.From);
        Assert.Equal(AssistantEvidenceTools.CiteState.Present, r.State);
        Assert.NotEqual(p.Revision, r.Revision);

        // lines added above: the same passage, further down
        host.Write("Projetos/Aurora.md", "Introdução nova.\n\n" + Aurora);
        r = AssistantEvidenceTools.Verify(host, p.Path, p.Text, p.Revision, p.From);
        Assert.Equal((AssistantEvidenceTools.CiteState.Moved, "Projetos/Aurora.md", 11, 12, false), (r.State, r.Path, r.From, r.To, r.OtherNote));

        // reworded: the nearest text is shown
        host.Write("Projetos/Aurora.md", Aurora.Replace("15 de março", "30 de abril"));
        r = AssistantEvidenceTools.Verify(host, p.Path, p.Text, p.Revision, p.From);
        Assert.Equal(AssistantEvidenceTools.CiteState.Changed, r.State);
        Assert.Contains("30 de abril", r.Nearest);

        // moved to another note
        host.Write("Projetos/Aurora.md", "# Aurora\n\nVer a outra nota.\n");
        host.Write("Outra.md", "Nada.\n\n" + p.Text + "\n");
        r = AssistantEvidenceTools.Verify(host, p.Path, p.Text, p.Revision, p.From);
        Assert.Equal((AssistantEvidenceTools.CiteState.Moved, "Outra.md", 3, true), (r.State, r.Path, r.From, r.OtherNote));

        // gone everywhere
        host.Write("Outra.md", "Nada.\n");
        r = AssistantEvidenceTools.Verify(host, p.Path, p.Text, p.Revision, p.From);
        Assert.Equal(AssistantEvidenceTools.CiteState.Gone, r.State);
    }

    [Fact]
    public async Task CiteCheckToolReportsStatesInWords()
    {
        var host = new Host(("Projetos/Aurora.md", Aurora));
        var p = AssistantEvidenceTools.Rank(host, "prazo entrega Aurora").Passages[0];
        var ok = await Run(AssistantEvidenceTools.Check, host, new JsonObject { ["path"] = p.Path, ["passage"] = p.Text, ["revision"] = p.Revision, ["start_line"] = p.From });
        Assert.StartsWith("INTACTO: Projetos/Aurora.md", ok);
        var missing = await Run(AssistantEvidenceTools.Check, host, new JsonObject { ["path"] = "Sumida.md", ["passage"] = "texto que nunca existiu em lugar nenhum" });
        Assert.StartsWith("SUMIU: não há nota Sumida.md", missing);
        // a comment hidden inside a cited paragraph does not break the check
        host.Write("Projetos/Aurora.md", Aurora.Replace("15 de março.", "15 de março.%%confirmar%%"));
        var still = await Run(AssistantEvidenceTools.Check, host, new JsonObject { ["path"] = p.Path, ["passage"] = p.Text, ["revision"] = p.Revision, ["start_line"] = p.From });
        Assert.StartsWith("PRESENTE:", still);
    }
}
