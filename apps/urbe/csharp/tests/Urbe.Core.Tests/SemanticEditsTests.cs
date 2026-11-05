using System.Text.Json.Nodes;
using Urbe.Core;

namespace Urbe.Core.Tests;

/// <summary>CST-224: structural edits — all or nothing, explicit errors, the rest of the note untouched.</summary>
public sealed class SemanticEditsTests
{
    private const string Note = "---\ntitulo: Projeto\ntags: [a, b]\n---\n# Projeto\n\nIntro.\n\n## Tarefas\n\n- [ ] comprar pão\n- [x] ligar para Ana\n\n## Custos\n\n| item | valor |\n|---|---:|\n| pão | 10 |\n| leite | 5 |\n\n## Notas\n\nTexto antigo.\n\n### Detalhe\n\nSub.\n\n```md\n## Tarefas\n- [ ] falso\n```\n";

    private static string Run(string text, string json) => SemanticEdits.Apply(text, (JsonArray)JsonNode.Parse(json)!);

    [Fact]
    public void Sections_ReplaceAppendInsertRenameDelete()
    {
        var r = Run(Note, """[{"op":"replace_section","heading":"Notas","content":"Texto novo."}]""");
        Assert.Contains("## Notas\nTexto novo.\n", r);
        Assert.DoesNotContain("Texto antigo", r);
        Assert.DoesNotContain("### Detalhe", r); // a section holds its subsections
        r = Run(Note, """[{"op":"append_to_section","heading":"## Tarefas","content":"Mais uma linha."}]""");
        Assert.Contains("- [x] ligar para Ana\n\nMais uma linha.\n", r);
        r = Run(Note, """[{"op":"insert_section","title":"Riscos","content":"Nenhum.","after":"Custos"}]""");
        Assert.True(r.IndexOf("## Riscos", StringComparison.Ordinal) > r.IndexOf("| leite", StringComparison.Ordinal) && r.IndexOf("## Riscos", StringComparison.Ordinal) < r.IndexOf("## Notas", StringComparison.Ordinal));
        r = Run(Note, """[{"op":"rename_heading","heading":"custos","title":"Orçamento"}]""");
        Assert.Contains("## Orçamento\n", r);
        r = Run(Note, """[{"op":"delete_section","heading":"Detalhe"}]""");
        Assert.DoesNotContain("Sub.", r);
        Assert.Contains("Texto antigo.", r);
    }

    [Fact]
    public void PropertiesTasksAndTables()
    {
        var r = Run(Note, """[{"op":"set_property","name":"status","value":"em andamento"},{"op":"set_property","name":"titulo","value":"Projeto X: fase 2"},{"op":"remove_property","name":"tags"}]""");
        Assert.StartsWith("---\ntitulo: \"Projeto X: fase 2\"\nstatus: em andamento\n---\n", r);
        r = Run(Note, """[{"op":"set_task","text":"comprar pão","done":true},{"op":"add_task","text":"pagar conta","heading":"Tarefas"}]""");
        Assert.Contains("- [x] comprar pão\n- [x] ligar para Ana\n- [ ] pagar conta\n", r);
        Assert.Contains("- [ ] falso", r); // code is never touched
        r = Run(Note, """[{"op":"table_set_cell","row":"leite","column":"valor","value":"7"},{"op":"table_add_row","values":{"item":"café","valor":"20"}}]""");
        Assert.Contains("| leite | 7 |", r);
        Assert.Contains("| café | 20 |", r);
        Assert.Contains("|---|---:|", r); // alignment kept
    }

    [Theory]
    [InlineData("""[{"op":"replace_section","heading":"Inexistente","content":"x"}]""", "Não há seção")]
    [InlineData("""[{"op":"set_task","text":"a","done":true}]""", "aparece em")]
    [InlineData("""[{"op":"table_set_cell","row":"pão","column":"preço","value":"1"}]""", "Não há coluna")]
    [InlineData("""[{"op":"pular"}]""", "Operação desconhecida")]
    public void Mistakes_AreExplained_AndNothingChanges(string ops, string message)
    {
        var ex = Assert.Throws<SemanticEdits.EditError>(() => Run(Note, ops));
        Assert.Contains(message, ex.Message);
        Assert.Contains("Nada foi alterado", ex.Message);
    }

    [Fact]
    public void AllOrNothing_ALaterMistakeUndoesTheEarlierOperations()
    {
        Assert.Throws<SemanticEdits.EditError>(() => Run(Note, """[{"op":"set_task","text":"comprar pão","done":true},{"op":"delete_section","heading":"Nada"}]"""));
        // the input text is a value: the caller keeps the original when Apply throws
        Assert.Contains("- [ ] comprar pão", Note);
    }

    [Fact]
    public void AgentTool_GoesThroughTheAuthoredPath()
    {
        Assert.True(Urbe.Core.Assistant.AssistantTools.IsAuthoredWrite("edit_note_structure"));
        Assert.Equal("write", Urbe.Core.Assistant.AssistantTools.StructureTool.Access);
        Assert.DoesNotContain(Urbe.Core.Assistant.AssistantTools.All, t => t.Name == "edit_note_structure"); // the 1.8.4 list stays as it was
    }
}
