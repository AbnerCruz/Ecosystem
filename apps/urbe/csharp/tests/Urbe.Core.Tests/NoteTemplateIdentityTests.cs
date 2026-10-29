using Urbe.Core;

namespace Urbe.Core.Tests;

/// <summary>
/// CST-044/046: a template is a note in Modelos/ or a note named *.modelo.md anywhere (the owner asked for its own
/// extension; it stays Markdown, like .page.json and .grafo.json stay JSON). Text in [brackets] is not a field; the
/// Urbe says so and can turn it into {{fields}}.
/// </summary>
public sealed class NoteTemplateIdentityTests
{
    [Theory]
    [InlineData("Modelos/Ficha.md", true)]
    [InlineData("Projetos/Droerni/Personagem.modelo.md", true)]
    [InlineData("Personagem.modelo.md", true)]
    [InlineData("Projetos/Personagem.md", false)]
    [InlineData(".urbe/x.modelo.md", false)]
    public void TemplatesAreTheModelosFolderOrTheirOwnExtension(string path, bool expected) =>
        Assert.Equal(expected, NoteTemplateEngine.IsTemplatePath(path));

    [Fact]
    public void ATemplatesNameHasNeitherFolderNorExtension()
    {
        Assert.Equal("Ficha", NoteTemplateEngine.NameOf("Modelos/Ficha.md"));
        Assert.Equal("Personagem", NoteTemplateEngine.NameOf("Projetos/Droerni/Personagem.modelo.md"));
    }

    [Fact]
    public void TextInBracketsIsFoundButLinksTasksAndCodeAreNot()
    {
        const string t = "# [Nome]\n\nNascido em [Data de nascimento], em [[Cidade]].\n- [ ] tarefa\n- [x] feita\n[link](https://x.y) e ![img](a.png) e [^1]\n`[codigo]`\n\n```\n[bloco]\n```\n[Nome] de novo";
        Assert.Equal(["Nome", "Data de nascimento"], NoteTemplateEngine.BracketPlaceholders(t));
        Assert.Empty(NoteTemplateEngine.BracketPlaceholders("Já com {{campo}} e [[Nota]]."));
    }

    [Fact]
    public void BracketsBecomeFieldsAndTheFormShowsThemReadably()
    {
        var converted = NoteTemplateEngine.ConvertBrackets("# [Nome]\n\nNascido em [Data de nascimento], em [[Cidade]].\n- [ ] tarefa\n");
        Assert.Equal("# {{Nome}}\n\nNascido em {{Data_de_nascimento}}, em [[Cidade]].\n- [ ] tarefa\n", converted);
        Assert.Equal(["Nome", "Data_de_nascimento"], NoteTemplateEngine.InputFields(converted));
        var form = FormDefinition.FromTemplate("Ficha", converted);
        Assert.Equal("Data de nascimento", form.Fields[1].Label);
    }

    [Fact]
    public void FillingInTheEditorLeavesTheUnansweredFieldsBlank()
    {
        var note = NoteTemplateEngine.FillPartially("# {{titulo}}\n\nNome: {{Nome}}\nIdade: {{Idade}}\n", "Ana", new DateTime(2026, 10, 28), new Dictionary<string, string> { ["Nome"] = "Ana Silva" });
        Assert.Equal("# Ana\n\nNome: Ana Silva\nIdade: \n", note);
    }
}
